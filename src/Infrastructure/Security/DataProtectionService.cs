using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace OS.Infrastructure.Security;

/// <summary>
/// Servicio de protección de datos usando DPAPI (Data Protection API) de Windows.
/// Permite cifrar y descifrar cadenas de conexión y otros datos sensibles.
/// </summary>
public interface IDataProtectionService
{
    /// <summary>
    /// Cifra una cadena de texto plano usando DPAPI.
    /// </summary>
    /// <param name="plainText">Texto plano a cifrar</param>
    /// <returns>Texto cifrado en Base64</returns>
    string Protect(string plainText);

    /// <summary>
    /// Descifra una cadena cifrada con DPAPI.
    /// </summary>
    /// <param name="encryptedText">Texto cifrado en Base64</param>
    /// <returns>Texto plano original</returns>
    string Unprotect(string encryptedText);

    /// <summary>
    /// Cifra una cadena de texto plano usando DPAPI con ámbito de máquina.
    /// Útil para escenarios donde múltiples usuarios del mismo sistema necesitan acceso.
    /// </summary>
    /// <param name="plainText">Texto plano a cifrar</param>
    /// <returns>Texto cifrado en Base64</returns>
    string ProtectForMachine(string plainText);

    /// <summary>
    /// Descifra una cadena cifrada con DPAPI con ámbito de máquina.
    /// </summary>
    /// <param name="encryptedText">Texto cifrado en Base64</param>
    /// <returns>Texto plano original</returns>
    string UnprotectForMachine(string encryptedText);
}

/// <summary>
/// Implementación de servicio de protección de datos usando DPAPI de Windows.
/// </summary>
[SupportedOSPlatform("windows")]
public class DataProtectionService : IDataProtectionService
{
    /// <summary>
    /// Cifra una cadena de texto plano usando DPAPI con ámbito de usuario actual.
    /// </summary>
    public string Protect(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
            throw new ArgumentException("El texto a cifrar no puede estar vacío.", nameof(plainText));

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encryptedBytes);
    }

    /// <summary>
    /// Descifra una cadena cifrada con DPAPI con ámbito de usuario actual.
    /// </summary>
    public string Unprotect(string encryptedText)
    {
        if (string.IsNullOrWhiteSpace(encryptedText))
            throw new ArgumentException("El texto cifrado no puede estar vacío.", nameof(encryptedText));

        try
        {
            var encryptedBytes = Convert.FromBase64String(encryptedText);
            var plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException("No se pudo descifrar el texto. Posiblemente fue cifrado por un usuario diferente o en otra máquina.", ex);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("El texto cifrado no es un Base64 válido.", ex);
        }
    }

    /// <summary>
    /// Cifra una cadena de texto plano usando DPAPI con ámbito de máquina.
    /// </summary>
    public string ProtectForMachine(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
            throw new ArgumentException("El texto a cifrar no puede estar vacío.", nameof(plainText));

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.LocalMachine);
        return Convert.ToBase64String(encryptedBytes);
    }

    /// <summary>
    /// Descifra una cadena cifrada con DPAPI con ámbito de máquina.
    /// </summary>
    public string UnprotectForMachine(string encryptedText)
    {
        if (string.IsNullOrWhiteSpace(encryptedText))
            throw new ArgumentException("El texto cifrado no puede estar vacío.", nameof(encryptedText));

        try
        {
            var encryptedBytes = Convert.FromBase64String(encryptedText);
            var plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.LocalMachine);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException("No se pudo descifrar el texto. Posiblemente fue cifrado en otra máquina.", ex);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("El texto cifrado no es un Base64 válido.", ex);
        }
    }
}
