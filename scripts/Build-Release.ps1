param(
    [string]$Version = "1.0.0",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectPath = Join-Path $repoRoot "DynamicIslandBar\DynamicIslandBar.csproj"
$testProjectPath = Join-Path $repoRoot "DynamicIslandBar.Tests\DynamicIslandBar.Tests.csproj"
$releaseRoot = Join-Path $repoRoot "artifacts\release"
$releaseName = "DynamicIslandBar-v$Version-$Runtime"
$releaseDir = Join-Path $releaseRoot $releaseName
$publishDir = Join-Path $releaseDir "publish"
$packageDir = Join-Path $releaseDir "packages"
$installerDir = Join-Path $releaseDir "installer"
$installerScript = Join-Path $repoRoot "release\installer\DynamicIslandBar.iss"
$dependencyLabel = if ($SelfContained) { "self-contained" } else { "framework-dependent" }

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Invoke-DotNet {
    param([string[]]$Arguments)
    Write-Host "dotnet $($Arguments -join ' ')" -ForegroundColor DarkGray
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet command failed with exit code $LASTEXITCODE"
    }
}

Write-Step "Preparing release directories"
if (Test-Path $releaseDir) {
    Remove-Item -LiteralPath $releaseDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $publishDir, $packageDir, $installerDir | Out-Null

$running = Get-Process DynamicIslandBar -ErrorAction SilentlyContinue
if ($running) {
    Write-Step "Stopping running DynamicIslandBar processes"
    $running | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}

if (-not $SkipTests) {
    Write-Step "Running tests"
    Invoke-DotNet @("test", $testProjectPath, "-c", $Configuration)
}

Write-Step "Publishing application"
$publishArgs = @(
    "publish",
    $projectPath,
    "-c", $Configuration,
    "-r", $Runtime,
    "--self-contained", $SelfContained.ToString().ToLowerInvariant(),
    "-o", $publishDir,
    "-p:PublishSingleFile=true",
    "-p:EnableCompressionInSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:DebugType=none",
    "-p:DebugSymbols=false",
    "-p:Version=$Version"
)
Invoke-DotNet $publishArgs

Write-Step "Creating portable package"
$zipPath = Join-Path $packageDir "$releaseName-$dependencyLabel.zip"
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

$iscc = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
$installerPath = $null
if ($iscc) {
    Write-Step "Building Inno Setup installer"
    & $iscc.Source `
        "/DAppVersion=$Version" `
        "/DSourceDir=$publishDir" `
        "/DOutputDir=$installerDir" `
        "/DDependencyLabel=$dependencyLabel" `
        $installerScript
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup failed with exit code $LASTEXITCODE"
    }

    $installerPath = Join-Path $installerDir "DynamicIslandBar-Setup-v$Version-$Runtime-$dependencyLabel.exe"
} else {
    Write-Host "Inno Setup compiler (ISCC.exe) was not found; installer exe was skipped." -ForegroundColor Yellow
}

$manifest = [ordered]@{
    product = "DynamicIslandBar"
    version = $Version
    runtime = $Runtime
    dependency = $dependencyLabel
    createdAt = (Get-Date).ToString("s")
    publishDir = $publishDir
    portableZip = $zipPath
    installer = $installerPath
}
$manifestPath = Join-Path $releaseDir "release-manifest.json"
$manifest | ConvertTo-Json -Depth 4 | Set-Content -Path $manifestPath -Encoding UTF8

Write-Step "Release output"
Write-Host "Publish:   $publishDir"
Write-Host "Zip:       $zipPath"
Write-Host "Installer: $(if ($installerPath) { $installerPath } else { 'skipped - install Inno Setup to build it' })"
Write-Host "Manifest:  $manifestPath"
