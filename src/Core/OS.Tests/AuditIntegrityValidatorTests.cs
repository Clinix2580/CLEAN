using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using OS.Application.Interfaces;
using OS.Infrastructure.Data;
using OS.Infrastructure.Security;
using Xunit;

namespace OS.Tests;

public class AuditIntegrityValidatorTests : IDisposable
{
    private readonly AuditDbContext _dbContext;
    private readonly AuditIntegrityValidator _validator;

    public AuditIntegrityValidatorTests()
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AuditDbContext(options);
        _dbContext.Database.EnsureCreated();

        var auditServiceMock = new Mock<IAuditService>();
        _validator = new AuditIntegrityValidator(_dbContext, auditServiceMock.Object);
    }

    [Fact]
    public async Task ValidateAuditLogChainAsync_ShouldReturnTrueForValidChain()
    {
        var now = DateTime.UtcNow;

        var firstLog = new AuditLogEntry
        {
            Id = 1,
            TableName = "patients",
            Operation = "INSERT",
            UserId = "SYSTEM",
            RecordId = "1",
            OldValues = null,
            NewValues = "{\"name\":\"Ana\"}",
            ChangedAtUtc = now,
            IpAddress = "127.0.0.1",
            PreviousHash = string.Empty
        };

        firstLog.VerificationHash = _validator.CalculateAuditLogHash(
            firstLog.Id,
            firstLog.TableName,
            firstLog.Operation,
            firstLog.RecordId,
            firstLog.UserId,
            firstLog.OldValues,
            firstLog.NewValues,
            firstLog.ChangedAtUtc,
            firstLog.PreviousHash,
            firstLog.IpAddress);

        var secondLog = new AuditLogEntry
        {
            Id = 2,
            TableName = "patients",
            Operation = "UPDATE",
            UserId = "SYSTEM",
            RecordId = "1",
            OldValues = "{\"name\":\"Ana\"}",
            NewValues = "{\"name\":\"Ana María\"}",
            ChangedAtUtc = now.AddMinutes(1),
            IpAddress = "127.0.0.1",
            PreviousHash = firstLog.VerificationHash
        };

        secondLog.VerificationHash = _validator.CalculateAuditLogHash(
            secondLog.Id,
            secondLog.TableName,
            secondLog.Operation,
            secondLog.RecordId,
            secondLog.UserId,
            secondLog.OldValues,
            secondLog.NewValues,
            secondLog.ChangedAtUtc,
            secondLog.PreviousHash,
            secondLog.IpAddress);

        _dbContext.AuditLogs.AddRange(firstLog, secondLog);
        await _dbContext.SaveChangesAsync();

        var result = await _validator.ValidateAuditLogChainAsync();

        Assert.True(result.IsIntegrityValid);
        Assert.Equal(2, result.TotalRecords);
        Assert.Equal(2, result.ValidRecords);
        Assert.Equal(0, result.InvalidRecords);
    }

    [Fact]
    public async Task ValidateAuditLogChainAsync_ShouldReturnFalseForTamperedChain()
    {
        var now = DateTime.UtcNow;

        var firstLog = new AuditLogEntry
        {
            Id = 1,
            TableName = "patients",
            Operation = "INSERT",
            UserId = "SYSTEM",
            RecordId = "1",
            OldValues = null,
            NewValues = "{\"name\":\"Ana\"}",
            ChangedAtUtc = now,
            IpAddress = "127.0.0.1",
            PreviousHash = string.Empty
        };

        firstLog.VerificationHash = _validator.CalculateAuditLogHash(
            firstLog.Id,
            firstLog.TableName,
            firstLog.Operation,
            firstLog.RecordId,
            firstLog.UserId,
            firstLog.OldValues,
            firstLog.NewValues,
            firstLog.ChangedAtUtc,
            firstLog.PreviousHash,
            firstLog.IpAddress);

        var secondLog = new AuditLogEntry
        {
            Id = 2,
            TableName = "patients",
            Operation = "UPDATE",
            UserId = "SYSTEM",
            RecordId = "1",
            OldValues = "{\"name\":\"Ana\"}",
            NewValues = "{\"name\":\"Ana María\"}",
            ChangedAtUtc = now.AddMinutes(1),
            IpAddress = "127.0.0.1",
            PreviousHash = firstLog.VerificationHash
        };

        secondLog.VerificationHash = _validator.CalculateAuditLogHash(
            secondLog.Id,
            secondLog.TableName,
            secondLog.Operation,
            secondLog.RecordId,
            secondLog.UserId,
            secondLog.OldValues,
            secondLog.NewValues,
            secondLog.ChangedAtUtc,
            secondLog.PreviousHash,
            secondLog.IpAddress);

        _dbContext.AuditLogs.AddRange(firstLog, secondLog);
        await _dbContext.SaveChangesAsync();

        // Tamper with the second log after the chain was built.
        secondLog.NewValues = "{\"name\":\"Ana Maria Tampered\"}";
        await _dbContext.SaveChangesAsync();

        var result = await _validator.ValidateAuditLogChainAsync();

        Assert.False(result.IsIntegrityValid);
        Assert.Equal(2, result.TotalRecords);
        Assert.Equal(1, result.ValidRecords);
        Assert.Equal(1, result.InvalidRecords);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
