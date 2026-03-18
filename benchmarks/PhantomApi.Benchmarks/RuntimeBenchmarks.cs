using System.Text.Json;
using BenchmarkDotNet.Attributes;

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
    private JsonElement _loginContract;
    private string _repoRoot = null!;

    [GlobalSetup]
    public void Setup()
    {
        _repoRoot = BenchmarkFixture.RepoRoot;
        var endpointPath = Path.Combine(_repoRoot, "instructions", "apps", "task-board", "endpoints", "auth", "login.md");
        _loginMarkdown = File.ReadAllText(endpointPath);
        using var contractDocument = JsonDocument.Parse("""
            {
              "sessionId": "session_123",
              "user": {
                "id": "user_1",
                "name": "Taylor"
              },
              "roles": ["admin", "editor"]
            }
            """);
        _loginContract = contractDocument.RootElement.Clone();
    }

    [Benchmark(Baseline = true)]
    public string ParseEndpointFrontmatter()
    {
        return EndpointMarkdownParser.Parse(_loginMarkdown).WarmStart.Mode;
    }

    [Benchmark]
    public string NormalizeResponseExampleToSchema()
    {
        return JsonSchemaUtilities.NormalizeToSchemaJson(_loginContract);
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
