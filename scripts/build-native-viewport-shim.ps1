$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $repoRoot 'src\NativeViewportShim\GpgViewportShim.cpp'
$outputDir = Join-Path $repoRoot 'artifacts\app\Release'
$outputPath = Join-Path $outputDir 'GpgViewportShim.dll'

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) {
    throw "vswhere.exe was not found. Install Visual Studio Build Tools with the C++ workload."
}

$installPath = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if ([string]::IsNullOrWhiteSpace($installPath)) {
    throw "Visual Studio C++ build tools were not found."
}

$vcvars = Join-Path $installPath 'VC\Auxiliary\Build\vcvars64.bat'
if (-not (Test-Path $vcvars)) {
    throw "vcvars64.bat was not found at $vcvars."
}

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$command = "`"$vcvars`" && cl /nologo /LD /O2 /EHsc /W4 /DUNICODE /D_UNICODE `"$sourcePath`" /Fe:`"$outputPath`" /link user32.lib kernel32.lib /MACHINE:X64"
cmd /c $command
if ($LASTEXITCODE -ne 0) {
    throw "Native viewport shim build failed with exit code $LASTEXITCODE."
}

Write-Host "Built $outputPath"
