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
# keyboard/mouse code in the playable layer is never type-checked, so pull the
# package in when it is present locally.
$isSrc = Join-Path $env:TEMP 'ispkg\package\InputSystem'
if (Test-Path $isSrc) {
    $src += Get-ChildItem $isSrc -Recurse -Filter *.cs |
        Where-Object { $_.FullName -notmatch '\\(Editor|Tests)\\' } |
        ForEach-Object { $_.FullName }
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

$out = & dotnet $csc "@$rspPath" 2>&1 | Out-String

# 4) Only diagnostics in THIS repository are actionable. Errors inside the
#    vendored package sources (InputSystem etc.) are noise that used to bury
#    real errors, so they are counted separately and never reported as ours.
#    Match on the 'client\Assets\' prefix: the compiler prints absolute paths for
#    package sources and repo-relative paths for our own files.
$repoErrors = $out -split "`r?`n" | Where-Object {
    $_ -match 'error CS\d+' -and $_ -match '[\\/]client[\\/]Assets[\\/]'
}

$report = @()
$report += "=== repo errors: $($repoErrors.Count) ==="
$report += ($repoErrors | Sort-Object -Unique)
$report += ''
$report += '=== histogram (repo only) ==='
$repoErrors | ForEach-Object { if ($_ -match 'error (CS\d+)') { $Matches[1] } } |
    Group-Object | Sort-Object Count -Descending |
    ForEach-Object { "$($_.Name) x$($_.Count)" }
$report += ''
$report += "(third-party package diagnostics suppressed)"
$outPath = Join-Path $env:TEMP 'smoke-errors.txt'
$report | Set-Content -LiteralPath $outPath -Encoding UTF8
"output: $outPath  (repo errors $($repoErrors.Count))"
