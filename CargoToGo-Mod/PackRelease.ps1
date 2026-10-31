<#
.SYNOPSIS
    Builds CargoToGo and packs the Thunderstore release zip.

.DESCRIPTION
    1. Validates the release files and that all version numbers agree
       (manifest.json, CargoToGo.csproj, Plugin.cs).
    2. Builds the project with MSBuild (Release by default).
    3. Zips manifest.json, README.md, icon.png (and CHANGELOG.md if present)
       plus CargoToGo.dll into  release\CargoToGo-<version>.zip

    Place this script in the project folder, next to CargoToGo.csproj.

.PARAMETER Configuration
    Build configuration. Default: Release.

.PARAMETER OutputDir
    Where the zip is written. Default: .\release

.PARAMETER NoBuild
    Skip the build and pack the DLL that is already in bin\<Configuration>.

.EXAMPLE
    .\PackRelease.ps1

.EXAMPLE
    .\PackRelease.ps1 -NoBuild
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$OutputDir,
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root         = $PSScriptRoot
$assemblyName = 'CargoToGo'
$projectFile  = Join-Path $root "$assemblyName.csproj"
$pluginFile   = Join-Path $root 'src\Plugin.cs'
$manifestFile = Join-Path $root 'manifest.json'
$readmeFile   = Join-Path $root 'README.md'
$iconFile     = Join-Path $root 'icon.png'
$changelog    = Join-Path $root 'CHANGELOG.md'   # optional
if (-not $OutputDir) { $OutputDir = Join-Path $root 'release' }

function Write-Step([string]$text) { Write-Host "`n==> $text" -ForegroundColor Cyan }

# ---------------------------------------------------------------- 1. validate inputs
Write-Step 'Checking release files'

foreach ($f in @($projectFile, $pluginFile, $manifestFile, $readmeFile, $iconFile)) {
    if (-not (Test-Path $f)) { throw "Required file not found: $f" }
}

# manifest.json
$manifest = Get-Content $manifestFile -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.name -notmatch '^[A-Za-z0-9_]+$') {
    throw "manifest.json: 'name' may only contain letters, digits and underscores (got '$($manifest.name)')."
}
if ($manifest.version_number -notmatch '^\d+\.\d+\.\d+$') {
    throw "manifest.json: 'version_number' must be Major.Minor.Patch (got '$($manifest.version_number)')."
}
if ($manifest.description.Length -gt 250) {
    throw "manifest.json: 'description' is $($manifest.description.Length) characters; Thunderstore allows at most 250."
}
$version = $manifest.version_number

# version must agree everywhere

$pluginMatch = [regex]::Match((Get-Content $pluginFile -Raw), 'ModVersion\s*=\s*"([^"]+)"')
$pluginVersion = if ($pluginMatch.Success) { $pluginMatch.Groups[1].Value } else { $null }

Write-Host ("  manifest.json : {0}" -f $version)
Write-Host ("  Plugin.cs     : {0}" -f $pluginVersion)

if ($pluginVersion -ne $version) {
    throw "Version mismatch. manifest.json and src\Plugin.cs must use the same version."
}

# Thunderstore requires a 256x256 icon
Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Image]::FromFile($iconFile)
try {
    if ($img.Width -ne 256 -or $img.Height -ne 256) {
        throw "icon.png must be 256x256 pixels (is $($img.Width)x$($img.Height))."
    }
}
finally { $img.Dispose() }

# ---------------------------------------------------------------- 2. build
$dllPath = Join-Path $root "bin\$Configuration\net48\$assemblyName.dll"

if (-not $NoBuild) {
    Write-Step 'Locating MSBuild'

    # Locate MSBuild even when this script is launched from a normal PowerShell window.
    $msbuild = $null
    $msbuildCommand = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if ($msbuildCommand) {
        $msbuild = $msbuildCommand.Source
    }

    if (-not $msbuild) {
        $vswhereCandidates = @(
            (Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"),
            (Join-Path $env:ProgramFiles "Microsoft Visual Studio\Installer\vswhere.exe")
        ) | Where-Object { $_ -and (Test-Path $_) }

        foreach ($vswhere in $vswhereCandidates) {
            $found = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" 2>$null | Select-Object -First 1
            if ($found -and (Test-Path $found)) {
                $msbuild = $found
                break
            }
        }
    }

    if (-not $msbuild) {
        $commonPaths = @(
            "$env:ProgramFiles\Microsoft Visual Studio\2026\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
            "$env:ProgramFiles\Microsoft Visual Studio\2026\Professional\MSBuild\Current\Bin\MSBuild.exe",
            "$env:ProgramFiles\Microsoft Visual Studio\2026\Community\MSBuild\Current\Bin\MSBuild.exe",
            "$env:ProgramFiles\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
            "$env:ProgramFiles\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
            "$env:ProgramFiles\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
            "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
            "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe",
            "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe"
        )

        $msbuild = $commonPaths | Where-Object { Test-Path $_ } | Select-Object -First 1
    }

    if (-not $msbuild) {
        throw @"
MSBuild.exe was not found.

If Visual Studio is installed, open "Developer PowerShell for Visual Studio" and run this script again.
If it is not installed, install Visual Studio or the Visual Studio Build Tools with the MSBuild component.
"@
    }
    Write-Host "  $msbuild"

    Write-Step "Building $assemblyName ($Configuration)"
    if (Test-Path $dllPath) { Remove-Item $dllPath -Force }   # never pack a stale DLL

    & $msbuild $projectFile /restore /t:Rebuild "/p:Configuration=$Configuration" /v:minimal /nologo
    if ($LASTEXITCODE -ne 0) { throw "MSBuild failed with exit code $LASTEXITCODE." }
}
else {
    Write-Step 'Skipping build (-NoBuild)'
}

if (-not (Test-Path $dllPath)) {
    # Fallback in case the target framework folder differs.
    $dllPath = Get-ChildItem (Join-Path $root "bin\$Configuration") -Recurse -Filter "$assemblyName.dll" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $dllPath -or -not (Test-Path $dllPath)) {
    throw "$assemblyName.dll was not found under bin\$Configuration. Build first or check the build output."
}

$dllVersion = (Get-Item $dllPath).VersionInfo.FileVersion
if ($dllVersion -and -not $dllVersion.StartsWith($version)) {
    Write-Warning "DLL file version is $dllVersion but the release version is $version."
}

# ---------------------------------------------------------------- 3. stage + zip
Write-Step 'Packing'

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$stage   = Join-Path $OutputDir '_staging'
$zipPath = Join-Path $OutputDir "$assemblyName-$version.zip"

if (Test-Path $stage)   { Remove-Item $stage -Recurse -Force }
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null

Copy-Item $manifestFile $stage
Copy-Item $readmeFile   $stage
Copy-Item $iconFile     $stage
Copy-Item $dllPath      $stage
if (Test-Path $changelog) { Copy-Item $changelog $stage }

Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zipPath -CompressionLevel Optimal
Remove-Item $stage -Recurse -Force

# ---------------------------------------------------------------- 4. verify
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $names = @($zip.Entries | ForEach-Object { $_.FullName })
    foreach ($required in @('manifest.json', 'README.md', 'icon.png', "$assemblyName.dll")) {
        if ($names -notcontains $required) { throw "Packed zip is missing $required." }
    }
    Write-Host "`nContents of $(Split-Path $zipPath -Leaf):"
    foreach ($e in $zip.Entries) { Write-Host ("  {0,-20} {1,10:N0} bytes" -f $e.FullName, $e.Length) }
}
finally { $zip.Dispose() }

Write-Host "`nRelease package ready:" -ForegroundColor Green
Write-Host "  $zipPath"
