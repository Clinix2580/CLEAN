using OS.Infrastructure.Security;
using OS.Application.Interfaces;

namespace OS.Presentation.UI.Navigation;

/// <summary>
/// Elemento de menú de la aplicación.
/// </summary>
public class MenuItem
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public ModuleCode RequiredModule { get; set; }
    public PermissionLevel RequiredLevel { get; set; }
    public bool IsVisible { get; set; }
}

/// <summary>
/// Servicio de filtrado de UI basado en roles.
/// Filtra menú y botones según el rol del usuario activo.
/// </summary>
public interface IRoleBasedUIFilterService
{
    /// <summary>
    /// Filtra los elementos de menú según el rol del usuario.
    /// </summary>
    Task<List<MenuItem>> FilterMenuItemsAsync(Guid userId, List<MenuItem> menuItems);

    /// <summary>
    /// Verifica si un botón de acción debe ser visible.
    /// </summary>
    Task<bool> IsButtonVisibleAsync(Guid userId, ModuleCode module, PermissionLevel level);

    /// <summary>
    /// Verifica si un botón de acción debe estar habilitado.
    /// </summary>
    Task<bool> IsButtonEnabledAsync(Guid userId, ModuleCode module, PermissionLevel level);
}

/// <summary>
/// Implementación del servicio de filtrado de UI basado en roles.
/// </summary>
public class RoleBasedUIFilterService : IRoleBasedUIFilterService
{
    private readonly IRbacPolicyService _rbacPolicyService;

    public RoleBasedUIFilterService(IRbacPolicyService rbacPolicyService)
    {
        _rbacPolicyService = rbacPolicyService ?? throw new ArgumentNullException(nameof(rbacPolicyService));
    }

    /// <summary>
    /// Filtra los elementos de menú según el rol del usuario.
    /// </summary>
    public async Task<List<MenuItem>> FilterMenuItemsAsync(Guid userId, List<MenuItem> menuItems)
    {
        var filteredItems = new List<MenuItem>();

        foreach (var item in menuItems)
        {
            var canAccess = await _rbacPolicyService.CanAccessModuleAsync(
                userId,
                item.RequiredModule,
                item.RequiredLevel);

            item.IsVisible = canAccess;

            if (canAccess)
            {
                filteredItems.Add(item);
            }
        }

        return filteredItems;
    }

    /// <summary>
    /// Verifica si un botón de acción debe ser visible.
    /// </summary>
    public async Task<bool> IsButtonVisibleAsync(Guid userId, ModuleCode module, PermissionLevel level)
    {
        return await _rbacPolicyService.CanAccessModuleAsync(userId, module, level);
    }

    /// <summary>
    /// Verifica si un botón de acción debe estar habilitado.
    /// </summary>
    public async Task<bool> IsButtonEnabledAsync(Guid userId, ModuleCode module, PermissionLevel level)
    {
        return await _rbacPolicyService.CanAccessModuleAsync(userId, module, level);
    }
}
