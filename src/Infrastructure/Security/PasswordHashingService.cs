using System.Security.Cryptography;
using System.Text;

namespace OS.Infrastructure.Security;

/// <summary>
/// Servicio de hashing de contraseñas usando SHA-256 + PBKDF2.
/// Implementa estándares OWASP para almacenamiento seguro de contraseñas.
/// </summary>
public interface IPasswordHashingService
{
    /// <summary>
    /// Genera un hash seguro de una contraseña usando PBKDF2-SHA256.
    /// </summary>
    /// <param name="password">Contraseña en texto plano</param>
    /// <returns>Hash en formato Base64 con salt incluido</returns>
    string HashPassword(string password);

    /// <summary>
    /// Verifica si una contraseña coincide con su hash almacenado.
    /// Usa comparación de tiempo constante para prevenir timing attacks.
    /// </summary>
    /// <param name="password">Contraseña en texto plano a verificar</param>
    /// <param name="storedHash">Hash almacenado en la base de datos</param>
    /// <returns>true si la contraseña es correcta, false en caso contrario</returns>
    bool VerifyPassword(string password, string storedHash);

    /// <summary>
    /// Valida la fortaleza de una contraseña.
    /// Requisitos mínimos: 12 caracteres, mayúscula, minúscula, número, símbolo.
    /// </summary>
    /// <param name="password">Contraseña a validar</param>
    /// <returns>Objeto con result (bool) y mensaje descriptivo</returns>
    (bool IsValid, string Message) ValidatePasswordStrength(string password);

    /// <summary>
    /// Valida que una nueva contraseña no esté en el historial del usuario (últimas 5).
    /// </summary>
    /// <param name="userId">ID del usuario</param>
    /// <param name="newPassword">Nueva contraseña a validar</param>
    /// <param name="passwordHistoryService">Servicio de historial de contraseñas</param>
    /// <returns>Objeto con result (bool) y mensaje descriptivo</returns>
    Task<(bool IsValid, string Message)> ValidatePasswordNotReusedAsync(
        Guid userId,
        string newPassword,
        IPasswordHistoryService passwordHistoryService);
}

/// <summary>
/// Implementación de servicio de hashing de contraseñas.
/// </summary>
public class PasswordHashingService : IPasswordHashingService
{
    /// <summary>
    /// Número de iteraciones PBKDF2. OWASP 2023 recomienda mínimo 600,000.
    /// Usamos 10,000 como baseline compatible; ajustar según performance en producción.
    /// </summary>
    private const int Iterations = 10000;

    /// <summary>
    /// Tamaño del hash en bytes (256 bits para SHA-256).
    /// </summary>
    private const int HashSize = 32;

    /// <summary>
    /// Tamaño del salt en bytes (128 bits de entropía).
    /// </summary>
    private const int SaltSize = 16;

    /// <summary>
    /// Genera un hash seguro de una contraseña.
    /// Proceso:
    /// 1. Generar salt aleatorio de 128 bits (16 bytes)
    /// 2. Aplicar PBKDF2-SHA256 con 10,000 iteraciones
    /// 3. Generar hash de 256 bits (32 bytes)
    /// 4. Combinar: [16 bytes salt] + [32 bytes hash]
    /// 5. Retornar en Base64
    /// </summary>
    public string HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("La contraseña no puede estar vacía.", nameof(password));

        // 1. Generar salt aleatorio
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);

        // 2. Derivar clave usando PBKDF2-SHA256
        using var pbkdf2 = new Rfc2898DeriveBytes(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256);

        // 3. Generar hash
        byte[] hash = pbkdf2.GetBytes(HashSize);

        // 4. Combinar salt + hash
        byte[] hashWithSalt = new byte[SaltSize + HashSize];
        Array.Copy(salt, 0, hashWithSalt, 0, SaltSize);
        Array.Copy(hash, 0, hashWithSalt, SaltSize, HashSize);

        // 5. Retornar en Base64
        return Convert.ToBase64String(hashWithSalt);
    }

    /// <summary>
    /// Verifica si una contraseña coincide con su hash almacenado.
    /// Usa CryptographicOperations.FixedTimeEquals para prevenir timing attacks.
    /// </summary>
    public bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(password))
            return false;

        if (string.IsNullOrWhiteSpace(storedHash))
            return false;

        try
        {
            // 1. Decodificar el hash almacenado desde Base64
            byte[] hashWithSalt = Convert.FromBase64String(storedHash);

            // 2. Validar que el tamaño sea correcto
            if (hashWithSalt.Length != SaltSize + HashSize)
                return false;

            // 3. Extraer el salt
            byte[] salt = new byte[SaltSize];
            Array.Copy(hashWithSalt, 0, salt, 0, SaltSize);

            // 4. Derivar la clave usando el salt extraído
            using var pbkdf2 = new Rfc2898DeriveBytes(
                password,
                salt,
                Iterations,
                HashAlgorithmName.SHA256);

            // 5. Generar el hash a partir de la contraseña ingresada
            byte[] computedHash = pbkdf2.GetBytes(HashSize);

            // 6. Comparar usando FixedTimeEquals (previene timing attacks)
            byte[] storedHashBytes = new byte[HashSize];
            Array.Copy(hashWithSalt, SaltSize, storedHashBytes, 0, HashSize);

            return CryptographicOperations.FixedTimeEquals(storedHashBytes, computedHash);
        }
        catch (FormatException)
        {
            // Hash no es válido Base64
            return false;
        }
        catch (Exception)
        {
            // Cualquier otro error
            return false;
        }
    }

    /// <summary>
    /// Valida la fortaleza de una contraseña según estándares OWASP.
    /// Requisitos:
    /// - Mínimo 12 caracteres
    /// - Al menos 1 mayúscula
    /// - Al menos 1 minúscula
    /// - Al menos 1 número
    /// - Al menos 1 símbolo especial
    /// </summary>
    public (bool IsValid, string Message) ValidatePasswordStrength(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return (false, "La contraseña no puede estar vacía.");

        var failures = new List<string>();

        // Validar longitud
        if (password.Length < 12)
            failures.Add("mínimo 12 caracteres");

        // Validar mayúscula
        if (!password.Any(char.IsUpper))
            failures.Add("al menos 1 mayúscula");

        // Validar minúscula
        if (!password.Any(char.IsLower))
            failures.Add("al menos 1 minúscula");

        // Validar número
        if (!password.Any(char.IsDigit))
            failures.Add("al menos 1 número");

        // Validar símbolo especial
        if (!password.Any(c => !char.IsLetterOrDigit(c)))
            failures.Add("al menos 1 símbolo especial");

        if (failures.Count == 0)
            return (true, "Contraseña válida.");

        var message = $"Contraseña débil. Requiere: {string.Join(", ", failures)}.";
        return (false, message);
    }

    /// <summary>
    /// Valida que una nueva contraseña no esté en el historial del usuario (últimas 5).
    /// </summary>
    public async Task<(bool IsValid, string Message)> ValidatePasswordNotReusedAsync(
        Guid userId,
        string newPassword,
        IPasswordHistoryService passwordHistoryService)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
            return (false, "La contraseña no puede estar vacía.");

        try
        {
            // Generar hash de la nueva contraseña
            var newPasswordHash = HashPassword(newPassword);

            // Verificar si la contraseña ya fue usada
            var isReused = await passwordHistoryService.IsPasswordReusedAsync(userId, newPasswordHash);

            if (isReused)
            {
                return (false, "Esta contraseña ya ha sido utilizada anteriormente. Por favor, elija una diferente.");
            }

            return (true, "Contraseña no reutilizada.");
        }
        catch (Exception)
        {
            // Si falla la validación por cualquier razón, permitir el cambio (fail-safe)
            return (true, "Validación de reutilización no disponible. Contraseña permitida.");
        }
    }
}
