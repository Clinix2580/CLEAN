using OS.Domain.Entities;

namespace OS.Domain.Interfaces;

public interface IAuditService
{
    Task LogAsync(string action, string entityType, string? entityId = null, 
        string? oldValues = null, string? newValues = null);
    Task<IEnumerable<AuditLog>> GetAuditLogsAsync(DateTime? from = null, DateTime? to = null, 
        string? userId = null, string? action = null);
    Task<string> GenerateTamperProofHashAsync(AuditLog log);
    Task<bool> VerifyAuditChainAsync();
}
