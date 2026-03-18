using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

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
    phantomOptions.CliArgumentsTemplate = BuildCliArgumentsTemplate(phantomOptions, defaultProfile);
}

if (phantomOptions.CliTimeoutSeconds <= 0)
{
    phantomOptions.CliTimeoutSeconds = 180;
}

if (phantomOptions.WarmTurnGraceSeconds <= 0)
{
    phantomOptions.WarmTurnGraceSeconds = 4;
}

var app = builder.Build();
var traceLogger = new TraceLogger(repoRoot);
var instructionBundleCompiler = new InstructionBundleCompiler(repoRoot);
var endpointContractCache = new EndpointContractCache(repoRoot);
var execSessionPool = phantomOptions.UseExecSessionPool
    ? new CodexExecSessionPool(repoRoot)
    : null;
var appServerClient = phantomOptions.UseWarmAppServer
    ? new CodexAppServerClient(phantomOptions, repoRoot, traceLogger)
    : null;

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
        cliArgumentsTemplateWasProvided));
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

        var responseContractJson = resolvedContract.ResponseContractJson;
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
                ["sourcePath"] = resolvedContract.SourcePath.Replace('\\', '/'),
                ["outputSchemaDerived"] = resolvedContract.OutputSchemaWasDerived
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

                    cliRawResponse = await TraceStepAsyncResult(
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
            }
            else
            {
                cliRawResponse = await TraceStepAsyncResult(
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

        var contractMatch = await TraceStepAsyncResult(
            traceLogger,
            correlationId,
            appId,
            endpoint,
            "response.contract-match",
            () =>
            {
                var match = MatchesContract(resolvedContract.ResponseContract, cliResponse.RootElement, "$", out var contractError);
                return Task.FromResult((match, contractError));
            },
            null,
            new() { ["path"] = "/dynamic-api" });

        if (!contractMatch.match)
        {
            return BuildProblem("CLI response failed the hard guard.", contractMatch.contractError, 502);
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
    bool cliArgumentsTemplateWasProvided)
{
    var configuredWarmStarts = EndpointWarmStartCatalog.Discover(contentRootPath);
    if (configuredWarmStarts.Count == 0)
    {
        return;
    }

    foreach (var target in configuredWarmStarts)
    {
        var correlationId = $"warmstart_{Guid.NewGuid():N}";
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
                await traceLogger.WriteEventAsync(
                    correlationId,
                    target.AppId,
                    target.Endpoint,
                    "warmstart.exec-session",
                    "skipped",
                    null,
                    detail: "Exec-session warm start skipped because the exec session pool is disabled.",
                    metadata: new() { ["mode"] = target.WarmStart.Mode });
                continue;
            }

            if (cliArgumentsTemplateWasProvided)
            {
                await traceLogger.WriteEventAsync(
                    correlationId,
                    target.AppId,
                    target.Endpoint,
                    "warmstart.exec-session",
                    "skipped",
                    null,
                    detail: "Exec-session warm start skipped because a custom CliArgumentsTemplate is configured.",
                    metadata: new() { ["mode"] = target.WarmStart.Mode });
                continue;
            }

            if (!target.WarmStart.ReadOnlyWarmup)
            {
                await traceLogger.WriteEventAsync(
                    correlationId,
                    target.AppId,
                    target.Endpoint,
                    "warmstart.exec-session",
                    "skipped",
                    null,
                    detail: "Exec-session warm start skipped because the endpoint is not marked as readonly-safe for startup warmup.",
                    metadata: new() { ["mode"] = target.WarmStart.Mode });
                continue;
            }

            var execSessionKey = BuildExecSessionKey(target.AppId, target.Endpoint, profile, instructionBundle);
            var existingSession = await execSessionPool.GetAsync(execSessionKey, CancellationToken.None);
            if (existingSession is not null)
            {
                await traceLogger.WriteEventAsync(
                    correlationId,
                    target.AppId,
                    target.Endpoint,
                    "warmstart.exec-session",
                    "skipped",
                    null,
                    detail: "Exec-session warm start skipped because a compatible stored session already exists.",
                    metadata: new()
                    {
                        ["sessionKey"] = execSessionKey,
                        ["sessionId"] = existingSession.SessionId
                    });
                continue;
            }

            if (string.IsNullOrWhiteSpace(target.WarmStart.WarmupRequest))
            {
                await traceLogger.WriteEventAsync(
                    correlationId,
                    target.AppId,
                    target.Endpoint,
                    "warmstart.exec-session",
                    "skipped",
                    null,
                    detail: "Exec-session warm start skipped because no warmupRequest is configured.",
                    metadata: new() { ["mode"] = target.WarmStart.Mode });
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

            var warmupRequest = await File.ReadAllTextAsync(warmupRequestPath);
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
                    CancellationToken.None,
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

        var directResult = await InvokeCliProcessAsync(
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

        var busyFallbackResult = await InvokeCliProcessAsync(
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
            var resumedResult = await InvokeCliProcessAsync(
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

    var freshResult = await InvokeCliProcessAsync(
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

static async Task<CodexCliExecutionResult> InvokeCliProcessAsync(
    PhantomOptions options,
    RuntimeProfile profile,
    string workingDirectory,
    string rawRequest,
    CancellationToken cancellationToken,
    TraceLogger traceLogger,
    string correlationId,
    string? resumeSessionId,
    bool allowEarlyTermination)
{
    var stopwatch = Stopwatch.StartNew();
    var outputPath = Path.Combine(Path.GetTempPath(), $"phantomapi-{Guid.NewGuid():N}.json");
    var arguments = resumeSessionId is null
        ? TokenizeArguments(BuildCliArgumentsTemplate(options, profile).Replace("{output}", QuoteArgument(outputPath)))
        : BuildResumeCliArguments(profile, outputPath, resumeSessionId);

    var startInfo = new ProcessStartInfo
    {
        FileName = options.CliCommand,
        WorkingDirectory = workingDirectory,
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    foreach (var argument in arguments)
    {
        startInfo.ArgumentList.Add(argument);
    }

    using var process = new Process { StartInfo = startInfo };
    var processStarted = false;
    try
    {
        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start the CLI process.");
        }

        processStarted = true;

        await traceLogger.WriteEventAsync(
            correlationId,
            null,
            null,
            "codex.exec.cold.start",
            "success",
            null,
            metadata: new()
            {
                ["model"] = profile.Model,
                ["reasoningEffort"] = profile.ReasoningEffort,
                ["serviceTier"] = profile.ServiceTier ?? "default",
                ["resumeSessionId"] = resumeSessionId,
                ["allowEarlyTermination"] = allowEarlyTermination
            });

        await process.StandardInput.WriteAsync(rawRequest);
        await process.StandardInput.FlushAsync(cancellationToken);
        process.StandardInput.Close();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(options.CliTimeoutSeconds));
        var waitForExitTask = process.WaitForExitAsync(timeoutCts.Token);
        var waitForOutputTask = WaitForOutputFileAsync(outputPath, timeoutCts.Token);
        string? earlyOutput = null;

        try
        {
            var completedTask = await Task.WhenAny(waitForExitTask, waitForOutputTask);
            if (completedTask == waitForOutputTask)
            {
                earlyOutput = await waitForOutputTask;
                await traceLogger.WriteEventAsync(
                    correlationId,
                    null,
                    null,
                    "codex.exec.cold.output-ready",
                    "success",
                    stopwatch.ElapsedMilliseconds,
                    metadata: new()
                    {
                        ["rawResponseLength"] = earlyOutput.Length,
                        ["resumeSessionId"] = resumeSessionId
                    });

                if (allowEarlyTermination && !process.HasExited)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                    }
                }
            }

            await waitForExitTask;
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                }
            }

            var partialStdout = (await stdoutTask).Trim();
            var partialStderr = (await stderrTask).Trim();
            var timeoutDetail = BuildCliTimeoutDetail(partialStdout, partialStderr);
            throw new TimeoutException(timeoutDetail is null
                ? $"CLI call timed out after {options.CliTimeoutSeconds} seconds."
                : $"CLI call timed out after {options.CliTimeoutSeconds} seconds. {timeoutDetail}");
        }

        var stdout = (await stdoutTask).Trim();
        var stderr = (await stderrTask).Trim();

        string rawResponse;
        if (!string.IsNullOrWhiteSpace(earlyOutput))
        {
            rawResponse = earlyOutput;
            TryDeleteFile(outputPath);
        }
        else if (File.Exists(outputPath))
        {
            rawResponse = File.ReadAllText(outputPath).Trim().Trim('\uFEFF');
            TryDeleteFile(outputPath);
        }
        else
        {
            rawResponse = stdout;
        }

        if (string.IsNullOrWhiteSpace(earlyOutput) && process.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail) ? $"CLI exited with code {process.ExitCode}." : detail);
        }

        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            throw new InvalidOperationException("CLI returned an empty response.");
        }

        var parsedSessionId = TryParseSessionId(stdout, stderr);
        await traceLogger.WriteEventAsync(
            correlationId,
            null,
            null,
            "codex.exec.cold.complete",
            "success",
            stopwatch.ElapsedMilliseconds,
            metadata: new()
            {
                ["exitCode"] = process.ExitCode,
                ["rawResponseLength"] = rawResponse.Length,
                ["resumeSessionId"] = resumeSessionId,
                ["parsedSessionId"] = parsedSessionId
            });

        return new CodexCliExecutionResult(rawResponse, parsedSessionId);
    }
    catch (Exception ex)
    {
        var result = processStarted ? "failed" : "failed-to-start";
        await traceLogger.WriteEventAsync(
            correlationId,
            null,
            null,
            "codex.exec.cold.complete",
            result,
            stopwatch.ElapsedMilliseconds,
            detail: ex.Message,
            error: ex.Message,
            exception: ex,
            metadata: new()
            {
                ["processStarted"] = processStarted,
                ["resumeSessionId"] = resumeSessionId
            });

        throw;
    }
}

static List<string> BuildResumeCliArguments(RuntimeProfile profile, string outputPath, string sessionId)
{
    return
    [
        "--dangerously-bypass-approvals-and-sandbox",
        "exec",
        "resume",
        "-m",
        profile.Model,
        "-c",
        $"model_reasoning_effort={profile.ReasoningEffort}",
        "--skip-git-repo-check",
        "--output-last-message",
        outputPath,
        sessionId,
        "-"
    ];
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

static string? TryParseSessionId(string stdout, string stderr)
{
    var combined = string.IsNullOrWhiteSpace(stderr)
        ? stdout
        : $"{stdout}\n{stderr}";
    var match = Regex.Match(combined, @"session id:\s*([0-9a-fA-F-]{36})", RegexOptions.IgnoreCase);
    return match.Success
        ? match.Groups[1].Value
        : null;
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

static List<string> TokenizeArguments(string commandLine)
{
    var tokens = new List<string>();
    var current = new StringBuilder();
    var inQuotes = false;

    foreach (var character in commandLine)
    {
        if (character == '"')
        {
            inQuotes = !inQuotes;
            continue;
        }

        if (char.IsWhiteSpace(character) && !inQuotes)
        {
            if (current.Length > 0)
            {
                tokens.Add(current.ToString());
                current.Clear();
            }

            continue;
        }

        current.Append(character);
    }

    if (current.Length > 0)
    {
        tokens.Add(current.ToString());
    }

    return tokens;
}

static string QuoteArgument(string value)
{
    return value.Contains(' ') ? $"\"{value}\"" : value;
}

static string BuildCliArgumentsTemplate(PhantomOptions options, RuntimeProfile profile)
{
    var arguments = new List<string>
    {
        "--dangerously-bypass-approvals-and-sandbox",
        "exec",
        "-m",
        profile.Model,
        "-c",
        $"model_reasoning_effort={profile.ReasoningEffort}",
        "--skip-git-repo-check",
        "--output-last-message",
        "{output}",
        "-"
    };

    return string.Join(' ', arguments.Select(QuoteArgument));
}

static string? BuildCliTimeoutDetail(string stdout, string stderr)
{
    var candidate = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
    if (string.IsNullOrWhiteSpace(candidate))
    {
        return null;
    }

    var lastLine = candidate
        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .LastOrDefault();

    if (string.IsNullOrWhiteSpace(lastLine))
    {
        return null;
    }

    return $"Last CLI activity: {Truncate(lastLine, 240)}";
}

static async Task<string> WaitForOutputFileAsync(string outputPath, CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        var candidate = TryReadCompletedJsonFile(outputPath);
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            return candidate;
        }

        await Task.Delay(150, cancellationToken);
    }

    throw new OperationCanceledException(cancellationToken);
}

static string? TryReadCompletedJsonFile(string outputPath)
{
    if (!File.Exists(outputPath))
    {
        return null;
    }

    try
    {
        var raw = File.ReadAllText(outputPath).Trim().Trim('\uFEFF');
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        using var _ = JsonDocument.Parse(raw);
        return raw;
    }
    catch (IOException)
    {
        return null;
    }
    catch (UnauthorizedAccessException)
    {
        return null;
    }
    catch (JsonException)
    {
        return null;
    }
}

static void TryDeleteFile(string path)
{
    try
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
    catch
    {
    }
}

static string Truncate(string value, int maxLength)
{
    if (value.Length <= maxLength)
    {
        return value;
    }

    return value[..(maxLength - 3)] + "...";
}

static bool MatchesContract(JsonElement contract, JsonElement response, string path, out string? error)
{
    if (contract.ValueKind == JsonValueKind.Null)
    {
        if (response.ValueKind == JsonValueKind.Null)
        {
            error = null;
            return true;
        }

        error = $"{path} must be null, got {response.ValueKind}.";
        return false;
    }

    if (contract.ValueKind == JsonValueKind.Object)
    {
        if (response.ValueKind != JsonValueKind.Object)
        {
            error = $"{path} must be an object, got {response.ValueKind}.";
            return false;
        }

        foreach (var contractProperty in contract.EnumerateObject())
        {
            if (!response.TryGetProperty(contractProperty.Name, out var responseProperty))
            {
                error = $"{path} is missing property '{contractProperty.Name}'.";
                return false;
            }

            if (!MatchesContract(contractProperty.Value, responseProperty, $"{path}.{contractProperty.Name}", out error))
            {
                return false;
            }
        }

        error = null;
        return true;
    }

    if (contract.ValueKind == JsonValueKind.Array)
    {
        if (response.ValueKind != JsonValueKind.Array)
        {
            error = $"{path} must be an array, got {response.ValueKind}.";
            return false;
        }

        var itemTemplate = contract.EnumerateArray().FirstOrDefault();
        foreach (var responseItem in response.EnumerateArray())
        {
            if (itemTemplate.ValueKind == JsonValueKind.Undefined)
            {
                break;
            }

            if (!MatchesContract(itemTemplate, responseItem, $"{path}[]", out error))
            {
                return false;
            }
        }

        error = null;
        return true;
    }

    if (!KindsMatch(contract.ValueKind, response.ValueKind))
    {
        error = $"{path} type mismatch, expected {contract.ValueKind}, got {response.ValueKind}.";
        return false;
    }

    error = null;
    return true;
}

static bool KindsMatch(JsonValueKind expected, JsonValueKind actual)
{
    if (expected is JsonValueKind.True or JsonValueKind.False)
    {
        return actual is JsonValueKind.True or JsonValueKind.False;
    }

    if (expected == actual)
    {
        return true;
    }

    return false;
}
