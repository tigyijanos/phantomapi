using System.Text;
using System.Text.Json;

sealed class TraceLogger
{
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
    private readonly string _tracePath;

    public TraceLogger(string contentRootPath)
    {
        _tracePath = Path.Combine(contentRootPath, "data", "framework", "traces", "events.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(_tracePath)!);
    }

    public async Task WriteEventAsync(
        string correlationId,
        string? appId,
        string? endpoint,
        string stage,
        string result,
        long? durationMs,
        string? detail = null,
        string? error = null,
        Exception? exception = null,
        Dictionary<string, object?>? metadata = null)
    {
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["timestamp"] = DateTime.UtcNow.ToString("O"),
            ["correlationId"] = correlationId,
            ["stage"] = stage,
            ["result"] = result
        };

        if (!string.IsNullOrWhiteSpace(appId))
        {
            payload["app"] = appId;
        }

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            payload["endpoint"] = endpoint;
        }

        if (durationMs is not null)
        {
            payload["durationMs"] = durationMs;
        }

        if (!string.IsNullOrWhiteSpace(detail))
        {
            payload["detail"] = detail;
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            payload["error"] = error;
        }

        if (exception is not null)
        {
            payload["errorType"] = exception.GetType().FullName;
        }

        if (metadata is not null && metadata.Count > 0)
        {
            payload["metadata"] = metadata;
        }

        var line = JsonSerializer.Serialize(payload, _jsonOptions) + Environment.NewLine;

        await _writeGate.WaitAsync();
        try
        {
            await File.AppendAllTextAsync(_tracePath, line);
        }
        catch
        {
            // Tracing must never impact request success.
        }
        finally
        {
            _writeGate.Release();
        }
    }
}
