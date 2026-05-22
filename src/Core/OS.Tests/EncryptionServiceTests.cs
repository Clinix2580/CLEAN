using FluentAssertions;
using OS.Domain.Interfaces;
using OS.Infrastructure.Security;
using Xunit;

namespace OS.Tests.Security;

public class EncryptionServiceTests
{
    private readonly HardwareId _hardwareId;
    private readonly IEncryptionService _encryptionService;

    public EncryptionServiceTests()
    {
        // Create a test HardwareId with known values
        _hardwareId = new HardwareId("TEST_CPU_ID", "TEST_MOTHERBOARD_ID");
        _encryptionService = new EncryptionService(_hardwareId);
    }

    [Fact]
    public void Encrypt_ShouldEncryptPlainTextAndDecryptShouldReturnOriginal()
    {
        // Arrange
        var plainText = "Hello, World! This is a test message.";

        // Act
        var encrypted = _encryptionService.Encrypt(plainText);
        var decrypted = _encryptionService.Decrypt(encrypted);

        // Assert
        encrypted.Should().NotBeNullOrEmpty();
        encrypted.Should().NotBe(plainText);
        decrypted.Should().Be(plainText);
    }

    [Fact]
    public void EncryptBytes_ShouldEncryptDataAndDecryptBytesShouldReturnOriginal()
    {
        // Arrange
        var originalData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        // Act
        var encrypted = _encryptionService.EncryptBytes(originalData);
        var decrypted = _encryptionService.DecryptBytes(encrypted);

        // Assert
        encrypted.Should().NotBeNull();
        encrypted.Length.Should().BeGreaterThan(originalData.Length); // IV is prepended
        decrypted.Should().BeEquivalentTo(originalData);
    }

    [Fact]
    public void Encrypt_ShouldThrowArgumentException_WhenPlainTextIsNullOrEmpty()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _encryptionService.Encrypt(null!));
        Assert.Throws<ArgumentException>(() => _encryptionService.Encrypt(string.Empty));
    }

    [Fact]
    public void Decrypt_ShouldThrowArgumentException_WhenCipherTextIsNullOrEmpty()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _encryptionService.Decrypt(null!));
        Assert.Throws<ArgumentException>(() => _encryptionService.Decrypt(string.Empty));
    }

    [Fact]
    public void Decrypt_ShouldThrowInvalidOperationException_WhenCipherTextIsInvalidBase64()
    {
        // Arrange
        var invalidCipherText = "invalid-base64-string!@#";

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _encryptionService.Decrypt(invalidCipherText));
    }

    [Fact]
    public void EncryptBytes_ShouldThrowArgumentException_WhenDataIsNullOrEmpty()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _encryptionService.EncryptBytes(null!));
        Assert.Throws<ArgumentException>(() => _encryptionService.EncryptBytes(Array.Empty<byte>()));
    }

    [Fact]
    public void DecryptBytes_ShouldThrowArgumentException_WhenDataIsNullOrTooShort()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _encryptionService.DecryptBytes(null!));
        Assert.Throws<ArgumentException>(() => _encryptionService.DecryptBytes(new byte[15])); // Less than IV size
    }
}