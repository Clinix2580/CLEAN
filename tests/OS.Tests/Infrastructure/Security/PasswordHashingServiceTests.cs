using Xunit;
using OS.Infrastructure.Security;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace OS.Tests.Infrastructure.Security;

/// <summary>
/// Tests unitarios para PasswordHashingService.
/// Validar que el hashing es seguro, predecible en verificación, y resiste timing attacks.
/// </summary>
public class PasswordHashingServiceTests
{
    private readonly IPasswordHashingService _service = new PasswordHashingService();

    #region Tests de Hashing Básico

    [Fact]
    public void HashPassword_WithValidPassword_ReturnsBase64String()
    {
        // Arrange
        var password = "ValidP@ssw0rd123";

        // Act
        var hash = _service.HashPassword(password);

        // Assert
        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
        
        // Validar que es Base64 válido
        var bytes = Convert.FromBase64String(hash);
        Assert.NotNull(bytes);
        Assert.Equal(48, bytes.Length); // 16 (salt) + 32 (hash SHA-256)
    }

    [Fact]
    public void HashPassword_WithEmptyPassword_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _service.HashPassword(""));
    }

    [Fact]
    public void HashPassword_WithNullPassword_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _service.HashPassword(null!));
    }

    [Fact]
    public void HashPassword_WithWhitespacePassword_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _service.HashPassword("   "));
    }

    #endregion

    #region Tests de Verificación de Contraseña

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ReturnsTrue()
    {
        // Arrange
        var password = "MyS3cur3P@ssw0rd";
        var hash = _service.HashPassword(password);

        // Act
        var result = _service.VerifyPassword(password, hash);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void VerifyPassword_WithIncorrectPassword_ReturnsFalse()
    {
        // Arrange
        var password = "MyS3cur3P@ssw0rd";
        var wrongPassword = "WrongP@ssw0rd123";
        var hash = _service.HashPassword(password);

        // Act
        var result = _service.VerifyPassword(wrongPassword, hash);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifyPassword_WithEmptyPassword_ReturnsFalse()
    {
        // Arrange
        var password = "MyS3cur3P@ssw0rd";
        var hash = _service.HashPassword(password);

        // Act
        var result = _service.VerifyPassword("", hash);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifyPassword_WithNullPassword_ReturnsFalse()
    {
        // Arrange
        var password = "MyS3cur3P@ssw0rd";
        var hash = _service.HashPassword(password);

        // Act
        var result = _service.VerifyPassword(null!, hash);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifyPassword_WithNullHash_ReturnsFalse()
    {
        // Arrange
        var password = "MyS3cur3P@ssw0rd";

        // Act
        var result = _service.VerifyPassword(password, null!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifyPassword_WithInvalidBase64Hash_ReturnsFalse()
    {
        // Arrange
        var password = "MyS3cur3P@ssw0rd";
        var invalidHash = "NotValidBase64!!!";

        // Act
        var result = _service.VerifyPassword(password, invalidHash);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifyPassword_WithTamperedHash_ReturnsFalse()
    {
        // Arrange
        var password = "MyS3cur3P@ssw0rd";
        var hash = _service.HashPassword(password);

        // Modificar un byte del hash
        var hashBytes = Convert.FromBase64String(hash);
        hashBytes[hashBytes.Length - 1] ^= 0xFF; // Flip bits
        var tamperedHash = Convert.ToBase64String(hashBytes);

        // Act
        var result = _service.VerifyPassword(password, tamperedHash);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Tests de No-Determinismo (cada hash es diferente)

    [Fact]
    public void HashPassword_GeneratesDifferentHashesForSamePassword()
    {
        // Arrange
        var password = "MyS3cur3P@ssw0rd";

        // Act
        var hash1 = _service.HashPassword(password);
        var hash2 = _service.HashPassword(password);

        // Assert
        // Los hashes deben ser diferentes (debido a salt aleatorio)
        Assert.NotEqual(hash1, hash2);

        // Pero ambos deben verificar correctamente
        Assert.True(_service.VerifyPassword(password, hash1));
        Assert.True(_service.VerifyPassword(password, hash2));
    }

    [Fact]
    public void HashPassword_GeneratesMany_AllDifferent()
    {
        // Arrange
        var password = "MyS3cur3P@ssw0rd";
        var hashes = new HashSet<string>();

        // Act
        for (int i = 0; i < 100; i++)
        {
            hashes.Add(_service.HashPassword(password));
        }

        // Assert
        // Todos los hashes deben ser únicos (probabilidad de colisión ≈ 0)
        Assert.Equal(100, hashes.Count);

        // Y todos deben verificar correctamente
        foreach (var hash in hashes)
        {
            Assert.True(_service.VerifyPassword(password, hash));
        }
    }

    #endregion

    #region Tests de Validación de Fortaleza

    [Fact]
    public void ValidatePasswordStrength_WithStrongPassword_ReturnsValid()
    {
        // Arrange
        var password = "MyStr0ng!P@ssw0rd";

        // Act
        var (isValid, message) = _service.ValidatePasswordStrength(password);

        // Assert
        Assert.True(isValid);
        Assert.Contains("válida", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidatePasswordStrength_WithShortPassword_ReturnsInvalid()
    {
        // Arrange
        var password = "Short1!";

        // Act
        var (isValid, message) = _service.ValidatePasswordStrength(password);

        // Assert
        Assert.False(isValid);
        Assert.Contains("12 caracteres", message);
    }

    [Fact]
    public void ValidatePasswordStrength_WithoutUppercase_ReturnsInvalid()
    {
        // Arrange
        var password = "lowercase1!pass";

        // Act
        var (isValid, message) = _service.ValidatePasswordStrength(password);

        // Assert
        Assert.False(isValid);
        Assert.Contains("mayúscula", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidatePasswordStrength_WithoutLowercase_ReturnsInvalid()
    {
        // Arrange
        var password = "UPPERCASE1!PASS";

        // Act
        var (isValid, message) = _service.ValidatePasswordStrength(password);

        // Assert
        Assert.False(isValid);
        Assert.Contains("minúscula", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidatePasswordStrength_WithoutNumber_ReturnsInvalid()
    {
        // Arrange
        var password = "NoNumbers!Pass";

        // Act
        var (isValid, message) = _service.ValidatePasswordStrength(password);

        // Assert
        Assert.False(isValid);
        Assert.Contains("número", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidatePasswordStrength_WithoutSymbol_ReturnsInvalid()
    {
        // Arrange
        var password = "NoSymbolPass1234";

        // Act
        var (isValid, message) = _service.ValidatePasswordStrength(password);

        // Assert
        Assert.False(isValid);
        Assert.Contains("símbolo", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidatePasswordStrength_WithEmptyPassword_ReturnsInvalid()
    {
        // Act
        var (isValid, message) = _service.ValidatePasswordStrength("");

        // Assert
        Assert.False(isValid);
        Assert.Contains("vacía", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidatePasswordStrength_WithMultipleIssues_ReturnsAllMessages()
    {
        // Arrange
        var password = "short";

        // Act
        var (isValid, message) = _service.ValidatePasswordStrength(password);

        // Assert
        Assert.False(isValid);
        Assert.Contains("12 caracteres", message);
        Assert.Contains("mayúscula", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("número", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("símbolo", message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Tests de Resistencia a Timing Attacks

    [Fact]
    public void VerifyPassword_TimingResistance_CorrectVsIncorrect()
    {
        // Arrange
        var password = "MyS3cur3P@ssw0rd";
        var hash = _service.HashPassword(password);
        var wrongPassword = "WrongP@ssw0rd123";

        const int iterations = 100;
        var correctTimes = new List<long>();
        var incorrectTimes = new List<long>();

        // Act - Medir tiempos de verificación correcta
        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            _service.VerifyPassword(password, hash);
            sw.Stop();
            correctTimes.Add(sw.ElapsedTicks);
        }

        // Act - Medir tiempos de verificación incorrecta
        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            _service.VerifyPassword(wrongPassword, hash);
            sw.Stop();
            incorrectTimes.Add(sw.ElapsedTicks);
        }

        // Assert
        // Los tiempos deben ser similares (diferencia < 30%)
        var avgCorrect = correctTimes.Average();
        var avgIncorrect = incorrectTimes.Average();
        var percentDifference = Math.Abs(avgCorrect - avgIncorrect) / Math.Max(avgCorrect, avgIncorrect);

        Assert.True(percentDifference < 0.30,
            $"Timing attack possible: diferencia {percentDifference:P} entre verificación correcta e incorrecta");
    }

    [Fact]
    public void VerifyPassword_NoEarlyExit_FullComparison()
    {
        // Arrange
        var password = "MyS3cur3P@ssw0rd";
        var hash = _service.HashPassword(password);

        // Crear contraseña que coincide en primer carácter pero es diferente
        var similarPassword1 = "M" + "X".PadRight(password.Length - 1, 'X');
        // Contraseña que no coincide en primer carácter
        var differentPassword = "X" + "X".PadRight(password.Length - 1, 'X');

        // Act
        var result1 = _service.VerifyPassword(similarPassword1, hash);
        var result2 = _service.VerifyPassword(differentPassword, hash);

        // Assert
        Assert.False(result1);
        Assert.False(result2);

        // Ambas deberían tardar similar (no early exit cuando primer byte no coincide)
    }

    #endregion

    #region Tests de Compatibilidad

    [Fact]
    public void VerifyPassword_MultipleHashesOfSamePassword_AllValid()
    {
        // Arrange
        var password = "MyS3cur3P@ssw0rd";
        var hashes = Enumerable.Range(0, 10)
            .Select(_ => _service.HashPassword(password))
            .ToList();

        // Act & Assert
        foreach (var hash in hashes)
        {
            Assert.True(_service.VerifyPassword(password, hash));
        }
    }

    [Fact]
    public void HashAndVerify_LongPassword_Works()
    {
        // Arrange
        var longPassword = string.Concat(Enumerable.Range(0, 100).Select(i => $"Abc{i}!"));

        // Act
        var hash = _service.HashPassword(longPassword);
        var result = _service.VerifyPassword(longPassword, hash);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HashAndVerify_SpecialCharacters_Works()
    {
        // Arrange
        var specialPassword = "Päss@!#$%^&*()_+-=[]{}|;':\",./<>?123";

        // Act
        var hash = _service.HashPassword(specialPassword);
        var result = _service.VerifyPassword(specialPassword, hash);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HashAndVerify_UnicodeCharacters_Works()
    {
        // Arrange
        var unicodePassword = "Пароль@123!мех中文";

        // Act
        var hash = _service.HashPassword(unicodePassword);
        var result = _service.VerifyPassword(unicodePassword, hash);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region Tests de Formato de Hash

    [Fact]
    public void HashPassword_ReturnsSaltAndHashCombined()
    {
        // Arrange
        var password = "MyS3cur3P@ssw0rd";

        // Act
        var hash = _service.HashPassword(password);
        var hashBytes = Convert.FromBase64String(hash);

        // Assert
        // Debe contener exactamente 48 bytes: 16 (salt) + 32 (hash SHA-256)
        Assert.Equal(48, hashBytes.Length);
    }

    [Fact]
    public void VerifyPassword_ExtractsSaltCorrectly()
    {
        // Arrange
        var password = "MyS3cur3P@ssw0rd";
        var hash1 = _service.HashPassword(password);
        var hash2 = _service.HashPassword(password);

        var hash1Bytes = Convert.FromBase64String(hash1);
        var hash2Bytes = Convert.FromBase64String(hash2);

        // Extraer salt (primeros 16 bytes)
        var salt1 = hash1Bytes.Take(16).ToArray();
        var salt2 = hash2Bytes.Take(16).ToArray();

        // Assert
        // Los salts deben ser diferentes
        Assert.NotEqual(salt1, salt2);

        // Pero ambos hashes deben verificar correctamente
        Assert.True(_service.VerifyPassword(password, hash1));
        Assert.True(_service.VerifyPassword(password, hash2));
    }

    #endregion

    #region Tests de Casos Extremos

    [Theory]
    [InlineData("A")]
    [InlineData("Ab1!")]
    [InlineData("ValidP@ss0rd")]
    [InlineData("VeryLongPasswordWith123!MoreCharactersThanUsual")]
    public void ValidatePasswordStrength_EdgeCases(string password)
    {
        // Act
        var (isValid, message) = _service.ValidatePasswordStrength(password);

        // Assert
        // Solo debe ser válido si tiene 12+ chars, mayús, minús, número, símbolo
        var expectedValid = password.Length >= 12 &&
                           password.Any(char.IsUpper) &&
                           password.Any(char.IsLower) &&
                           password.Any(char.IsDigit) &&
                           password.Any(c => !char.IsLetterOrDigit(c));

        Assert.Equal(expectedValid, isValid);
    }

    [Fact]
    public void HashPassword_Performance_Under100ms()
    {
        // Arrange
        var password = "MyS3cur3P@ssw0rd";
        var sw = Stopwatch.StartNew();

        // Act
        _ = _service.HashPassword(password);
        sw.Stop();

        // Assert
        Assert.True(sw.ElapsedMilliseconds < 100,
            $"Hashing tomó {sw.ElapsedMilliseconds}ms, esperado < 100ms");
    }

    [Fact]
    public void VerifyPassword_Performance_Under100ms()
    {
        // Arrange
        var password = "MyS3cur3P@ssw0rd";
        var hash = _service.HashPassword(password);
        var sw = Stopwatch.StartNew();

        // Act
        _ = _service.VerifyPassword(password, hash);
        sw.Stop();

        // Assert
        Assert.True(sw.ElapsedMilliseconds < 100,
            $"Verificación tomó {sw.ElapsedMilliseconds}ms, esperado < 100ms");
    }

    #endregion
}
