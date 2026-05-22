param(
    [string]$CertificateFile = "$PSScriptRoot\..\certificates\SoftwareOS_Compliance.cer",
    [switch]$InstallToLocalMachine
)

$ErrorActionPreference = 'Stop'

function Get-StoreLocation {
    if ($InstallToLocalMachine) {
        if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
            throw "Para instalar en LocalMachine se requieren privilegios de administrador. Ejecute PowerShell como administrador."
        }

        return 'LocalMachine'
    }

    return 'CurrentUser'
}

function Assert-CertificateFileExists {
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        throw "No se encontró el certificado: $Path"
    }
}

function Install-TrustCertificate {
    param(
        [string]$Path,
        [string]$StoreLocation
    )

    $storePath = "Cert:\$StoreLocation\Root"
    $certificate = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($Path)

    $existing = Get-ChildItem -Path $storePath | Where-Object { $_.Thumbprint -eq $certificate.Thumbprint }
    if ($existing) {
        Write-Host "El certificado ya está instalado en $storePath." -ForegroundColor Yellow
        return
    }

    Write-Host "Instalando certificado en $storePath..." -ForegroundColor Cyan
    Import-Certificate -FilePath $Path -CertStoreLocation $storePath | Out-Null
    Write-Host "Certificado instalado correctamente." -ForegroundColor Green
}

function Verify-Installation {
    param(
        [string]$StoreLocation
    )

    $storePath = "Cert:\$StoreLocation\Root"
    $certificate = Get-ChildItem -Path $storePath | Where-Object { $_.Subject -like '*SoftwareOS*' }

    if ($null -eq $certificate) {
        throw "No se encontró el certificado SoftwareOS en $storePath después de la instalación."
    }

    Write-Host "Verificación completada. Certificado encontrado en $storePath." -ForegroundColor Green
}

Write-Host "Iniciando instalación del certificado de confianza..." -ForegroundColor Cyan
Assert-CertificateFileExists -Path $CertificateFile
$storeLocation = Get-StoreLocation
Install-TrustCertificate -Path $CertificateFile -StoreLocation $storeLocation
Verify-Installation -StoreLocation $storeLocation
Write-Host "Ejecución finalizada. El certificado de confianza se ha instalado correctamente." -ForegroundColor Green
