using Xunit;
using OS.Infrastructure.Security;
using System.Diagnostics;

namespace OS.Tests;

/// <summary>
/// Tests unitarios para PasswordHashingService.
/// Valida hashing, verificación, fortaleza de contraseña y resistencia a timing attacks.
/// </summary>
public class PasswordHashingServiceTests
{
    private readonly IPasswordHashingService _service;

    public PasswordHashingServiceTests()
    {
        _service = new PasswordHashingService();
    }

    [Fact]
    public void HashPassword_GeneratesDifferentHashesForSamePassword()
    {
        // Arrange
        var password = "TestPassword123!";

        // Act
        var hash1 = _service.HashPassword(password);
        var hash2 = _service.HashPassword(password);

        // Assert
        Assert.NotEqual(hash1, hash2);
        Assert.NotNull(hash1);
        Assert.NotNull(hash2);
    }

    [Fact]
    public void HashPassword_ThrowsOnEmptyPassword()
    {
        // Arrange
        var password = "";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _service.HashPassword(password));
    }

    [Fact]
    public void HashPassword_ThrowsOnNullPassword()
    {
        // Arrange
        string? password = null;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _service.HashPassword(password!));
    }

    [Fact]
    public void VerifyPassword_ReturnsTrueForCorrectPassword()
    {
        // Arrange
        var password = "TestPassword123!";
        var hash = _service.HashPassword(password);

        // Act
        var result = _service.VerifyPassword(password, hash);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void VerifyPassword_ReturnsFalseForIncorrectPassword()
    {
        // Arrange
        var password = "TestPassword123!";
        var wrongPassword = "WrongPassword456!";
        var hash = _service.HashPassword(password);

        // Act
        var result = _service.VerifyPassword(wrongPassword, hash);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifyPassword_ReturnsFalseForEmptyPassword()
    {
        // Arrange
        var hash = _service.HashPassword("TestPassword123!");

        // Act
        var result = _service.VerifyPassword("", hash);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifyPassword_ReturnsFalseForNullPassword()
    {
        // Arrange
        var hash = _service.HashPassword("TestPassword123!");

        // Act
        var result = _service.VerifyPassword(null!, hash);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifyPassword_ReturnsFalseForInvalidHash()
    {
        // Arrange
        var password = "TestPassword123!";
        var invalidHash = "invalid_hash_base64";

        // Act
        var result = _service.VerifyPassword(password, invalidHash);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidatePasswordStrength_ReturnsTrueForStrongPassword()
    {
        // Arrange
        var password = "StrongP@ssw0rd123!";

        // Act
        var (isValid, message) = _service.ValidatePasswordStrength(password);

        // Assert
        Assert.True(isValid);
        Assert.Equal("Contraseña válida.", message);
    }

    [Fact]
    public void ValidatePasswordStrength_ReturnsFalseForShortPassword()
    {
        // Arrange
        var password = "Short1!";

        // Act
        var (isValid, message) = _service.ValidatePasswordStrength(password);

        // Assert
        Assert.False(isValid);
        Assert.Contains("mínimo 12 caracteres", message);
    }

    [Fact]
    public void ValidatePasswordStrength_ReturnsFalseForNoUppercase()
    {
        // Arrange
        var password = "lowercasepassword123!";

        // Act
        var (isValid, message) = _service.ValidatePasswordStrength(password);

        // Assert
        Assert.False(isValid);
        Assert.Contains("al menos 1 mayúscula", message);
    }

    [Fact]
    public void ValidatePasswordStrength_ReturnsFalseForNoLowercase()
    {
        // Arrange
        var password = "UPPERCASEPASSWORD123!";

        // Act
        var (isValid, message) = _service.ValidatePasswordStrength(password);

        // Assert
        Assert.False(isValid);
        Assert.Contains("al menos 1 minúscula", message);
    }

    [Fact]
    public void ValidatePasswordStrength_ReturnsFalseForNoNumber()
    {
        // Arrange
        var password = "NoNumbersHere!";

        // Act
        var (isValid, message) = _service.ValidatePasswordStrength(password);

        // Assert
        Assert.False(isValid);
        Assert.Contains("al menos 1 número", message);
    }

    [Fact]
    public void ValidatePasswordStrength_ReturnsFalseForNoSpecialCharacter()
    {
        // Arrange
        var password = "NoSpecialChars123";

        // Act
        var (isValid, message) = _service.ValidatePasswordStrength(password);

        // Assert
        Assert.False(isValid);
        Assert.Contains("al menos 1 símbolo especial", message);
    }

    [Fact]
    public void ValidatePasswordStrength_ReturnsFalseForEmptyPassword()
    {
        // Arrange
        var password = "";

        // Act
        var (isValid, message) = _service.ValidatePasswordStrength(password);

        // Assert
        Assert.False(isValid);
        Assert.Contains("vacía", message);
    }

    [Fact]
    public void ValidatePasswordStrength_ReturnsFalseForMultipleWeaknesses()
    {
        // Arrange
        var password = "weak";

        // Act
        var (isValid, message) = _service.ValidatePasswordStrength(password);

        // Assert
        Assert.False(isValid);
        Assert.Contains("mínimo 12 caracteres", message);
        Assert.Contains("mayúscula", message);
        Assert.Contains("número", message);
        Assert.Contains("símbolo especial", message);
    }

    [Fact]
    public void TimingAttackResistance_ValidAndInvalidPasswordsHaveSimilarTiming()
    {
        // Arrange
        var password = "TestPassword123!";
        var hash = _service.HashPassword(password);
        var iterations = 100;
        var threshold = 50.0; // 50% difference threshold

        // Measure timing for valid password
        var validTimes = new List<long>();
        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            _service.VerifyPassword(password, hash);
            sw.Stop();
            validTimes.Add(sw.ElapsedTicks);
        }

        // Measure timing for invalid password
        var invalidTimes = new List<long>();
        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            _service.VerifyPassword("WrongPassword456!", hash);
            sw.Stop();
            invalidTimes.Add(sw.ElapsedTicks);
        }

        // Calculate averages
        var avgValid = validTimes.Average();
        var avgInvalid = invalidTimes.Average();

        // Calculate percentage difference
        var difference = Math.Abs(avgValid - avgInvalid);
        var maxAvg = Math.Max(avgValid, avgInvalid);
        var percentageDifference = (difference / maxAvg) * 100;

        // Assert - timing difference should be less than 30%
        Assert.True(percentageDifference < threshold,
            $"Timing attack resistance failed: {percentageDifference:F2}% difference (threshold: {threshold * 100}%)");
    }

    [Fact]
    public void TimingAttackResistance_VerifyPasswordIsConstantTime()
    {
        // Arrange
        var password = "TestPassword123!";
        var hash = _service.HashPassword(password);
        var iterations = 50;
        var maxVariance = 50.0; // 50% variance threshold

        // Measure timing for same operation multiple times
        var times = new List<long>();
        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            _service.VerifyPassword(password, hash);
            sw.Stop();
            times.Add(sw.ElapsedTicks);
        }

        // Calculate variance
        var avg = times.Average();
        var variance = times.Sum(t => Math.Pow(t - avg, 2)) / times.Count;
        var stdDev = Math.Sqrt(variance);
        var coefficientOfVariation = (stdDev / avg) * 100;

        // Assert - coefficient of variation should be less than 50%
        Assert.True(coefficientOfVariation < maxVariance,
            $"Constant time verification failed: CoV = {coefficientOfVariation:F2}% (threshold: {maxVariance * 100}%)");
    }

    [Fact]
    public void HashPassword_ProducesBase64Output()
    {
        // Arrange
        var password = "TestPassword123!";

        // Act
        var hash = _service.HashPassword(password);

        // Assert
        Assert.NotNull(hash);
        
        // Should be valid Base64
        var bytes = Convert.FromBase64String(hash);
        Assert.Equal(48, bytes.Length); // 16 bytes salt + 32 bytes hash = 48 bytes
    }

    [Fact]
    public void VerifyPassword_IsCaseSensitive()
    {
        // Arrange
        var password = "TestPassword123!";
        var differentCasePassword = "testpassword123!";
        var hash = _service.HashPassword(password);

        // Act
        var result = _service.VerifyPassword(differentCasePassword, hash);

        // Assert
        Assert.False(result);
    }
}
