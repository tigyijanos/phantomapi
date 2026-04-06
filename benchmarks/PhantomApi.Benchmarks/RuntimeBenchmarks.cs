using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Json.Schema;

[MemoryDiagnoser]
public class InstructionBundleCompilerBenchmarks
{
    private readonly string _repoRoot = BenchmarkFixture.RepoRoot;
    private InstructionBundleCompiler _cachedCompiler = null!;

    [GlobalSetup]
    public void Setup()
    {
        _cachedCompiler = new InstructionBundleCompiler(_repoRoot);
        _ = _cachedCompiler.GetOrCompile("task-board", "auth/login");
    }

    [Benchmark(Baseline = true)]
    public string GetOrCompile_Cold()
    {
        var compiler = new InstructionBundleCompiler(_repoRoot);
        return compiler.GetOrCompile("task-board", "auth/login").BundleHash;
    }

    [Benchmark]
    public string GetOrCompile_CacheHit()
    {
        return _cachedCompiler.GetOrCompile("task-board", "auth/login").BundleHash;
    }
}

[MemoryDiagnoser]
public class EndpointContractCacheBenchmarks
{
    private readonly string _repoRoot = BenchmarkFixture.RepoRoot;
    private EndpointContractCache _cachedContracts = null!;

    [GlobalSetup]
    public void Setup()
    {
        _cachedContracts = new EndpointContractCache(_repoRoot);
        _ = _cachedContracts.GetOrLoad("bank-api", "auth/login");
    }

    [Benchmark(Baseline = true)]
    public int GetOrLoad_Cold()
    {
        var cache = new EndpointContractCache(_repoRoot);
        return cache.GetOrLoad("bank-api", "auth/login").OutputSchemaJson.Length;
    }

    [Benchmark]
    public int GetOrLoad_CacheHit()
    {
        return _cachedContracts.GetOrLoad("bank-api", "auth/login").OutputSchemaJson.Length;
    }
}

[MemoryDiagnoser]
public class EndpointMetadataBenchmarks
{
    private string _loginMarkdown = null!;
    private JsonSchema _loginSchema = null!;
    private JsonElement _sampleResponse;
    private string _repoRoot = null!;

    [GlobalSetup]
    public void Setup()
    {
        _repoRoot = BenchmarkFixture.RepoRoot;
        var endpointPath = Path.Combine(_repoRoot, "instructions", "apps", "task-board", "endpoints", "auth", "login.md");
        _loginMarkdown = File.ReadAllText(endpointPath);
        _loginSchema = JsonSchema.FromText("""
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "object",
              "properties": {
                "ok": { "type": "boolean" },
                "token": { "type": "string" },
                "userId": { "type": "integer" },
                "fullName": { "type": "string" },
                "expiresAt": { "type": "string" },
                "error": { "type": "string" }
              },
              "required": ["ok", "token", "userId", "fullName", "expiresAt", "error"],
              "additionalProperties": false
            }
            """);
        using var responseDocument = JsonDocument.Parse("""
            {
              "ok": true,
              "token": "session_123",
              "userId": 10,
              "fullName": "Taylor Example",
              "expiresAt": "2026-03-15T12:00:00Z",
              "error": ""
            }
            """);
        _sampleResponse = responseDocument.RootElement.Clone();
    }

    [Benchmark(Baseline = true)]
    public string ParseEndpointFrontmatter()
    {
        return EndpointMarkdownParser.Parse(_loginMarkdown).WarmStart.Mode;
    }

    [Benchmark]
    public bool ValidateResponseAgainstSchema()
    {
        var result = _loginSchema.Evaluate(_sampleResponse);
        return result.IsValid;
    }

    [Benchmark]
    public int DiscoverConfiguredWarmStarts()
    {
        return EndpointWarmStartCatalog.Discover(_repoRoot).Count;
    }
}

static class BenchmarkFixture
{
    public static string RepoRoot { get; } = RepoRootLocator.Resolve(
        Directory.GetCurrentDirectory(),
        AppContext.BaseDirectory);
}
