<#
.SYNOPSIS
    Builds the YT Music Client as a sideloadable MSIX package.
.DESCRIPTION
    This script generates a self-signed test certificate, builds the project,
    and produces an MSIX package in the AppPackages folder.
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
$SolutionFile = Join-Path $ProjectRoot 'YTMusicClient.sln'
$CertDir = Join-Path $ProjectRoot 'Certificates'
$CertPath = Join-Path $CertDir 'YTMusicClient_TestCert.pfx'

Write-Host '===========================================' -ForegroundColor Cyan
Write-Host '  YT Music Client - MSIX Build Script' -ForegroundColor Cyan
Write-Host '===========================================' -ForegroundColor Cyan
Write-Host ''

# Step 1: Create directories
Write-Host '[1/4] Preparing certificate directory...' -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path $CertDir | Out-Null

# Step 2: Generate self-signed certificate
Write-Host '[2/4] Generating self-signed test certificate...' -ForegroundColor Yellow
$certThumbprint = $null
if (-not (Test-Path $CertPath)) {
    $cert = New-SelfSignedCertificate `
        -Type Custom `
        -Subject 'CN=YTMusicClient' `
        -KeyUsage DigitalSignature `
        -FriendlyName 'YT Music Client Test Certificate' `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')

    # Add to TrustedPeople store
    $store = New-Object System.Security.Cryptography.X509Certificates.X509Store "TrustedPeople", "CurrentUser"
    $store.Open("ReadWrite")
    $store.Add($cert)
    $store.Close()

    $securePassword = ConvertTo-SecureString -String $CertPassword -Force -AsPlainText
    Export-PfxCertificate -Cert "Cert:\CurrentUser\My\$($cert.Thumbprint)" `
        -FilePath $CertPath -Password $securePassword -CryptoAlgorithmOption TripleDES_SHA1 | Out-Null

    $certThumbprint = $cert.Thumbprint
    Write-Host "  Certificate created: $CertPath" -ForegroundColor Green
    Write-Host "  Thumbprint: $($cert.Thumbprint)" -ForegroundColor Gray
} else {
    Write-Host '  Certificate already exists, using existing cert...' -ForegroundColor Gray
}

# Step 3: Restore NuGet packages
Write-Host '[3/4] Restoring dependencies...' -ForegroundColor Yellow
dotnet restore $SolutionFile /p:Platform=x64
if ($LASTEXITCODE -ne 0) { throw 'NuGet restore failed.' }

# Step 4: Build & Package MSIX
Write-Host '[4/4] Building solution & generating MSIX package...' -ForegroundColor Yellow
$extraParams = @()
if ($certThumbprint) {
    $extraParams += "/p:PackageCertificateThumbprint=$certThumbprint"
}

dotnet build $SolutionFile `
    --configuration $Configuration `
    --no-restore `
    /p:Platform=x64 `
    /p:GenerateAppxPackageOnBuild=true `
    /p:UapAppxPackageBuildMode=SideloadOnly `
    /p:AppxBundle=Never `
    /p:AppxPackageSigningEnabled=true `
    /p:PackageCertificateKeyFile="$CertPath" `
    /p:PackageCertificatePassword="$CertPassword" `
    @extraParams

if ($LASTEXITCODE -ne 0) { throw 'Build & Packaging failed.' }

Write-Host ''
Write-Host '===========================================' -ForegroundColor Green
Write-Host '  Build & Packaging complete!' -ForegroundColor Green
Write-Host '  Check src/YTMusicClient/AppPackages for your installable .msix' -ForegroundColor Green
Write-Host '===========================================' -ForegroundColor Green
