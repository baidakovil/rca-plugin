using System;
using System.CommandLine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

internal class Program
{
    private static int Main(string[] args)
    {
        var optRoots = new Option<string>("--roots", description: "Root directories to scan for source files (separated by ';' or ',').") { IsRequired = false };
        var optRoot = new Option<string>("--root", description: "Single root directory to scan for source files") { IsRequired = false };
        var optOut = new Option<string>("--out", description: "Output file path for the hash (default: source-hash.txt in first root)") { IsRequired = false };
        var optExt = new Option<string>("--ext", () => ".cs,.csproj,.props,.targets,.xaml,.resx,.json,.tt", "Comma-separated list of extensions to include");

        var rootCmd = new RootCommand("Source hash generator for RCA hot-reload");
        rootCmd.AddOption(optRoots);
        rootCmd.AddOption(optRoot);
        rootCmd.AddOption(optOut);
        rootCmd.AddOption(optExt);

        rootCmd.SetHandler((string roots, string root, string outPath, string ext) => Run(roots, root, outPath, ext), optRoots, optRoot, optOut, optExt);

        return rootCmd.Invoke(args);
    }

    private static void Run(string roots, string root, string @out, string ext)
    {
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

        var outputPath = @out;
        if (string.IsNullOrWhiteSpace(outputPath))
            outputPath = Path.Combine(rootList.First(), "source-hash.txt");

        var outDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

        File.WriteAllText(outputPath, hash);

        // Print the hash to stdout for logging
        Console.WriteLine(hash);
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
