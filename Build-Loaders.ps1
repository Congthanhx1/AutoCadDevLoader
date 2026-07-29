[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$output = Join-Path $root "Build"

function Find-AutoCad {
    param([int[]]$Years)

    foreach ($year in $Years) {
        $candidate = "C:\Program Files\Autodesk\AutoCAD $year"
        if (Test-Path -LiteralPath (Join-Path $candidate "AcCoreMgd.dll")) {
            return $candidate
        }
    }

    return $null
}

function Build-Loader {
    param(
        [string]$Project,
        [string]$AutoCadDirectory,
        [string]$Name
    )

    if (-not $AutoCadDirectory) {
        Write-Warning "$Name skipped because no compatible AutoCAD installation was found."
        return
    }

    Write-Host "Building $Name using $AutoCadDirectory"

    & dotnet build $Project `
        --configuration $Configuration `
        --output $output `
        "-p:AutoCADDir=$AutoCadDirectory"

    if ($LASTEXITCODE -ne 0) {
        throw "$Name build failed with exit code $LASTEXITCODE."
    }
}

$net48Cad = Find-AutoCad -Years @(2024, 2023, 2022, 2021)
$net8Cad = Find-AutoCad -Years @(2026, 2025)

Build-Loader `
    -Project (Join-Path $root "CadDevLoader.Net48\CadDevLoader.Net48.csproj") `
    -AutoCadDirectory $net48Cad `
    -Name "AutoCAD-2021-2024"

Build-Loader `
    -Project (Join-Path $root "CadDevLoader.Net8\CadDevLoader.Net8.csproj") `
    -AutoCadDirectory $net8Cad `
    -Name "AutoCAD-2025-2026"

Write-Host "Finished. Always NETLOAD CadDevLoader from: $output"
