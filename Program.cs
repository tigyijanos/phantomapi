using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

var phantomOptions = new PhantomOptions();
builder.Configuration.GetSection("Phantom").Bind(phantomOptions);

if (string.IsNullOrWhiteSpace(phantomOptions.CliCommand))
{
    phantomOptions.CliCommand = OperatingSystem.IsWindows() ? "codex.cmd" : "codex";
}

if (string.IsNullOrWhiteSpace(phantomOptions.CliArgumentsTemplate))
{
    phantomOptions.CliArgumentsTemplate = "--dangerously-bypass-approvals-and-sandbox exec --skip-git-repo-check --output-last-message {output} -";
}

if (phantomOptions.CliTimeoutSeconds <= 0)
{
    phantomOptions.CliTimeoutSeconds = 300;
}

var app = builder.Build();

app.MapPost("/dynamic-api", async (HttpRequest request, IWebHostEnvironment environment, CancellationToken cancellationToken) =>
{
    string rawRequest;

    try
    {
        rawRequest = await ReadRequestBodyAsync(request, cancellationToken);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = "Failed to read request body.", detail = ex.Message });
    }

    if (string.IsNullOrWhiteSpace(rawRequest))
    {
        return Results.BadRequest(new { error = "Request body is required." });
    }

    JsonDocument requestDocument;

    try
    {
        requestDocument = JsonDocument.Parse(rawRequest);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = "Request body must be valid JSON.", detail = ex.Message });
    }

    using var requestDocumentCleanup = requestDocument;

    if (requestDocument.RootElement.ValueKind != JsonValueKind.Object)
    {
        return Results.BadRequest(new { error = "Request body must be a JSON object." });
    }

    if (!TryGetRoute(requestDocument.RootElement, out var appId, out var endpoint, out var routeError))
    {
        return Results.BadRequest(new { error = routeError });
    }

    string contractInstruction;

    try
    {
        contractInstruction = ResolveContractInstruction(environment.ContentRootPath, appId!, endpoint!);
    }
    catch (Exception ex)
    {
        return Results.Problem(title: "Failed to resolve response contract.", detail: ex.Message, statusCode: 500);
    }

    if (!TryExtractFirstJsonCodeBlock(contractInstruction, out var responseContractJson, out var contractExtractionError))
    {
        return Results.Problem(title: "Resolved contract instruction is missing a response contract.", detail: contractExtractionError, statusCode: 500);
    }

    JsonDocument responseContract;

    try
    {
        responseContract = JsonDocument.Parse(responseContractJson!);
    }
    catch (Exception ex)
    {
        return Results.Problem(title: "Endpoint response contract is not valid JSON.", detail: ex.Message, statusCode: 500);
    }

    using var responseContractCleanup = responseContract;

    string cliRawResponse;

    try
    {
        cliRawResponse = await InvokeCliAsync(phantomOptions, environment.ContentRootPath, rawRequest, cancellationToken);
    }
    catch (Exception ex)
    {
        return Results.Problem(title: "CLI invocation failed.", detail: ex.Message, statusCode: 502);
    }

    JsonDocument cliResponse;

    try
    {
        cliResponse = JsonDocument.Parse(cliRawResponse);
    }
    catch (Exception ex)
    {
        return Results.Problem(title: "CLI returned invalid JSON.", detail: ex.Message, statusCode: 502);
    }

    using var cliResponseCleanup = cliResponse;

    if (!MatchesContract(responseContract.RootElement, cliResponse.RootElement, "$", out var guardError))
    {
        return Results.Problem(title: "CLI response failed the hard guard.", detail: guardError, statusCode: 502);
    }

    return Results.Content(cliResponse.RootElement.GetRawText(), "application/json");
});

app.Run();

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

static string ResolveContractInstruction(string contentRootPath, string appId, string endpoint)
{
    var appDirectory = Path.Combine(contentRootPath, "instructions", "apps", appId);

    if (!Directory.Exists(appDirectory))
    {
        return LoadFrameworkInstruction(contentRootPath, Path.Combine("errors", "app-not-found.md"));
    }

    var relativePath = endpoint.Replace('/', Path.DirectorySeparatorChar) + ".md";
    var endpointPath = Path.Combine(appDirectory, "endpoints", relativePath);

    if (!File.Exists(endpointPath))
    {
        return LoadFrameworkInstruction(contentRootPath, Path.Combine("errors", "endpoint-not-found.md"));
    }

    return File.ReadAllText(endpointPath).Trim();
}

static string LoadFrameworkInstruction(string contentRootPath, string relativePath)
{
    var fullPath = Path.Combine(contentRootPath, "instructions", "framework", relativePath);

    if (!File.Exists(fullPath))
    {
        throw new FileNotFoundException($"No framework instruction file found at instructions/framework/{relativePath.Replace('\\', '/')}");
    }

    return File.ReadAllText(fullPath).Trim();
}

static bool TryExtractFirstJsonCodeBlock(string markdown, out string? json, out string? error)
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

static async Task<string> InvokeCliAsync(PhantomOptions options, string workingDirectory, string rawRequest, CancellationToken cancellationToken)
{
    var outputPath = Path.Combine(Path.GetTempPath(), $"phantomapi-{Guid.NewGuid():N}.json");
    var arguments = TokenizeArguments(options.CliArgumentsTemplate.Replace("{output}", QuoteArgument(outputPath)));

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

    if (!process.Start())
    {
        throw new InvalidOperationException("Failed to start the CLI process.");
    }

    await process.StandardInput.WriteAsync(rawRequest);
    await process.StandardInput.FlushAsync();
    process.StandardInput.Close();

    var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
    var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    timeoutCts.CancelAfter(TimeSpan.FromSeconds(options.CliTimeoutSeconds));

    try
    {
        await process.WaitForExitAsync(timeoutCts.Token);
    }
    catch (OperationCanceledException)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }

        throw new TimeoutException($"CLI call timed out after {options.CliTimeoutSeconds} seconds.");
    }

    var stdout = (await stdoutTask).Trim();
    var stderr = (await stderrTask).Trim();

    string rawResponse;

    if (File.Exists(outputPath))
    {
        rawResponse = File.ReadAllText(outputPath).Trim().Trim('\uFEFF');
        File.Delete(outputPath);
    }
    else
    {
        rawResponse = stdout;
    }

    if (process.ExitCode != 0)
    {
        var detail = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail) ? $"CLI exited with code {process.ExitCode}." : detail);
    }

    if (string.IsNullOrWhiteSpace(rawResponse))
    {
        throw new InvalidOperationException("CLI returned an empty response.");
    }

    return rawResponse;
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

static bool MatchesContract(JsonElement contract, JsonElement actual, string path, out string? error)
{
    switch (contract.ValueKind)
    {
        case JsonValueKind.Object:
            if (actual.ValueKind != JsonValueKind.Object)
            {
                error = $"{path} must be an object.";
                return false;
            }

            var expectedProperties = contract.EnumerateObject().Select(property => property.Name).OrderBy(name => name).ToArray();
            var actualProperties = actual.EnumerateObject().Select(property => property.Name).OrderBy(name => name).ToArray();

            if (!expectedProperties.SequenceEqual(actualProperties, StringComparer.Ordinal))
            {
                error = $"{path} properties do not match the response contract. Expected [{string.Join(", ", expectedProperties)}] but got [{string.Join(", ", actualProperties)}].";
                return false;
            }

            foreach (var property in contract.EnumerateObject())
            {
                if (!actual.TryGetProperty(property.Name, out var actualProperty))
                {
                    error = $"{path}.{property.Name} is missing.";
                    return false;
                }

                if (!MatchesContract(property.Value, actualProperty, $"{path}.{property.Name}", out error))
                {
                    return false;
                }
            }

            error = null;
            return true;

        case JsonValueKind.Array:
            if (actual.ValueKind != JsonValueKind.Array)
            {
                error = $"{path} must be an array.";
                return false;
            }

            var expectedItems = contract.EnumerateArray().ToArray();
            var actualItems = actual.EnumerateArray().ToArray();

            if (expectedItems.Length == 0)
            {
                error = null;
                return true;
            }

            if (expectedItems.Length == 1)
            {
                for (var index = 0; index < actualItems.Length; index++)
                {
                    if (!MatchesContract(expectedItems[0], actualItems[index], $"{path}[{index}]", out error))
                    {
                        return false;
                    }
                }

                error = null;
                return true;
            }

            if (expectedItems.Length != actualItems.Length)
            {
                error = $"{path} array length does not match the response contract.";
                return false;
            }

            for (var index = 0; index < expectedItems.Length; index++)
            {
                if (!MatchesContract(expectedItems[index], actualItems[index], $"{path}[{index}]", out error))
                {
                    return false;
                }
            }

            error = null;
            return true;

        case JsonValueKind.String:
            error = actual.ValueKind == JsonValueKind.String ? null : $"{path} must be a string.";
            return error is null;

        case JsonValueKind.Number:
            error = actual.ValueKind == JsonValueKind.Number ? null : $"{path} must be a number.";
            return error is null;

        case JsonValueKind.True:
        case JsonValueKind.False:
            error = actual.ValueKind is JsonValueKind.True or JsonValueKind.False ? null : $"{path} must be a boolean.";
            return error is null;

        case JsonValueKind.Null:
            error = actual.ValueKind == JsonValueKind.Null ? null : $"{path} must be null.";
            return error is null;

        default:
            error = $"{path} contains an unsupported contract token.";
            return false;
    }
}

sealed class PhantomOptions
{
    public string CliCommand { get; set; } = string.Empty;
    public string CliArgumentsTemplate { get; set; } = string.Empty;
    public int CliTimeoutSeconds { get; set; } = 300;
}
