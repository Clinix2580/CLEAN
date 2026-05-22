# Script para cifrar cadenas de conexión usando DPAPI
# Uso: .\scripts\encrypt-connection-strings.ps1 -ConnectionString "Host=127.0.0.1;Port=5432;Database=clinicos_db;Username=clinic_admin;Password=CLINIC_ADMIN_PASS;Ssl Mode=Require;Trust Server Certificate=false;"

param(
    [Parameter(Mandatory=$true)]
    [string]$ConnectionString
)

# Cargar el ensamblado necesario
Add-Type -AssemblyName System.Security

# Función para cifrar usando DPAPI (máquina)
function Protect-StringForMachine {
    param(
        [string]$PlainText
    )
    
    $plainBytes = [System.Text.Encoding]::UTF8.GetBytes($PlainText)
    $encryptedBytes = [System.Security.Cryptography.ProtectedData]::Protect(
        $plainBytes, 
        $null, 
        [System.Security.Cryptography.DataProtectionScope]::LocalMachine
    )
    return [System.Convert]::ToBase64String($encryptedBytes)
}

# Cifrar la cadena de conexión
$encrypted = Protect-StringForMachine -PlainText $ConnectionString

# Output en formato para appsettings.json
Write-Host "Cadena de conexión cifrada (para appsettings.json):"
Write-Host "enc:$encrypted"
Write-Host ""
Write-Host "Para usar en appsettings.json, reemplace la cadena original con:"
Write-Host "  ""enc:$encrypted"""
