#!/usr/bin/env pwsh
# SoftwareOS Build Script - ClinicOS Stage 2 Configurations
# Generates: ClinicOS MX/USA in Demo/Admin/Full.
# Uses environment variables and Windows Credential Manager for certificate secrets.

param(
    [string]$Configuration = "All",
    [string]$Version = "1.0.0.0"
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$solutionDir = Split-Path $scriptDir -Parent
$slnFile = Join-Path $solutionDir "Nuevo_Sistema.sln"
$presentationProject = Join-Path $solutionDir "src\Presentation\OS.Presentation.csproj"
$distDir = Join-Path $solutionDir "Distribucion"

function Get-CertificatePath {
    if (-not [string]::IsNullOrWhiteSpace($env:SOFTWAREOS_PFX_PATH)) {
        return $env:SOFTWAREOS_PFX_PATH
    }

    return Join-Path $solutionDir "certificates\SoftwareOS_Identity.pfx"
}

function Get-WindowsCredentialSecret {
    param(
        [string]$TargetName = "SoftwareOS_PFX_Password"
    )

    $credentialClass = @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class WinCred {
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
    public struct CREDENTIAL {
        public UInt32 Flags;
        public UInt32 Type;
        public string TargetName;
        public string Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public UInt32 CredentialBlobSize;
        public IntPtr CredentialBlob;
        public UInt32 Persist;
        public UInt32 AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", SetLastError=true, CharSet=CharSet.Unicode)]
    public static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", SetLastError=true)]
    public static extern bool CredFree(IntPtr cred);

    public static string ReadPassword(string target) {
        IntPtr credPtr;
        if (!CredRead(target, 1, 0, out credPtr)) return null;
        try {
            CREDENTIAL cred = (CREDENTIAL)Marshal.PtrToStructure(credPtr, typeof(CREDENTIAL));
            if (cred.CredentialBlobSize == 0) return null;
            byte[] blob = new byte[cred.CredentialBlobSize];
            Marshal.Copy(cred.CredentialBlob, blob, 0, (int)cred.CredentialBlobSize);
            return Encoding.Unicode.GetString(blob).TrimEnd('\0');
        } finally {
            CredFree(credPtr);
        }
    }
}
"@
    if (-not [type]::GetType("WinCred")) {
        Add-Type -TypeDefinition $credentialClass -ErrorAction SilentlyContinue
    }

    return [WinCred]::ReadPassword($TargetName)
}

function Get-CertificatePassword {
    if (-not [string]::IsNullOrWhiteSpace($env:CERT_PASS)) {
        return $env:CERT_PASS
    }

    $credentialSecret = Get-WindowsCredentialSecret
    if (-not [string]::IsNullOrWhiteSpace($credentialSecret)) {
        Write-Host "Password retrieved from Windows Credential Manager." -ForegroundColor Green
        return $credentialSecret
    }

    return $null
}

try {
    $certPassword = Get-CertificatePassword
    if ([string]::IsNullOrWhiteSpace($certPassword)) {
        Write-Warning "Certificate password not available. Executables will be built but not signed."
    }
} catch {
    Write-Warning "Failed to retrieve certificate password from local secret storage: $($_.Exception.Message). Executables will be built but not signed."
    $certPassword = $null
}

# 6 ClinicOS market distributions
$buildConfigs = @(
    @{ Name = "ClinicOS_MX_Demo_ES"; Description = "ClinicOS MX Demo Spanish"; Product = "ClinicOS_MX_Demo"; Market = "MX"; Edition = "Demo" },
    @{ Name = "ClinicOS_MX_Admin_ES"; Description = "ClinicOS MX Admin Spanish"; Product = "ClinicOS_MX_Admin"; Market = "MX"; Edition = "Admin" },
    @{ Name = "ClinicOS_MX_Full_ES"; Description = "ClinicOS MX Full Spanish"; Product = "ClinicOS_MX_Full"; Market = "MX"; Edition = "Full" },
    @{ Name = "ClinicOS_USA_Demo_EN"; Description = "ClinicOS USA Demo English"; Product = "ClinicOS_USA_Demo"; Market = "USA"; Edition = "Demo" },
    @{ Name = "ClinicOS_USA_Admin_EN"; Description = "ClinicOS USA Admin English"; Product = "ClinicOS_USA_Admin"; Market = "USA"; Edition = "Admin" },
    @{ Name = "ClinicOS_USA_Full_EN"; Description = "ClinicOS USA Full English"; Product = "ClinicOS_USA_Full"; Market = "USA"; Edition = "Full" }
)

function Write-Header($text) {
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host $text -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
}

function Build-Configuration($config) {
    Write-Header "Building: $($config.Description)"
    
    $configDir = Join-Path $distDir $config.Name
    $publishDir = Join-Path $configDir "publish"
    
    # Clean
    if (Test-Path $configDir) {
        Remove-Item $configDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $configDir -Force | Out-Null
    
    # Build with AOT publishing
    Write-Host "Publishing native AOT executable..." -ForegroundColor Yellow
    
    $env:PRODUCT_NAME = $config.Product
    
    & dotnet publish $presentationProject `
        -c $config.Name `
        -r win-x64 `
        --self-contained true `
        -p:PublishAot=true `
        -p:PublishTrimmed=true `
        -p:TrimMode=full `
        -p:IlcOptimizationPreference=speed `
        -p:StackTraceSupport=false `
        -p:UseWindowsThreadPool=true `
        -p:Version=$Version `
        -p:FileVersion=$Version `
        -p:AssemblyVersion=$Version `
        -o $publishDir
    
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed for configuration: $($config.Name)"
    }
    
    # Sign the executable
    $exeName = "$($config.Product).exe"
    $exePath = Join-Path $publishDir $exeName
    
    if ((Test-Path $exePath) -and -not [string]::IsNullOrWhiteSpace($certPassword)) {
        Write-Host "Signing executable with certificate..." -ForegroundColor Yellow
        & signtool.exe sign `
            /f $certPath `
            /p $certPassword `
            /fd sha256 `
            /as `
            $exePath
        
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Signing failed, but build succeeded"
        } else {
            Write-Host "Successfully signed: $exeName" -ForegroundColor Green
        }
    }
    
    # Create version info file
    $versionInfo = @{
        Product = $config.Product
        Configuration = $config.Name
        Market = $config.Market
        Edition = $config.Edition
        Version = $Version
        BuildDate = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
        AOT = $true
        Encrypted = $true
    } | ConvertTo-Json
    
    $versionInfo | Out-File (Join-Path $configDir "version.json") -Encoding UTF8
    
    Write-Host "Build completed: $($config.Name)" -ForegroundColor Green
}

function Build-Installers() {
    Write-Header "Building Installers with Inno Setup"
    
    $issCompiler = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    
    if (-not (Test-Path $issCompiler)) {
        Write-Warning "Inno Setup not found at $issCompiler. Skipping installer creation."
        return
    }
    
    foreach ($config in $buildConfigs) {
        $issFile = Join-Path $solutionDir "installers\innosetup\$($config.Name).iss"
        
        if (Test-Path $issFile) {
            Write-Host "Building installer for: $($config.Name)" -ForegroundColor Yellow
            & $issCompiler $issFile
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "Installer created successfully" -ForegroundColor Green
            } else {
                Write-Warning "Failed to create installer for: $($config.Name)"
            }
        }
    }
}

# Main Execution
Write-Header "SoftwareOS Build System v$Version"

# Verify dotnet SDK
$dotnetVersion = & dotnet --version
Write-Host "Using .NET SDK: $dotnetVersion" -ForegroundColor Gray

# Verify signtool
$signtool = Get-Command signtool.exe -ErrorAction SilentlyContinue
if (-not $signtool) {
    Write-Warning "signtool.exe not found in PATH. Signing will be skipped."
}

# Build requested configurations
if ($Configuration -eq "All") {
    foreach ($config in $buildConfigs) {
        Build-Configuration $config
    }
    Build-Installers
} else {
    $selectedConfig = $buildConfigs | Where-Object { $_.Name -eq $Configuration }
    if ($selectedConfig) {
        Build-Configuration $selectedConfig
    } else {
        throw "Unknown configuration: $Configuration"
    }
}

Write-Header "Build Process Complete!"
Write-Host "Output directory: $distDir" -ForegroundColor Cyan
Write-Host "`nGenerated configurations:" -ForegroundColor White
foreach ($config in $buildConfigs) {
    $configDir = Join-Path $distDir $config.Name
    if (Test-Path $configDir) {
        Write-Host "  [OK] $($config.Name)" -ForegroundColor Green
    }
}
