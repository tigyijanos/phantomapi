using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RuntimeProfile = (string Model, string ReasoningEffort, string? ServiceTier);
using CodexCliExecutionResult = (string RawResponse, string? SessionId);

static class CodexCliExecutor
{
    public static async Task<CodexCliExecutionResult> ExecuteAsync(
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
                        TryTerminateProcess(process);
                    }
                }

                await waitForExitTask;
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    TryTerminateProcess(process);
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

            string responsePayload;
            if (!string.IsNullOrWhiteSpace(earlyOutput))
            {
                responsePayload = earlyOutput;
                TryDeleteFile(outputPath);
            }
            else if (File.Exists(outputPath))
            {
                responsePayload = File.ReadAllText(outputPath).Trim().Trim('\uFEFF');
                TryDeleteFile(outputPath);
            }
            else
            {
                responsePayload = stdout;
            }

            if (string.IsNullOrWhiteSpace(earlyOutput) && process.ExitCode != 0)
            {
                var detail = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail) ? $"CLI exited with code {process.ExitCode}." : detail);
            }

            if (string.IsNullOrWhiteSpace(responsePayload))
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
                    ["rawResponseLength"] = responsePayload.Length,
                    ["resumeSessionId"] = resumeSessionId,
                    ["parsedSessionId"] = parsedSessionId
                });

            return (RawResponse: responsePayload, SessionId: parsedSessionId);
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

    public static string BuildCliArgumentsTemplate(PhantomOptions options, RuntimeProfile profile)
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

    private static List<string> BuildResumeCliArguments(RuntimeProfile profile, string outputPath, string sessionId)
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

    private static string? TryParseSessionId(string stdout, string stderr)
    {
        var combined = string.IsNullOrWhiteSpace(stderr)
            ? stdout
            : $"{stdout}\n{stderr}";
        var match = Regex.Match(combined, @"session id:\s*([0-9a-fA-F-]{36})", RegexOptions.IgnoreCase);
        return match.Success
            ? match.Groups[1].Value
            : null;
    }

    private static List<string> TokenizeArguments(string commandLine)
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

    private static string QuoteArgument(string value)
    {
        return value.Contains(' ') ? $"\"{value}\"" : value;
    }

    private static string? BuildCliTimeoutDetail(string stdout, string stderr)
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

    private static async Task<string> WaitForOutputFileAsync(string outputPath, CancellationToken cancellationToken)
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

    private static string? TryReadCompletedJsonFile(string outputPath)
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

    private static void TryDeleteFile(string path)
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

    private static void TryTerminateProcess(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..(maxLength - 3)] + "...";
    }
}
