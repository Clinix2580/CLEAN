using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using OS.Domain.Interfaces;

namespace OS.Infrastructure.Security;

/// <summary>
/// Servicio de encriptación AES-256 con claves derivadas de Hardware ID usando PBKDF2.
/// </summary>
[SupportedOSPlatform("windows")]
public class EncryptionService : IEncryptionService
{
    private readonly byte[] _key;
    private const int Pbkdf2Iterations = 100_000; // 100,000 iteraciones como se requiere
    private const int KeySize = 32; // 256 bits para AES-256

    /// <summary>
    /// Inicializa el servicio con una clave derivada del Hardware ID.
    /// </summary>
    /// <param name="hardwareId">Identificador hardware que sirve como semilla</param>
    public EncryptionService(HardwareId hardwareId)
    {
        _key = DeriveKeyFromHardwareInternal(hardwareId.CombinedHash);
    }

    /// <summary>
    /// Encripta texto plano usando AES-256 en modo CBC con PBKDF2.
    /// </summary>
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            throw new ArgumentException("El texto plano no puede estar vacío.", nameof(plainText));

        var plaintextBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = EncryptBytes(plaintextBytes);
        return Convert.ToBase64String(encryptedBytes);
    }

    /// <summary>
    /// Desencripta texto cifrado en Base64.
    /// </summary>
    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            throw new ArgumentException("El texto cifrado no puede estar vacío.", nameof(cipherText));

        try
        {
            var cipherBytes = Convert.FromBase64String(cipherText);
            var plaintextBytes = DecryptBytes(cipherBytes);
            return Encoding.UTF8.GetString(plaintextBytes);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("El texto cifrado no está en un formato Base64 válido.");
        }
    }

    /// <summary>
    /// Encripta bytes directamente.
    /// El IV se genera aleatoriamente y se prepende al ciphertext.
    /// </summary>
    public byte[] EncryptBytes(byte[] data)
    {
        if (data == null || data.Length == 0)
            throw new ArgumentException("Los datos no pueden estar vacíos.", nameof(data));

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.GenerateIV();

        var iv = aes.IV;

        using var encryptor = aes.CreateEncryptor();
        var encryptedData = encryptor.TransformFinalBlock(data, 0, data.Length);

        // Prepend IV al ciphertext
        var result = new byte[iv.Length + encryptedData.Length];
        Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
        Buffer.BlockCopy(encryptedData, 0, result, iv.Length, encryptedData.Length);

        return result;
    }

    /// <summary>
    /// Desencripta bytes que contienen IV prepended.
    /// </summary>
    public byte[] DecryptBytes(byte[] data)
    {
        if (data == null || data.Length < 16)
            throw new ArgumentException("Los datos cifrados son inválidos o están incompletos.", nameof(data));

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        // Extract IV (primeros 16 bytes)
        var iv = new byte[16];
        Buffer.BlockCopy(data, 0, iv, 0, 16);
        aes.IV = iv;

        // Extract ciphertext (resto de los bytes)
        var cipherData = new byte[data.Length - 16];
        Buffer.BlockCopy(data, 16, cipherData, 0, cipherData.Length);

        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(cipherData, 0, cipherData.Length);
    }

    /// <summary>
    /// Deriva una clave de 256 bits (32 bytes) usando PBKDF2 con SHA-256.
    /// Utiliza 100,000 iteraciones como se requiere para seguridad militar.
    /// </summary>
    /// <param name="hardwareHash">Hash del Hardware ID que sirve como contraseña</param>
    /// <returns>Clave de 256 bits derivada</returns>
    public string DeriveKeyFromHardware(string hardwareHash)
    {
        var derivedKey = DeriveKeyFromHardwareInternal(hardwareHash);
        return Convert.ToHexString(derivedKey).ToLowerInvariant();
    }

    private static byte[] DeriveKeyFromHardwareInternal(string hardwareHash)
    {
        if (string.IsNullOrEmpty(hardwareHash))
            throw new ArgumentException("El hash de hardware no puede estar vacío.", nameof(hardwareHash));

        // Salt fijo pero único por aplicación
        var salt = Encoding.UTF8.GetBytes("ClinicOS_Offline_First_v1_2026");

        using var pbkdf2 = new Rfc2898DeriveBytes(
            hardwareHash,
            salt,
            Pbkdf2Iterations,
            HashAlgorithmName.SHA256);

        return pbkdf2.GetBytes(KeySize);
    }

    /// <summary>
    /// Obtiene la clave derivada en formato hexadecimal, lista para SQLCipher.
    /// </summary>
    public string GetDerivedKeyHex()
    {
        return Convert.ToHexString(_key);
    }
}
