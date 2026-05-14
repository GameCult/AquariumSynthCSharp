param(
    [string]$MsysRoot = "C:\msys64",
    [string]$BuildDir = "artifacts/zyn-reference-build-msys",
    [string]$SourceDir = "artifacts/zyn-source-msys"
)

$ErrorActionPreference = "Stop"
if (Get-Variable PSNativeCommandUseErrorActionPreference -ErrorAction SilentlyContinue) {
    $PSNativeCommandUseErrorActionPreference = $false
}

function Convert-ToMsysPath([string]$Path) {
    $resolved = [System.IO.Path]::GetFullPath($Path)
    if ($resolved -notmatch "^[A-Za-z]:\\") {
        throw "Cannot convert path to MSYS form: $resolved"
    }

    $drive = $resolved.Substring(0, 1).ToLowerInvariant()
    $tail = $resolved.Substring(2).Replace("\", "/")
    return "/$drive$tail"
}

function Invoke-GitProbe([string[]]$Arguments) {
    $oldPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        & git @Arguments 1>$null 2>$null
        return $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $oldPreference
    }
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$zynRoot = Join-Path $repoRoot "external\zynaddsubfx"
$workSource = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $SourceDir))
$patchPath = Join-Path $PSScriptRoot "patches\0001-msys-part-constructor.patch"
$bash = Join-Path $MsysRoot "usr\bin\bash.exe"

if (-not (Test-Path $bash)) {
    throw "MSYS2 bash not found at $bash"
}

if (-not (Test-Path (Join-Path $zynRoot "src\Params\PADnoteParameters.cpp"))) {
    throw "ZynAddSubFX submodule is not initialized at $zynRoot"
}

& git -C $zynRoot submodule update --init rtosc
if ($LASTEXITCODE -ne 0) {
    throw "Failed to initialize Zyn rtosc submodule."
}

$sourceParent = Split-Path $workSource -Parent
if (-not (Test-Path $sourceParent)) {
    New-Item -ItemType Directory -Path $sourceParent | Out-Null
}

& robocopy $zynRoot $workSource /MIR /XD .git /XF .git /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -gt 7) {
    throw "Failed to copy Zyn source to $workSource"
}

$applyCheck = Invoke-GitProbe @("-C", $workSource, "apply", "--check", $patchPath)
if ($applyCheck -eq 0) {
    & git -C $workSource apply $patchPath
    if ($LASTEXITCODE -ne 0) { throw "Failed to apply Zyn build patch." }
    Write-Host "Applied Zyn build patch to artifact source."
}
else {
    $reverseCheck = Invoke-GitProbe @("-C", $workSource, "apply", "--reverse", "--check", $patchPath)
    if ($reverseCheck -eq 0) {
        Write-Host "Zyn artifact source already has build patch."
    }
    else {
        throw "Zyn build patch cannot be applied cleanly to $workSource and is not already applied."
    }
}

$repoUnix = Convert-ToMsysPath $repoRoot
$sourceUnix = Convert-ToMsysPath $workSource
$buildUnix = Convert-ToMsysPath (Join-Path $repoRoot $BuildDir)
$script = @"
set -euo pipefail
export PATH=/mingw64/bin:/usr/bin:/bin:`$PATH
cd "$repoUnix"
cmake -S "$sourceUnix" -B "$buildUnix" -G Ninja \
  -DGuiModule=off \
  -DPluginEnable=OFF \
  -DCompileTests=ON \
  -DCompileExtensiveTests=OFF \
  -DCMAKE_BUILD_TYPE=Release \
  -DWerror=OFF
cmake --build "$buildUnix" --target PadNoteTest -j 4
ctest --test-dir "$buildUnix" -R PadNoteTest --output-on-failure
"@

& $bash -lc $script
if ($LASTEXITCODE -ne 0) {
    throw "Zyn reference build failed."
}
