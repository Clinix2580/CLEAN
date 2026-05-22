using Microsoft.EntityFrameworkCore;
using OS.Infrastructure.Security;
using OS.Application.Interfaces;
using OS.Infrastructure.Data;
using OS.Domain.Entities;

namespace OS.Presentation.Services;

/// <summary>
/// Servicio de navegación con verificación de permisos RBAC.
/// Valida permisos antes de permitir navegación a diferentes módulos.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Navega a una vista específica verificando permisos primero.
    /// </summary>
    /// <param name="viewName">Nombre de la vista destino</param>
    /// <param name="userId">ID del usuario actual</param>
    /// <param name="requiredModule">Módulo requerido</param>
    /// <param name="requiredLevel">Nivel de permiso requerido</param>
    /// <returns>true si la navegación fue permitida, false en caso contrario</returns>
    Task<bool> NavigateAsync(
        string viewName,
        Guid userId,
        ModuleCode requiredModule,
        PermissionLevel requiredLevel);

    /// <summary>
    /// Verifica si el usuario tiene permiso para acceder a un módulo.
    /// </summary>
    Task<bool> CanNavigateAsync(Guid userId, ModuleCode module, PermissionLevel level);

    /// <summary>
    /// Registra un intento de acceso denegado.
    /// </summary>
    Task LogDeniedAccessAsync(Guid userId, string viewName, string reason);

    /// <summary>
    /// Registra un intento de acceso exitoso.
    /// </summary>
    Task LogGrantedAccessAsync(Guid userId, string viewName, string action);
}

/// <summary>
/// Implementación del servicio de navegación con verificación de permisos.
/// </summary>
public class NavigationService : INavigationService
{
    private readonly IRbacPolicyService _rbacPolicyService;
    private readonly IAuditService _auditService;
    private readonly ClinicDbContext _dbContext;

    public NavigationService(
        IRbacPolicyService rbacPolicyService,
        IAuditService auditService,
        ClinicDbContext dbContext)
    {
        _rbacPolicyService = rbacPolicyService ?? throw new ArgumentNullException(nameof(rbacPolicyService));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Navega a una vista específica verificando permisos primero.
    /// </summary>
    public async Task<bool> NavigateAsync(
        string viewName,
        Guid userId,
        ModuleCode requiredModule,
        PermissionLevel requiredLevel)
    {
        var canAccess = await CanNavigateAsync(userId, requiredModule, requiredLevel);

        if (!canAccess)
        {
            var reason = $"Permiso insuficiente para módulo {requiredModule} con nivel {requiredLevel}";
            await LogDeniedAccessAsync(userId, viewName, reason);
            return false;
        }

        // Log acceso exitoso
        await LogGrantedAccessAsync(userId, viewName, "NAVIGATE");

        // Aquí se implementaría la lógica de navegación real de WPF
        // Por ahora, solo retornamos true indicando que el permiso fue concedido
        return true;
    }

    /// <summary>
    /// Verifica si el usuario tiene permiso para acceder a un módulo.
    /// </summary>
    public async Task<bool> CanNavigateAsync(Guid userId, ModuleCode module, PermissionLevel level)
    {
        return await _rbacPolicyService.CanAccessModuleAsync(userId, module, level);
    }

    /// <summary>
    /// Registra un intento de acceso denegado.
    /// </summary>
    public async Task LogDeniedAccessAsync(Guid userId, string viewName, string reason)
    {
        // Registrar en access_logs
        var accessLog = new AccessLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Resource = viewName,
            Action = "NAVIGATE",
            Granted = false,
            DenialReason = reason,
            AttemptAt = DateTime.UtcNow
        };

        _dbContext.AccessLogs.Add(accessLog);
        await _dbContext.SaveChangesAsync();

        // También registrar en auditoría para redundancia
        await _auditService.LogWarningAsync(
            "ACCESS_CONTROL",
            "NAVIGATION_DENIED",
            $"Acceso denegado a vista '{viewName}' para usuario {userId}. Razón: {reason}",
            userId);
    }

    /// <summary>
    /// Registra un intento de acceso exitoso.
    /// </summary>
    public async Task LogGrantedAccessAsync(Guid userId, string viewName, string action)
    {
        var accessLog = new AccessLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Resource = viewName,
            Action = action,
            Granted = true,
            AttemptAt = DateTime.UtcNow
        };

        _dbContext.AccessLogs.Add(accessLog);
        await _dbContext.SaveChangesAsync();
    }
}
