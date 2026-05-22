using System;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace OS.Infrastructure.Security;

/// <summary>
/// Servicio para generación de claves de licencia.
/// Combina el HWID del cliente con la versión del software.
/// </summary>
[SupportedOSPlatform("windows")]
public static class LicenseGenerator
{
    private const string LICENSE_PREFIX = "CLINIC";
    private const string HWID_PREFIX = "HW";
    private const string VERSION_PREFIX = "v";
    private const int HWID_SHORT_LENGTH = 3; // Longitud del identificador HWID abreviado

    /// <summary>
    /// Genera una clave de licencia combinando el HWID y la versión del software.
    /// Formato: CLINIC-HW-{abreviado}-v{version}
    /// Ejemplo: CLINIC-HW-ABC-v1.0
    /// </summary>
    /// <param name="hardwareId">Instancia de HardwareId del cliente</param>
    /// <param name="softwareVersion">Versión actual del software (ej: "1.0.0")</param>
    /// <returns>Clave de licencia generada</returns>
    /// <exception cref="ArgumentNullException">Si hardwareId o softwareVersion son null</exception>
    /// <exception cref="ArgumentException">Si softwareVersion está vacío</exception>
    public static string GenerateLicenseKey(HardwareId hardwareId, string softwareVersion)
    {
        if (hardwareId == null)
            throw new ArgumentNullException(nameof(hardwareId));

        if (string.IsNullOrWhiteSpace(softwareVersion))
            throw new ArgumentException("La versión del software no puede estar vacía.", nameof(softwareVersion));

        // Obtener identificador HWID abreviado (primeros 3 caracteres del hash en mayúsculas)
        string hwidShort = GetShortHwid(hardwareId.CombinedHash);

        // Formatear versión (remover 'v' si ya está presente y asegurar formato)
        string formattedVersion = FormatVersion(softwareVersion);

        // Combinar componentes
        return $"{LICENSE_PREFIX}-{HWID_PREFIX}-{hwidShort}-{VERSION_PREFIX}{formattedVersion}";
    }

    /// <summary>
    /// Genera una clave de licencia sobrecargada que acepta strings directamente.
    /// </summary>
    /// <param name="hwidHash">Hash del HWID del cliente</param>
    /// <param name="softwareVersion">Versión del software</param>
    /// <returns>Clave de licencia generada</returns>
    public static string GenerateLicenseKey(string hwidHash, string softwareVersion)
    {
        if (string.IsNullOrWhiteSpace(hwidHash))
            throw new ArgumentException("El hash del HWID no puede estar vacío.", nameof(hwidHash));

        string hwidShort = GetShortHwid(hwidHash);
        string formattedVersion = FormatVersion(softwareVersion);

        return $"{LICENSE_PREFIX}-{HWID_PREFIX}-{hwidShort}-{VERSION_PREFIX}{formattedVersion}";
    }

    /// <summary>
    /// Obtiene una representación abreviada del HWID para la licencia.
    /// Toma los primeros 3 caracteres del hash en mayúsculas.
    /// </summary>
    private static string GetShortHwid(string hwidHash)
    {
        if (string.IsNullOrWhiteSpace(hwidHash))
            return "XXX"; // Fallback para casos excepcionales

        // Tomar primeros 3 caracteres y convertir a mayúsculas
        string shortHwid = hwidHash.Length >= HWID_SHORT_LENGTH
            ? hwidHash.Substring(0, HWID_SHORT_LENGTH).ToUpper()
            : hwidHash.PadRight(HWID_SHORT_LENGTH, 'X').ToUpper();

        // Asegurar que solo contiene caracteres válidos (letras y números)
        return SanitizeHwid(shortHwid);
    }

    /// <summary>
    /// Formatea la versión del software para consistencia.
    /// </summary>
    private static string FormatVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return "0.0.0";

        // Remover prefijo 'v' si existe
        version = version.TrimStart('v', 'V');

        // Asegurar formato básico X.Y.Z
        var parts = version.Split('.');
        if (parts.Length >= 3)
            return $"{parts[0]}.{parts[1]}.{parts[2]}";
        else if (parts.Length == 2)
            return $"{parts[0]}.{parts[1]}.0";
        else
            return $"{parts[0]}.0.0";
    }

    /// <summary>
    /// Sanitiza el HWID abreviado para asegurar caracteres válidos.
    /// </summary>
    private static string SanitizeHwid(string hwid)
    {
        var sanitized = new StringBuilder();
        foreach (char c in hwid)
        {
            if (char.IsLetterOrDigit(c))
                sanitized.Append(c);
            else
                sanitized.Append('X'); // Reemplazar caracteres inválidos
        }
        return sanitized.ToString();
    }

    /// <summary>
    /// Valida si una clave de licencia tiene el formato correcto.
    /// </summary>
    public static bool ValidateLicenseFormat(string licenseKey)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
            return false;

        // Patrón esperado: CLINIC-HW-XXX-vX.Y.Z
        var parts = licenseKey.Split('-');
        if (parts.Length != 4)
            return false;

        return parts[0] == LICENSE_PREFIX &&
               parts[1] == HWID_PREFIX &&
               parts[2].Length == HWID_SHORT_LENGTH &&
               parts[3].StartsWith(VERSION_PREFIX);
    }

    /// <summary>
    /// Extrae componentes de una clave de licencia válida.
    /// </summary>
    public static (string HwidShort, string Version)? ParseLicenseKey(string licenseKey)
    {
        if (!ValidateLicenseFormat(licenseKey))
            return null;

        var parts = licenseKey.Split('-');
        string hwidShort = parts[2];
        string version = parts[3].Substring(1); // Remover 'v'

        return (hwidShort, version);
    }
}