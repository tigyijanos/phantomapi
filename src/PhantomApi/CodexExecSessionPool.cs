using System.Collections.Concurrent;
using System.Text.Json;

sealed class CodexExecSessionPool
{
    private readonly string _storePath;
    private readonly SemaphoreSlim _stateGate = new(1, 1);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _leases = new(StringComparer.Ordinal);
    private Dictionary<string, CodexExecSessionRecord> _records;

    public CodexExecSessionPool(string contentRootPath)
    {
        _storePath = Path.Combine(contentRootPath, "data", "framework", "exec-sessions.json");
        _records = LoadRecords();
    }

    public async Task<CodexExecSessionRecord?> GetAsync(string key, CancellationToken cancellationToken)
    {
        await _stateGate.WaitAsync(cancellationToken);
        try
        {
            return _records.TryGetValue(key, out var record)
                ? record
                : null;
        }
        finally
        {
            _stateGate.Release();
        }
    }

    public async Task SetAsync(string key, CodexExecSessionRecord record, CancellationToken cancellationToken)
    {
        await _stateGate.WaitAsync(cancellationToken);
        try
        {
            _records[key] = record;
            await PersistAsync(cancellationToken);
        }
        finally
        {
            _stateGate.Release();
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        await _stateGate.WaitAsync(cancellationToken);
        try
        {
            if (_records.Remove(key))
            {
                await PersistAsync(cancellationToken);
            }
        }
        finally
        {
            _stateGate.Release();
        }
    }

    public async Task<ExecSessionLease?> TryAcquireAsync(string key, CancellationToken cancellationToken)
    {
        var semaphore = _leases.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        if (!await semaphore.WaitAsync(0, cancellationToken))
        {
            return null;
        }

        return new ExecSessionLease(semaphore);
    }

    private Dictionary<string, CodexExecSessionRecord> LoadRecords()
    {
        if (!File.Exists(_storePath))
        {
            return new Dictionary<string, CodexExecSessionRecord>(StringComparer.Ordinal);
        }

        try
        {
            var raw = File.ReadAllText(_storePath);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, CodexExecSessionRecord>>(raw);
            return loaded is not null
                ? new Dictionary<string, CodexExecSessionRecord>(loaded, StringComparer.Ordinal)
                : new Dictionary<string, CodexExecSessionRecord>(StringComparer.Ordinal);
        }
        catch
        {
            return new Dictionary<string, CodexExecSessionRecord>(StringComparer.Ordinal);
        }
    }

    private async Task PersistAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_storePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _storePath + ".tmp";
        var json = JsonSerializer.Serialize(_records, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(tempPath, json, cancellationToken);
        File.Move(tempPath, _storePath, overwrite: true);
    }
}

sealed record CodexExecSessionRecord(
    string SessionId,
    string Model,
    string ReasoningEffort,
    string? ServiceTier,
    string BundleHash,
    DateTimeOffset UpdatedAtUtc);

sealed class ExecSessionLease : IAsyncDisposable
{
    private readonly SemaphoreSlim _semaphore;
    private bool _released;

    public ExecSessionLease(SemaphoreSlim semaphore)
    {
        _semaphore = semaphore;
    }

    public ValueTask DisposeAsync()
    {
        if (!_released)
        {
            _released = true;
            _semaphore.Release();
        }

        return ValueTask.CompletedTask;
    }
}
