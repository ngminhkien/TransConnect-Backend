param(
    [string]$DotnetPath,
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
$previousPath = $env:PATH
$previousRoot = $env:DOTNET_ROOT
$previousRootX64 = $env:DOTNET_ROOT_X64
$runExitCode = 1
Push-Location $PSScriptRoot
try {
    # Resolve from the repository so global.json controls SDK compatibility.
    $candidates = if ($DotnetPath) {
        @($DotnetPath)
    } else {
        @('dotnet', (Join-Path $env:USERPROFILE '.dotnet\dotnet.exe'))
    }
    $dotnetExecutable = $null
    foreach ($candidate in $candidates) {
        $command = Get-Command $candidate -CommandType Application -ErrorAction SilentlyContinue
        if (-not $command) { continue }
        # Windows PowerShell treats native stderr as errors under Stop.
        $ErrorActionPreference = 'Continue'
        try { $sdkVersion = & $command.Source --version 2>$null }
        finally { $ErrorActionPreference = 'Stop' }
        if ($LASTEXITCODE -eq 0) {
            $dotnetExecutable = $command.Source
            break
        }
    }
    if (-not $dotnetExecutable) {
        throw 'No compatible SDK found. Install .NET SDK 10 or pass -DotnetPath <path-to-dotnet.exe>.'
    }

    $env:DOTNET_ROOT = Split-Path $dotnetExecutable -Parent
    $env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
    $env:PATH = "$env:DOTNET_ROOT;$previousPath"
    Write-Host "Using .NET SDK $sdkVersion ($dotnetExecutable)"
    $runArguments = @('run', '--project', 'TransConnect.Host/TransConnect.Host.csproj', '--launch-profile', 'http')
    if ($NoRestore) { $runArguments += '--no-restore' }
    & $dotnetExecutable @runArguments
    $runExitCode = $LASTEXITCODE
}
finally {
    $env:PATH = $previousPath
    $env:DOTNET_ROOT = $previousRoot
    $env:DOTNET_ROOT_X64 = $previousRootX64
    Pop-Location
}
exit $runExitCode
