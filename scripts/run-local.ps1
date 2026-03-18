param(
    [int]$PreferredPort = 5050,
    [int]$SearchStart = 5000,
    [int]$SearchEnd = 5099
)

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "src\PhantomApi\PhantomApi.csproj"
Set-Location $repoRoot

function Get-PortOwner([int]$Port)
{
    $connection = Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($connection -eq $null)
    {
        return "free"
    }

    try
    {
        $process = Get-Process -Id $connection.OwningProcess -ErrorAction SilentlyContinue
        $owner = if ($process) { $process.ProcessName } else { "unknown" }
        return "$owner (PID $($connection.OwningProcess))"
    }
    catch
    {
        return "used (PID $($connection.OwningProcess))"
    }
}

function Get-FirstFreePort([int]$StartPort, [int]$EndPort, [int]$Preferred)
{
    $candidatePorts = @($Preferred) + ( $StartPort..$EndPort | Where-Object { $_ -ne $Preferred } )
    foreach ($candidate in $candidatePorts)
    {
        if (Get-PortOwner -Port $candidate -eq "free")
        {
            return $candidate
        }
    }
    return $null
}

if ($PreferredPort -lt $SearchStart)
{
    $SearchStart = $PreferredPort
}

$selectedPort = Get-FirstFreePort -StartPort $SearchStart -EndPort $SearchEnd -Preferred $PreferredPort
if ($selectedPort -eq $null)
{
    throw "No free port found in range $SearchStart-$SearchEnd."
}

if ($selectedPort -ne $PreferredPort)
{
    Write-Host "Preferred port $PreferredPort is busy. Using fallback port $selectedPort."
}

$preferredOwner = Get-PortOwner -Port $PreferredPort
if ($PreferredPort -eq $selectedPort -and $preferredOwner -ne "free")
{
    Write-Host "Preferred port $PreferredPort is occupied by $preferredOwner."
    Write-Host "Start the script with -PreferredPort to choose another port."
    throw "Cannot use preferred port."
}

Write-Host "Starting PhantomAPI locally on http://localhost:$selectedPort ..."
Write-Host "Request sample: Invoke-RestMethod -Method Post -Uri http://localhost:$selectedPort/dynamic-api -ContentType application/json -Body (Get-Content instructions/apps/task-board/.examples/login.json -Raw)"

dotnet run --project $projectPath --urls "http://localhost:$selectedPort"
