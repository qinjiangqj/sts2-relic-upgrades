param(
    [Parameter(Mandatory)] [string]$GameDir
)

$ErrorActionPreference = 'Stop'
$game = (Resolve-Path -LiteralPath (Join-Path $GameDir 'SlayTheSpire2.exe')).Path
$project = Split-Path -Parent $PSScriptRoot
$art = (Resolve-Path -LiteralPath (Join-Path $project 'art')).Path
$scratch = Join-Path $project 'tools\obj\art-pack'
$package = Join-Path $project 'Mod\RelicForge'
New-Item -ItemType Directory -Path $scratch, $package -Force | Out-Null

$catalogue = Get-Content -LiteralPath (Join-Path $project 'src\ForgeCatalog.Generated.cs') -Raw -Encoding UTF8
$names = @([regex]::Matches($catalogue, '"([A-Z0-9_]+)" => ModelDb\.Relic') | ForEach-Object {
    $_.Groups[1].Value.ToLowerInvariant() + '.png'
})
if ($names.Count -ne 171) { throw 'Expected 171 relics in the forge catalogue.' }
foreach ($variant in 'small', 'detail') {
    $actual = @(Get-ChildItem -LiteralPath (Join-Path $art $variant) -Filter '*.png' -File | Select-Object -ExpandProperty Name)
    if ($actual.Count -ne $names.Count -or (Compare-Object -ReferenceObject $names -DifferenceObject $actual)) {
        throw "The $variant image filenames do not match the recipe catalogue."
    }
}

$script = Join-Path $PSScriptRoot 'pack-art.gd'
$engine = Join-Path $scratch 'RelicPackEngine.exe'
Copy-Item -LiteralPath $game -Destination $engine -Force
$res = Join-Path $scratch 'res'
$archive = Join-Path $scratch 'RelicForge.pck'
$stdout = Join-Path $scratch 'godot.log'
$stderr = Join-Path $scratch 'godot.err'
$argsLine = '--headless --path "{0}" --script "{1}" -- "{2}" "{3}" "{4}" {5}' -f `
    $PSScriptRoot, $script, $art, $res, $archive, $names.Count

$process = Start-Process -FilePath $engine -WorkingDirectory $PSScriptRoot -ArgumentList $argsLine `
    -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
if (-not $process.WaitForExit(90000)) {
    Stop-Process -Id $process.Id
    throw 'The art pack builder timed out. See tools/obj/art-pack/godot.log.'
}
$success = Select-String -LiteralPath $stdout -SimpleMatch "[RelicForgeArt] Packed $($names.Count) relics"
if ($process.ExitCode -ne 0 -or -not $success -or -not (Test-Path -LiteralPath $archive -PathType Leaf)) {
    Get-Content -LiteralPath $stdout, $stderr -Encoding UTF8
    throw 'The art pack builder failed.'
}
Copy-Item -LiteralPath $archive -Destination (Join-Path $package 'RelicForge.pck') -Force
Write-Output $success.Line
