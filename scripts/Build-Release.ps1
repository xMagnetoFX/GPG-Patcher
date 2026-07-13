param(
    [string]$Configuration = "Release",
    [string]$OutputRoot = ""
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot "artifacts\release"
}

$propsPath = Join-Path $repoRoot "Directory.Build.props"
[xml]$props = Get-Content $propsPath
$version = @(
    $props.Project.PropertyGroup |
    ForEach-Object { $_.VersionPrefix } |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Select-Object -First 1
)

if ([string]::IsNullOrWhiteSpace($version)) {
    throw "VersionPrefix was not found in $propsPath."
}

$solutionPath = Join-Path $repoRoot "GPG-Patcher.slnx"
$guiOutputDir = Join-Path $repoRoot "artifacts\app\$Configuration"
$packageName = "GPG-Patcher-v$version-win-x64"
$packageDir = Join-Path $OutputRoot $packageName
$zipPath = Join-Path $OutputRoot ($packageName + ".zip")
$hashPath = Join-Path $OutputRoot "SHA256SUMS.txt"

function Get-Sha256HashValue {
    param(
        [Parameter(Mandatory = $true)]
        [string]$LiteralPath
    )

    $getFileHashCommand = Get-Command Get-FileHash -ErrorAction SilentlyContinue
    if ($null -ne $getFileHashCommand) {
        return (Get-FileHash -LiteralPath $LiteralPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }

    $stream = [System.IO.File]::OpenRead($LiteralPath)
    try {
        $sha256 = [System.Security.Cryptography.SHA256]::Create()
        try {
            $hashBytes = $sha256.ComputeHash($stream)
        }
        finally {
            $sha256.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }

    return ([System.BitConverter]::ToString($hashBytes).Replace("-", "").ToLowerInvariant())
}

Write-Host "Building solution..."
dotnet build $solutionPath -c $Configuration

if (Test-Path $packageDir) {
    Remove-Item -LiteralPath $packageDir -Recurse -Force
}

if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Path $packageDir -Force | Out-Null

$bundleFiles = @(
    "GPG Patcher.exe",
    "GPG Patcher.exe.config",
    "GpgPatcher.Core.dll",
    "GpgPatcher.Hooks.dll",
    "dnlib.dll"
)

foreach ($file in $bundleFiles) {
    $source = Join-Path $guiOutputDir $file
    if (-not (Test-Path $source)) {
        throw "Required release artifact not found: $source"
    }

    Copy-Item -LiteralPath $source -Destination (Join-Path $packageDir $file)
}

$docFiles = @(
    "README.md",
    "THIRD_PARTY_NOTICES.md"
)

foreach ($file in $docFiles) {
    Copy-Item -LiteralPath (Join-Path $repoRoot $file) -Destination (Join-Path $packageDir $file)
}

$projectLicense = Join-Path $repoRoot "LICENSE"
if (Test-Path $projectLicense) {
    Copy-Item -LiteralPath $projectLicense -Destination (Join-Path $packageDir "LICENSE")
}

Compress-Archive -Path (Join-Path $packageDir "*") -DestinationPath $zipPath -Force

$hashEntries = @()
$zipHash = Get-Sha256HashValue -LiteralPath $zipPath
$hashEntries += ("{0}  {1}" -f $zipHash, (Split-Path -Leaf $zipPath))

Set-Content -LiteralPath $hashPath -Value $hashEntries

Write-Host ""
Write-Host "Release bundle created:"
Write-Host "  Folder: $packageDir"
Write-Host "  Zip:    $zipPath"
Write-Host "  Hashes: $hashPath"
