sealed record ConfiguredWarmStart(
    string AppId,
    string Endpoint,
    string EndpointPath,
    EndpointWarmStartOptions WarmStart);

static class EndpointWarmStartCatalog
{
    public static IReadOnlyList<ConfiguredWarmStart> Discover(string contentRootPath)
    {
        var appsRoot = Path.Combine(contentRootPath, "instructions", "apps");
        if (!Directory.Exists(appsRoot))
        {
            return [];
        }

        var configured = new List<ConfiguredWarmStart>();
        foreach (var appDirectory in Directory.GetDirectories(appsRoot).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var endpointRoot = Path.Combine(appDirectory, "endpoints");
            if (!Directory.Exists(endpointRoot))
            {
                continue;
            }

            var appId = Path.GetFileName(appDirectory);
            foreach (var endpointPath in Directory.GetFiles(endpointRoot, "*.md", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var document = EndpointMarkdownParser.Load(endpointPath);
                if (!document.WarmStart.IsConfigured)
                {
                    continue;
                }

                var endpoint = Path.GetRelativePath(endpointRoot, endpointPath)
                    .Replace('\\', '/');

                if (endpoint.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                {
                    endpoint = endpoint[..^3];
                }

                configured.Add(new ConfiguredWarmStart(appId, endpoint, endpointPath, document.WarmStart));
            }
        }

        return configured;
    }
}
