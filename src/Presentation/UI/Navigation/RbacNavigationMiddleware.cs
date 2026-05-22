using OS.Infrastructure.Security;
using OS.Application.Interfaces;

namespace OS.Presentation.UI.Navigation;

/// <summary>
/// Middleware de navegación RBAC.
/// Verifica permisos antes de cada navegación.
/// </summary>
public interface IRbacNavigationMiddleware
{
    /// <summary>
    /// Verifica si el usuario puede navegar a un módulo específico.
    /// </summary>
    Task<bool> CanNavigateAsync(Guid userId, ModuleCode module, PermissionLevel level);

    /// <summary>
    /// Registra un intento de navegación denegado.
    /// </summary>
    Task LogDeniedNavigationAsync(Guid userId, ModuleCode module, PermissionLevel level);
}

/// <summary>
/// Implementación del middleware de navegación RBAC.
/// </summary>
public class RbacNavigationMiddleware : IRbacNavigationMiddleware
{
    private readonly IRbacPolicyService _rbacPolicyService;
    private readonly IAuditService _auditService;

    public RbacNavigationMiddleware(
        IRbacPolicyService rbacPolicyService,
        IAuditService auditService)
    {
        _rbacPolicyService = rbacPolicyService ?? throw new ArgumentNullException(nameof(rbacPolicyService));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
    }

    /// <summary>
    /// Verifica si el usuario puede navegar a un módulo específico.
    /// </summary>
    public async Task<bool> CanNavigateAsync(Guid userId, ModuleCode module, PermissionLevel level)
    {
        return await _rbacPolicyService.CanAccessModuleAsync(userId, module, level);
    }

    /// <summary>
    /// Registra un intento de navegación denegado.
    /// </summary>
    public async Task LogDeniedNavigationAsync(Guid userId, ModuleCode module, PermissionLevel level)
    {
        await _auditService.LogWarningAsync(
            "SECURITY",
            "NAVIGATION_DENIED",
            $"Intento de navegación denegado: Usuario {userId} intentó acceder a {module} con nivel {level}",
            userId);
    }
}
