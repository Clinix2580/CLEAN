using System.Security.Cryptography;
using System.Text;
using OS.Application.Interfaces;
using OS.Domain.Entities;

namespace OS.Infrastructure.Security;

/// <summary>
/// Offline-first audit service with a SHA-256 hash chain for operational traceability.
/// </summary>
public class AuditService : IAuditService
{
    private const string SystemUserId = "SYSTEM";
    private readonly IUnitOfWork _unitOfWork;

    public AuditService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public Task LogAsync(
        string action,
        string entityType,
        string? entityId = null,
        string? oldValues = null,
        string? newValues = null,
        CancellationToken cancellationToken = default)
    {
        return LogAuditAsync(
            SystemUserId,
            action,
            entityType,
            entityId,
            oldValues,
            newValues,
            cancellationToken: cancellationToken);
    }

    public async Task LogAuditAsync(
        string userId,
        string action,
        string entityType,
        string? entityId,
        string? oldValues,
        string? newValues,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("El usuario no puede estar vacio.", nameof(userId));
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("La accion no puede estar vacia.", nameof(action));
        if (string.IsNullOrWhiteSpace(entityType))
            throw new ArgumentException("El tipo de entidad no puede estar vacio.", nameof(entityType));

        var lastLog = await GetLastAuditLogAsync(cancellationToken);
        var auditLog = new AuditLog(action, entityType, userId)
        {
            EntityId = entityId,
            OldValues = oldValues,
            NewValues = newValues,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            PreviousHash = lastLog?.Hash ?? string.Empty
        };

        auditLog.Hash = ComputeTamperProofHash(auditLog);

        await _unitOfWork.AuditLogs.AddAsync(auditLog);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<AuditLog>> GetAuditLogsAsync(
        string entityType,
        string? entityId = null,
        CancellationToken cancellationToken = default)
    {
        var logs = await _unitOfWork.AuditLogs.FindAsync(
            log => log.EntityType == entityType && (entityId == null || log.EntityId == entityId),
            cancellationToken);

        return logs.OrderByDescending(log => log.CreatedAt).ToList();
    }

    public async Task<IEnumerable<AuditLog>> GetUserAuditLogsAsync(
        string userId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var logs = await _unitOfWork.AuditLogs.FindAsync(
            log => log.CreatedBy == userId
                && (startDate == null || log.CreatedAt >= startDate)
                && (endDate == null || log.CreatedAt <= endDate),
            cancellationToken);

        return logs.OrderByDescending(log => log.CreatedAt).ToList();
    }

    public Task LogInfoAsync(
        string entityType,
        string action,
        string? message = null,
        object? userId = null,
        CancellationToken cancellationToken = default)
    {
        return LogAuditAsync(
            userId?.ToString() ?? SystemUserId,
            action,
            entityType,
            null,
            null,
            message,
            cancellationToken: cancellationToken);
    }

    public Task LogWarningAsync(
        string entityType,
        string action,
        string? message = null,
        object? userId = null,
        CancellationToken cancellationToken = default)
    {
        return LogInfoAsync(entityType, action, message, userId, cancellationToken);
    }

    public Task LogErrorAsync(
        string entityType,
        string action,
        string? message = null,
        object? userId = null,
        Exception? exception = null,
        CancellationToken cancellationToken = default)
    {
        var oldValues = exception?.ToString();
        return LogAuditAsync(
            userId?.ToString() ?? SystemUserId,
            action,
            entityType,
            null,
            oldValues,
            message,
            cancellationToken: cancellationToken);
    }

    public Task LogCriticalAsync(
        string entityType,
        string action,
        string? message = null,
        object? userId = null,
        CancellationToken cancellationToken = default)
    {
        return LogErrorAsync(entityType, action, message, userId, null, cancellationToken);
    }

    public async Task<IEnumerable<AuditLog>> GetRecentAuditLogsAsync(
        int count = 20,
        CancellationToken cancellationToken = default)
    {
        if (count <= 0)
            throw new ArgumentException("El número de registros debe ser mayor a 0.", nameof(count));

        var logs = await _unitOfWork.AuditLogs.GetAllAsync(cancellationToken);
        return logs.OrderByDescending(log => log.CreatedAt)
            .Take(count)
            .ToList();
    }

    public Task<bool> ValidateAuditLogIntegrityAsync(AuditLog auditLog)
    {
        if (auditLog == null)
            throw new ArgumentNullException(nameof(auditLog));

        return Task.FromResult(auditLog.Hash == ComputeTamperProofHash(auditLog));
    }

    public async Task<int> PurgeOldAuditLogsAsync(int daysToKeep, CancellationToken cancellationToken = default)
    {
        if (daysToKeep <= 0)
            throw new ArgumentException("Los dias a retener deben ser mayores a 0.", nameof(daysToKeep));

        var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);
        var logsToDelete = (await _unitOfWork.AuditLogs.FindAsync(
            log => log.CreatedAt < cutoffDate,
            cancellationToken)).ToList();

        if (logsToDelete.Count == 0)
            return 0;

        await _unitOfWork.AuditLogs.RemoveRangeAsync(logsToDelete);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return logsToDelete.Count;
    }

    private static string ComputeTamperProofHash(AuditLog log)
    {
        var data = new StringBuilder();
        data.Append(log.CreatedAt.ToUniversalTime().Ticks);
        data.Append('|').Append(log.CreatedBy);
        data.Append('|').Append(log.Action);
        data.Append('|').Append(log.EntityType);
        data.Append('|').Append(log.EntityId ?? "NULL");
        data.Append('|').Append(log.OldValues ?? "NULL");
        data.Append('|').Append(log.NewValues ?? "NULL");
        data.Append('|').Append(log.PreviousHash ?? "NULL");

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data.ToString())));
    }

    private async Task<AuditLog?> GetLastAuditLogAsync(CancellationToken cancellationToken)
    {
        var logs = await _unitOfWork.AuditLogs.GetAllAsync(cancellationToken);
        return logs.OrderByDescending(log => log.CreatedAt).FirstOrDefault();
    }
}
