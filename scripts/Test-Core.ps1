param(
    [string]$DotnetPath = 'dotnet',
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
$dotnetExecutable = (Get-Command $DotnetPath -ErrorAction Stop).Source
$previousRoot = $env:DOTNET_ROOT
$previousRootX64 = $env:DOTNET_ROOT_X64
$checkExitCode = 0
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    $env:DOTNET_ROOT = Split-Path $dotnetExecutable -Parent
    $env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
    $buildArguments = @('build', 'TransConnect.sln', '--verbosity', 'minimal')
    if ($NoRestore) { $buildArguments += '--no-restore' }
    & $dotnetExecutable @buildArguments
    $checkExitCode = $LASTEXITCODE
    if ($checkExitCode -eq 0) {
        & $dotnetExecutable test TransConnect.Tests/TransConnect.Tests.csproj --no-build --no-restore --verbosity minimal
        $checkExitCode = $LASTEXITCODE
    }
}
finally {
    $env:DOTNET_ROOT = $previousRoot
    $env:DOTNET_ROOT_X64 = $previousRootX64
    Pop-Location
}
exit $checkExitCode
