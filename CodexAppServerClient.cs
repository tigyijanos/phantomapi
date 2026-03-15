using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

sealed class CodexAppServerClient : IAsyncDisposable
{
    private readonly PhantomOptions _options;
    private readonly TraceLogger _traceLogger;
    private readonly string _workingDirectory;
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly SemaphoreSlim _startupGate = new(1, 1);
    private readonly SemaphoreSlim _initGate = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _pendingResponses = new();
    private readonly ConcurrentDictionary<string, TurnCompletionState> _pendingTurnCompletions = new();
    private readonly ProcessStartInfo _appServerStartInfo;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
    private readonly string _warmFailureSnapshotDirectory;

    private readonly string[] _stdoutErrorIndicators =
    [
        "error",
        "failed",
        "unhandled",
        "panic"
    ];

    private Process? _process;
    private StreamWriter? _writer;
    private Task? _readLoop;
    private CancellationTokenSource? _readLoopCts;
    private long _requestId;
    private bool _isRunning;
    private bool _disposed;
    private readonly object _processSync = new();

    public CodexAppServerClient(PhantomOptions options, string workingDirectory, TraceLogger traceLogger)
    {
        _options = options;
        _traceLogger = traceLogger;
        _workingDirectory = workingDirectory;
        _warmFailureSnapshotDirectory = Path.Combine(workingDirectory, "data", "framework", "traces", "warm-failures");
        _appServerStartInfo = new ProcessStartInfo
        {
            FileName = options.CliCommand,
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            ArgumentList =
            {
                "app-server",
                "--listen",
                "stdio://",
            }
        };
    }

    public async Task<string> InvokeAsync(
        RuntimeProfile profile,
        string rawRequest,
        string outputSchemaJson,
        CompiledInstructionBundle instructionBundle,
        string correlationId,
        string appId,
        string endpoint,
        CancellationToken cancellationToken)
    {
        await _startupGate.WaitAsync(cancellationToken);
        try
        {
            await TraceStepAsync(
                correlationId,
                appId,
                endpoint,
                "appserver.startup-check",
                () => StartIfNeededAsync(correlationId, appId, endpoint, cancellationToken),
                new()
                {
                    ["mode"] = _options.UseWarmAppServer ? "warm" : "cold",
                    ["serviceTier"] = profile.ServiceTier ?? "default"
                });
        }
        finally
        {
            _startupGate.Release();
        }

        var requestThreadId = await TraceStepAsync(
            correlationId,
            appId,
            endpoint,
            "appserver.start-thread",
            () => StartThreadAsync(profile, instructionBundle, cancellationToken, ephemeral: true),
            new()
            {
                ["routeKey"] = instructionBundle.RouteKey,
                ["bundleHash"] = instructionBundle.BundleHash,
                ["bundleCacheHit"] = instructionBundle.CacheHit,
                ["sourceFileCount"] = instructionBundle.SourceFiles.Count
            });

        var responseTask = TraceStepAsync(
            correlationId,
            appId,
            endpoint,
            "appserver.start-turn",
            () => StartTurnAsync(requestThreadId, profile, rawRequest, outputSchemaJson, correlationId, appId, endpoint, cancellationToken),
            new() { ["threadId"] = requestThreadId, ["model"] = profile.Model });
        return await responseTask;
    }

    private async Task StartIfNeededAsync(string correlationId, string appId, string endpoint, CancellationToken cancellationToken)
    {
        var shouldInitializeExistingProcess = false;
        var shouldStartProcess = false;

        lock (_processSync)
        {
            if (_isRunning && _process is not null && !_process.HasExited)
            {
                if (_isInitialized)
                {
                    return;
                }

                shouldInitializeExistingProcess = true;
                return;
            }

            if (_process is not null && _process.HasExited)
            {
                _process = null;
                _writer = null;
                _isInitialized = false;
            }

            shouldStartProcess = true;
            _isRunning = true;
        }

        if (shouldInitializeExistingProcess)
        {
            await EnsureInitializedAsync(correlationId, appId, endpoint, cancellationToken);
            return;
        }

        try
        {
            if (!shouldStartProcess)
            {
                return;
            }

            var process = new Process { StartInfo = _appServerStartInfo };
            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start `codex app-server`.");
            }

            await _traceLogger.WriteEventAsync(
                correlationId,
                appId,
                endpoint,
                "appserver.start",
                "success",
                null,
                metadata: new()
                {
                    ["serviceTier"] = _options.NormalServiceTier ?? "default"
                });

            _process = process;
            _writer = process.StandardInput;
            _readLoopCts = new CancellationTokenSource();
            var reader = process.StandardOutput;

            _readLoop = Task.Run(() => ConsumeOutputAsync(reader, _readLoopCts.Token), cancellationToken);
            _ = Task.Run(() => ConsumeErrorsAsync(process.StandardError, _readLoopCts.Token), cancellationToken);

            _isInitialized = false;
            try
            {
                await EnsureInitializedAsync(correlationId, appId, endpoint, cancellationToken);
            }
            catch
            {
                await StopAsync(false);
                throw;
            }
        }
        catch
        {
            await StopAsync(false);
            throw;
        }
    }

    private async Task EnsureInitializedAsync(string correlationId, string appId, string endpoint, CancellationToken cancellationToken)
    {
        if (_isInitialized)
        {
            return;
        }

        await _initGate.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized)
            {
                return;
            }

            await TraceStepAsync(
                correlationId,
                appId,
                endpoint,
                "appserver.initialize",
                () => SendJsonRpcRequestAsync<Dictionary<string, object?>>(
                    "initialize",
                    new Dictionary<string, object?>
                    {
                        ["clientInfo"] = new Dictionary<string, object?>
                        {
                            ["name"] = "phantomapi",
                            ["version"] = "1.0.0"
                        }
                    },
                    cancellationToken),
                new() { ["method"] = "initialize" });

            await TraceStepAsync(
                correlationId,
                appId,
                endpoint,
                "appserver.initialized-notification",
                () => SendNotificationAsync(
                    "initialized",
                    new Dictionary<string, object?>(),
                    cancellationToken),
                new() { ["method"] = "initialized" });

            _isInitialized = true;
        }
        finally
        {
            _initGate.Release();
        }
    }

    private async Task SendNotificationAsync(string method, Dictionary<string, object?> parameters, CancellationToken cancellationToken)
    {
        if (_writer is null || _process is null || _process.HasExited)
        {
            throw new InvalidOperationException("app-server is not running.");
        }

        var payload = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["method"] = method,
            ["params"] = parameters
        };

        var raw = JsonSerializer.Serialize(payload, _jsonOptions);
        await WriteLineAsync(raw, cancellationToken);
    }

    private bool _isInitialized;

    private async Task<T> TraceStepAsync<T>(
        string correlationId,
        string appId,
        string endpoint,
        string stage,
        Func<Task<T>> action,
        Dictionary<string, object?>? metadata = null)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await action();
            await _traceLogger.WriteEventAsync(
                correlationId,
                appId,
                endpoint,
                stage,
                "success",
                stopwatch.ElapsedMilliseconds,
                detail: "completed",
                metadata: metadata);

            return result;
        }
        catch (Exception ex)
        {
            await _traceLogger.WriteEventAsync(
                correlationId,
                appId,
                endpoint,
                stage,
                "failed",
                stopwatch.ElapsedMilliseconds,
                error: ex.Message,
                exception: ex,
                metadata: metadata);
            throw;
        }
    }

    private async Task TraceStepAsync(
        string correlationId,
        string appId,
        string endpoint,
        string stage,
        Func<Task> action,
        Dictionary<string, object?>? metadata = null)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await action();
            await _traceLogger.WriteEventAsync(
                correlationId,
                appId,
                endpoint,
                stage,
                "success",
                stopwatch.ElapsedMilliseconds,
                detail: "completed",
                metadata: metadata);
        }
        catch (Exception ex)
        {
            await _traceLogger.WriteEventAsync(
                correlationId,
                appId,
                endpoint,
                stage,
                "failed",
                stopwatch.ElapsedMilliseconds,
                error: ex.Message,
                exception: ex,
                metadata: metadata);
            throw;
        }
    }

    private async Task<string> StartThreadAsync(
        RuntimeProfile profile,
        CompiledInstructionBundle? instructionBundle,
        CancellationToken cancellationToken,
        bool ephemeral = false)
    {
        var startParams = new Dictionary<string, object?>
        {
            ["cwd"] = _workingDirectory,
            ["approvalPolicy"] = "never",
            ["sandbox"] = "danger-full-access",
            ["model"] = profile.Model
        };

        if (profile.ServiceTier is not null)
        {
            startParams["serviceTier"] = profile.ServiceTier;
        }

        if (ephemeral)
        {
            startParams["ephemeral"] = true;
        }

        if (instructionBundle is not null)
        {
            startParams["baseInstructions"] = instructionBundle.BaseInstructions;
            startParams["developerInstructions"] = instructionBundle.DeveloperInstructions;
        }

        var threadResult = await SendJsonRpcRequestAsync<Dictionary<string, object?>>("thread/start", startParams, cancellationToken);
        return ExtractThreadId(threadResult, "app-server did not return a thread identifier.");
    }

    private async Task<string> StartTurnAsync(
        string threadId,
        RuntimeProfile profile,
        string rawRequest,
        string outputSchemaJson,
        string correlationId,
        string appId,
        string endpoint,
        CancellationToken cancellationToken)
    {
        var inputItem = new Dictionary<string, object?>
        {
            ["type"] = "text",
            ["text"] = rawRequest
        };
        var turnParams = new Dictionary<string, object?>();
        turnParams["threadId"] = threadId;
        turnParams["input"] = new[] { inputItem };
        turnParams["approvalPolicy"] = "never";
        turnParams["model"] = profile.Model;
        turnParams["effort"] = profile.ReasoningEffort;
        if (profile.ServiceTier is not null)
        {
            turnParams["serviceTier"] = profile.ServiceTier;
        }

        if (!string.IsNullOrWhiteSpace(outputSchemaJson))
        {
            using var outputSchema = JsonDocument.Parse(outputSchemaJson);
            if (LooksLikeJsonSchema(outputSchema.RootElement))
            {
                turnParams["outputSchema"] = outputSchema.RootElement.Clone();
            }
            else
            {
                turnParams["outputSchema"] = BuildJsonSchemaFromExample(outputSchema.RootElement);
                _ = _traceLogger.WriteEventAsync(
                    correlationId,
                    appId,
                    endpoint,
                    "appserver.output-schema.derived",
                    "info",
                    null,
                    detail: "Derived JSON Schema from response contract example.",
                    metadata: new()
                    {
                        ["method"] = "turn.start",
                        ["reason"] = "contract-example"
                    });
            }
        }

        using var turnCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        turnCts.CancelAfter(TimeSpan.FromSeconds(_options.CliTimeoutSeconds));
        var turnResult = await SendJsonRpcRequestAsync<Dictionary<string, object?>>("turn/start", turnParams, turnCts.Token);
        if (!turnResult.TryGetProperty("turn", out var turnElement) || !turnElement.TryGetProperty("id", out var turnIdElement))
        {
            throw new InvalidOperationException("app-server did not return a turn identifier.");
        }

        var turnId = turnIdElement.GetString();
        if (string.IsNullOrWhiteSpace(turnId))
        {
            throw new InvalidOperationException("app-server returned an empty turn identifier.");
        }

        var completionState = new TurnCompletionState
        {
            CompletionSource = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously),
            CorrelationId = correlationId,
            AppId = appId,
            Endpoint = endpoint,
            ThreadId = threadId
        };

        if (!_pendingTurnCompletions.TryAdd(turnId, completionState))
        {
            throw new InvalidOperationException($"Duplicate turn identifier from app-server: {turnId}");
        }
        _ = ScheduleTurnFallback(completionState, turnId);

        try
        {
            using var completionWaitCts = CancellationTokenSource.CreateLinkedTokenSource(turnCts.Token);
            completionWaitCts.CancelAfter(TimeSpan.FromSeconds(_options.CliTimeoutSeconds));
            return await completionState.CompletionSource.Task.WaitAsync(completionWaitCts.Token);
        }
        finally
        {
            _pendingTurnCompletions.TryRemove(turnId, out _);
        }
    }

    private static string ExtractThreadId(JsonElement response, string errorMessage)
    {
        if (response.TryGetProperty("thread", out var threadElement) &&
            threadElement.TryGetProperty("id", out var threadIdElement))
        {
            var threadId = threadIdElement.GetString();
            if (!string.IsNullOrWhiteSpace(threadId))
            {
                return threadId;
            }
        }

        throw new InvalidOperationException(errorMessage);
    }

    private async Task<JsonElement> SendJsonRpcRequestAsync<TParams>(string method, TParams parameters, CancellationToken cancellationToken)
        where TParams : class?
    {
        if (_writer is null || _process is null || _process.HasExited)
        {
            throw new InvalidOperationException("app-server is not running.");
        }

        var id = Interlocked.Increment(ref _requestId).ToString(CultureInfo.InvariantCulture);
        var request = new Dictionary<string, object?>();
        request["jsonrpc"] = "2.0";
        request["id"] = id;
        request["method"] = method;
        request["params"] = parameters;

        var payload = JsonSerializer.Serialize(request, _jsonOptions);
        var responseTcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingResponses[id] = responseTcs;

        await WriteLineAsync(payload, cancellationToken);

        using var responseCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        responseCts.CancelAfter(TimeSpan.FromSeconds(_options.CliTimeoutSeconds));
        try
        {
            return await responseTcs.Task.WaitAsync(responseCts.Token);
        }
        catch
        {
            _pendingResponses.TryRemove(id, out _);
            throw;
        }
    }

    private async Task WriteLineAsync(string payload, CancellationToken cancellationToken)
    {
        if (_writer is null)
        {
            throw new InvalidOperationException("app-server writer is not initialized.");
        }

        await _sendGate.WaitAsync(cancellationToken);
        try
        {
            await _writer.WriteLineAsync(payload.AsMemory(), cancellationToken);
            await _writer.FlushAsync(cancellationToken);
        }
        finally
        {
            _sendGate.Release();
        }
    }

    private async Task ConsumeOutputAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            ProcessMessageLine(line);
        }

        if (!_disposed && _process is not null && !_process.HasExited)
        {
            // If output closes unexpectedly, surface a failure for all in-flight requests.
            foreach (var pendingTurn in _pendingTurnCompletions)
            {
                pendingTurn.Value.CompletionSource.TrySetException(new InvalidOperationException("app-server closed its output stream."));
            }

            foreach (var pendingResponse in _pendingResponses)
            {
                pendingResponse.Value.TrySetException(new InvalidOperationException("app-server closed its output stream."));
            }
        }
    }

    private async Task ConsumeErrorsAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            // We don't treat stderr as fatal, but we keep the stream drained to avoid deadlock.
            if (_stdoutErrorIndicators.Any(line.Contains))
            {
                // keep diagnostics available through exception messages when available.
                continue;
            }
        }
    }

    private void ProcessMessageLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (root.TryGetProperty("id", out var idElement))
        {
            var id = ExtractRequestId(idElement);
            if (!string.IsNullOrWhiteSpace(id))
            {
                if (root.TryGetProperty("result", out var resultElement))
                {
                    if (_pendingResponses.TryRemove(id, out var responseTcs))
                    {
                        responseTcs.TrySetResult(resultElement.Clone());
                    }

                    return;
                }

                if (root.TryGetProperty("error", out var errorElement))
                {
                    if (_pendingResponses.TryRemove(id, out var responseTcs))
                    {
                        responseTcs.TrySetException(new InvalidOperationException(
                            $"app-server returned an error for request {id}: {errorElement.GetRawText()}"));
                    }

                    return;
                }
            }
        }

        if (!root.TryGetProperty("method", out var methodElement) || methodElement.ValueKind != JsonValueKind.String)
        {
            return;
        }

        var method = methodElement.GetString();
        if (string.IsNullOrWhiteSpace(method))
        {
            return;
        }

        TrackNotification(root, method);

        if (method == "turn/completed")
        {
            HandleTurnCompleted(root);
            return;
        }

        if (method == "item/completed")
        {
            HandleItemCompleted(root);
            return;
        }

        if (method == "item/agentMessage/delta")
        {
            HandleAgentMessageDelta(root);
            return;
        }

        var codexEventType = method.StartsWith("codex/event/", StringComparison.Ordinal)
            ? method["codex/event/".Length..]
            : null;
        if (!string.IsNullOrWhiteSpace(codexEventType))
        {
            if (codexEventType is "task_complete" or "task/complete")
            {
                HandleTaskComplete(root);
            }
            else if (codexEventType is "agent_message")
            {
                HandleAgentMessageEvent(root);
            }
            else if (codexEventType is "agent_message_delta" or "agent_message_content_delta")
            {
                HandleAgentMessageDelta(root);
            }
            else if (codexEventType is "raw_response_item")
            {
                HandleRawResponseItem(root);
            }

            return;
        }

        if (method == "event")
        {
            if (root.TryGetProperty("params", out var eventParamsElement) &&
                eventParamsElement.ValueKind == JsonValueKind.Object &&
                eventParamsElement.TryGetProperty("type", out var eventTypeElement) &&
                eventTypeElement.ValueKind == JsonValueKind.String)
            {
                var eventType = eventTypeElement.GetString();
                if (eventType is "task_complete" or "task/complete")
                {
                    HandleTaskComplete(root);
                }
                else if (eventType is "agent_message")
                {
                    HandleAgentMessageEvent(root);
                }
                else if (eventType is "agent_message_delta" or "agent_message_content_delta")
                {
                    HandleAgentMessageDelta(root);
                }
                else if (eventType is "raw_response_item")
                {
                    HandleRawResponseItem(root);
                }
            }

            return;
        }

        if (method is "task/complete" or "task_complete")
        {
            HandleTaskComplete(root);
        }
    }

    private void TrackNotification(JsonElement message, string method)
    {
        if (!message.TryGetProperty("params", out var paramsElement) || paramsElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (!TryResolveCompletionState(paramsElement, out _, out var completionState))
        {
            return;
        }

        var label = method;
        if (string.Equals(method, "event", StringComparison.Ordinal) &&
            paramsElement.TryGetProperty("type", out var eventTypeElement) &&
            eventTypeElement.ValueKind == JsonValueKind.String)
        {
            label = $"event:{eventTypeElement.GetString()}";
        }

        completionState.NotificationTrail.Add(label);
        if (completionState.NotificationTrail.Count > 12)
        {
            completionState.NotificationTrail.RemoveAt(0);
        }

        completionState.NotificationSamples.Add(BuildNotificationSample(label, paramsElement));
        if (completionState.NotificationSamples.Count > 8)
        {
            completionState.NotificationSamples.RemoveAt(0);
        }

        completionState.LastNotificationLabel = label;
        completionState.LastNotificationAtUtc = DateTimeOffset.UtcNow;
    }

    private static string BuildNotificationSample(string label, JsonElement paramsElement)
    {
        var parts = new List<string>
        {
            label
        };

        var turnId = TryGetStringProperty(paramsElement, "turnId", "turn_id", "turn", "threadId", "thread_id");
        if (!string.IsNullOrWhiteSpace(turnId))
        {
            parts.Add($"turn={turnId}");
        }

        if (paramsElement.TryGetProperty("item", out var itemElement) && itemElement.ValueKind == JsonValueKind.Object)
        {
            var itemType = TryGetStringProperty(itemElement, "type");
            if (!string.IsNullOrWhiteSpace(itemType))
            {
                parts.Add($"item.type={itemType}");
            }

            var phase = TryGetStringProperty(itemElement, "phase");
            if (!string.IsNullOrWhiteSpace(phase))
            {
                parts.Add($"item.phase={phase}");
            }

            var directText = TryGetStringProperty(itemElement, "text");
            if (!string.IsNullOrWhiteSpace(directText))
            {
                parts.Add($"item.text.len={directText.Length}");
            }

            if (itemElement.TryGetProperty("content", out var contentElement) && contentElement.ValueKind == JsonValueKind.Array)
            {
                parts.Add($"item.content.count={contentElement.GetArrayLength()}");
            }
        }

        if (paramsElement.TryGetProperty("status", out var statusElement) && statusElement.ValueKind == JsonValueKind.String)
        {
            parts.Add($"status={statusElement.GetString()}");
        }

        if (paramsElement.TryGetProperty("type", out var typeElement) && typeElement.ValueKind == JsonValueKind.String)
        {
            parts.Add($"type={typeElement.GetString()}");
        }

        if (paramsElement.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == JsonValueKind.String)
        {
            parts.Add($"message.len={messageElement.GetString()?.Length ?? 0}");
        }

        return string.Join(" | ", parts);
    }

    private void HandleTurnCompleted(JsonElement message)
    {
        if (!message.TryGetProperty("params", out var paramsElement) || paramsElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        JsonElement? turnElement = null;
        if (paramsElement.TryGetProperty("turn", out var nestedTurnElement) && nestedTurnElement.ValueKind == JsonValueKind.Object)
        {
            turnElement = nestedTurnElement;
        }

        var turnId = TryGetStringProperty(turnElement ?? paramsElement, "id", "turnId", "turn_id");
        if (string.IsNullOrWhiteSpace(turnId))
        {
            turnId = TryGetStringProperty(paramsElement, "threadId", "turnId", "turn_id", "thread_id");
        }

        if (string.IsNullOrWhiteSpace(turnId))
        {
            return;
        }

        if (!_pendingTurnCompletions.TryGetValue(turnId, out var completionState))
        {
            return;
        }

        var statusElement = turnElement is null ? (JsonElement?)null : turnElement.Value.TryGetProperty("status", out var existingStatus) ? existingStatus : null;
        if (statusElement is null && paramsElement.TryGetProperty("status", out var fallbackStatus))
        {
            statusElement = fallbackStatus;
        }

        if (statusElement is not null && statusElement.Value.GetString() is "failed" or "interrupted")
        {
            var status = statusElement.Value.GetString() ?? "failed";
            var details = $"app-server turn {turnId} status was {status}.";
            var statusSource = turnElement ?? paramsElement;
            if (statusSource.TryGetProperty("error", out var errorElement) && errorElement.ValueKind != JsonValueKind.Null)
            {
                details += $" Error: {errorElement.GetRawText()}";
            }

            completionState.CompletionSource.TrySetException(new InvalidOperationException(details));
            return;
        }

        var finalText = turnElement is null ? null : ExtractFinalMessageFromTurn(turnElement.Value);
        if (string.IsNullOrWhiteSpace(finalText))
        {
            finalText = TryGetStringProperty(paramsElement, "text", "message", "finalText", "final_text");
        }
        if (string.IsNullOrWhiteSpace(finalText))
        {
            completionState.TurnCompletedWithoutMessage = true;
            if (!string.IsNullOrWhiteSpace(completionState.PendingAgentMessageText))
            {
                TryCompleteTurn(
                    completionState,
                    turnId,
                    completionState.PendingAgentMessageText,
                    "appserver.item-completed",
                    "Recovered final text from turn item event.");
                return;
            }

            if (TryGetCompletedJsonFromStreamedText(completionState, out var streamedJson))
            {
                TryCompleteTurn(
                    completionState,
                    turnId,
                    streamedJson,
                    "appserver.agent-message-delta",
                    "Recovered final JSON from streamed agent-message deltas.");
                return;
            }

            ScheduleCompletedTurnRecovery(completionState, turnId);
            _ = _traceLogger.WriteEventAsync(
                completionState.CorrelationId,
                completionState.AppId,
                completionState.Endpoint,
                "appserver.turn-completed",
                "success",
                null,
                detail: "Received completed turn event without agentMessage text.",
                metadata: new()
                {
                    ["turnId"] = turnId,
                    ["status"] = statusElement.HasValue && statusElement.Value.ValueKind == JsonValueKind.String
                        ? statusElement.Value.GetString()
                        : "unknown",
                    ["itemsPresent"] = turnElement is not null &&
                        turnElement.Value.TryGetProperty("items", out var itemsElement) &&
                        itemsElement.ValueKind == JsonValueKind.Array
                        && itemsElement.GetArrayLength() > 0
                });
            _ = ScheduleTurnFallback(completionState, turnId);

            return;
        }

        TryCompleteTurn(
            completionState,
            turnId,
            finalText,
            "appserver.turn-completed",
            "Captured final text from turn.items.");
    }

    private void HandleTaskComplete(JsonElement message)
    {
        if (!message.TryGetProperty("params", out var paramsElement) || paramsElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var turnId = TryGetStringProperty(
            paramsElement,
            "turn_id",
            "turnId",
            "turn");
        if (string.IsNullOrWhiteSpace(turnId))
        {
            return;
        }

        if (!_pendingTurnCompletions.TryGetValue(turnId, out var completionState))
        {
            return;
        }

        var finalText = TryGetStringProperty(
            paramsElement,
            "last_agent_message",
            "lastAgentMessage",
            "message",
            "text");

        if (string.IsNullOrWhiteSpace(finalText))
        {
            completionState.TaskCompletedWithoutMessage = true;
            if (completionState.TurnCompletedWithoutMessage)
            {
                ScheduleCompletedTurnRecovery(completionState, turnId);
                _ = ScheduleTurnFallback(completionState, turnId);
            }
            else if (!string.IsNullOrWhiteSpace(completionState.PendingAgentMessageText))
            {
                TryCompleteTurn(
                    completionState,
                    turnId,
                    completionState.PendingAgentMessageText,
                    "appserver.item-completed",
                    "Recovered final text from turn item event.");
            }

            return;
        }

        completionState.PendingAgentMessageText = finalText;
        TryCompleteTurn(
            completionState,
            turnId,
            finalText,
            "appserver.task-complete",
            "Captured final text from task_complete.");
    }

    private void HandleAgentMessageDelta(JsonElement message)
    {
        if (!message.TryGetProperty("params", out var paramsElement) || paramsElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (!TryResolveCompletionState(paramsElement, out var turnId, out var completionState))
        {
            return;
        }

        var delta = TryGetStringProperty(paramsElement, "delta");
        if (string.IsNullOrWhiteSpace(delta))
        {
            return;
        }

        completionState.StreamedAgentMessageText.Append(delta);
    }

    private void HandleAgentMessageEvent(JsonElement message)
    {
        if (!message.TryGetProperty("params", out var paramsElement) || paramsElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (!TryResolveCompletionState(paramsElement, out var turnId, out var completionState))
        {
            return;
        }

        var text = TryGetStringProperty(paramsElement, "message", "text");
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        completionState.PendingAgentMessageText = text;
        var isFinalPhase = string.Equals(
            TryGetStringProperty(paramsElement, "phase"),
            "final_answer",
            StringComparison.OrdinalIgnoreCase);

        if (isFinalPhase || completionState.TurnCompletedWithoutMessage)
        {
            TryCompleteTurn(
                completionState,
                turnId,
                text,
                "appserver.agent-message",
                isFinalPhase
                    ? "Captured final text from agent_message event."
                    : "Recovered final text from agent_message event.");
        }
    }

    private void HandleItemCompleted(JsonElement message)
    {
        if (!message.TryGetProperty("params", out var paramsElement) || paramsElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (!TryResolveCompletionState(paramsElement, out var turnId, out var completionState))
        {
            return;
        }

        if (!paramsElement.TryGetProperty("item", out var itemElement) || itemElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (!TryExtractTextFromThreadItem(itemElement, out var text, out var isFinalPhase))
        {
            return;
        }

        completionState.PendingAgentMessageText = text;
        if (isFinalPhase || completionState.TurnCompletedWithoutMessage)
        {
            TryCompleteTurn(
                completionState,
                turnId,
                text,
                "appserver.item-completed",
                isFinalPhase
                    ? "Captured final turn item text from item/completed."
                    : "Recovered final text from turn item event.");
        }
    }

    private void HandleRawResponseItem(JsonElement message)
    {
        if (!message.TryGetProperty("params", out var paramsElement) || paramsElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (!TryResolveCompletionState(paramsElement, out var turnId, out var completionState))
        {
            return;
        }

        if (!paramsElement.TryGetProperty("item", out var itemElement) || itemElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (!TryExtractTextFromResponseItem(itemElement, out var text, out var isFinalPhase))
        {
            return;
        }

        completionState.PendingAgentMessageText = text;
        if (isFinalPhase || completionState.TurnCompletedWithoutMessage)
        {
            TryCompleteTurn(
                completionState,
                turnId,
                text,
                "appserver.raw-response-item",
                isFinalPhase
                    ? "Captured final text from raw response item."
                    : "Recovered final text from raw response item.");
        }
    }

    private bool TryResolveCompletionState(
        JsonElement paramsElement,
        out string turnId,
        out TurnCompletionState completionState)
    {
        turnId = TryGetStringProperty(paramsElement, "turnId", "turn_id", "turn", "threadId", "thread_id") ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(turnId) && _pendingTurnCompletions.TryGetValue(turnId, out completionState!))
        {
            return true;
        }

        if (_pendingTurnCompletions.Count == 1)
        {
            var singlePendingTurn = _pendingTurnCompletions.First();
            turnId = singlePendingTurn.Key;
            completionState = singlePendingTurn.Value;
            return true;
        }

        completionState = null!;
        turnId = string.Empty;
        return false;
    }

    private static bool TryExtractTextFromThreadItem(
        JsonElement itemElement,
        out string text,
        out bool isFinalPhase)
    {
        text = string.Empty;
        isFinalPhase = string.Equals(
            TryGetStringProperty(itemElement, "phase"),
            "final_answer",
            StringComparison.OrdinalIgnoreCase);

        var itemType = TryGetStringProperty(itemElement, "type");
        if (!string.Equals(itemType, "agentMessage", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(itemType, "AgentMessage", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var directText = TryGetStringProperty(itemElement, "text");
        if (!string.IsNullOrWhiteSpace(directText))
        {
            text = directText;
            return true;
        }

        if (!itemElement.TryGetProperty("content", out var contentElement) || contentElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var builder = new StringBuilder();
        foreach (var contentItem in contentElement.EnumerateArray())
        {
            var contentType = TryGetStringProperty(contentItem, "type");
            if (!string.IsNullOrWhiteSpace(contentType) &&
                !string.Equals(contentType, "Text", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(contentType, "text", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(contentType, "output_text", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var contentText = TryGetStringProperty(contentItem, "text");
            if (!string.IsNullOrWhiteSpace(contentText))
            {
                builder.Append(contentText);
            }
        }

        text = builder.ToString();
        return !string.IsNullOrWhiteSpace(text);
    }

    private static bool TryGetCompletedJsonFromStreamedText(TurnCompletionState completionState, out string text)
    {
        text = completionState.StreamedAgentMessageText.ToString().Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        try
        {
            using var _ = JsonDocument.Parse(text);
            return true;
        }
        catch (JsonException)
        {
            text = string.Empty;
            return false;
        }
    }

    private void ScheduleCompletedTurnRecovery(TurnCompletionState completionState, string turnId)
    {
        if (completionState.RecoveryScheduled)
        {
            return;
        }

        completionState.RecoveryScheduled = true;
        _ = Task.Run(() => RecoverCompletedTurnTextAsync(completionState, turnId));
    }

    private async Task RecoverCompletedTurnTextAsync(TurnCompletionState completionState, string turnId)
    {
        try
        {
            using var recoveryCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var recoveredText = await TryReadTurnTextAsync(completionState, turnId, recoveryCts.Token, attempts: 3);
            if (!string.IsNullOrWhiteSpace(recoveredText))
            {
                TryCompleteTurn(
                    completionState,
                    turnId,
                    recoveredText,
                    "appserver.turn-read",
                    "Recovered final text from thread/read after turn completion.");
                return;
            }

            var streamedText = completionState.StreamedAgentMessageText.ToString();
            if (!string.IsNullOrWhiteSpace(streamedText))
            {
                TryCompleteTurn(
                    completionState,
                    turnId,
                    streamedText,
                    "appserver.agent-message-delta",
                    "Recovered final text from streamed agent-message deltas.");
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _ = _traceLogger.WriteEventAsync(
                completionState.CorrelationId,
                completionState.AppId,
                completionState.Endpoint,
                "appserver.turn-read",
                "failed",
                null,
                error: ex.Message,
                exception: ex,
                metadata: new()
                {
                    ["threadId"] = completionState.ThreadId,
                    ["turnId"] = turnId,
                    ["notificationTrail"] = string.Join(" -> ", completionState.NotificationTrail)
                });
        }
    }

    private async Task<string?> TryReadTurnTextAsync(
        TurnCompletionState completionState,
        string turnId,
        CancellationToken cancellationToken,
        int attempts)
    {
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            if (completionState.CompletionSource.Task.IsCompleted)
            {
                return null;
            }

            var threadRead = await ReadThreadAsync(completionState.ThreadId, cancellationToken);

            var recoveredText = ExtractFinalMessageFromThread(threadRead, turnId);
            if (!string.IsNullOrWhiteSpace(recoveredText))
            {
                return recoveredText;
            }

            if (attempt < attempts)
            {
                await Task.Delay(150, cancellationToken);
            }
        }

        return null;
    }

    private async Task DumpWarmFailureSnapshotAsync(
        TurnCompletionState completionState,
        string turnId,
        string failureDetail,
        CancellationToken cancellationToken)
    {
        var threadRead = await ReadThreadAsync(completionState.ThreadId, cancellationToken);

        Directory.CreateDirectory(_warmFailureSnapshotDirectory);

        var fileName = string.Join(
            "__",
            DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture),
            SanitizePathSegment(completionState.AppId),
            SanitizePathSegment(completionState.Endpoint.Replace('/', '-')),
            SanitizePathSegment(completionState.CorrelationId),
            SanitizePathSegment(turnId)) + ".json";
        var outputPath = Path.Combine(_warmFailureSnapshotDirectory, fileName);

        var snapshot = new Dictionary<string, object?>
        {
            ["capturedAtUtc"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            ["correlationId"] = completionState.CorrelationId,
            ["app"] = completionState.AppId,
            ["endpoint"] = completionState.Endpoint,
            ["threadId"] = completionState.ThreadId,
            ["turnId"] = turnId,
            ["failureDetail"] = failureDetail,
            ["lastNotification"] = completionState.LastNotificationLabel,
            ["notificationTrail"] = completionState.NotificationTrail,
            ["notificationSamples"] = completionState.NotificationSamples,
            ["threadRead"] = JsonSerializer.Deserialize<object?>(threadRead.GetRawText(), _jsonOptions)
        };

        var snapshotJson = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions(_jsonOptions)
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(outputPath, snapshotJson, Encoding.UTF8, cancellationToken);

        await _traceLogger.WriteEventAsync(
            completionState.CorrelationId,
            completionState.AppId,
            completionState.Endpoint,
            "appserver.turn-snapshot",
            "success",
            null,
            detail: "Captured warm-failure thread snapshot for offline analysis.",
            metadata: new()
            {
                ["turnId"] = turnId,
                ["threadId"] = completionState.ThreadId,
                ["path"] = outputPath
            });
    }

    private async Task<JsonElement> ReadThreadAsync(string threadId, CancellationToken cancellationToken)
    {
        try
        {
            return await SendJsonRpcRequestAsync(
                "thread/read",
                new Dictionary<string, object?>
                {
                    ["threadId"] = threadId,
                    ["includeTurns"] = true
                },
                cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("ephemeral threads do not support includeTurns", StringComparison.OrdinalIgnoreCase))
        {
            return await SendJsonRpcRequestAsync(
                "thread/read",
                new Dictionary<string, object?>
                {
                    ["threadId"] = threadId
                },
                cancellationToken);
        }
    }

    private async Task ScheduleTurnFallback(TurnCompletionState completionState, string turnId)
    {
        if (completionState.FallbackScheduled)
        {
            return;
        }

        completionState.FallbackScheduled = true;
        var maxGrace = TimeSpan.FromSeconds(_options.WarmTurnGraceSeconds);
        var earlyDecision = TimeSpan.FromSeconds(Math.Min(_options.WarmTurnGraceSeconds, 3));
        await Task.Delay(earlyDecision);
        if (completionState.CompletionSource.Task.IsCompleted)
        {
            return;
        }

        var deadline = DateTimeOffset.UtcNow + (maxGrace - earlyDecision);
        while (DateTimeOffset.UtcNow < deadline && ShouldContinueWaitingForWarm(completionState))
        {
            if (!completionState.ExtendedWarmWaitLogged)
            {
                completionState.ExtendedWarmWaitLogged = true;
                _ = _traceLogger.WriteEventAsync(
                    completionState.CorrelationId,
                    completionState.AppId,
                    completionState.Endpoint,
                    "appserver.turn-fallback-extended",
                    "info",
                    null,
                    detail: "Warm fallback grace extended because the turn still showed active progress.",
                    metadata: new()
                    {
                        ["turnId"] = turnId,
                        ["lastNotification"] = completionState.LastNotificationLabel ?? "unknown"
                    });
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
            if (completionState.CompletionSource.Task.IsCompleted)
            {
                return;
            }
        }

        try
        {
            using var recoveryCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var recoveredText = await TryReadTurnTextAsync(completionState, turnId, recoveryCts.Token, attempts: 2);
            if (!string.IsNullOrWhiteSpace(recoveredText))
            {
                TryCompleteTurn(
                    completionState,
                    turnId,
                    recoveredText,
                    "appserver.turn-read",
                    "Recovered final text from thread/read during warm fallback grace.");
                return;
            }
        }
        catch
        {
        }

        var details = $"app-server turn {turnId} completed without a final textual response.";
        try
        {
            using var snapshotCts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await DumpWarmFailureSnapshotAsync(completionState, turnId, details, snapshotCts.Token);
        }
        catch (Exception ex)
        {
            await _traceLogger.WriteEventAsync(
                completionState.CorrelationId,
                completionState.AppId,
                completionState.Endpoint,
                "appserver.turn-snapshot",
                "failed",
                null,
                error: ex.Message,
                exception: ex,
                metadata: new()
                {
                    ["turnId"] = turnId,
                    ["threadId"] = completionState.ThreadId
                });
        }

        if (completionState.CompletionSource.TrySetException(new InvalidOperationException(details)))
        {
            _ = _traceLogger.WriteEventAsync(
                completionState.CorrelationId,
                completionState.AppId,
                completionState.Endpoint,
                "appserver.turn-completed",
                "failed",
                null,
                error: details,
                metadata: new()
                {
                    ["turnId"] = turnId,
                    ["notificationTrail"] = string.Join(" -> ", completionState.NotificationTrail)
                });
        }
    }

    private static string SanitizePathSegment(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(invalidChars.Contains(ch) ? '_' : ch);
        }

        return builder.ToString();
    }

    private static bool ShouldContinueWaitingForWarm(TurnCompletionState completionState)
    {
        if (completionState.CompletionSource.Task.IsCompleted)
        {
            return false;
        }

        var lastNotificationAge = DateTimeOffset.UtcNow - completionState.LastNotificationAtUtc;
        if (lastNotificationAge > TimeSpan.FromSeconds(1.5))
        {
            return false;
        }

        var label = completionState.LastNotificationLabel;
        if (string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        var lastSample = completionState.NotificationSamples.Count > 0
            ? completionState.NotificationSamples[^1]
            : string.Empty;

        if (lastSample.Contains("item.type=reasoning", StringComparison.OrdinalIgnoreCase) ||
            lastSample.Contains("item.type=userMessage", StringComparison.OrdinalIgnoreCase) ||
            label.Contains("token_count", StringComparison.OrdinalIgnoreCase) ||
            label.Contains("account/rateLimits/updated", StringComparison.OrdinalIgnoreCase) ||
            label.Contains("user_message", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return label.Contains("agent_message", StringComparison.OrdinalIgnoreCase)
            || label.Contains("raw_response_item", StringComparison.OrdinalIgnoreCase)
            || lastSample.Contains("item.type=agentMessage", StringComparison.OrdinalIgnoreCase)
            || label.Contains("item/started", StringComparison.OrdinalIgnoreCase)
            || label.Contains("exec_command_begin", StringComparison.OrdinalIgnoreCase)
            || label.Contains("outputDelta", StringComparison.OrdinalIgnoreCase);
    }

    private void TryCompleteTurn(TurnCompletionState completionState, string turnId, string text, string stage, string detail)
    {
        if (completionState.CompletionSource.TrySetResult(text))
        {
            _ = _traceLogger.WriteEventAsync(
                completionState.CorrelationId,
                completionState.AppId,
                completionState.Endpoint,
                stage,
                "success",
                null,
                detail: detail,
                metadata: new()
                {
                    ["turnId"] = turnId,
                    ["finalTextLength"] = text.Length
                });
        }
    }

    private static string? TryGetStringProperty(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (element.TryGetProperty(propertyName, out var propertyElement) &&
                propertyElement.ValueKind == JsonValueKind.String)
            {
                return propertyElement.GetString();
            }
        }

        return null;
    }

    private static string? ExtractFinalMessageFromTurn(JsonElement turnElement)
    {
        if (turnElement.TryGetProperty("items", out var itemsElement) && itemsElement.ValueKind == JsonValueKind.Array)
        {
            string? latestAgentMessage = null;
            foreach (var item in itemsElement.EnumerateArray().Reverse())
            {
                if (TryExtractTextFromThreadItem(item, out var text, out var isFinalPhase))
                {
                    if (isFinalPhase)
                    {
                        return text;
                    }

                    latestAgentMessage ??= text;
                }
            }

            if (!string.IsNullOrWhiteSpace(latestAgentMessage))
            {
                return latestAgentMessage;
            }
        }

        return null;
    }

    private static string? ExtractFinalMessageFromThread(JsonElement threadReadResult, string turnId)
    {
        if (!threadReadResult.TryGetProperty("thread", out var threadElement) ||
            threadElement.ValueKind != JsonValueKind.Object ||
            !threadElement.TryGetProperty("turns", out var turnsElement) ||
            turnsElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        JsonElement? latestTurn = null;
        foreach (var turnElement in turnsElement.EnumerateArray().Reverse())
        {
            latestTurn ??= turnElement;
            if (TryGetStringProperty(turnElement, "id") == turnId)
            {
                return ExtractFinalMessageFromTurn(turnElement);
            }
        }

        return latestTurn is not null
            ? ExtractFinalMessageFromTurn(latestTurn.Value)
            : null;
    }

    private static bool TryExtractTextFromResponseItem(
        JsonElement itemElement,
        out string text,
        out bool isFinalPhase)
    {
        text = string.Empty;
        isFinalPhase = false;

        if (!string.Equals(TryGetStringProperty(itemElement, "type"), "message", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        isFinalPhase = string.Equals(
            TryGetStringProperty(itemElement, "phase"),
            "final_answer",
            StringComparison.OrdinalIgnoreCase);
        if (!itemElement.TryGetProperty("content", out var contentElement) || contentElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var builder = new StringBuilder();
        foreach (var contentItem in contentElement.EnumerateArray())
        {
            var contentType = TryGetStringProperty(contentItem, "type");
            if (!string.IsNullOrWhiteSpace(contentType) &&
                !string.Equals(contentType, "output_text", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(contentType, "text", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var contentText = TryGetStringProperty(contentItem, "text");
            if (!string.IsNullOrWhiteSpace(contentText))
            {
                builder.Append(contentText);
            }
        }

        text = builder.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (!isFinalPhase &&
            itemElement.TryGetProperty("end_turn", out var endTurnElement) &&
            endTurnElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            isFinalPhase = endTurnElement.GetBoolean();
        }

        return true;
    }

    private static string? ExtractRequestId(JsonElement idElement)
    {
        return idElement.ValueKind switch
        {
            JsonValueKind.String => idElement.GetString(),
            JsonValueKind.Number => idElement.GetInt64().ToString(CultureInfo.InvariantCulture),
            _ => null
        };
    }

    private static bool LooksLikeJsonSchema(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return element.TryGetProperty("$schema", out _)
            || element.TryGetProperty("type", out _)
            || element.TryGetProperty("properties", out _)
            || element.TryGetProperty("items", out _)
            || element.TryGetProperty("required", out _)
            || element.TryGetProperty("$defs", out _)
            || element.TryGetProperty("definitions", out _);
    }

    private static object BuildJsonSchemaFromExample(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => BuildObjectSchema(element),
            JsonValueKind.Array => BuildArraySchema(element),
            JsonValueKind.String => new Dictionary<string, object?> { ["type"] = "string" },
            JsonValueKind.Number => new Dictionary<string, object?> { ["type"] = element.TryGetInt64(out _) ? "integer" : "number" },
            JsonValueKind.True or JsonValueKind.False => new Dictionary<string, object?> { ["type"] = "boolean" },
            JsonValueKind.Null => new Dictionary<string, object?> { ["type"] = "null" },
            _ => new Dictionary<string, object?>()
        };
    }

    private static Dictionary<string, object?> BuildObjectSchema(JsonElement element)
    {
        var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
        var required = new List<string>();
        foreach (var property in element.EnumerateObject())
        {
            properties[property.Name] = BuildJsonSchemaFromExample(property.Value);
            required.Add(property.Name);
        }

        return new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false
        };
    }

    private static Dictionary<string, object?> BuildArraySchema(JsonElement element)
    {
        object itemsSchema;
        if (element.GetArrayLength() == 0)
        {
            itemsSchema = new Dictionary<string, object?>();
        }
        else
        {
            itemsSchema = BuildJsonSchemaFromExample(element.EnumerateArray().First());
        }

        return new Dictionary<string, object?>
        {
            ["type"] = "array",
            ["items"] = itemsSchema
        };
    }

    private sealed class TurnCompletionState
    {
        public required TaskCompletionSource<string> CompletionSource { get; init; }
        public required string CorrelationId { get; init; }
        public required string AppId { get; init; }
        public required string Endpoint { get; init; }
        public required string ThreadId { get; init; }
        public string? PendingAgentMessageText { get; set; }
        public StringBuilder StreamedAgentMessageText { get; } = new();
        public List<string> NotificationTrail { get; } = [];
        public List<string> NotificationSamples { get; } = [];
        public bool TurnCompletedWithoutMessage { get; set; }
        public bool TaskCompletedWithoutMessage { get; set; }
        public bool FallbackScheduled { get; set; }
        public bool RecoveryScheduled { get; set; }
        public bool ExtendedWarmWaitLogged { get; set; }
        public string? LastNotificationLabel { get; set; }
        public DateTimeOffset LastNotificationAtUtc { get; set; } = DateTimeOffset.MinValue;
    }

    private async Task StopAsync(bool swallowErrors)
    {
        _readLoopCts?.Cancel();
        _isRunning = false;
        _isInitialized = false;
        _writer = null;
        if (_process is null)
        {
            return;
        }

        foreach (var item in _pendingResponses)
        {
            item.Value.TrySetException(new InvalidOperationException("app-server stopped unexpectedly."));
        }

        foreach (var item in _pendingTurnCompletions)
        {
            item.Value.CompletionSource.TrySetException(new InvalidOperationException("app-server stopped unexpectedly."));
        }

        _pendingTurnCompletions.Clear();
        _pendingResponses.Clear();

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            if (!swallowErrors)
            {
                throw;
            }
        }

        _process = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await StopAsync(true);
    }
}
