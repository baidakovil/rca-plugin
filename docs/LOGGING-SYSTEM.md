# RCA Unified Logging System (Updated)

> Version: Schema 1  
> Components: Runtime (hot-reloadable), Loader (stable), UI adapter (UiLog)

## 1. High-Level Overview
Structured, hot‑reload resilient logging: Runtime and UI streams JSONL over a dedicated named pipe (`RCA_LOG_PIPE`) to the Loader. Loader enriches and persists logs (file + Debug) and also emits its own internal events through the same sinks (prefixed with `LOADER|`).

```
(UI) ─┐                 (Runtime ALC) ─┐         Named Pipe          (Loader)
      ├─ JSON lines  ─────────────────┴──────────────▶  Ingest ▶ Enrich ▶ Sinks (File, Debug)
(Placeholder / fallback)                               ▲                ▲
                                                       │                │
                                     LoaderLog (internal)───────────────┘
```

New since previous version:
- `UiLog` lightweight adapter so UI (part of merged runtime set or design-time loader environment) logs without needing runtime ALC types.
- Unified internal logger `LoaderLog` + `LoaderInternalLogger` ensuring early startup diagnostics recorded before runtime connects.
- Simplified dispatcher elimination: direct sink writes via static helper (reduced indirection, earlier availability).
- Added `Rca.Logging.Contracts` to `NonCollectibleAssemblies` to avoid duplicate ALC loads and FileLoad exceptions.
- Extended docs for enrichment and operation IDs in runtime management paths.

## 2. Contracts (`Rca.Logging.Contracts`)
DTO: `LogEntryDto` (record) + constants `LoggingSchema.Version`, flags `LoggingFlags`.
Fields (Runtime/UI → Loader):
- SchemaVersion
- TimestampTicks (local)
- Level (string)
- Category
- Message
- Exception (flattened)
- RuntimeSessionId
- SequenceId (per runtime / UI session)
- RuntimeProcessId
- ALCInstanceId? (runtime only, optional)
- IsFallback
- Flags (bitfield: 1=SerializationFailed, 2=FallbackUsed)
- IsPing (keepalive)

Loader-only enrichment:
- GlobalSequenceId
- ReceivedTimestamp
- LoaderProcessId

## 3. Runtime Side
`NamedPipeLoggerProvider` + `NamedPipeLogger` produce DTOs. Transport: `PipeLogTransport` handles:
- Lazy pipe connection
- Exponential backoff (50→200→500→1000→2000→5000 ms, jitter ±20%)
- Keepalive PING every 10s (suppressed by Loader)
- Fallback JSONL (if pipe unavailable) with size-based part rotation (50MB)
- Emergency plain-text file for serialization or catastrophic failures
- Flags set for fallback/serialization scenarios

## 4. UI Adapter (`UiLog`)
Motivation: UI project should not depend directly on runtime transport internals or create tight coupling, but must replace legacy `Debug.WriteLine` calls. Features:
- Independent session id (`UI-<guid>`)
- Same JSON schema; Loader coalesces seamlessly
- Minimal fallback (plain text) when pipe not ready
- No ping emission (UI logs typically sparse)

## 5. Loader Components
- `LoggingPipeServerService`: single-thread accept/read loop; on disconnect waits for new session (hot reload safe)
- `LoaderLog` & `LoaderInternalLogger`: internal structured logging, writing directly to sinks
- Sinks: `FileLogSink` (session file `rca-logs-<timestamp>.log`), `DebugSink`
- Enrichment: adds `GlobalSequenceId`, `ReceivedTimestamp`, `LoaderProcessId` before sink dispatch

## 6. Recent Improvements
| Area | Change | Benefit |
|------|--------|---------|
| Internal logging | `LoaderLog.GetLogger<T>()` returns lightweight logger | Early startup logs persisted |
| Assembly loading | `Rca.Logging.Contracts` in non-collectible list | Prevents duplicate contract loads / identity issues |
| Dispatcher simplification | Removed intermediate dispatcher class | Less complexity, earlier sink availability |
| Placeholder host | `DockablePanelHost` now logs via unified logger | Easier diagnosing UI swap issues |

## 7. Pings & Keepalive
Runtime emits `IsPing=true` entries every 10s; Loader ignores them. (Future: implement idle disconnect / watchdog.) UI adapter does not emit pings.

## 8. Failure Matrix (unchanged core)
| Failure | Action | Persistence |
|---------|--------|-------------|
| Serialization error | Write emergency line + drop | Emergency file only |
| Pipe connect fail | Backoff + fallback | Fallback file |
| Pipe mid-write IO | Force disconnect + fallback | Partial line at worst |
| Fallback > 50MB | Rotate part counter | New part file |
| Emergency write fail | Swallow | Lost line only |

## 9. Log Line Formats
File sink:
```
GlobalSeq|OriginalTimestamp|Recv:ReceivedTimestamp|Level|Category|Message|F=Flags|Seq=RuntimeSeq|Proc=RuntimePid|Sess=SessionId
```
Loader internal lines:
```
LOADER|Timestamp|Level|Category|Message[|EX=ExceptionType:Message]
```

## 10. Usage Examples
Runtime:
```csharp
var provider = new NamedPipeLoggerProvider("RCA_LOG_PIPE", sessionId);
var log = provider.CreateLogger("Runtime.Startup");
log.LogInformation("Runtime initialized hash={Hash}", buildHash);
```
Loader internal:
```csharp
private static readonly ILogger Log = LoaderLog.GetLogger<HotReloadService>();
Log.LogInformation("Hot reload request path={Path}", runtimePath);
```
UI adapter:
```csharp
private static readonly ILogger Log = UiLog.GetLogger<RcaDockablePanel>();
Log.LogDebug("XAML resource loaded size={Size}", xaml.Length);
```

## 11. Migration Status
All `Debug.WriteLine` replaced except inside `DebugSink` (intentional sink implementation). Placeholder host and panel now use structured logging. Contracts stable for schema 1.

## 12. Backlog
- Idle watchdog & session timeout
- Retention & cleanup policy
- Dynamic runtime log level switching (control pipe)
- Structured scopes & contextual properties
- Binary framing for bulk performance
- Compression / pruning of fallback parts

## 13. Operational Notes
- Safe to remove fallback / emergency files while idle
- If logs show repeated fallback without recovery, inspect pipe availability or Loader startup sequence
- Multiple concurrent runtimes not yet supported (single-client assumption)

## 14. Quick Verification Checklist
- Loader starts: look for `Logging pipe server starting` and `Waiting for runtime logging connection` in file
- Runtime reload: new connection followed by runtime initialization logs; global sequence continues monotonic
- UI panel load: XAML resource log or InitializeComponent fallback log

---
Schema 1 complete; future changes must increment schema or supply compatibility handling in Loader.

Note: the build uses a named-pipe notification (`BUILD_COMPLETED`) to inform the running addin. Connection attempts may time out; these are logged as warnings by the build notifier and are non‑fatal.