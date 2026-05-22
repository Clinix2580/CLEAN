using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OS.Infrastructure.Data;

namespace OS.Infrastructure.Security;

/// <summary>
/// Servicio responsable de escribir entradas de auditoría en AuditDbContext
/// con hash chain SHA-256 calculado y persistido en cada registro.
///
/// Garantía: cada AuditLogEntry y AccessLogEntry almacenado en audit_db
/// contiene VerificationHash = SHA-256(campos || previousHash) y
/// PreviousHash = hash del registro inmediatamente anterior (por orden de inserción).
///
/// Esto hace que AuditIntegrityValidator pueda verificar la cadena de forma real
/// y detectar cualquier manipulación post-inserción.
/// </summary>
public interface IAuditHashWriter
{
    /// <summary>
    /// Escribe un AuditLogEntry en audit_db con hash chain calculado.
    /// </summary>
    Task WriteAuditLogAsync(
        string tableName,
        string operation,
        string userId,
        string? recordId,
        string? oldValues,
        string? newValues,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Escribe un AccessLogEntry en audit_db con hash chain calculado.
    /// </summary>
    Task WriteAccessLogAsync(
        string userId,
        string tableName,
        string operation,
        bool success,
        string? recordId = null,
        string? ipAddress = null,
        string? failureReason = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementación de IAuditHashWriter.
/// Usa un lock por instancia para serializar escrituras y mantener la cadena de hashes
/// coherente incluso bajo concurrencia moderada (escritorio single-user).
/// </summary>
public sealed class AuditHashWriter : IAuditHashWriter
{
    private readonly AuditDbContext _auditDbContext;
    private readonly IAuditIntegrityValidator _integrityValidator;

    // Serializa escrituras para garantizar que previousHash siempre apunta
    // al último registro real en la tabla.
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public AuditHashWriter(
        AuditDbContext auditDbContext,
        IAuditIntegrityValidator integrityValidator)
    {
        _auditDbContext = auditDbContext ?? throw new ArgumentNullException(nameof(auditDbContext));
        _integrityValidator = integrityValidator ?? throw new ArgumentNullException(nameof(integrityValidator));
    }

    /// <inheritdoc />
    public async Task WriteAuditLogAsync(
        string tableName,
        string operation,
        string userId,
        string? recordId,
        string? oldValues,
        string? newValues,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Obtener el hash del último registro para encadenar
            var previousHash = await GetLastAuditLogHashAsync(cancellationToken).ConfigureAwait(false);

            var now = DateTime.UtcNow;

            // Necesitamos el Id antes de calcular el hash; usamos un placeholder
            // y lo recalculamos después de que EF asigne el Id.
            var entry = new AuditLogEntry
            {
                TableName = tableName,
                Operation = operation,
                UserId = userId,
                RecordId = recordId,
                OldValues = oldValues,
                NewValues = newValues,
                ChangedAtUtc = now,
                IpAddress = ipAddress,
                PreviousHash = previousHash
                // VerificationHash se calcula abajo
            };

            _auditDbContext.AuditLogs.Add(entry);

            // Primer SaveChanges para obtener el Id asignado por la BD
            await _auditDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Ahora calculamos el hash con el Id real
            entry.VerificationHash = _integrityValidator.CalculateAuditLogHash(
                entry.Id,
                entry.TableName,
                entry.Operation,
                entry.RecordId,
                entry.UserId,
                entry.OldValues,
                entry.NewValues,
                entry.ChangedAtUtc,
                entry.PreviousHash,
                entry.IpAddress);

            // Segundo SaveChanges para persistir el hash
            await _auditDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task WriteAccessLogAsync(
        string userId,
        string tableName,
        string operation,
        bool success,
        string? recordId = null,
        string? ipAddress = null,
        string? failureReason = null,
        CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var previousHash = await GetLastAccessLogHashAsync(cancellationToken).ConfigureAwait(false);

            var now = DateTime.UtcNow;

            var entry = new AccessLogEntry
            {
                UserId = userId,
                TableName = tableName,
                Operation = operation,
                RecordId = recordId,
                AccessedAtUtc = now,
                IpAddress = ipAddress,
                Success = success,
                FailureReason = failureReason,
                PreviousHash = previousHash
            };

            _auditDbContext.AccessLogs.Add(entry);
            await _auditDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Calcular hash con el Id real asignado
            entry.VerificationHash = CalculateAccessLogHash(
                entry.Id,
                entry.UserId,
                entry.TableName,
                entry.Operation,
                entry.Success,
                entry.FailureReason,
                entry.AccessedAtUtc,
                entry.PreviousHash);

            await _auditDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private async Task<string?> GetLastAuditLogHashAsync(CancellationToken ct)
    {
        var last = await _auditDbContext.AuditLogs
            .OrderByDescending(a => a.ChangedAtUtc)
            .ThenByDescending(a => a.Id)
            .Select(a => a.VerificationHash)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return string.IsNullOrWhiteSpace(last) ? null : last;
    }

    private async Task<string?> GetLastAccessLogHashAsync(CancellationToken ct)
    {
        var last = await _auditDbContext.AccessLogs
            .OrderByDescending(a => a.AccessedAtUtc)
            .ThenByDescending(a => a.Id)
            .Select(a => a.VerificationHash)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return string.IsNullOrWhiteSpace(last) ? null : last;
    }

    /// <summary>
    /// Calcula el hash SHA-256 de un AccessLogEntry.
    /// Mantiene el mismo algoritmo que AuditIntegrityValidator.ValidateAccessLogChainAsync.
    /// </summary>
    private static string CalculateAccessLogHash(
        long id,
        string userId,
        string resource,
        string action,
        bool granted,
        string? denialReason,
        DateTime accessedAtUtc,
        string? previousHash)
    {
        var sb = new StringBuilder();
        sb.Append(id.ToString());
        sb.Append(userId);
        sb.Append(resource);
        sb.Append(action);
        sb.Append(granted.ToString());
        sb.Append(denialReason ?? string.Empty);
        sb.Append(accessedAtUtc.ToString("O")); // ISO 8601
        sb.Append(previousHash ?? string.Empty);

        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
