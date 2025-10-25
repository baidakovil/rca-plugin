using System;
using System.CommandLine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

internal class Program
{
    private static int Main(string[] args)
    {
        var optRoots = new Option<string>("--roots", description: "Root directories to scan for source files (separated by ';' or ',').") { IsRequired = false };
        var optRoot = new Option<string>("--root", description: "Single root directory to scan for source files") { IsRequired = false };
        var optOut = new Option<string>("--out", description: "Output file path for the hash") { IsRequired = false };
        var optExt = new Option<string>("--ext", () => ".cs,.csproj,.props,.targets,.xaml,.resx,.json,.tt", "Comma-separated list of extensions to include");

        // New options for group/timestamp/deploydir and timeout
        var optGroup = new Option<string>("--group", description: "Group name: Loader or Runtime") { IsRequired = true };
        var optTimestamp = new Option<string>("--timestamp", description: "Build timestamp used for deploy folder") { IsRequired = true };
        var optDeployDir = new Option<string>("--deployDir", description: "Directory where runtime/<timestamp> folder is located (optional)") { IsRequired = false };
        var optWaitMs = new Option<int>("--wait-ms", () => 600000, "Mutex wait timeout in milliseconds (default 600000 = 10min)");
        var optShortLen = new Option<int>("--short-length", () => 6, "Length of the short hash to emit (default 6)");

        var rootCmd = new RootCommand("Source hash generator for RCA hot-reload");
        rootCmd.AddOption(optRoots);
        rootCmd.AddOption(optRoot);
        rootCmd.AddOption(optOut);
        rootCmd.AddOption(optExt);
        rootCmd.AddOption(optGroup);
        rootCmd.AddOption(optTimestamp);
        rootCmd.AddOption(optDeployDir);
        rootCmd.AddOption(optWaitMs);
        rootCmd.AddOption(optShortLen);

        rootCmd.SetHandler((System.CommandLine.Invocation.InvocationContext ctx) =>
        {
            var pr = ctx.ParseResult;
            var rootsVal = pr.GetValueForOption(optRoots);
            var rootVal = pr.GetValueForOption(optRoot);
            var outVal = pr.GetValueForOption(optOut);
            var extVal = pr.GetValueForOption(optExt);
            var groupVal = pr.GetValueForOption(optGroup);
            var timestampVal = pr.GetValueForOption(optTimestamp);
            var deployDirVal = pr.GetValueForOption(optDeployDir);
            var waitMsVal = pr.GetValueForOption(optWaitMs);
            var shortLenVal = pr.GetValueForOption(optShortLen);
            Run(rootsVal, rootVal, outVal, extVal, groupVal, timestampVal, deployDirVal, waitMsVal, shortLenVal);
        });

        return rootCmd.Invoke(args);
    }

    private static void Run(string roots, string root, string @out, string ext, string group, string timestamp, string deployDir, int waitMs, int shortLen)
    {
        // Normalize group
        if (string.IsNullOrWhiteSpace(group))
        {
            Console.Error.WriteLine("--group is required");
            Environment.ExitCode = 2;
            return;
        }
        group = group.Trim();
        if (!string.Equals(group, "Loader", StringComparison.OrdinalIgnoreCase) && !string.Equals(group, "Runtime", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("--group must be 'Loader' or 'Runtime'");
            Environment.ExitCode = 2;
            return;
        }
        group = string.Equals(group, "Loader", StringComparison.OrdinalIgnoreCase) ? "Loader" : "Runtime";

        var rootList = new List<string>();

        if (!string.IsNullOrWhiteSpace(roots))
        {
            rootList.AddRange(SplitRoots(roots));
        }

        if (!string.IsNullOrWhiteSpace(root))
        {
            rootList.Add(root);
        }

        // Deduplicate and validate
        rootList = rootList
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => Path.GetFullPath(r))
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (rootList.Count == 0)
        {
            Console.Error.WriteLine("No valid root directories provided");
            Environment.ExitCode = 2;
            return;
        }

        var exts = ext.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.StartsWith('.') ? e : "." + e)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var ignoreDirs = new[] { "bin", "obj", ".git", ".vs", "node_modules", "packages" };

        // Collect files from all roots, with relative path rooted at the common parent if possible
        var allFiles = new List<string>();
        foreach (var r in rootList)
        {
            var files = Directory.EnumerateFiles(r, "*.*", SearchOption.AllDirectories)
                .Where(f => exts.Contains(Path.GetExtension(f)))
                .Where(f => !IsUnderIgnoredDir(f, r, ignoreDirs))
                .Select(f => Path.GetFullPath(f))
                .ToList();

            allFiles.AddRange(files);
        }

        // Deduplicate and sort by full path to ensure deterministic order
        var distinctFiles = allFiles.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(f => f, StringComparer.Ordinal).ToList();

        // Determine output path
        var outputPath = @out;
        // Do not create a default fallback file. Only write output when --out is provided.
        var writeOut = !string.IsNullOrWhiteSpace(outputPath);

        // Determine deploy folder where timestamp subfolder should exist
        string deployFolder = null;
        if (!string.IsNullOrWhiteSpace(deployDir))
        {
            deployFolder = Path.GetFullPath(deployDir);
        }
        else
        {
            // Fallback to output folder's parent if deployDir not provided
            var outDir = Path.GetDirectoryName(outputPath);
            deployFolder = string.IsNullOrEmpty(outDir) ? Directory.GetCurrentDirectory() : Path.GetFullPath(outDir);
        }

        var runtimeTimestampFolder = Path.Combine(deployFolder, timestamp);

        // Ensure directory exists (as user promised, it should exist), but create if missing to avoid exceptions
        try { Directory.CreateDirectory(runtimeTimestampFolder); } catch { /* ignore */ }

        // Mutex name - use solution-specific name if possible. Minimal: group + timestamp
        var mutexName = $"Global\\RCA_SourceHash_{group}_{timestamp}";

        bool acquired = false;
        Mutex? m = null;
        try
        {
            m = new Mutex(false, mutexName);
            try
            {
                acquired = m.WaitOne(waitMs);
            }
            catch (AbandonedMutexException)
            {
                // Treat as error per user's minimal policy
                Console.Error.WriteLine("Mutex was abandoned while waiting - aborting");
                Environment.ExitCode = 3;
                return;
            }

            if (!acquired)
            {
                Console.Error.WriteLine($"Timeout waiting for mutex '{mutexName}' after {waitMs} ms");
                Environment.ExitCode = 3;
                return;
            }

            // Under mutex: check for existing marker file in runtimeTimestampFolder
            var pattern = $"SourceHash-{group}-*.txt";
            var existing = Directory.GetFiles(runtimeTimestampFolder, pattern);
            if (existing.Length > 0)
            {
                // Read first valid file
                try
                {
                    var txt = File.ReadAllText(existing[0]).Trim();
                    // Write to outputPath for MSBuild
                    if (writeOut)
                    {
                        var outDir = Path.GetDirectoryName(outputPath);
                        if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
                        File.WriteAllText(outputPath, txt);
                    }
                    Console.WriteLine(txt);
                    return;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Failed to read existing source hash file '{existing[0]}': {ex.Message}");
                    Environment.ExitCode = 4;
                    return;
                }
            }

            // No existing file - compute hash
            using var sha = SHA256.Create();
            foreach (var f in distinctFiles)
            {
                if (IsTextFile(f))
                {
                    var text = File.ReadAllText(f);
                    text = text.Replace("\r\n", "\n").Replace("\r", "\n");
                    var bytes = Encoding.UTF8.GetBytes(text);
                    sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
                }
                else
                {
                    var bytes = File.ReadAllBytes(f);
                    sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
                }
            }

            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            var hash = BitConverter.ToString(sha.Hash!).Replace("-", "").ToLowerInvariant();
            var effectiveLen = shortLen > 0 ? shortLen : 6;
            var shortHash = hash.Length >= effectiveLen ? hash.Substring(0, effectiveLen) : hash;

            // Write marker file into runtimeTimestampFolder
            var markerFile = Path.Combine(runtimeTimestampFolder, $"SourceHash-{group}-{shortHash}.txt");
            try
            {
                File.WriteAllText(markerFile, shortHash);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to write marker file '{markerFile}': {ex.Message}");
                Environment.ExitCode = 5;
                return;
            }

            // Optionally write outputPath for MSBuild compatibility only if --out provided
            if (writeOut)
            {
                try
                {
                    var outDir = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
                    File.WriteAllText(outputPath, shortHash);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Failed to write output file '{outputPath}': {ex.Message}");
                    Environment.ExitCode = 6;
                    return;
                }
            }

            Console.WriteLine(shortHash);
        }
        finally
        {
            try { if (acquired && m != null) m.ReleaseMutex(); } catch { }
            try { m?.Dispose(); } catch { }
        }
    }

    private static IEnumerable<string> SplitRoots(string roots)
    {
        var seps = new[] { ';', '|', ',' };
        return roots.Split(seps, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool IsUnderIgnoredDir(string filePath, string root, string[] ignoreDirs)
    {
        var rel = Path.GetRelativePath(root, filePath);
        var parts = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var p in parts)
        {
            if (ignoreDirs.Contains(p, StringComparer.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool IsTextFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var textExt = new HashSet<string> { ".cs", ".csproj", ".props", ".targets", ".xaml", ".resx", ".json", ".tt", ".config", ".xml" };
        return textExt.Contains(ext);
    }
}
