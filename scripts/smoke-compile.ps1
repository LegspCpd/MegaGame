# Local compile smoke test: csc with full references (net8 ref pack + Unity DLLs
# + vendored grpc + locally generated protos). Prints high-signal errors only.
# Usage: pwsh -File smoke-compile.ps1  -> %TEMP%\smoke-errors.txt
$ErrorActionPreference = 'SilentlyContinue'
$repo = 'G:\megame\client\Assets\Scripts'
$protoSrc = Join-Path $env:TEMP 'clientcheck\Assets\Scripts\Generated\Proto'
$csc = 'C:\Program Files\dotnet\sdk\8.0.425\Roslyn\bincore\csc.dll'

# 1) sources: repo Scripts + locally generated protos (not in repo)
$src = @()
$src += Get-ChildItem $repo -Recurse -Filter *.cs | ForEach-Object { $_.FullName }
if (Test-Path $protoSrc) { $src += Get-ChildItem $protoSrc -Filter *.cs | ForEach-Object { $_.FullName } }

# The Input System package ships as source, not a DLL. Without its sources the
# keyboard/mouse code in the playable layer is never type-checked. They are
# vendored under tools/thirdparty rather than %TEMP%: the temp directory gets
# cleaned, which silently removed them once, after which every Input System
# file "passed" while actually being unresolvable -- and Roslyn suppresses
# method-body errors behind declaration errors, so the type errors underneath
# never showed up either.
$isCandidates = @(
    (Join-Path $PSScriptRoot '..\tools\thirdparty\inputsystem\package\InputSystem'),
    (Join-Path $PSScriptRoot '..\tools\thirdparty\inputsystem\InputSystem')
)
$isSrc = $isCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if ($isSrc) {
    $src += Get-ChildItem $isSrc -Recurse -Filter *.cs |
        Where-Object { $_.FullName -notmatch '\\(Editor|Tests)\\' } |
        ForEach-Object { $_.FullName }
} else {
    Write-Warning 'Input System sources missing under tools/thirdparty: input code will not be type-checked.'
}

# 2) references: net8 ref pack + Unity 6000 Managed (broken install, DLLs intact) + vendored grpc
$refs = @()
$refDir = 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.31\ref\net8.0'
$refs += Get-ChildItem $refDir -Filter *.dll | ForEach-Object { $_.FullName }
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Data\Managed'
$refs += (Join-Path $unity 'UnityEngine.dll')
$refs += (Join-Path $unity 'UnityEditor.dll')
$refs += Get-ChildItem (Join-Path $unity 'UnityEngine') -Filter *.dll | ForEach-Object { $_.FullName }
# Exclude Unity's Google.Protobuf (older version conflicts with vendored 3.25.3)
$refs = $refs | Where-Object { $_ -notmatch 'Google\.Protobuf' }
$refs += Get-ChildItem 'G:\megame\client\Assets\Plugins\Grpc' -Filter *.dll | ForEach-Object { $_.FullName }

# 3) response file to bypass command-line length limits
$rspPath = Join-Path $env:TEMP 'smoke.rsp'
$rspLines = @('-target:library', '-nostdlib+', '-noconfig', '-unsafe', '-nowarn:0169,0649,0414,0162,0219,1701,1702')
$rspLines += ($refs | ForEach-Object { '-r:"' + $_ + '"' })
$rspLines += ($src | ForEach-Object { '"' + $_ + '"' })
if (-not $rspPath) { throw 'rspPath is null' }
$rspLines | Set-Content -LiteralPath $rspPath -Encoding UTF8

# Guard against reporting on a stale build. csc reads the sources named in the
# response file at the moment it runs, but a source edited *after* the previous
# run will not be re-checked unless this script is run again -- which has twice
# let a real error reach CI while the local report said "0 actionable".
$newest = ($src | ForEach-Object { Get-Item -LiteralPath $_ } | Measure-Object LastWriteTime -Maximum).Maximum
$rspTime = (Get-Item -LiteralPath $rspPath).LastWriteTime
if ($rspTime -lt $newest) {
    Write-Warning "response file is older than the newest source ($($newest)); inputs were refreshed anyway"
}

$out = & dotnet $csc "@$rspPath" 2>&1 | Out-String

# 4) Separate our diagnostics from third-party noise.
#
# Do NOT filter on a path prefix. csc sometimes prints only the bare file name
# (e.g. "MeshBuilder.cs(18,6): error CS0710") with no directory at all, so a
# prefix match silently classified our own errors as third-party noise -- which
# is exactly how a static-class-with-instance-members slipped through.
#
# Exclusion is by exact third-party roots instead: the vendored Input System
# sources and the generated protobuf. Everything else, including anything under
# client\, is ours and is reported.
$thirdPartyRoots = @(
    [regex]::Escape($PSScriptRoot + '\..\tools\thirdparty'),
    [regex]::Escape($PSScriptRoot + '\..\tools\thirdparty'),
    'tools[\\/]thirdparty',
    'clientcheck'
) | ForEach-Object { $_.Trim() }

$repoErrors = $out -split "`r?`n" | Where-Object {
    if ($_ -notmatch 'error CS\d+') { return $false }
    foreach ($root in $thirdPartyRoots) {
        if ($_ -match $root) { return $false }
    }
    return $true
}

$report = @()
# CS0246/CS0234 here mean "package assembly not referenced by this smoke build"
# (TMPro, UnityEngine.UI, Unity.Collections, Unity.Mathematics). CI resolves the
# real packages, so those are expected and must not be confused with real bugs.
$knownMissing = $repoErrors | Where-Object { $_ -match 'error CS0246|error CS0234' }
$actionable = $repoErrors | Where-Object { $_ -notmatch 'error CS0246|error CS0234' }

$report += "=== actionable repo errors: $($actionable.Count) ==="
$report += ($actionable | Sort-Object -Unique)
$report += ''
$report += "=== expected package-reference noise: $($knownMissing.Count) (CS0246/CS0234) ==="
$report += '=== histogram (actionable only) ==='
$actionable | ForEach-Object { if ($_ -match 'error (CS\d+)') { $Matches[1] } } |
    Group-Object | Sort-Object Count -Descending |
    ForEach-Object { "$($_.Name) x$($_.Count)" }
$report += ''
$report += '(third-party package diagnostics suppressed)'

# ------------------------------------------------------------------------
# Strict pass over the playable layer.
#
# The full compile above always contains declaration-phase errors (CS0246 for
# TMPro, UnityEngine.UI, Megame.Network and friends, from the legacy files in
# Scripts/Controllers, Scripts/Systems and Scripts/UI). Roslyn does not bind
# method bodies in other files once a declaration error exists, so every
# body-level error in our own code is dropped: CS0120 in DayNightCycle and
# CS1503 in PlayableNpc both reached CI this way while this script reported
# zero actionable errors.
#
# So the playable layer is compiled again on its own, against hand-written
# stubs for the two dependencies it cannot resolve here (the Input System
# package, which cannot compile outside Unity, and the networked GameClient,
# which needs the generated protobuf). With no declaration errors, method
# bodies actually get bound and real mistakes surface.
#
# Any error here is ours and must be fixed.
# ------------------------------------------------------------------------
$playable = @(
    'CinematicPlayer', 'CinematicStarter', 'DayNightCycle', 'DialogueSystem',
    'InteriorProps', 'MeshBuilder', 'PauseMenu', 'PlayableCameraRig',
    'PlayableHUD', 'PlayableNpc', 'PlayablePlayer', 'PlayablePlayerInteraction',
    'PlayableRadar', 'PlayableVehicle', 'PlayableWeapon', 'PlayableWorld',
    'PlayerDamage', 'PlayerVitals', 'QuestSystem', 'SaveSystem', 'Staircase',
    'StreetProps', 'SwingingDoor', 'WeaponInventory', 'WindowCut'
)

$strictSrc = @()
foreach ($name in $playable) {
    $p = Join-Path $repo "Core\$name.cs"
    if (-not (Test-Path $p)) { throw "playable layer file missing: $p" }
    $strictSrc += $p
}
$stub = Join-Path $PSScriptRoot '..\tools\check\InputSystemStubs.cs'
if (-not (Test-Path $stub)) { throw "stub file missing: $stub" }
$strictSrc += $stub

$strictRsp = Join-Path $env:TEMP 'smoke-strict.rsp'
$strictLines = @('-target:library', '-nostdlib+', '-noconfig', '-unsafe',
    '-nowarn:0169,0649,0414,0162,0219,1701,1702')
$strictLines += ($refs | ForEach-Object { '-r:"' + $_ + '"' })
$strictLines += ($strictSrc | ForEach-Object { '"' + $_ + '"' })
$strictLines | Set-Content -LiteralPath $strictRsp -Encoding UTF8

$strictOut = & dotnet $csc "@$strictRsp" 2>&1 | Out-String
$strictErrors = $strictOut -split "`r?`n" |
    Where-Object { $_ -match 'error CS\d+' } |
    Sort-Object -Unique

$report += ''
$report += '=== STRICT pass (playable layer, no stubs tolerated): ' +
           "$($strictErrors.Count) error(s) ==="
$report += $strictErrors
if ($strictErrors.Count -gt 0) {
    $report += 'These are real errors in our own code. Fix them before committing.'
}

$outPath = Join-Path $env:TEMP 'smoke-errors.txt'
$report | Set-Content -LiteralPath $outPath -Encoding UTF8
"output: $outPath  (strict $($strictErrors.Count) / actionable $($actionable.Count) / package noise $($knownMissing.Count))"
if ($strictErrors.Count -gt 0) { exit 1 }
