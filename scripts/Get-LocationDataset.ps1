<#
.SYNOPSIS
Downloads or reads the pinned administrative dataset and prepares an import JSON. Does not change any database.
#>
[CmdletBinding()]
param(
    [string]$PythonPath = 'python',
    [string]$SourceDirectory,
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$pythonCommand = Get-Command $PythonPath -ErrorAction Stop
$arguments = @((Join-Path $PSScriptRoot 'get_location_dataset.py'))
if ($SourceDirectory) { $arguments += @('--source-directory', $SourceDirectory) }
if ($OutputDirectory) { $arguments += @('--output-directory', $OutputDirectory) }
& $pythonCommand.Source @arguments
if ($LASTEXITCODE -ne 0) { throw 'Location dataset preparation failed. No database changes were made.' }
