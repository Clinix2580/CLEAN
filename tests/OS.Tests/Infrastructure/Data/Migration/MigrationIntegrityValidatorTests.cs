using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OS.Infrastructure.Data.Migration;
using OS.Infrastructure.Data;

namespace OS.Tests.Infrastructure.Data.Migration;

/// <summary>
/// Tests unitarios para MigrationIntegrityValidator.
/// </summary>
public class MigrationIntegrityValidatorTests
{
    private readonly Mock<ILogger<MigrationIntegrityValidator>> _mockLogger;
    private readonly IMigrationIntegrityValidator _validator;

    public MigrationIntegrityValidatorTests()
    {
        _mockLogger = new Mock<ILogger<MigrationIntegrityValidator>>();
        _validator = new MigrationIntegrityValidator(_mockLogger.Object);
    }

    #region Tests de Checksum

    [Fact]
    public async Task CalculateSourceChecksum_WithValidSqliteFile_ReturnsHexString()
    {
        // Arrange
        var testFile = Path.Combine(Path.GetTempPath(), "test_db.sqlite");
        File.WriteAllBytes(testFile, new byte[] { 0x01, 0x02, 0x03, 0x04 });

        try
        {
            // Act
            var checksum = await _validator.CalculateSourceChecksumAsync(testFile);

            // Assert
            Assert.NotNull(checksum);
            Assert.NotEmpty(checksum);
            // SHA-256 produce 64 caracteres hex (256 bits / 4 bits por char)
            Assert.Equal(64, checksum.Length);
            // Validar que es válido hex
            _ = Convert.FromHexString(checksum);
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
        }
    }

    [Fact]
    public async Task CalculateSourceChecksum_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentFile = Path.Combine(Path.GetTempPath(), "nonexistent_db_12345.sqlite");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _validator.CalculateSourceChecksumAsync(nonExistentFile));
    }

    [Fact]
    public async Task CalculateSourceChecksum_SameFile_ReturnsSameChecksum()
    {
        // Arrange
        var testFile = Path.Combine(Path.GetTempPath(), "test_db_consistency.sqlite");
        var testData = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        File.WriteAllBytes(testFile, testData);

        try
        {
            // Act
            var checksum1 = await _validator.CalculateSourceChecksumAsync(testFile);
            var checksum2 = await _validator.CalculateSourceChecksumAsync(testFile);

            // Assert
            Assert.Equal(checksum1, checksum2);
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
        }
    }

    [Fact]
    public async Task CalculateSourceChecksum_DifferentFiles_ReturnsDifferentChecksums()
    {
        // Arrange
        var testFile1 = Path.Combine(Path.GetTempPath(), "test_db_1.sqlite");
        var testFile2 = Path.Combine(Path.GetTempPath(), "test_db_2.sqlite");
        File.WriteAllBytes(testFile1, new byte[] { 0x01, 0x02, 0x03 });
        File.WriteAllBytes(testFile2, new byte[] { 0x04, 0x05, 0x06 });

        try
        {
            // Act
            var checksum1 = await _validator.CalculateSourceChecksumAsync(testFile1);
            var checksum2 = await _validator.CalculateSourceChecksumAsync(testFile2);

            // Assert
            Assert.NotEqual(checksum1, checksum2);
        }
        finally
        {
            if (File.Exists(testFile1))
                File.Delete(testFile1);
            if (File.Exists(testFile2))
                File.Delete(testFile2);
        }
    }

    #endregion

    #region Tests de Validación

    [Fact]
    public async Task ValidateMigrationAsync_WithMissingSourceDatabase_ReturnsFailed()
    {
        // Arrange
        var nonExistentFile = Path.Combine(Path.GetTempPath(), "nonexistent_db_99999.sqlite");
        var mockServiceProvider = new Mock<IServiceProvider>();

        // Act
        var report = await _validator.ValidateMigrationAsync(nonExistentFile, mockServiceProvider.Object);

        // Assert
        Assert.NotNull(report);
        Assert.False(report.IsValid);
        Assert.NotEmpty(report.ValidationErrors);
    }

    [Fact]
    public async Task ValidateMigrationAsync_ReturnsReportWithTimestamp()
    {
        // Arrange
        var testFile = Path.Combine(Path.GetTempPath(), "test_db_timestamp.sqlite");
        File.WriteAllBytes(testFile, new byte[] { 0x01, 0x02 });

        var mockServiceProvider = new Mock<IServiceProvider>();

        try
        {
            // Act
            var beforeTime = DateTime.UtcNow;
            var report = await _validator.ValidateMigrationAsync(testFile, mockServiceProvider.Object);
            var afterTime = DateTime.UtcNow;

            // Assert
            Assert.NotNull(report);
            Assert.True(report.ValidatedAtUtc >= beforeTime);
            Assert.True(report.ValidatedAtUtc <= afterTime);
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
        }
    }

    [Fact]
    public async Task ValidateMigrationAsync_ReturnsReportWithDuration()
    {
        // Arrange
        var testFile = Path.Combine(Path.GetTempPath(), "test_db_duration.sqlite");
        File.WriteAllBytes(testFile, new byte[] { 0x01, 0x02 });

        var mockServiceProvider = new Mock<IServiceProvider>();

        try
        {
            // Act
            var report = await _validator.ValidateMigrationAsync(testFile, mockServiceProvider.Object);

            // Assert
            Assert.NotNull(report);
            Assert.True(report.ValidationDuration.TotalMilliseconds >= 0);
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
        }
    }

    #endregion

    #region Tests de Reporte

    [Fact]
    public async Task ValidateMigrationAsync_GeneratesNonEmptyReport()
    {
        // Arrange
        var testFile = Path.Combine(Path.GetTempPath(), "test_db_report.sqlite");
        File.WriteAllBytes(testFile, new byte[] { 0x01, 0x02, 0x03 });

        var mockServiceProvider = new Mock<IServiceProvider>();

        try
        {
            // Act
            var report = await _validator.ValidateMigrationAsync(testFile, mockServiceProvider.Object);

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.SourceChecksum);
            Assert.NotEmpty(report.SourceChecksum);
            Assert.NotNull(report.ValidationErrors);
            Assert.NotNull(report.ValidationWarnings);
            Assert.NotNull(report.EntitiesCounts);
            Assert.True(report.ValidationDuration.TotalMilliseconds >= 0);
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
        }
    }

    [Fact]
    public async Task ValidateMigrationAsync_ReportContainsSummary()
    {
        // Arrange
        var testFile = Path.Combine(Path.GetTempPath(), "test_db_summary.sqlite");
        File.WriteAllBytes(testFile, new byte[] { 0x01, 0x02 });

        var mockServiceProvider = new Mock<IServiceProvider>();

        try
        {
            // Act
            var report = await _validator.ValidateMigrationAsync(testFile, mockServiceProvider.Object);

            // Assert
            Assert.NotNull(report.Summary);
            Assert.NotEmpty(report.Summary);
            Assert.Contains("Validación", report.Summary);
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
        }
    }

    #endregion

    #region Tests de EntityMigrationStatus

    [Fact]
    public void EntityMigrationStatus_MatchingCounts_IsMatchTrue()
    {
        // Arrange
        var status = new EntityMigrationStatus
        {
            EntityName = "Users",
            SourceCount = 100,
            TargetCount = 100
        };

        // Act & Assert
        Assert.True(status.IsMatch);
        Assert.Equal(0, status.Difference);
        Assert.Equal("✓", status.Status);
    }

    [Fact]
    public void EntityMigrationStatus_DifferentCounts_IsMatchFalse()
    {
        // Arrange
        var status = new EntityMigrationStatus
        {
            EntityName = "Users",
            SourceCount = 100,
            TargetCount = 95
        };

        // Act & Assert
        Assert.False(status.IsMatch);
        Assert.Equal(5, status.Difference);
        Assert.Equal("✗", status.Status);
    }

    #endregion

    #region Tests de Manejo de Excepciones

    [Fact]
    public async Task ValidateMigrationAsync_WithException_CatchesAndReturnsFailedReport()
    {
        // Arrange
        var testFile = Path.Combine(Path.GetTempPath(), "test_db_exception.sqlite");
        File.WriteAllBytes(testFile, new byte[] { 0x01, 0x02 });

        var mockServiceProvider = new Mock<IServiceProvider>();
        mockServiceProvider
            .Setup(sp => sp.CreateScope())
            .Throws(new InvalidOperationException("Mock error"));

        try
        {
            // Act
            var report = await _validator.ValidateMigrationAsync(testFile, mockServiceProvider.Object);

            // Assert
            Assert.NotNull(report);
            Assert.False(report.IsValid);
            Assert.NotEmpty(report.ValidationErrors);
            Assert.True(report.ValidationErrors.Any(e => e.Contains("excepción") || e.Contains("Exception")));
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
        }
    }

    #endregion

    #region Tests de Logging

    [Fact]
    public async Task ValidateMigrationAsync_LogsInformationMessages()
    {
        // Arrange
        var testFile = Path.Combine(Path.GetTempPath(), "test_db_logging.sqlite");
        File.WriteAllBytes(testFile, new byte[] { 0x01, 0x02 });

        var mockServiceProvider = new Mock<IServiceProvider>();

        try
        {
            // Act
            _ = await _validator.ValidateMigrationAsync(testFile, mockServiceProvider.Object);

            // Assert
            // Verificar que se llamó a Log (al menos una vez)
            _mockLogger.Verify(
                x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
        }
    }

    #endregion
}
