using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

sealed class InstructionBundleCompiler
{
    private static readonly string[] RequiredFrameworkFiles =
    [
        "authority.md",
        "engine.md",
        "structure.md",
        "app-package.md",
        "contract-discipline.md",
        "request-lifecycle.md",
        "feature-catalog.md",
        "capability-model.md",
        "request-governance.md",
        "error-policy.md",
        "reliability.md",
        "self-healing.md",
        "change-governance.md",
        "validation-loop.md",
        "rollback-policy.md",
        "observability.md",
        "generic-runtime.md",
        "generic-security.md",
        "rate-limits.md"
    ];

    private readonly string _contentRootPath;
    private readonly ConcurrentDictionary<string, CachedInstructionBundle> _cache = new(StringComparer.Ordinal);

    public InstructionBundleCompiler(string contentRootPath)
    {
        _contentRootPath = contentRootPath;
    }

    public CompiledInstructionBundle GetOrCompile(string appId, string endpoint)
    {
        var routeKey = BuildRouteKey(appId, endpoint);
        var sourceFiles = CollectSourceFiles(appId, endpoint);
        var fingerprint = ComputeFingerprint(sourceFiles);

        if (_cache.TryGetValue(routeKey, out var cached) &&
            string.Equals(cached.Fingerprint, fingerprint, StringComparison.Ordinal))
        {
            return cached.Bundle with { CacheHit = true };
        }

        var baseInstructions = BuildBaseInstructions(appId, endpoint, sourceFiles);
        var developerInstructions = BuildDeveloperInstructions(appId, endpoint);
        var bundleHash = ComputeBundleHash(baseInstructions, developerInstructions);
        var bundle = new CompiledInstructionBundle(
            routeKey,
            appId,
            endpoint,
            bundleHash,
            baseInstructions,
            developerInstructions,
            sourceFiles,
            CacheHit: false);

        _cache[routeKey] = new CachedInstructionBundle(fingerprint, bundle);
        return bundle;
    }

    private IReadOnlyList<string> CollectSourceFiles(string appId, string endpoint)
    {
        var files = new List<string>();
        foreach (var frameworkFile in RequiredFrameworkFiles)
        {
            files.Add(GetFrameworkPath(frameworkFile));
        }

        var appDirectory = Path.Combine(_contentRootPath, "instructions", "apps", appId);
        if (!Directory.Exists(appDirectory))
        {
            files.Add(GetFrameworkPath(Path.Combine("errors", "app-not-found.md")));
            return files;
        }

        AddIfExists(files, Path.Combine(appDirectory, "app.md"));

        var endpointPath = Path.Combine(appDirectory, "endpoints", endpoint.Replace('/', Path.DirectorySeparatorChar) + ".md");
        if (!File.Exists(endpointPath))
        {
            files.Add(GetFrameworkPath(Path.Combine("errors", "endpoint-not-found.md")));
            return files;
        }

        files.Add(endpointPath);
        AddPreferredThenRemaining(files, Path.Combine(appDirectory, "config"), ["capabilities.md", "rate-limits.md", "self-healing.md"]);
        AddPreferredThenRemaining(files, Path.Combine(appDirectory, "storage"), ["local-json-state.md", "repair-policy.md"]);
        AddDirectoryFiles(files, Path.Combine(appDirectory, "entities"));

        foreach (var examplePath in SelectRelevantExampleFiles(Path.Combine(appDirectory, ".examples"), endpoint))
        {
            files.Add(examplePath);
        }

        return files;
    }

    private string BuildBaseInstructions(string appId, string endpoint, IReadOnlyList<string> sourceFiles)
    {
        var builder = new StringBuilder();
        builder.AppendLine("PhantomAPI authoritative compiled instruction bundle.");
        builder.AppendLine("These file snapshots are already loaded in the required framework order.");
        builder.AppendLine("Treat them as authoritative in-memory copies of the instruction system.");
        builder.AppendLine("Do not spend tool calls rediscovering instruction markdown files that are already included here.");
        builder.AppendLine("Use tools for live runtime state under data/apps/<app> and for safe mutations required by the selected endpoint.");
        builder.AppendLine();
        builder.AppendLine($"Selected app: {appId}");
        builder.AppendLine($"Selected endpoint: {endpoint}");
        builder.AppendLine($"Included source files: {sourceFiles.Count}");

        foreach (var fullPath in sourceFiles)
        {
            var relativePath = Path.GetRelativePath(_contentRootPath, fullPath).Replace('\\', '/');
            builder.AppendLine();
            builder.AppendLine($"===== BEGIN FILE: {relativePath} =====");
            builder.AppendLine(File.ReadAllText(fullPath).Trim());
            builder.AppendLine($"===== END FILE: {relativePath} =====");
        }

        return builder.ToString().Trim();
    }

    private static string BuildDeveloperInstructions(string appId, string endpoint)
    {
        var builder = new StringBuilder();
        builder.AppendLine("The base instructions already contain the authoritative PhantomAPI framework and app bundle for this route.");
        builder.AppendLine("Treat the bundled file snapshots as satisfying the required instruction read order for this request.");
        builder.AppendLine("This is a runtime turn, not a coding task.");
        builder.AppendLine("Do not inspect or modify instruction/source files under instructions/ or the repository unless a recovery path explicitly requires it.");
        builder.AppendLine("Bundled instructions describe rules, contracts, and shapes. They do not contain authoritative live entity values.");
        builder.AppendLine($"When current entities, sessions, balances, tasks, counters, or other persisted facts matter, read the live files under data/apps/{appId} before deciding.");
        builder.AppendLine("Do not infer current runtime state from examples, manifests, entity docs, or previous turn memory.");
        builder.AppendLine($"Only read or write live runtime state under data/apps/{appId} when the selected endpoint requires it.");
        builder.AppendLine("Do not produce patches, diffs, markdown, or explanations.");
        builder.AppendLine("After the minimum required runtime work is done, emit the final contract-shaped JSON immediately.");
        builder.AppendLine($"Selected route: app={appId}, endpoint={endpoint}.");
        builder.AppendLine("The user message is the raw HTTP request JSON body.");
        builder.AppendLine("Return only the final JSON response that matches the selected response contract exactly.");
        return builder.ToString().Trim();
    }

    private string GetFrameworkPath(string relativePath)
    {
        var fullPath = Path.Combine(_contentRootPath, "instructions", "framework", relativePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"No framework instruction file found at instructions/framework/{relativePath.Replace('\\', '/')}");
        }

        return fullPath;
    }

    private static IEnumerable<string> SelectRelevantExampleFiles(string examplesDirectory, string endpoint)
    {
        if (!Directory.Exists(examplesDirectory))
        {
            return [];
        }

        var endpointTokens = Tokenize(endpoint);
        return Directory.GetFiles(examplesDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(path => new
            {
                Path = path,
                Score = Tokenize(Path.GetFileNameWithoutExtension(path))
                    .Intersect(endpointTokens, StringComparer.Ordinal)
                    .Count()
            })
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .Select(candidate => candidate.Path);
    }

    private static HashSet<string> Tokenize(string value)
    {
        return value
            .Split(['/', '-', '_', '.', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => token.Trim().ToLowerInvariant())
            .Where(token => token.Length > 0)
            .Select(NormalizeToken)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string NormalizeToken(string token)
    {
        return token.Length > 3 && token.EndsWith('s')
            ? token[..^1]
            : token;
    }

    private static void AddPreferredThenRemaining(List<string> files, string directory, IReadOnlyList<string> preferredFileNames)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var preferredFileName in preferredFileNames)
        {
            AddIfExists(files, Path.Combine(directory, preferredFileName));
        }

        var remainingFiles = Directory.GetFiles(directory, "*.md", SearchOption.TopDirectoryOnly)
            .Where(path => !preferredFileNames.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

        foreach (var path in remainingFiles)
        {
            files.Add(path);
        }
    }

    private static void AddDirectoryFiles(List<string> files, string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var path in Directory.GetFiles(directory, "*.md", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            files.Add(path);
        }
    }

    private static void AddIfExists(List<string> files, string path)
    {
        if (File.Exists(path))
        {
            files.Add(path);
        }
    }

    private static string ComputeFingerprint(IReadOnlyList<string> sourceFiles)
    {
        var builder = new StringBuilder();
        foreach (var path in sourceFiles)
        {
            var info = new FileInfo(path);
            builder.Append(path);
            builder.Append('|');
            builder.Append(info.Length);
            builder.Append('|');
            builder.Append(info.LastWriteTimeUtc.Ticks);
            builder.AppendLine();
        }

        return ComputeBundleHash(builder.ToString(), string.Empty);
    }

    private static string ComputeBundleHash(string baseInstructions, string developerInstructions)
    {
        var bytes = Encoding.UTF8.GetBytes(baseInstructions + "\n---\n" + developerInstructions);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string BuildRouteKey(string appId, string endpoint)
    {
        return $"{appId}:{endpoint}";
    }

    private sealed record CachedInstructionBundle(string Fingerprint, CompiledInstructionBundle Bundle);
}

sealed record CompiledInstructionBundle(
    string RouteKey,
    string AppId,
    string Endpoint,
    string BundleHash,
    string BaseInstructions,
    string DeveloperInstructions,
    IReadOnlyList<string> SourceFiles,
    bool CacheHit);
