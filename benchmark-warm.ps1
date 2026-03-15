param(
    [ValidateSet("task-board-login", "bank-login", "missing-app")]
    [string[]]$Requests = @("task-board-login", "bank-login"),

    [ValidateSet(
        "warm-medium-default",
        "warm-low-default",
        "warm-low-fast",
        "warm-medium-fast",
        "cold-medium-default",
        "cold-low-default",
        "warm-medium-default-gpt54",
        "warm-low-default-gpt54",
        "warm-low-fast-gpt54",
        "warm-medium-fast-gpt54",
        "cold-medium-default-gpt54",
        "cold-low-default-gpt54",
        "warm-low-fast-gpt54-grace3",
        "warm-low-fast-gpt54-grace12"
    )]
    [string[]]$Variants = @(
        "warm-medium-default",
        "warm-low-default",
        "warm-low-fast",
        "cold-medium-default"
    ),

    [int]$Repetitions = 2,
    [int]$StartingPort = 5070,
    [switch]$BuildFirst
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $repoRoot

if ($BuildFirst) {
    dotnet build | Out-Host
}

$tracePath = Join-Path $repoRoot "data\framework\traces\events.jsonl"
$runId = Get-Date -Format "yyyyMMdd-HHmmss"
$outputDir = Join-Path $repoRoot "data\framework\benchmarks\$runId"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$requestMap = @{
    "task-board-login" = @{
        Name = "task-board-login"
        Body = @{
            app = "task-board"
            endpoint = "auth/login"
            email = "taylor@tasks.test"
            password = "tasks123"
        }
    }
    "bank-login" = @{
        Name = "bank-login"
        Body = @{
            app = "bank-api"
            endpoint = "auth/login"
            email = "ada@phantom.test"
            password = "ada123"
        }
    }
    "missing-app" = @{
        Name = "missing-app"
        Body = @{
            app = "missing-app"
            endpoint = "bank/get-balance"
        }
    }
}

$variantMap = @{
    "warm-medium-default" = @{
        Name = "warm-medium-default"
        Env = @{
            "Phantom__UseWarmAppServer" = "true"
            "Phantom__FallbackToColdExecution" = "true"
            "Phantom__ReasoningEffort" = "medium"
            "Phantom__NormalServiceTier" = ""
        }
    }
    "warm-low-default" = @{
        Name = "warm-low-default"
        Env = @{
            "Phantom__UseWarmAppServer" = "true"
            "Phantom__FallbackToColdExecution" = "true"
            "Phantom__ReasoningEffort" = "low"
            "Phantom__NormalServiceTier" = ""
        }
    }
    "warm-low-fast" = @{
        Name = "warm-low-fast"
        Env = @{
            "Phantom__UseWarmAppServer" = "true"
            "Phantom__FallbackToColdExecution" = "true"
            "Phantom__ReasoningEffort" = "low"
            "Phantom__NormalServiceTier" = "fast"
        }
    }
    "warm-medium-fast" = @{
        Name = "warm-medium-fast"
        Env = @{
            "Phantom__UseWarmAppServer" = "true"
            "Phantom__FallbackToColdExecution" = "true"
            "Phantom__ReasoningEffort" = "medium"
            "Phantom__NormalServiceTier" = "fast"
        }
    }
    "cold-medium-default" = @{
        Name = "cold-medium-default"
        Env = @{
            "Phantom__UseWarmAppServer" = "false"
            "Phantom__FallbackToColdExecution" = "false"
            "Phantom__ReasoningEffort" = "medium"
            "Phantom__NormalServiceTier" = ""
        }
    }
    "cold-low-default" = @{
        Name = "cold-low-default"
        Env = @{
            "Phantom__UseWarmAppServer" = "false"
            "Phantom__FallbackToColdExecution" = "false"
            "Phantom__ReasoningEffort" = "low"
            "Phantom__NormalServiceTier" = ""
        }
    }
    "warm-medium-default-gpt54" = @{
        Name = "warm-medium-default-gpt54"
        Env = @{
            "Phantom__UseWarmAppServer" = "true"
            "Phantom__FallbackToColdExecution" = "true"
            "Phantom__Model" = "gpt-5.4"
            "Phantom__FastModeModel" = "gpt-5.4"
            "Phantom__ReasoningEffort" = "medium"
            "Phantom__NormalServiceTier" = ""
        }
    }
    "warm-low-default-gpt54" = @{
        Name = "warm-low-default-gpt54"
        Env = @{
            "Phantom__UseWarmAppServer" = "true"
            "Phantom__FallbackToColdExecution" = "true"
            "Phantom__Model" = "gpt-5.4"
            "Phantom__FastModeModel" = "gpt-5.4"
            "Phantom__ReasoningEffort" = "low"
            "Phantom__NormalServiceTier" = ""
        }
    }
    "warm-low-fast-gpt54" = @{
        Name = "warm-low-fast-gpt54"
        Env = @{
            "Phantom__UseWarmAppServer" = "true"
            "Phantom__FallbackToColdExecution" = "true"
            "Phantom__Model" = "gpt-5.4"
            "Phantom__FastModeModel" = "gpt-5.4"
            "Phantom__ReasoningEffort" = "low"
            "Phantom__NormalServiceTier" = "fast"
        }
    }
    "warm-medium-fast-gpt54" = @{
        Name = "warm-medium-fast-gpt54"
        Env = @{
            "Phantom__UseWarmAppServer" = "true"
            "Phantom__FallbackToColdExecution" = "true"
            "Phantom__Model" = "gpt-5.4"
            "Phantom__FastModeModel" = "gpt-5.4"
            "Phantom__ReasoningEffort" = "medium"
            "Phantom__NormalServiceTier" = "fast"
        }
    }
    "cold-medium-default-gpt54" = @{
        Name = "cold-medium-default-gpt54"
        Env = @{
            "Phantom__UseWarmAppServer" = "false"
            "Phantom__FallbackToColdExecution" = "false"
            "Phantom__Model" = "gpt-5.4"
            "Phantom__FastModeModel" = "gpt-5.4"
            "Phantom__ReasoningEffort" = "medium"
            "Phantom__NormalServiceTier" = ""
        }
    }
    "cold-low-default-gpt54" = @{
        Name = "cold-low-default-gpt54"
        Env = @{
            "Phantom__UseWarmAppServer" = "false"
            "Phantom__FallbackToColdExecution" = "false"
            "Phantom__Model" = "gpt-5.4"
            "Phantom__FastModeModel" = "gpt-5.4"
            "Phantom__ReasoningEffort" = "low"
            "Phantom__NormalServiceTier" = ""
        }
    }
    "warm-low-fast-gpt54-grace3" = @{
        Name = "warm-low-fast-gpt54-grace3"
        Env = @{
            "Phantom__UseWarmAppServer" = "true"
            "Phantom__FallbackToColdExecution" = "true"
            "Phantom__Model" = "gpt-5.4"
            "Phantom__FastModeModel" = "gpt-5.4"
            "Phantom__ReasoningEffort" = "low"
            "Phantom__NormalServiceTier" = "fast"
            "Phantom__WarmTurnGraceSeconds" = "3"
        }
    }
    "warm-low-fast-gpt54-grace12" = @{
        Name = "warm-low-fast-gpt54-grace12"
        Env = @{
            "Phantom__UseWarmAppServer" = "true"
            "Phantom__FallbackToColdExecution" = "true"
            "Phantom__Model" = "gpt-5.4"
            "Phantom__FastModeModel" = "gpt-5.4"
            "Phantom__ReasoningEffort" = "low"
            "Phantom__NormalServiceTier" = "fast"
            "Phantom__WarmTurnGraceSeconds" = "12"
        }
    }
}

function Get-TraceLines {
    if (-not (Test-Path $tracePath)) {
        return @()
    }

    return @(Get-Content -Path $tracePath)
}

function Wait-ApiReady {
    param([string]$BaseUrl)

    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        try {
            Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/" -TimeoutSec 1 | Out-Null
            return
        }
        catch {
            if ($_.Exception.Response) {
                return
            }

            Start-Sleep -Milliseconds 500
        }
    }

    throw "Server did not become ready at $BaseUrl."
}

function Start-BenchmarkServer {
    param(
        [string]$Url,
        [hashtable]$EnvOverrides
    )

    $job = Start-Job -ScriptBlock {
        param($WorkingDirectory, $ServerUrl, $Overrides)

        Set-Location $WorkingDirectory
        foreach ($entry in $Overrides.GetEnumerator()) {
            Set-Item -Path "Env:$($entry.Key)" -Value $entry.Value
        }

        dotnet run --no-build --urls $ServerUrl
    } -ArgumentList $repoRoot, $Url, $EnvOverrides

    return $job
}

function Stop-BenchmarkServer {
    param([System.Management.Automation.Job]$Job, [string]$OutputPath)

    if ($null -eq $Job) {
        return
    }

    Stop-Job $Job -ErrorAction SilentlyContinue | Out-Null
    $jobOutput = Receive-Job $Job -Wait -ErrorAction SilentlyContinue
    if ($jobOutput) {
        $jobOutput | Out-File -FilePath $OutputPath -Encoding utf8
    }

    Remove-Job $Job -Force -ErrorAction SilentlyContinue | Out-Null
}

$results = New-Object System.Collections.Generic.List[object]
$port = $StartingPort

foreach ($variantName in $Variants) {
    foreach ($requestName in $Requests) {
        $variant = $variantMap[$variantName]
        $request = $requestMap[$requestName]
        $scenarioName = "$variantName--$requestName"
        $baseUrl = "http://127.0.0.1:$port"
        $stdoutPath = Join-Path $outputDir "$scenarioName.server.log"
        $traceOutputPath = Join-Path $outputDir "$scenarioName.trace.jsonl"
        $requestOutputPath = Join-Path $outputDir "$scenarioName.requests.json"

        $traceBefore = Get-TraceLines
        $traceStartIndex = $traceBefore.Count

        $job = $null
        try {
            Write-Host "Running $scenarioName on $baseUrl ..."
            $job = Start-BenchmarkServer -Url $baseUrl -EnvOverrides $variant.Env
            Start-Sleep -Seconds 2
            Wait-ApiReady -BaseUrl $baseUrl

            $requestBodies = New-Object System.Collections.Generic.List[object]
            for ($iteration = 1; $iteration -le $Repetitions; $iteration++) {
                $bodyJson = $request.Body | ConvertTo-Json -Compress -Depth 10
                $sw = [System.Diagnostics.Stopwatch]::StartNew()
                $statusCode = 0
                $responseText = ""
                $errorText = $null

                try {
                    $response = Invoke-WebRequest -UseBasicParsing -Method Post -Uri "$baseUrl/dynamic-api" -ContentType "application/json" -Body $bodyJson -TimeoutSec 180
                    $statusCode = [int]$response.StatusCode
                    $responseText = $response.Content
                }
                catch {
                    $statusCode = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode.value__ } else { 0 }
                    $errorText = $_.Exception.Message
                }
                finally {
                    $sw.Stop()
                }

                $requestBodies.Add([pscustomobject]@{
                    iteration = $iteration
                    elapsedMs = [int]$sw.ElapsedMilliseconds
                    statusCode = $statusCode
                    responseLength = if ($responseText) { $responseText.Length } else { 0 }
                    error = $errorText
                    response = $responseText
                }) | Out-Null

                Start-Sleep -Milliseconds 500
            }

            $traceAfter = Get-TraceLines
            $traceDelta = if ($traceAfter.Count -gt $traceStartIndex) {
                $traceAfter[$traceStartIndex..($traceAfter.Count - 1)]
            }
            else {
                @()
            }

            $traceDelta | Out-File -FilePath $traceOutputPath -Encoding utf8
            $requestBodies | ConvertTo-Json -Depth 10 | Out-File -FilePath $requestOutputPath -Encoding utf8

            $warmSuccessCount = @($traceDelta | Where-Object { $_ -match '"stage":"codex\.exec\.warm","result":"success"' }).Count
            $warmFallbackCount = @($traceDelta | Where-Object { $_ -match '"stage":"codex\.exec\.warm-fallback","result":"started"' }).Count
            $coldCount = @($traceDelta | Where-Object { $_ -match '"stage":"codex\.exec\.cold","result":"success"' }).Count
            $warmFailureCount = @($traceDelta | Where-Object { $_ -match '"stage":"appserver\.turn-completed","result":"failed"' }).Count
            $warmSnapshotCount = @($traceDelta | Where-Object { $_ -match '"stage":"appserver\.turn-snapshot","result":"success"' }).Count
            $usageLimitCount = @($traceDelta | Where-Object { $_ -match 'usageLimitExceeded|You\\u0027ve hit your usage limit|You''ve hit your usage limit' }).Count
            $requestCompleteCount = @($traceDelta | Where-Object { $_ -match '"stage":"request\.complete"' }).Count
            $requestTimes = @($requestBodies | ForEach-Object { $_.elapsedMs })
            $okResponses = @($requestBodies | Where-Object { $_.statusCode -ge 200 -and $_.statusCode -lt 300 }).Count
            $failedResponses = @($requestBodies | Where-Object { $_.statusCode -lt 200 -or $_.statusCode -ge 300 }).Count

            $results.Add([pscustomobject]@{
                scenario = $scenarioName
                variant = $variantName
                request = $requestName
                repetitions = $Repetitions
                minMs = ($requestTimes | Measure-Object -Minimum).Minimum
                avgMs = [math]::Round(($requestTimes | Measure-Object -Average).Average, 0)
                maxMs = ($requestTimes | Measure-Object -Maximum).Maximum
                warmSuccess = $warmSuccessCount
                warmFallback = $warmFallbackCount
                warmFailedNoText = $warmFailureCount
                warmSnapshots = $warmSnapshotCount
                coldSuccess = $coldCount
                httpOk = $okResponses
                httpFailed = $failedResponses
                usageLimitSignals = $usageLimitCount
                completed = $requestCompleteCount
                trace = $traceOutputPath
                requestsFile = $requestOutputPath
            }) | Out-Null
        }
        finally {
            Stop-BenchmarkServer -Job $job -OutputPath $stdoutPath
            $port++
        }
    }
}

$results |
    Sort-Object request, avgMs |
    Tee-Object -Variable sortedResults |
    Format-Table scenario, avgMs, minMs, maxMs, warmSuccess, warmFallback, warmFailedNoText, coldSuccess, httpOk, usageLimitSignals -AutoSize

$summaryPath = Join-Path $outputDir "summary.json"
$sortedResults | ConvertTo-Json -Depth 10 | Out-File -FilePath $summaryPath -Encoding utf8
Write-Host ""
Write-Host "Saved benchmark output to $outputDir"
