sealed record EndpointWarmStartOptions(
    string Mode,
    string? WarmupRequest,
    bool ReadOnlyWarmup)
{
    public static EndpointWarmStartOptions None { get; } = new("none", null, false);

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Mode) &&
        !string.Equals(Mode, "none", StringComparison.OrdinalIgnoreCase);

    public bool UsesExecSession =>
        string.Equals(Mode, "exec-session", StringComparison.OrdinalIgnoreCase);
}

sealed record EndpointMarkdownDocument(
    string Body,
    EndpointWarmStartOptions WarmStart);

static class EndpointMarkdownParser
{
    public static EndpointMarkdownDocument Load(string path)
    {
        return Parse(File.ReadAllText(path));
    }

    public static EndpointMarkdownDocument Parse(string markdown)
    {
        var normalized = markdown.Replace("\r\n", "\n");
        if (!normalized.StartsWith("---\n", StringComparison.Ordinal))
        {
            return new EndpointMarkdownDocument(markdown.Trim(), EndpointWarmStartOptions.None);
        }

        var closingMarkerIndex = normalized.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        if (closingMarkerIndex < 0)
        {
            return new EndpointMarkdownDocument(markdown.Trim(), EndpointWarmStartOptions.None);
        }

        var frontmatter = normalized[4..closingMarkerIndex];
        var body = normalized[(closingMarkerIndex + 5)..].Trim();

        var mode = "none";
        string? warmupRequest = null;
        var readOnlyWarmup = false;

        foreach (var rawLine in frontmatter.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (rawLine.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = rawLine.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = rawLine[..separatorIndex].Trim();
            var value = rawLine[(separatorIndex + 1)..].Trim().Trim('"', '\'');
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            switch (key)
            {
                case "warmStart":
                    mode = value;
                    break;
                case "warmupRequest":
                    warmupRequest = value;
                    break;
                case "readOnlyWarmup":
                    if (bool.TryParse(value, out var parsed))
                    {
                        readOnlyWarmup = parsed;
                    }
                    break;
            }
        }

        return new EndpointMarkdownDocument(
            body,
            new EndpointWarmStartOptions(mode, warmupRequest, readOnlyWarmup));
    }
}
