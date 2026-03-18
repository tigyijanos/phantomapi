using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

sealed class EndpointContractCache
{
    private readonly string _contentRootPath;
    private readonly ConcurrentDictionary<string, CachedContract> _cache = new(StringComparer.Ordinal);

    public EndpointContractCache(string contentRootPath)
    {
        _contentRootPath = contentRootPath;
    }

    public ResolvedEndpointContract GetOrLoad(string appId, string endpoint)
    {
        var routeKey = BuildRouteKey(appId, endpoint);
        var sourcePath = ResolveInstructionSourcePath(appId, endpoint);
        var fingerprint = ComputeFingerprint(sourcePath);

        if (_cache.TryGetValue(routeKey, out var cached) &&
            string.Equals(cached.Fingerprint, fingerprint, StringComparison.Ordinal))
        {
            return cached.Contract with { CacheHit = true };
        }

        var document = EndpointMarkdownParser.Load(sourcePath);
        if (!TryExtractFirstJsonCodeBlock(document.Body, out var responseContractJson, out var extractionError))
        {
            throw new InvalidOperationException(extractionError);
        }

        using var responseContractDocument = JsonDocument.Parse(responseContractJson!);
        var responseContract = responseContractDocument.RootElement.Clone();
        var outputSchemaWasDerived = !JsonSchemaUtilities.LooksLikeJsonSchema(responseContract);
        var outputSchemaJson = JsonSchemaUtilities.NormalizeToSchemaJson(responseContract);

        var contract = new ResolvedEndpointContract(
            routeKey,
            sourcePath,
            responseContractJson!,
            responseContract,
            outputSchemaJson,
            outputSchemaWasDerived,
            CacheHit: false);

        _cache[routeKey] = new CachedContract(fingerprint, contract);
        return contract;
    }

    private string ResolveInstructionSourcePath(string appId, string endpoint)
    {
        var appDirectory = Path.Combine(_contentRootPath, "instructions", "apps", appId);
        if (!Directory.Exists(appDirectory))
        {
            return GetFrameworkPath(Path.Combine("errors", "app-not-found.md"));
        }

        var endpointPath = Path.Combine(appDirectory, "endpoints", endpoint.Replace('/', Path.DirectorySeparatorChar) + ".md");
        if (!File.Exists(endpointPath))
        {
            return GetFrameworkPath(Path.Combine("errors", "endpoint-not-found.md"));
        }

        return endpointPath;
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

    private static bool TryExtractFirstJsonCodeBlock(string markdown, out string? json, out string? error)
    {
        var match = Regex.Match(markdown, "```json\\s*(?<json>[\\s\\S]*?)```", RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            json = null;
            error = "Add a ```json ... ``` block to the endpoint instruction. That block is the hard response contract.";
            return false;
        }

        json = match.Groups["json"].Value.Trim();
        error = null;
        return true;
    }

    private static string ComputeFingerprint(string sourcePath)
    {
        var fileInfo = new FileInfo(sourcePath);
        return $"{sourcePath}|{fileInfo.Length}|{fileInfo.LastWriteTimeUtc.Ticks}";
    }

    private static string BuildRouteKey(string appId, string endpoint)
    {
        return $"{appId}:{endpoint}";
    }

    private sealed record CachedContract(string Fingerprint, ResolvedEndpointContract Contract);
}

sealed record ResolvedEndpointContract(
    string RouteKey,
    string SourcePath,
    string ResponseContractJson,
    JsonElement ResponseContract,
    string OutputSchemaJson,
    bool OutputSchemaWasDerived,
    bool CacheHit);
