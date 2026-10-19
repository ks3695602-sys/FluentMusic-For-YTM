<#
.SYNOPSIS
    Builds the YT Music Client as a sideloadable MSIX package.
.DESCRIPTION
    This script generates a self-signed test certificate, builds the project,
    and produces an MSIX package in the /Artifacts folder.
.PARAMETER Configuration
    Build configuration (Debug or Release). Default: Release.
.PARAMETER CertPassword
    Password for the self-signed certificate. Default: YTMusic123!
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$CertPassword = 'YTMusic123!'
)

$ErrorActionPreference = 'Stop'
$ProjectRoot = $PSScriptRoot
$ProjectFile = Join-Path $ProjectRoot 'src\YTMusicClient\YTMusicClient.csproj'
$ArtifactsDir = Join-Path $ProjectRoot 'Artifacts'
$CertDir = Join-Path $ProjectRoot 'Certificates'
$CertPath = Join-Path $CertDir 'YTMusicClient_TestCert.pfx'

Write-Host '===========================================' -ForegroundColor Cyan
Write-Host '  YT Music Client - MSIX Build Script' -ForegroundColor Cyan
Write-Host '===========================================' -ForegroundColor Cyan
Write-Host ''

# Step 1: Create directories
Write-Host '[1/5] Creating output directories...' -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path $ArtifactsDir | Out-Null
New-Item -ItemType Directory -Force -Path $CertDir | Out-Null

# Step 2: Generate self-signed certificate
Write-Host '[2/5] Generating self-signed test certificate...' -ForegroundColor Yellow
if (-not (Test-Path $CertPath)) {
    $cert = New-SelfSignedCertificate `
        -Type Custom `
        -Subject 'CN=YTMusicClient' `
        -KeyUsage DigitalSignature `
        -FriendlyName 'YT Music Client Test Certificate' `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')

    $securePassword = ConvertTo-SecureString -String $CertPassword -Force -AsPlainText
    Export-PfxCertificate -Cert "Cert:\CurrentUser\My\$($cert.Thumbprint)" `
        -FilePath $CertPath -Password $securePassword | Out-Null

    Write-Host "  Certificate created: $CertPath" -ForegroundColor Green
    Write-Host "  Thumbprint: $($cert.Thumbprint)" -ForegroundColor Gray
} else {
    Write-Host '  Certificate already exists, skipping...' -ForegroundColor Gray
}

# Step 3: Restore NuGet packages
Write-Host '[3/5] Restoring NuGet packages...' -ForegroundColor Yellow
dotnet restore $ProjectFile --runtime win-x64
if ($LASTEXITCODE -ne 0) { throw 'NuGet restore failed.' }

# Step 4: Build the project
Write-Host '[4/5] Building project...' -ForegroundColor Yellow
dotnet build $ProjectFile `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

# Step 5: Publish MSIX
Write-Host '[5/5] Publishing MSIX package...' -ForegroundColor Yellow
dotnet publish $ProjectFile `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -o $ArtifactsDir `
    -p:GenerateAppxPackageOnBuild=true `
    -p:AppxPackageDir=$ArtifactsDir\ `
    -p:AppxPackageSigningEnabled=true `
    -p:PackageCertificateKeyFile=$CertPath `
    -p:PackageCertificatePassword=$CertPassword
if ($LASTEXITCODE -ne 0) {
    Write-Host 'MSIX signing failed, trying unsigned...' -ForegroundColor Yellow
    dotnet publish $ProjectFile `
        -c $Configuration `
        -r win-x64 `
        --self-contained true `
        -o $ArtifactsDir
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
}

Write-Host ''
Write-Host '===========================================' -ForegroundColor Green
Write-Host '  Build complete!' -ForegroundColor Green
Write-Host "  Artifacts: $ArtifactsDir" -ForegroundColor Green
Write-Host '===========================================' -ForegroundColor Green
