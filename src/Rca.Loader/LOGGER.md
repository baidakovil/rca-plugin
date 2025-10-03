# RCA Unified Logging System

> Version: Schema 1
> Components: Runtime (hot-reloadable) + Loader (stable host)

## 1. High-Level Overview
The logging system provides a hot-reload resilient, structured, unidirectional logging channel from the Runtime (loaded in a custom AssemblyLoadContext and frequently reloaded) to the Loader (stable Revit AppDomain host). It replaces the legacy in‑process debug log window and enables: deterministic sequencing, safe transport across unload boundaries, fallback durability, and future extensibility.

```
(Runtime ALC) ----JSON lines over Named Pipe----> (Loader) ----> Sinks (File, Debug)
             fallback file / emergency file               enrichment
```

Key goals:
- No compile-time circular dependencies (Runtime depends only on `Rca.Logging.Contracts`).
- Survives Runtime reloads (pipe reconnect handshake is lazy; Loader waits passively).
- Zero queue / minimal buffering (low expected volume; immediate write).
- Deterministic ordering within each Runtime session (SequenceId) + global ordering in Loader (GlobalSequenceId).
- Explicit failure handling for serialization and transport.

## 2. Contracts (Project: `Rca.Logging.Contracts`)
Single record `LogEntryDto` (schema versioned) with primitive fields only:
- Timestamps: `TimestampTicks` (local) captured at emission; `ReceivedTimestamp` added only in Loader.
- Ordering: `SequenceId` (per Runtime) and `GlobalSequenceId` (Loader).
- Identity: `RuntimeSessionId`, optional `ALCInstanceId`, `RuntimeProcessId`.
- Reliability flags (`Flags` bitset): `SerializationFailed`, `FallbackUsed`, reserved `IncompatibleSchema`.
- Transport meta: `IsFallback`, `IsPing`.

Design choices:
- Record (immutable init-only) for safe structural cloning (`with`).
- Strings for Level to avoid coupling to `Microsoft.Extensions.Logging` enums across ALC boundary.
- No interface abstractions (intentional early-phase simplicity / reduced indirection cost).

## 3. Runtime Side Components
### 3.1 `NamedPipeLoggerProvider` / `NamedPipeLogger`
Implements `ILoggerProvider` and `ILogger`. Responsibilities:
- Format + create `LogEntryDto`.
- Obtain monotonic `SequenceId` via `Interlocked.Increment`.
- Forward to transport (no direct IO in provider besides serialization).

### 3.2 `PipeLogTransport`
Encapsulates all transport, resiliency, and file fallback logic.
Responsibilities:
- Lazy connect to named pipe `RCA_LOG_PIPE`.
- Exponential backoff with jitter (±20%) sequence: 50ms → 200ms → 500ms → 1s → 2s → 5s (capped).
- Immediate JSON serialization (System.Text.Json, camelCase, ignore null).
- On serialization exception: write to emergency file (human-readable line), skip forwarding.
- On transport exception: force disconnect; next log triggers reconnect attempt.
- Fallback logging to per-day file with part rotation on size > 50 MB (`runtime-fallback-YYYYMMDD_partN.log`).
- Emergency file path: `%LOCALAPPDATA%\RCA\Logs\runtime-emergency-YYYYMMDD.log`.
- Keepalive: silent `PING` entries every 10s (flagged `IsPing=true`)—suppressed by Loader sinks.

Why no buffering? Expected log volume low; simplicity > throughput. Each log = single line write; latency acceptable.

### 3.3 File Strategy
- Fallback file: JSONL containing enriched entries marked `IsFallback` + flag `FallbackUsed`.
- Emergency file: plain text (not JSON) so corruption / partial writes cannot cascade; includes truncated message + exception summary.
- Size caps applied only to fallback (to prevent unbounded disk consumption if pipe unreachable for long time). Part counter increments; old parts not auto-pruned (future policy hook).

## 4. Loader Side Components
### 4.1 `LoggingPipeServerService`
Single long-running accept loop:
1. Create `NamedPipeServerStream` (In, single client, async).
2. Wait for connection.
3. Read lines synchronously (StreamReader.ReadLineAsync) until disconnect.
4. Process each line individually.
5. On disconnect: loop and await next Runtime (hot reload or crash recovery).

### 4.2 Deserialization & Filtering
- Deserialize JSON → `LogEntryDto`.
- Schema mismatch (future extension) currently: ignore silently; flagged conceptually by `IncompatibleSchema` if set.
- `IsPing` entries dropped before enrichment & sink dispatch.

### 4.3 Enrichment Pipeline (in-process, synchronous)
Adds:
- `GlobalSequenceId` (monotonic `Interlocked.Increment`).
- `ReceivedTimestamp` (local time at ingestion).
- `LoaderProcessId`.

### 4.4 Sinks (No Interfaces Yet)
- `FileLogSink`: session file `rca-logs-<start_timestamp>.log` (header stub). Line format (pipe-friendly, human-readable):
  `GlobalSeq|OriginalTimestamp|Recv:<ReceivedTimestamp>|Level|Category|Message|F=<Flags>|Seq=<RuntimeSeq>|Proc=<RuntimeProcessId>|Sess=<SessionId>`
- `DebugSink`: `System.Diagnostics.Debug.WriteLine` short format.

Rationale: Start with minimal fan-out; later insert abstraction if additional sinks (e.g., rolling policy, external aggregator) added.

## 5. Keepalive (Ping) Protocol
- Runtime emits every 10s if connected.
- Represented as normal DTO with `IsPing=true` and `Category="__ping"`.
- Loader discards early; not persisted in file or debug output.
- Benefit: Allows future idle timeout detection (Loader could measure last activity; not yet enforcing disconnect logic in this iteration).

## 6. Failure & Recovery Semantics
| Failure Type | Detection | Action | User Impact |
|--------------|-----------|--------|-------------|
| Serialization (`JsonException`) | Try/catch serialize | Emergency line write; drop entry | Entry not in main log; emergency file contains summary |
| Pipe connect timeout | Exception on connect | Backoff + jitter schedule; fallback path for subsequent logs | Slight delay; logs land in fallback until reconnect |
| Pipe broken mid-write | IOException | ForceDisconnect → fallback | Few entries may duplicate none (write is line atomic) |
| Fallback file size overflow | Pre-write size check | Rotate to next `_partN` file | Disk usage segmented |
| Emergency file write failure | Exception | Swallow last resort | Data lost for that entry only |

## 7. Threading Model
- Runtime logger calls originate on arbitrary threads; `PipeLogTransport` uses minimal locking (only fallback writer rotation uses a `lock`). Sequence IDs are atomic.
- Loader processing: single thread (pipe reader loop) => ordering preserved exactly as received.

## 8. Configuration Surface (Current)
Hard-coded constants (future config injection possible):
- Pipe name: `RCA_LOG_PIPE`.
- Backoff steps & jitter range.
- Ping interval: 10 seconds.
- Fallback size cap: 50 MB per part per day.
- Log directory base: `%LOCALAPPDATA%\RCA\Logs`.

## 9. Usage (Runtime)
```csharp
// During runtime bootstrap
var provider = new NamedPipeLoggerProvider(
    pipeName: "RCA_LOG_PIPE",
    runtimeSessionId: sessionId,
    alcInstanceId: currentAlcId);
ILogger logger = provider.CreateLogger("My.Feature.Component");
logger.LogInformation("Runtime started");
```
The provider can be registered into `ILoggerFactory` if one is introduced later. Dispose provider on ALC unload to close pipe early.

## 10. Usage (Loader)
```csharp
// Early in Loader startup before Runtime loads
var loggingServer = new LoggingPipeServerService("RCA_LOG_PIPE");
loggingServer.Start();
// Sinks immediately begin capturing once Runtime connects
```
No explicit stop needed on Runtime reloads. Dispose on Loader shutdown.

## 11. Extensibility Roadmap
Planned improvements (not yet implemented):
- Timeout-based connection liveness (auto-close if no pings > 30s).
- Pluggable sink abstraction (e.g., interface + composite) without breaking existing DTO or transport.
- Binary framing (length-prefix) to reduce overhead; backward-compatible by pipe name versioning.
- Structured scope capture (currently omitted for simplicity) with primitive value sanitization.
- Schema evolution: negotiation & downgrade / quarantine file for incompatible versions.
- Loader-driven dynamic level switching (control channel feedback).

## 12. Known Limitations / Weak Spots
| Area | Limitation | Risk | Possible Mitigation |
|------|------------|------|---------------------|
| No batching | One OS syscall per log line | Higher overhead under burst | Introduce small ring buffer & flush timer (opt-in) |
| JSON size unbounded | Large exception stack traces | Fallback file bloat | Truncate large fields; add size counters |
| No retention policy | Log directory may grow indefinitely | Disk consumption | Periodic cleanup (age / size threshold) |
| Fallback rotation only by size/date | Numerous parts on long outage | File proliferation | Add max parts per day or compression |
| Ping suppression only client-driven | Silent Runtime freeze undetected until logs resume | Delayed detection | Loader watchdog with timer on last receive |
| Single client assumption | Multiple runtimes would contend | Undefined ordering | Extend to multi-instance by unique pipe per session |
| No security on pipe | Local non-sandboxed process could write | Log poisoning | Add ACL tightening or handshake token |
| No flow control | Writer always pushes | Potential backpressure on slow disk (rare) | Buffered stream + flush strategy |

## 13. Rationale Summary
- Chose Named Pipes over TCP: zero config, local-only, low latency, easy reconnect.
- Avoided DI for transport: minimize indirection for a foundational cross-boundary service.
- Record DTO + primitive fields: stable binary layout for future protocol evolution.
- Immediate flush: favors diagnostic fidelity over throughput (primary use case: development + user issue capture).
- Separation of fallback vs emergency: isolates logic vs data problems.

## 14. Operational Notes
- If the Loader starts after the Runtime (rare), first connection attempts will fail -> Runtime writes fallback until Loader launches; automatic recovery when pipe becomes available.
- Safe to delete old `runtime-fallback-*` or session log files while system idle (no locking except active file handles).
- Emergency file presence should be investigated; frequent entries imply serialization bugs or DTO drift.

## 15. Testing Strategy (Suggested)
(Not fully implemented yet)
- Unit test transport reconnection by forcing `IOException` on a mocked stream.
- Contract test: emit sample DTO → ensure Loader enrichment fields present.
- Stress test: rapid log emission + periodic forced disconnect → ensure no crash, fallback rotation occurs.
- Schema mismatch test (future): send modified `SchemaVersion` and verify ignore behavior.

## 16. Migration Notes
Legacy `DebugLogService` and UI artifacts removed. Any former direct calls should now use `ILogger` obtained via the provider (or future centralized factory). Python execution service no longer logs via legacy API—caller responsible for logging success/error externally.

---
**Status:** Phase 1 complete (core transport, fallback, basic resilience). Phase 2 items (retention, dynamic levels, scopes, binary protocol) deferred.
