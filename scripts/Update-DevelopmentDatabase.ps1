param(
    [string]$DotnetPath = 'dotnet',
    [switch]$IncludeComplianceDemo,
    [switch]$ProbeOnly
)

$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path $PSScriptRoot -Parent
$configPath = Join-Path $workspaceRoot 'TransConnect.Host/appsettings.Development.json'
if (!(Test-Path -LiteralPath $configPath)) { throw 'Missing local Development configuration.' }
$config = Get-Content -Raw -Encoding UTF8 -LiteralPath $configPath | ConvertFrom-Json
$sqlSettings = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.ConnectionStrings.Default)
$sqlcmdPath = (Get-Command sqlcmd -ErrorAction Stop).Source
$sqlArgs = @('-S', $sqlSettings.DataSource, '-d', $sqlSettings.InitialCatalog, '-b', '-l', '5')
if ($sqlSettings.TrustServerCertificate) { $sqlArgs += '-C' }
if ($sqlSettings.IntegratedSecurity) { $sqlArgs += '-E' }
else { $sqlArgs += @('-U', $sqlSettings.UserID) }
$previousPassword = $env:SQLCMDPASSWORD
$previousRoot = $env:DOTNET_ROOT
$previousRootX64 = $env:DOTNET_ROOT_X64
$previousPath = $env:PATH
$previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
Push-Location $workspaceRoot
try {
    if (!$sqlSettings.IntegratedSecurity) { $env:SQLCMDPASSWORD = $sqlSettings.Password }
    & $sqlcmdPath @sqlArgs -Q 'SET NOCOUNT ON; SELECT 1 AS ConnectionProbe;'
    if ($LASTEXITCODE -ne 0) { throw 'SQL connection probe failed. No migrations were started.' }
    if ($ProbeOnly) { return }
    $tableCountSql = "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name+'.'+t.name IN ('identity.Users','operator.Members','operator.Drivers','operator.Organizations','operator.Invitations','notification.EmailMessages');"
    $tableOutput = & $sqlcmdPath @sqlArgs -h -1 -W -Q $tableCountSql
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect schema. No migrations were started.' }
    $tableCount = [int](($tableOutput | Where-Object { $_.Trim() -match '^\d+$' } | Select-Object -First 1).Trim())
    if ($tableCount -eq 6) {
        & $sqlcmdPath @sqlArgs -i (Join-Path $PSScriptRoot 'Check-FoundationData.sql')
        if ($LASTEXITCODE -ne 0) { throw 'Preflight found conflicting data. Resolve it before migrating.' }
    }
    elseif ($tableCount -ne 0) { throw 'Partial foundation schema found. Review its migration history before continuing.' }

    $dotnetExecutable = (Get-Command $DotnetPath -ErrorAction Stop).Source
    $env:DOTNET_ROOT = Split-Path $dotnetExecutable -Parent
    $env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
    $env:PATH = $env:DOTNET_ROOT + ';' + $env:PATH
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $efExecutable = Join-Path $env:DOTNET_ROOT 'tools/dotnet-ef.exe'
    if (!(Test-Path -LiteralPath $efExecutable)) { throw 'dotnet-ef was not found under the selected SDK tools directory.' }
    & $dotnetExecutable build TransConnect.sln --no-restore --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'Build failed. No migrations were started.' }
    $modules = @('Identity', 'Notification', 'Operator')
    if ($IncludeComplianceDemo -or $config.ComplianceDemo.Enabled) { $modules += 'Compliance' }
    foreach ($module in $modules) {
        Write-Host "Applying Development migrations for $module..."
        & $efExecutable database update --project "TransConnect.Modules.$module" --startup-project TransConnect.Host --context "$($module)DbContext" --no-build
        if ($LASTEXITCODE -ne 0) { throw "Migration failed for $module. Later modules were not run; inspect migration history before retrying." }
    }
    Write-Host 'Development migrations completed. Demo feature configuration is unchanged.'
}
finally {
    $env:SQLCMDPASSWORD = $previousPassword
    $env:DOTNET_ROOT = $previousRoot
    $env:DOTNET_ROOT_X64 = $previousRootX64
    $env:PATH = $previousPath
    $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment
    Pop-Location
}
