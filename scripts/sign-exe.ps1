#!/usr/bin/env pwsh
# Sign executable with SoftwareOS certificate using environment variables or Windows Credential Manager.

param(
    [Parameter(Mandatory=$true)]
    [string]$ExecutablePath,

    [string]$CertificatePath = $null,
    [string]$CertificatePassword = $null
)

$ErrorActionPreference = "Stop"

function Get-CertificatePath {
    if (-not [string]::IsNullOrWhiteSpace($env:SOFTWAREOS_PFX_PATH)) {
        return $env:SOFTWAREOS_PFX_PATH
    }

    return Join-Path $PSScriptRoot "..\certificates\SoftwareOS_Identity.pfx"
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

if (-not (Test-Path $ExecutablePath)) {
    throw "Executable not found: $ExecutablePath"
}

if (-not $CertificatePath) {
    $CertificatePath = Get-CertificatePath
}

if (-not (Test-Path $CertificatePath)) {
    throw "Certificate not found: $CertificatePath"
}

if (-not $CertificatePassword) {
    $CertificatePassword = Get-CertificatePassword
}

if ([string]::IsNullOrWhiteSpace($CertificatePassword)) {
    throw "Certificate password missing. Configure CERT_PASS or store it in Windows Credential Manager under 'SoftwareOS_PFX_Password'."
}

Write-Host "Signing: $ExecutablePath" -ForegroundColor Cyan
Write-Host "Certificate: $CertificatePath" -ForegroundColor Gray

& signtool.exe sign `
    /f $CertificatePath `
    /p $CertificatePassword `
    /fd sha256 `
    /as `
    /d "SoftwareOS - Local Healthcare Management" `
    $ExecutablePath

if ($LASTEXITCODE -eq 0) {
    Write-Host "Successfully signed executable!" -ForegroundColor Green
    & signtool.exe verify /pa $ExecutablePath
} else {
    throw "Signing failed with exit code: $LASTEXITCODE"
}
