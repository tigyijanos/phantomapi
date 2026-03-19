using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Json.Schema;
using CodexCliExecutionResult = (string RawResponse, string? SessionId);
using RuntimeProfile = (string Model, string ReasoningEffort, string? ServiceTier);

var builder = WebApplication.CreateBuilder(args);
var repoRoot = RepoRootLocator.Resolve(
    builder.Environment.ContentRootPath,
    Directory.GetCurrentDirectory(),
    AppContext.BaseDirectory);

var phantomOptions = new PhantomOptions();
builder.Configuration.GetSection("Phantom").Bind(phantomOptions);
var cliArgumentsTemplateWasProvided = !string.IsNullOrWhiteSpace(builder.Configuration["Phantom:CliArgumentsTemplate"]);

phantomOptions.CliCommand = phantomOptions.CliCommand.Trim();
phantomOptions.CliArgumentsTemplate = phantomOptions.CliArgumentsTemplate.Trim();
phantomOptions.Model = phantomOptions.Model.Trim();
phantomOptions.ReasoningEffort = phantomOptions.ReasoningEffort.Trim().ToLowerInvariant();
phantomOptions.FastModeModel = phantomOptions.FastModeModel.Trim();
phantomOptions.FastModeReasoningEffort = phantomOptions.FastModeReasoningEffort.Trim().ToLowerInvariant();
phantomOptions.NormalServiceTier = phantomOptions.NormalServiceTier?.Trim();
phantomOptions.FastModeServiceTier = phantomOptions.FastModeServiceTier?.Trim();

if (string.IsNullOrWhiteSpace(phantomOptions.CliCommand))
{
    phantomOptions.CliCommand = OperatingSystem.IsWindows() ? "codex.cmd" : "codex";
}

if (string.IsNullOrWhiteSpace(phantomOptions.Model))
{
    phantomOptions.Model = "gpt-5.3-codex-spark";
}

if (string.IsNullOrWhiteSpace(phantomOptions.ReasoningEffort))
{
    phantomOptions.ReasoningEffort = "medium";
}

if (string.IsNullOrWhiteSpace(phantomOptions.FastModeModel))
{
    phantomOptions.FastModeModel = phantomOptions.Model;
}

if (string.IsNullOrWhiteSpace(phantomOptions.FastModeReasoningEffort))
{
    phantomOptions.FastModeReasoningEffort = "low";
}

if (string.IsNullOrWhiteSpace(phantomOptions.NormalServiceTier))
{
    phantomOptions.NormalServiceTier = null;
}

if (string.IsNullOrWhiteSpace(phantomOptions.FastModeServiceTier))
{
    phantomOptions.FastModeServiceTier = "fast";
}

var defaultProfile = ResolveRuntimeProfile(phantomOptions, phantomOptions.FastModeEnabled);

if (string.IsNullOrWhiteSpace(phantomOptions.CliArgumentsTemplate))
{
    phantomOptions.CliArgumentsTemplate = CodexCliExecutor.BuildCliArgumentsTemplate(phantomOptions, defaultProfile);
}

if (phantomOptions.CliTimeoutSeconds <= 0)
{
    phantomOptions.CliTimeoutSeconds = 180;
}

if (phantomOptions.WarmTurnGraceSeconds <= 0)
{
    phantomOptions.WarmTurnGraceSeconds = 4;
}

var traceLogger = new TraceLogger(repoRoot);
var instructionBundleCompiler = new InstructionBundleCompiler(repoRoot);
var endpointContractCache = new EndpointContractCache(repoRoot);
var execSessionPool = phantomOptions.UseExecSessionPool
    ? new CodexExecSessionPool(repoRoot)
    : null;
var appServerClient = phantomOptions.UseWarmAppServer
    ? new CodexAppServerClient(phantomOptions, repoRoot, traceLogger)
    : null;

var app = builder.Build();

app.Lifetime.ApplicationStarted.Register(() =>
{
    if (appServerClient is null || !phantomOptions.UseWarmAppServer)
    {
        return;
    }

    _ = Task.Run(async () =>
    {
        var correlationId = $"startup_{Guid.NewGuid():N}";
        try
        {
            await appServerClient.PrimeAsync(defaultProfile, correlationId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            await traceLogger.WriteEventAsync(
                correlationId,
                "framework",
                "warm-start",
                "appserver.eager-startup",
                "failed",
                null,
                error: ex.Message,
                exception: ex,
                metadata: new()
                {
                    ["model"] = defaultProfile.Model,
                    ["reasoningEffort"] = defaultProfile.ReasoningEffort,
                    ["serviceTier"] = defaultProfile.ServiceTier ?? "default"
                });
        }
    });
});

app.Lifetime.ApplicationStarted.Register(() =>
{
    _ = Task.Run(() => WarmConfiguredEndpointsAsync(
        repoRoot,
        defaultProfile,
        phantomOptions,
        traceLogger,
        instructionBundleCompiler,
        endpointContractCache,
        execSessionPool,
        cliArgumentsTemplateWasProvided,
        CancellationToken.None));
});

app.Lifetime.ApplicationStopping.Register(() =>
{
    _ = appServerClient?.DisposeAsync().AsTask();
});

app.MapPost("/dynamic-api", async (HttpRequest request, CancellationToken cancellationToken) =>
{
    var correlationId = $"corr_{Guid.NewGuid():N}";
    var requestTimer = Stopwatch.StartNew();
    string? appId = null;
    string? endpoint = null;
    var finalResult = "success";
    var finalSummary = "request completed";
    string? finalError = null;

    IResult BuildBadRequest(string error, string? detail)
    {
        finalResult = "failure";
        finalSummary = error;
        finalError = detail;
        return Results.BadRequest(new { error, detail });
    }

    IResult BuildProblem(string title, string? detail, int statusCode)
    {
        finalResult = "failure";
        finalSummary = title;
        finalError = detail;
        return Results.Problem(title: title, detail: detail, statusCode: statusCode);
    }

    try
    {
        var rawRequest = await TraceStepAsyncResult(
            traceLogger,
            correlationId,
            null,
            null,
            "request.body.read",
            () => ReadRequestBodyAsync(request, cancellationToken),
            new Dictionary<string, object?> { ["path"] = "/dynamic-api" },
            new() { ["source"] = "HttpRequest" });

        if (string.IsNullOrWhiteSpace(rawRequest))
        {
            return BuildBadRequest("Request body is required.", null);
        }

        var requestDocument = await TraceStepAsyncResult(
            traceLogger,
            correlationId,
            null,
            null,
            "request.parse-json",
            () => Task.FromResult(JsonDocument.Parse(rawRequest)),
            null,
            new() { ["bodyLength"] = rawRequest.Length });

        using var requestDocumentCleanup = requestDocument;

        if (requestDocument.RootElement.ValueKind != JsonValueKind.Object)
        {
            return BuildBadRequest("Request body must be a JSON object.", null);
        }

        var routeResult = await TraceStepAsyncResult(
            traceLogger,
            correlationId,
            null,
            null,
            "request.route",
            () =>
            {
                if (!TryGetRoute(requestDocument.RootElement, out var resolvedAppId, out var resolvedEndpoint, out var routeError))
                {
                    throw new InvalidOperationException(routeError);
                }

                return Task.FromResult((resolvedAppId!, resolvedEndpoint!));
            },
            null,
            new() { ["requestBodyKind"] = requestDocument.RootElement.ValueKind.ToString() });

        appId = routeResult.Item1;
        endpoint = routeResult.Item2;

        var resolvedContract = await TraceStepAsyncResult(
            traceLogger,
            correlationId,
            appId,
            endpoint,
            "request.contract.resolve",
            () => Task.FromResult(endpointContractCache.GetOrLoad(appId, endpoint)),
            new()
            {
                ["routeKey"] = $"{appId}:{endpoint}"
            });

        await traceLogger.WriteEventAsync(
            correlationId,
            appId,
            endpoint,
            "request.contract.cache",
            "info",
            null,
            detail: "Resolved endpoint contract and output schema from cache-aware route metadata.",
            metadata: new()
            {
                ["routeKey"] = resolvedContract.RouteKey,
                ["cacheHit"] = resolvedContract.CacheHit,
                ["sourcePath"] = resolvedContract.SourcePath.Replace('\\', '/')
            });

        var fastMode = await TraceStepAsyncResult(
            traceLogger,
            correlationId,
            appId,
            endpoint,
            "request.fastmode",
            () => Task.FromResult(TryGetFastModePreference(requestDocument.RootElement, out var requestedFastMode)
                ? requestedFastMode
                : phantomOptions.FastModeEnabled),
            null,
            new() { ["configuredDefault"] = phantomOptions.FastModeEnabled });

        var runtimeProfile = ResolveRuntimeProfile(phantomOptions, fastMode);
        runtimeProfile = await TraceStepAsyncResult(
            traceLogger,
            correlationId,
            appId,
            endpoint,
            "request.runtime-profile",
            () => Task.FromResult(runtimeProfile),
            null,
            new()
            {
                ["model"] = runtimeProfile.Model,
                ["reasoningEffort"] = runtimeProfile.ReasoningEffort,
                ["serviceTier"] = runtimeProfile.ServiceTier ?? "default"
            });

        var needsInstructionBundle = (appServerClient is not null && phantomOptions.UseWarmAppServer)
            || (execSessionPool is not null && phantomOptions.UseExecSessionPool);
        CompiledInstructionBundle? instructionBundle = null;
        if (needsInstructionBundle)
        {
            instructionBundle = await TraceStepAsyncResult(
                traceLogger,
                correlationId,
                appId,
                endpoint,
                "request.instructions.bundle",
                () => Task.FromResult(instructionBundleCompiler.GetOrCompile(appId, endpoint)),
                new()
                {
                    ["routeKey"] = $"{appId}:{endpoint}"
                });

            await traceLogger.WriteEventAsync(
                correlationId,
                appId,
                endpoint,
                "request.instructions.bundle.info",
                "info",
                null,
                metadata: new()
                {
                    ["routeKey"] = instructionBundle.RouteKey,
                    ["bundleHash"] = instructionBundle.BundleHash,
                    ["cacheHit"] = instructionBundle.CacheHit,
                    ["sourceFileCount"] = instructionBundle.SourceFiles.Count
                });
        }

        var execSessionKey = instructionBundle is not null
            ? BuildExecSessionKey(appId, endpoint, runtimeProfile, instructionBundle)
            : null;

        Task<string> ExecuteColdAsync()
        {
            return TraceStepAsyncResult(
                traceLogger,
                correlationId,
                appId,
                endpoint,
                "codex.exec.cold",
                () => InvokeCliAsync(
                    phantomOptions,
                    runtimeProfile,
                    repoRoot,
                    rawRequest,
                    cancellationToken,
                    traceLogger,
                    correlationId,
                    appId,
                    endpoint,
                    instructionBundle,
                    execSessionKey,
                    execSessionPool,
                    cliArgumentsTemplateWasProvided),
                null,
                new()
                {
                    ["mode"] = "cold",
                    ["model"] = runtimeProfile.Model,
                    ["reasoningEffort"] = runtimeProfile.ReasoningEffort
                });
        }

        string cliRawResponse;
        try
        {
            if (appServerClient is not null && phantomOptions.UseWarmAppServer)
            {
                try
                {
                    cliRawResponse = await TraceStepAsyncResult(
                        traceLogger,
                        correlationId,
                        appId,
                        endpoint,
                        "codex.exec.warm",
                        () => appServerClient.InvokeAsync(runtimeProfile, rawRequest, resolvedContract.OutputSchemaJson, instructionBundle!, correlationId, appId, endpoint, cancellationToken),
                        null,
                        new()
                        {
                            ["mode"] = "warm",
                            ["model"] = runtimeProfile.Model,
                            ["reasoningEffort"] = runtimeProfile.ReasoningEffort
                        });
                }
                catch (Exception ex) when (phantomOptions.FallbackToColdExecution)
                {
                    await traceLogger.WriteEventAsync(
                        correlationId,
                        appId,
                        endpoint,
                        "codex.exec.warm-fallback",
                        "started",
                        null,
                        detail: "warm execution failed, falling back to cold execution",
                        metadata: new() { ["error"] = Truncate(ex.Message, 240) });

                    cliRawResponse = await ExecuteColdAsync();
                }
            }
            else
            {
                cliRawResponse = await ExecuteColdAsync();
            }
        }
        catch (Exception ex)
        {
            return BuildProblem("CLI invocation failed.", ex.Message, 502);
        }

        var cliResponse = await TraceStepAsyncResult(
            traceLogger,
            correlationId,
            appId,
            endpoint,
            "response.parse-json",
            () => Task.FromResult(JsonDocument.Parse(cliRawResponse)),
            null,
            new() { ["responseLength"] = cliRawResponse.Length });

        using var cliResponseCleanup = cliResponse;

        var schemaValidation = await TraceStepAsyncResult(
            traceLogger,
            correlationId,
            appId,
            endpoint,
            "response.schema-validate",
            () =>
            {
                var evaluation = resolvedContract.OutputSchema.Evaluate(
                    cliResponse.RootElement,
                    new EvaluationOptions
                    {
                        OutputFormat = OutputFormat.List
                    });
                return Task.FromResult((evaluation.IsValid, evaluation));
            },
            null,
            new() { ["path"] = "/dynamic-api" });

        if (!schemaValidation.IsValid)
        {
            return BuildProblem("CLI response failed the hard guard.", BuildSchemaValidationError(schemaValidation.evaluation), 502);
        }

        await traceLogger.WriteEventAsync(
            correlationId,
            appId,
            endpoint,
            "request.return",
            "success",
            null,
            detail: "response returned",
            metadata: new()
            {
                ["outputBytes"] = cliResponse.RootElement.GetRawText().Length
            });

        return Results.Content(cliResponse.RootElement.GetRawText(), "application/json");
    }
    catch (Exception ex)
    {
        finalResult = "failure";
        finalSummary = "Unhandled exception in request processing";
        finalError = ex.Message;
        return BuildProblem("Request processing failed.", ex.Message, 500);
    }
    finally
    {
        requestTimer.Stop();
        await traceLogger.WriteEventAsync(
            correlationId,
            appId,
            endpoint,
            "request.complete",
            finalResult,
            requestTimer.ElapsedMilliseconds,
            detail: finalSummary,
            error: finalError);
    }
});

app.Run();

static async Task<T> TraceStepAsyncResult<T>(
    TraceLogger traceLogger,
    string correlationId,
    string? appId,
    string? endpoint,
    string stage,
    Func<Task<T>> action,
    Dictionary<string, object?>? metadata = null,
    Dictionary<string, object?>? details = null)
{
    var stopwatch = Stopwatch.StartNew();
    try
    {
        var result = await action();
        await traceLogger.WriteEventAsync(
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
        await traceLogger.WriteEventAsync(
            correlationId,
            appId,
            endpoint,
            stage,
            "failed",
            stopwatch.ElapsedMilliseconds,
            detail: details is null ? null : JsonSerializer.Serialize(details),
            error: ex.Message,
            exception: ex,
            metadata: metadata);
        throw;
    }
}

static async Task<string> ReadRequestBodyAsync(HttpRequest request, CancellationToken cancellationToken)
{
    request.EnableBuffering();
    request.Body.Position = 0;

    using var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
    var body = await reader.ReadToEndAsync(cancellationToken);
    request.Body.Position = 0;
    return body;
}

static bool TryGetRoute(JsonElement requestRoot, out string? appId, out string? endpoint, out string? error)
{
    appId = null;
    endpoint = null;
    error = null;

    if (!requestRoot.TryGetProperty("app", out var appProperty) || appProperty.ValueKind != JsonValueKind.String)
    {
        error = "Request body must contain a string 'app' field, for example 'bank-api'.";
        return false;
    }

    appId = appProperty.GetString()?.Trim();
    if (string.IsNullOrWhiteSpace(appId))
    {
        error = "The 'app' field cannot be empty.";
        return false;
    }

    if (!Regex.IsMatch(appId, "^[A-Za-z0-9_-]+$"))
    {
        error = "The 'app' field contains invalid characters.";
        return false;
    }

    if (!requestRoot.TryGetProperty("endpoint", out var endpointProperty) || endpointProperty.ValueKind != JsonValueKind.String)
    {
        error = "Request body must contain a string 'endpoint' field, for example 'bank/get-balance'.";
        return false;
    }

    endpoint = endpointProperty.GetString()?.Trim();
    if (string.IsNullOrWhiteSpace(endpoint))
    {
        error = "The 'endpoint' field cannot be empty.";
        return false;
    }

    var segments = endpoint.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (segments.Length == 0 || segments.Any(segment => !Regex.IsMatch(segment, "^[A-Za-z0-9_-]+$")))
    {
        error = "The 'endpoint' field contains invalid path segments.";
        return false;
    }

    endpoint = string.Join('/', segments);
    return true;
}

static bool TryGetFastModePreference(JsonElement requestRoot, out bool fastMode)
{
    fastMode = false;

    if (!requestRoot.TryGetProperty("fastMode", out var fastModeProperty))
    {
        if (!requestRoot.TryGetProperty("fast", out fastModeProperty))
        {
            return false;
        }
    }

    if (fastModeProperty.ValueKind == JsonValueKind.True)
    {
        fastMode = true;
        return true;
    }

    if (fastModeProperty.ValueKind == JsonValueKind.False)
    {
        fastMode = false;
        return true;
    }

    if (fastModeProperty.ValueKind == JsonValueKind.String && bool.TryParse(fastModeProperty.GetString(), out var parsed))
    {
        fastMode = parsed;
        return true;
    }

    return false;
}

static async Task WarmConfiguredEndpointsAsync(
    string contentRootPath,
    RuntimeProfile profile,
    PhantomOptions options,
    TraceLogger traceLogger,
    InstructionBundleCompiler instructionBundleCompiler,
    EndpointContractCache endpointContractCache,
    CodexExecSessionPool? execSessionPool,
    bool cliArgumentsTemplateWasProvided,
    CancellationToken cancellationToken)
{
    var configuredWarmStarts = EndpointWarmStartCatalog.Discover(contentRootPath);
    if (configuredWarmStarts.Count == 0)
    {
        return;
    }

    foreach (var target in configuredWarmStarts)
    {
        var correlationId = $"warmstart_{Guid.NewGuid():N}";
        Task WriteWarmStartSkipAsync(string detail, Dictionary<string, object?>? metadata = null)
        {
            metadata ??= new Dictionary<string, object?>();
            metadata["mode"] = target.WarmStart.Mode;
            return traceLogger.WriteEventAsync(
                correlationId,
                target.AppId,
                target.Endpoint,
                "warmstart.exec-session",
                "skipped",
                null,
                detail: detail,
                metadata: metadata);
        }

        try
        {
            var resolvedContract = await TraceStepAsyncResult(
                traceLogger,
                correlationId,
                target.AppId,
                target.Endpoint,
                "warmstart.contract",
                () => Task.FromResult(endpointContractCache.GetOrLoad(target.AppId, target.Endpoint)),
                new()
                {
                    ["mode"] = target.WarmStart.Mode,
                    ["endpointPath"] = target.EndpointPath.Replace('\\', '/')
                });

            var instructionBundle = await TraceStepAsyncResult(
                traceLogger,
                correlationId,
                target.AppId,
                target.Endpoint,
                "warmstart.bundle",
                () => Task.FromResult(instructionBundleCompiler.GetOrCompile(target.AppId, target.Endpoint)),
                new()
                {
                    ["mode"] = target.WarmStart.Mode,
                    ["contractCacheHit"] = resolvedContract.CacheHit
                });

            if (!target.WarmStart.UsesExecSession)
            {
                await traceLogger.WriteEventAsync(
                    correlationId,
                    target.AppId,
                    target.Endpoint,
                    "warmstart.complete",
                    "success",
                    null,
                    detail: "Completed cache-only warm start.",
                    metadata: new()
                    {
                        ["mode"] = target.WarmStart.Mode,
                        ["bundleHash"] = instructionBundle.BundleHash
                    });
                continue;
            }

            if (execSessionPool is null || !options.UseExecSessionPool)
            {
                await WriteWarmStartSkipAsync("Exec-session warm start skipped because the exec session pool is disabled.");
                continue;
            }

            if (cliArgumentsTemplateWasProvided)
            {
                await WriteWarmStartSkipAsync("Exec-session warm start skipped because a custom CliArgumentsTemplate is configured.");
                continue;
            }

            if (!target.WarmStart.ReadOnlyWarmup)
            {
                await WriteWarmStartSkipAsync("Exec-session warm start skipped because the endpoint is not marked as readonly-safe for startup warmup.");
                continue;
            }

            var execSessionKey = BuildExecSessionKey(target.AppId, target.Endpoint, profile, instructionBundle);
            var existingSession = await execSessionPool.GetAsync(execSessionKey, cancellationToken);
            if (existingSession is not null)
            {
                await WriteWarmStartSkipAsync(
                    "Exec-session warm start skipped because a compatible stored session already exists.",
                    new()
                    {
                        ["sessionKey"] = execSessionKey,
                        ["sessionId"] = existingSession.SessionId
                    });
                continue;
            }

            if (string.IsNullOrWhiteSpace(target.WarmStart.WarmupRequest))
            {
                await WriteWarmStartSkipAsync("Exec-session warm start skipped because no warmupRequest is configured.");
                continue;
            }

            var warmupRequestPath = ResolveWarmupRequestPath(contentRootPath, target.AppId, target.WarmStart.WarmupRequest);
            if (!File.Exists(warmupRequestPath))
            {
                await traceLogger.WriteEventAsync(
                    correlationId,
                    target.AppId,
                    target.Endpoint,
                    "warmstart.exec-session",
                    "failed",
                    null,
                    error: $"Warmup request file not found: {warmupRequestPath}",
                    metadata: new() { ["mode"] = target.WarmStart.Mode });
                continue;
            }

            var warmupRequest = await File.ReadAllTextAsync(warmupRequestPath, cancellationToken);
            await TraceStepAsyncResult(
                traceLogger,
                correlationId,
                target.AppId,
                target.Endpoint,
                "warmstart.exec-session",
                () => InvokeCliAsync(
                    options,
                    profile,
                    contentRootPath,
                    warmupRequest,
                    cancellationToken,
                    traceLogger,
                    correlationId,
                    target.AppId,
                    target.Endpoint,
                    instructionBundle,
                    execSessionKey,
                    execSessionPool,
                    cliArgumentsTemplateWasProvided),
                new()
                {
                    ["mode"] = target.WarmStart.Mode,
                    ["warmupRequestPath"] = warmupRequestPath.Replace('\\', '/'),
                    ["sessionKey"] = execSessionKey
                });
        }
        catch (Exception ex)
        {
            await traceLogger.WriteEventAsync(
                correlationId,
                target.AppId,
                target.Endpoint,
                "warmstart.complete",
                "failed",
                null,
                error: ex.Message,
                exception: ex,
                metadata: new()
                {
                    ["mode"] = target.WarmStart.Mode,
                    ["endpointPath"] = target.EndpointPath.Replace('\\', '/')
                });
        }
    }
}

static string ResolveWarmupRequestPath(string contentRootPath, string appId, string warmupRequest)
{
    if (Path.IsPathRooted(warmupRequest))
    {
        return warmupRequest;
    }

    var normalized = warmupRequest.Replace('/', Path.DirectorySeparatorChar);
    return Path.GetFullPath(Path.Combine(contentRootPath, "instructions", "apps", appId, normalized));
}

static RuntimeProfile ResolveRuntimeProfile(PhantomOptions options, bool fastMode)
{
    return fastMode
        ? new RuntimeProfile(options.FastModeModel, options.FastModeReasoningEffort, NormalizeServiceTier(options.FastModeServiceTier))
        : new RuntimeProfile(options.Model, options.ReasoningEffort, NormalizeServiceTier(options.NormalServiceTier));
}

static string? NormalizeServiceTier(string? serviceTier)
{
    if (string.IsNullOrWhiteSpace(serviceTier))
    {
        return null;
    }

    var normalized = serviceTier.Trim().ToLowerInvariant();
    return normalized is "fast" or "flex" ? normalized : null;
}

static async Task<string> InvokeCliAsync(
    PhantomOptions options,
    RuntimeProfile profile,
    string workingDirectory,
    string rawRequest,
    CancellationToken cancellationToken,
    TraceLogger traceLogger,
    string correlationId,
    string? appId,
    string? endpoint,
    CompiledInstructionBundle? instructionBundle,
    string? execSessionKey,
    CodexExecSessionPool? execSessionPool,
    bool cliArgumentsTemplateWasProvided)
{
    var canUseSessionPool = execSessionPool is not null
        && options.UseExecSessionPool
        && !cliArgumentsTemplateWasProvided
        && instructionBundle is not null
        && !string.IsNullOrWhiteSpace(execSessionKey);

    if (!canUseSessionPool)
    {
        if (execSessionPool is not null && cliArgumentsTemplateWasProvided)
        {
            await traceLogger.WriteEventAsync(
                correlationId,
                appId,
                endpoint,
                "codex.exec.session-pool",
                "skipped",
                null,
                detail: "Session pool skipped because a custom CliArgumentsTemplate is configured.");
        }

        var directResult = await CodexCliExecutor.ExecuteAsync(
            options,
            profile,
            workingDirectory,
            rawRequest,
            cancellationToken,
            traceLogger,
            correlationId,
            resumeSessionId: null,
            allowEarlyTermination: true);
        return directResult.RawResponse;
    }

    await using var lease = await execSessionPool!.TryAcquireAsync(execSessionKey!, cancellationToken);
    if (lease is null)
    {
        await traceLogger.WriteEventAsync(
            correlationId,
            appId,
            endpoint,
            "codex.exec.session-pool",
            "busy",
            null,
            detail: "Session pool key is already in use; using a one-off cold exec instead.",
            metadata: new() { ["sessionKey"] = execSessionKey });

        var busyFallbackResult = await CodexCliExecutor.ExecuteAsync(
            options,
            profile,
            workingDirectory,
            rawRequest,
            cancellationToken,
            traceLogger,
            correlationId,
            resumeSessionId: null,
            allowEarlyTermination: true);
        return busyFallbackResult.RawResponse;
    }

    var storedSession = await execSessionPool.GetAsync(execSessionKey!, cancellationToken);
    if (storedSession is not null)
    {
        await traceLogger.WriteEventAsync(
            correlationId,
            appId,
            endpoint,
            "codex.exec.session-pool",
            "resume",
            null,
            detail: "Resuming a previously stored codex exec session.",
            metadata: new()
            {
                ["sessionKey"] = execSessionKey,
                ["sessionId"] = storedSession.SessionId,
                ["bundleHash"] = storedSession.BundleHash
            });

        try
        {
            var resumedResult = await CodexCliExecutor.ExecuteAsync(
                options,
                profile,
                workingDirectory,
                rawRequest,
                cancellationToken,
                traceLogger,
                correlationId,
                resumeSessionId: storedSession.SessionId,
                allowEarlyTermination: true);
            return resumedResult.RawResponse;
        }
        catch (Exception ex) when (LooksLikeInvalidResumeSession(ex.Message))
        {
            await execSessionPool.RemoveAsync(execSessionKey!, cancellationToken);
            await traceLogger.WriteEventAsync(
                correlationId,
                appId,
                endpoint,
                "codex.exec.session-pool",
                "invalidated",
                null,
                detail: "Stored exec session became invalid; removed it and retrying fresh.",
                metadata: new()
                {
                    ["sessionKey"] = execSessionKey,
                    ["sessionId"] = storedSession.SessionId
                });
        }
    }

    await traceLogger.WriteEventAsync(
        correlationId,
        appId,
        endpoint,
        "codex.exec.session-pool",
        "fresh",
        null,
        detail: "No reusable exec session was available; starting a fresh codex exec session.",
        metadata: new()
        {
            ["sessionKey"] = execSessionKey,
            ["bundleHash"] = instructionBundle!.BundleHash
        });

    var freshResult = await CodexCliExecutor.ExecuteAsync(
        options,
        profile,
        workingDirectory,
        rawRequest,
        cancellationToken,
        traceLogger,
        correlationId,
        resumeSessionId: null,
        allowEarlyTermination: false);

    if (!string.IsNullOrWhiteSpace(freshResult.SessionId))
    {
        await execSessionPool.SetAsync(
            execSessionKey!,
            new CodexExecSessionRecord(
                freshResult.SessionId,
                profile.Model,
                profile.ReasoningEffort,
                profile.ServiceTier,
                instructionBundle!.BundleHash,
                DateTimeOffset.UtcNow),
            cancellationToken);

        await traceLogger.WriteEventAsync(
            correlationId,
            appId,
            endpoint,
            "codex.exec.session-pool",
            "stored",
            null,
            detail: "Stored a fresh codex exec session for future resume.",
            metadata: new()
            {
                ["sessionKey"] = execSessionKey,
                ["sessionId"] = freshResult.SessionId,
                ["bundleHash"] = instructionBundle!.BundleHash
            });
    }

    return freshResult.RawResponse;
}

static string BuildExecSessionKey(string appId, string endpoint, RuntimeProfile profile, CompiledInstructionBundle instructionBundle)
{
    return string.Join(
        "::",
        appId,
        endpoint,
        profile.Model,
        profile.ReasoningEffort,
        profile.ServiceTier ?? "default",
        instructionBundle.BundleHash);
}

static bool LooksLikeInvalidResumeSession(string message)
{
    if (string.IsNullOrWhiteSpace(message))
    {
        return false;
    }

    return (message.Contains("resume", StringComparison.OrdinalIgnoreCase)
            || message.Contains("session", StringComparison.OrdinalIgnoreCase))
        && (message.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || message.Contains("no session", StringComparison.OrdinalIgnoreCase)
            || message.Contains("unknown", StringComparison.OrdinalIgnoreCase)
            || message.Contains("missing", StringComparison.OrdinalIgnoreCase));
}

static string Truncate(string value, int maxLength)
{
    if (value.Length <= maxLength)
    {
        return value;
    }

    return value[..(maxLength - 3)] + "...";
}

static string BuildSchemaValidationError(EvaluationResults evaluation)
{
    var errors = new List<string>();
    CollectSchemaErrors(evaluation, errors);

    return errors.Count == 0
        ? "The response did not match the output schema."
        : string.Join(" | ", errors.Take(5));
}

static void CollectSchemaErrors(EvaluationResults evaluation, List<string> errors)
{
    if (evaluation.Errors is not null)
    {
        foreach (var entry in evaluation.Errors)
        {
            var location = string.IsNullOrWhiteSpace(evaluation.InstanceLocation.ToString())
                ? "$"
                : evaluation.InstanceLocation.ToString();
            errors.Add($"{location}: {entry.Value}");
        }
    }

    if (evaluation.Details is null)
    {
        return;
    }

    foreach (var detail in evaluation.Details)
    {
        CollectSchemaErrors(detail, errors);
    }
}
