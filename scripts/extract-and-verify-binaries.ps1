# Script para extraer y verificar la integridad de los binarios portables de PostgreSQL
# Uso: .\scripts\extract-and-verify-binaries.ps1 -SourceDir "path\to\pg_bin" -TargetDir "path\to\target"

param(
    [Parameter(Mandatory=$false)]
    [string]$SourceDir = (Join-Path $PSScriptRoot "..\src\Presentation\bin\Debug\net8.0-windows\Database\pg_bin"),
    
    [Parameter(Mandatory=$false)]
    [string]$TargetDir = (Join-Path $env:LOCALAPPDATA "SoftwareOS\ClinicOS\pg_bin"),
    
    [Parameter(Mandatory=$false)]
    [switch]$Force
)

# Cargar ensamblados necesarios
Add-Type -AssemblyName System.Security

# Función para calcular hash SHA-256 de un directorio
function Get-DirectoryHash {
    param(
        [string]$DirectoryPath
    )
    
    if (-not (Test-Path $DirectoryPath)) {
        throw "Directory not found: $DirectoryPath"
    }
    
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    
    # Obtener todos los archivos ordenados por ruta relativa
    $files = Get-ChildItem -Path $DirectoryPath -Recurse -File | 
             Sort-Object { $_.FullName.Replace($DirectoryPath, "").Replace("\", "/") }
    
    foreach ($file in $files) {
        # Hash de la ruta relativa
        $relativePath = $file.FullName.Replace($DirectoryPath, "").Replace("\", "/")
        $pathBytes = [System.Text.Encoding]::UTF8.GetBytes($relativePath)
        $sha256.TransformBlock($pathBytes, 0, $pathBytes.Length, $null, 0)
        
        # Hash del contenido del archivo
        $contentBytes = [System.IO.File]::ReadAllBytes($file.FullName)
        $sha256.TransformBlock($contentBytes, 0, $contentBytes.Length, $null, 0)
    }
    
    $sha256.TransformFinalBlock($null, 0, 0)
    $hash = [System.BitConverter]::ToString($sha256.Hash).Replace("-", "").ToUpperInvariant()
    $sha256.Dispose()
    
    return $hash
}

# Función para guardar el hash en un archivo
function Save-HashFile {
    param(
        [string]$DirectoryPath,
        [string]$Hash
    )
    
    $hashFilePath = Join-Path $DirectoryPath "binaries.sha256"
    $Hash | Out-File -FilePath $hashFilePath -Encoding UTF8 -NoNewline
    Write-Host "✓ Hash guardado en: $hashFilePath"
}

# Función para leer el hash desde un archivo
function Read-HashFile {
    param(
        [string]$DirectoryPath
    )
    
    $hashFilePath = Join-Path $DirectoryPath "binaries.sha256"
    if (Test-Path $hashFilePath) {
        return (Get-Content $hashFilePath -Raw).Trim()
    }
    return $null
}

# Función para verificar el hash
function Test-DirectoryIntegrity {
    param(
        [string]$DirectoryPath,
        [string]$ExpectedHash
    )
    
    if ([string]::IsNullOrEmpty($ExpectedHash)) {
        Write-Host "⚠ No hay hash esperado, omitiendo verificación"
        return $true
    }
    
    $actualHash = Get-DirectoryHash -DirectoryPath $DirectoryPath
    
    if ($actualHash -eq $ExpectedHash) {
        Write-Host "✓ Integridad verificada: $actualHash"
        return $true
    } else {
        Write-Host "✗ ERROR: Integridad comprometida"
        Write-Host "  Esperado: $ExpectedHash"
        Write-Host "  Actual:   $actualHash"
        return $false
    }
}

# Función para copiar directorio
function Copy-DirectoryWithStructure {
    param(
        [string]$SourcePath,
        [string]$DestinationPath
    )
    
    if (-not (Test-Path $SourcePath)) {
        throw "Source directory not found: $SourcePath"
    }
    
    # Crear directorio destino si no existe
    if (-not (Test-Path $DestinationPath)) {
        New-Item -ItemType Directory -Path $DestinationPath -Force | Out-Null
        Write-Host "✓ Directorio creado: $DestinationPath"
    }
    
    # Copiar subdirectorios
    foreach ($dir in Get-ChildItem -Path $SourcePath -Recurse -Directory) {
        $targetDir = $dir.FullName.Replace($SourcePath, $DestinationPath)
        if (-not (Test-Path $targetDir)) {
            New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
        }
    }
    
    # Copiar archivos
    $fileCount = 0
    foreach ($file in Get-ChildItem -Path $SourcePath -Recurse -File) {
        $targetFile = $file.FullName.Replace($SourcePath, $DestinationPath)
        Copy-Item -Path $file.FullName -Destination $targetFile -Force
        $fileCount++
    }
    
    Write-Host "✓ $fileCount archivos copiados"
}

# Main script execution
Write-Host "=== Extracción y Verificación de Binarios PostgreSQL ===" -ForegroundColor Cyan
Write-Host ""

# Verificar directorio fuente
if (-not (Test-Path $SourceDir)) {
    Write-Host "✗ ERROR: Directorio fuente no encontrado: $SourceDir" -ForegroundColor Red
    Write-Host "  Asegúrese de que los binarios de PostgreSQL estén en la ubicación correcta."
    exit 1
}

Write-Host "Directorio fuente: $SourceDir"
Write-Host "Directorio destino: $TargetDir"
Write-Host ""

# Verificar si el directorio destino ya existe
if (Test-Path $TargetDir) {
    if ($Force) {
        Write-Host "⚠ Forzando sobreescritura del directorio destino..."
        Remove-Item -Path $TargetDir -Recurse -Force
    } else {
        Write-Host "⚠ El directorio destino ya existe"
        
        # Leer hash existente
        $existingHash = Read-HashFile -DirectoryPath $TargetDir
        if ($existingHash) {
            Write-Host "  Hash existente: $existingHash"
        }
        
        # Verificar integridad
        $integrityOk = Test-DirectoryIntegrity -DirectoryPath $TargetDir -ExpectedHash $existingHash
        
        if ($integrityOk) {
            Write-Host ""
            Write-Host "✓ Los binarios existentes son válidos. No se requiere extracción."
            exit 0
        } else {
            Write-Host ""
            $response = Read-Host "Los binarios existentes están corruptos. ¿Desea reemplazarlos? (S/N)"
            if ($response -ne "S" -and $response -ne "s") {
                Write-Host "Operación cancelada."
                exit 1
            }
            Remove-Item -Path $TargetDir -Recurse -Force
        }
    }
}

# Calcular hash del directorio fuente
Write-Host "Calculando hash SHA-256 del directorio fuente..."
$sourceHash = Get-DirectoryHash -DirectoryPath $SourceDir
Write-Host "Hash del directorio fuente: $sourceHash"
Write-Host ""

# Copiar binarios
Write-Host "Extrayendo binarios..."
try {
    Copy-DirectoryWithStructure -SourcePath $SourceDir -DestinationPath $TargetDir
    Write-Host ""
} catch {
    Write-Host "✗ ERROR durante la extracción: $_" -ForegroundColor Red
    exit 1
}

# Guardar hash en el directorio destino
Save-HashFile -DirectoryPath $TargetDir -Hash $sourceHash
Write-Host ""

# Verificar integridad de los binarios extraídos
Write-Host "Verificando integridad de los binarios extraídos..."
$integrityOk = Test-DirectoryIntegrity -DirectoryPath $TargetDir -ExpectedHash $sourceHash

if ($integrityOk) {
    Write-Host ""
    Write-Host "=== ✓ Extracción y verificación completadas exitosamente ===" -ForegroundColor Green
    exit 0
} else {
    Write-Host ""
    Write-Host "=== ✗ ERROR: Verificación de integridad fallida ===" -ForegroundColor Red
    exit 1
}
