<#
.SYNOPSIS
    Builds the Release DLL and the Thunderstore package (dist\PierreMarais-PeglinRelicRerolls-<version>.zip).

.DESCRIPTION
    1. Checks the version in src\Plugin.cs matches thunderstore\manifest.json.
    2. Runs the unit tests.
    3. Builds Release (this also copies the DLL into the game's BepInEx\plugins folder).
    4. Checks the DLL contains no dev tools (they only exist in Debug builds).
    5. Zips manifest.json, icon.png, README.md, CHANGELOG.md and the DLL, at the top level of the zip.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File pack.ps1
    powershell -ExecutionPolicy Bypass -File pack.ps1 -GamePath "D:\Games\Peglin"
#>
param(
    # Peglin install folder, if it isn't the default Steam location.
    [string]$GamePath
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$team = 'PierreMarais'

$manifest = Get-Content (Join-Path $root 'thunderstore\manifest.json') -Raw | ConvertFrom-Json
$version = $manifest.version_number

# 1. Versions must agree, or Thunderstore and the in-game log would disagree about what's installed.
$pluginVersion = (Select-String -Path (Join-Path $root 'src\Plugin.cs') -Pattern 'const string Version = "([^"]+)"').Matches[0].Groups[1].Value
if ($pluginVersion -ne $version) {
    throw "Version mismatch: src\Plugin.cs says $pluginVersion but thunderstore\manifest.json says $version."
}
Write-Host "Packing $($manifest.name) $version" -ForegroundColor Cyan

# 2. Tests.
dotnet test (Join-Path $root 'tests\PeglinRelicRerolls.Tests.csproj') -nologo --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'Tests failed; not packing.' }

# 3. Release build.
$buildArgs = @((Join-Path $root 'PeglinRelicRerolls.csproj'), '-c', 'Release', '-nologo', '--verbosity', 'quiet')
if ($GamePath) { $buildArgs += "-p:GamePath=$GamePath" }
dotnet build @buildArgs
if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
$dll = Join-Path $root 'bin\Release\net472\PeglinRelicRerolls.dll'

# 4. Dev tools must never ship to players.
if ([Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($dll)).Contains('DevTools')) {
    throw 'The Release DLL contains dev tools; check the #if DEBUG blocks.'
}

# 5. Stage and zip. Files must sit at the top level of the zip, not inside a folder.
$dist = Join-Path $root 'dist'
$stage = Join-Path $dist 'package'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage | Out-Null
Copy-Item (Join-Path $root 'thunderstore\manifest.json'), (Join-Path $root 'icon.png'), (Join-Path $root 'README.md'), (Join-Path $root 'CHANGELOG.md'), $dll -Destination $stage

$zip = Join-Path $dist "$team-$($manifest.name)-$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
Write-Host "Package ready: $zip" -ForegroundColor Green
