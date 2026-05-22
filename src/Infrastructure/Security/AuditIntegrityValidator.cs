using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OS.Infrastructure.Data;
using OS.Application.Interfaces;
using System.Collections.ObjectModel;

namespace OS.Infrastructure.Security;

/// <summary>
/// Resultado de la validación de integridad de auditoría.
/// </summary>
public class AuditIntegrityValidationResult
{
    public bool IsIntegrityValid { get; set; }
    public int TotalRecords { get; set; }
    public int ValidRecords { get; set; }
    public int InvalidRecords { get; set; }
    public Collection<string> Violations { get; } = new();
    public DateTime ValidationTime { get; set; }
}

/// <summary>
/// Validador de integridad de auditoría con verificación de hash chain.
/// Garantiza que los registros de auditoría no han sido manipulados.
/// </summary>
public interface IAuditIntegrityValidator
{
    /// <summary>
    /// Valida la integridad de la cadena de hashes de audit_logs.
    /// </summary>
    Task<AuditIntegrityValidationResult> ValidateAuditLogChainAsync();

    /// <summary>
    /// Valida la integridad de la cadena de hashes de access_logs.
    /// </summary>
    Task<AuditIntegrityValidationResult> ValidateAccessLogChainAsync();

    /// <summary>
    /// Valida la integridad de todas las tablas de auditoría.
    /// </summary>
    Task<AuditIntegrityValidationResult> ValidateAllAuditTablesAsync();

    /// <summary>
    /// Calcula el hash de un registro de auditoría.
    /// </summary>
    string CalculateAuditLogHash(long id, string tableName, string operation, string? recordId, string userId, string? oldValues, string? newValues, DateTime changedAtUtc, string? previousHash, string? ipAddress = null);
}

/// <summary>
/// Implementación del validador de integridad de auditoría.
/// </summary>
public class AuditIntegrityValidator : IAuditIntegrityValidator
{
    private readonly AuditDbContext _dbContext;
    private readonly IAuditService _auditService;

    public AuditIntegrityValidator(
        AuditDbContext dbContext,
        IAuditService auditService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
    }

    /// <summary>
    /// Valida la integridad de la cadena de hashes de audit_logs.
    /// </summary>
    public async Task<AuditIntegrityValidationResult> ValidateAuditLogChainAsync()
    {
        var result = new AuditIntegrityValidationResult
        {
            ValidationTime = DateTime.UtcNow
        };

        var auditLogs = await _dbContext.AuditLogs
            .OrderBy(a => a.ChangedAtUtc)
            .ThenBy(a => a.Id)
            .ToListAsync();

        result.TotalRecords = auditLogs.Count;
        string? previousHash = null;

        foreach (var auditLog in auditLogs)
        {
            var expectedHash = CalculateAuditLogHash(
                auditLog.Id,
                auditLog.TableName,
                auditLog.Operation,
                auditLog.RecordId,
                auditLog.UserId,
                auditLog.OldValues,
                auditLog.NewValues,
                auditLog.ChangedAtUtc,
                previousHash,
                auditLog.IpAddress);

            var hasViolation = false;

            if (string.IsNullOrWhiteSpace(auditLog.VerificationHash))
            {
                result.InvalidRecords++;
                result.Violations.Add($"Audit log {auditLog.Id} no tiene hash de verificación.");
                hasViolation = true;
            }
            else if (!AreHashesEqual(auditLog.VerificationHash, expectedHash))
            {
                result.InvalidRecords++;
                result.Violations.Add($"Audit log {auditLog.Id} tiene hash de verificación inválido.");
                hasViolation = true;
            }

            if (!string.IsNullOrWhiteSpace(auditLog.PreviousHash)
                && !string.Equals(auditLog.PreviousHash, previousHash ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                result.InvalidRecords++;
                result.Violations.Add($"Audit log {auditLog.Id} tiene PreviousHash inconsistente con el registro anterior.");
                hasViolation = true;
            }

            if (!hasViolation)
            {
                result.ValidRecords++;
            }

            previousHash = expectedHash;
        }

        result.IsIntegrityValid = result.InvalidRecords == 0;
        return result;
    }

    /// <summary>
    /// Valida la integridad de la cadena de hashes de access_logs.
    /// </summary>
    public async Task<AuditIntegrityValidationResult> ValidateAccessLogChainAsync()
    {
        var result = new AuditIntegrityValidationResult
        {
            ValidationTime = DateTime.UtcNow
        };

        var accessLogs = await _dbContext.AccessLogs
            .OrderBy(a => a.AccessedAtUtc)
            .ThenBy(a => a.Id)
            .ToListAsync();

        result.TotalRecords = accessLogs.Count;
        string? previousHash = null;

        foreach (var accessLog in accessLogs)
        {
            var expectedHash = CalculateAccessLogHash(
                accessLog.Id,
                accessLog.UserId,
                accessLog.TableName,
                accessLog.Operation,
                accessLog.Success,
                accessLog.FailureReason,
                accessLog.AccessedAtUtc,
                previousHash);

            var hasViolation = false;

            if (string.IsNullOrWhiteSpace(accessLog.VerificationHash))
            {
                result.InvalidRecords++;
                result.Violations.Add($"Access log {accessLog.Id} no tiene hash de verificación.");
                hasViolation = true;
            }
            else if (!AreHashesEqual(accessLog.VerificationHash, expectedHash))
            {
                result.InvalidRecords++;
                result.Violations.Add($"Access log {accessLog.Id} tiene hash de verificación inválido.");
                hasViolation = true;
            }

            if (!string.IsNullOrWhiteSpace(accessLog.PreviousHash)
                && !string.Equals(accessLog.PreviousHash, previousHash ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                result.InvalidRecords++;
                result.Violations.Add($"Access log {accessLog.Id} tiene PreviousHash inconsistente con el registro anterior.");
                hasViolation = true;
            }

            if (!hasViolation)
            {
                result.ValidRecords++;
            }

            previousHash = expectedHash;
        }

        result.IsIntegrityValid = result.InvalidRecords == 0;
        return result;
    }

    /// <summary>
    /// Valida la integridad de todas las tablas de auditoría.
    /// </summary>
    public async Task<AuditIntegrityValidationResult> ValidateAllAuditTablesAsync()
    {
        var auditLogResult = await ValidateAuditLogChainAsync();
        var accessLogResult = await ValidateAccessLogChainAsync();

        var combinedResult = new AuditIntegrityValidationResult
        {
            ValidationTime = DateTime.UtcNow,
            TotalRecords = auditLogResult.TotalRecords + accessLogResult.TotalRecords,
            ValidRecords = auditLogResult.ValidRecords + accessLogResult.ValidRecords,
            InvalidRecords = auditLogResult.InvalidRecords + accessLogResult.InvalidRecords,
            IsIntegrityValid = auditLogResult.IsIntegrityValid && accessLogResult.IsIntegrityValid
        };

        foreach (var violation in auditLogResult.Violations)
        {
            combinedResult.Violations.Add(violation);
        }

        foreach (var violation in accessLogResult.Violations)
        {
            combinedResult.Violations.Add(violation);
        }

        return combinedResult;
    }

    /// <summary>
    /// Calcula el hash de un registro de auditoría.
    /// </summary>
    public string CalculateAuditLogHash(long id, string tableName, string operation, string? recordId, string userId, string? oldValues, string? newValues, DateTime changedAtUtc, string? previousHash, string? ipAddress = null)
    {
        var hashInput = new StringBuilder();
        hashInput.Append(id.ToString()).Append('|');
        hashInput.Append(tableName).Append('|');
        hashInput.Append(operation).Append('|');
        hashInput.Append(recordId ?? string.Empty).Append('|');
        hashInput.Append(userId).Append('|');
        hashInput.Append(oldValues ?? string.Empty).Append('|');
        hashInput.Append(newValues ?? string.Empty).Append('|');
        hashInput.Append(changedAtUtc.ToString("O")).Append('|'); // ISO 8601
        hashInput.Append(previousHash ?? string.Empty).Append('|');
        hashInput.Append(ipAddress ?? string.Empty);

        return ComputeSha256Hash(hashInput.ToString());
    }

    /// <summary>
    /// Calcula el hash de un registro de access log.
    /// </summary>
    private string CalculateAccessLogHash(long id, string userId, string resource, string action, bool granted, string? denialReason, DateTime accessedAtUtc, string? previousHash)
    {
        var hashInput = new StringBuilder();
        hashInput.Append(id.ToString());
        hashInput.Append(userId);
        hashInput.Append(resource);
        hashInput.Append(action);
        hashInput.Append(granted.ToString());
        hashInput.Append(denialReason ?? string.Empty);
        hashInput.Append(accessedAtUtc.ToString("O")); // ISO 8601
        hashInput.Append(previousHash ?? string.Empty);

        return ComputeSha256Hash(hashInput.ToString());
    }

    private static bool AreHashesEqual(string? actualHash, string expectedHash)
    {
        if (string.IsNullOrWhiteSpace(actualHash) || string.IsNullOrWhiteSpace(expectedHash))
            return false;

        var actualBytes = Encoding.UTF8.GetBytes(actualHash);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedHash);
        return CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
    }

    /// <summary>
    /// Calcula el hash SHA-256 de una cadena.
    /// </summary>
    private static string ComputeSha256Hash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
