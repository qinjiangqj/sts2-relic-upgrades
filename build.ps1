param(
    [string]$GameDir,
    [string]$BaseLibDir,
    [string]$Dotnet = 'dotnet'
)

$ErrorActionPreference = 'Stop'

if (-not $GameDir -or -not $BaseLibDir) {
    $steamUser = Get-ItemProperty -LiteralPath 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue
    $steamMachine = Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam' -ErrorAction SilentlyContinue
    $steam = @($steamUser.SteamPath, $steamMachine.InstallPath) | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
    if (-not $steam) { throw 'Steam was not found. Supply -GameDir and -BaseLibDir.' }

    $libraries = @($steam)
    $libraryList = Join-Path $steam 'steamapps\libraryfolders.vdf'
    if (Test-Path -LiteralPath $libraryList) {
        $libraries += @(Select-String -LiteralPath $libraryList -Pattern '^\s*"path"\s+"([^"]+)"' |
            ForEach-Object { $_.Matches[0].Groups[1].Value.Replace('\\', '\') })
    }
    if (-not $GameDir) {
        $GameDir = $libraries | ForEach-Object { Join-Path $_ 'steamapps\common\Slay the Spire 2' } |
            Where-Object { Test-Path -LiteralPath (Join-Path $_ 'data_sts2_windows_x86_64\sts2.dll') } |
            Select-Object -First 1
    }
    if (-not $BaseLibDir) {
        $BaseLibDir = $libraries | ForEach-Object { Join-Path $_ 'steamapps\workshop\content\2868840\3737335127\BaseLib' } |
            Where-Object { Test-Path -LiteralPath (Join-Path $_ 'BaseLib.dll') } |
            Select-Object -First 1
    }
}

if (-not $GameDir -or -not $BaseLibDir) { throw 'Game or BaseLib files were not found. Supply -GameDir and -BaseLibDir.' }
$project = Join-Path $PSScriptRoot 'src\RelicForge.csproj'
$nuget = Join-Path $PSScriptRoot 'NuGet.Config'
& $Dotnet build $project -c Release --nologo "-p:Sts2GameDir=$GameDir" "-p:Sts2BaseLibDir=$BaseLibDir" "-p:RestoreConfigFile=$nuget"
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }

$package = Join-Path $PSScriptRoot 'Mod\RelicForge'
New-Item -Path $package -ItemType Directory -Force | Out-Null
$artPack = Get-Item -LiteralPath (Join-Path $package 'RelicForge.pck') -ErrorAction SilentlyContinue
$artInputs = Get-Item -LiteralPath (Join-Path $PSScriptRoot 'tools\pack-art.gd'), (Join-Path $PSScriptRoot 'art\ui\forge_fragments.png')
if (-not $artPack -or ($artInputs.LastWriteTimeUtc | Where-Object { $_ -gt $artPack.LastWriteTimeUtc })) {
    & (Join-Path $PSScriptRoot 'tools\Pack-Art.ps1') -GameDir $GameDir
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src\bin\Release\net9.0\RelicForge.dll'), (Join-Path $PSScriptRoot 'src\RelicForge.json') -Destination $package -Force
Get-ChildItem -LiteralPath $package -File | Select-Object Name,Length
