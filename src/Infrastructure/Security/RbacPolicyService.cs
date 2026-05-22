using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OS.Domain.Entities;
using OS.Infrastructure.Data;
using OS.Application.Interfaces;
using IAuditService = OS.Application.Interfaces.IAuditService;

namespace OS.Infrastructure.Security;

/// <summary>
/// Nivel de permiso para un módulo.
/// </summary>
public enum PermissionLevel
{
    Read = 0,
    Write = 1,
    Delete = 2,
    Admin = 3
}

/// <summary>
/// Código de módulo del sistema.
/// </summary>
public enum ModuleCode
{
    UserManagement,
    Licensing,
    MedicalRecords,
    Appointments,
    Imaging,
    Prescriptions,
    Pharmacy,
    Warehouse,
    Finance,
    Reports,
    Audit,
    Backup,
    Specialties
}

/// <summary>
/// Permiso de módulo para un usuario.
/// </summary>
public class ModulePermission
{
    public ModuleCode Module { get; set; }
    public PermissionLevel Level { get; set; }
}

/// <summary>
/// Servicio de políticas RBAC con caché en memoria.
/// Valida permisos de acceso por rol y módulo.
/// </summary>
public interface IRbacPolicyService
{
    /// <summary>
    /// Verifica si un usuario puede acceder a un módulo con un nivel específico.
    /// </summary>
    Task<bool> CanAccessModuleAsync(Guid userId, ModuleCode module, PermissionLevel level);

    /// <summary>
    /// Obtiene todos los permisos de un usuario.
    /// </summary>
    Task<IEnumerable<ModulePermission>> GetUserPermissionsAsync(Guid userId);

    /// <summary>
    /// Asigna un rol a un usuario.
    /// </summary>
    Task AssignRoleAsync(Guid userId, string roleCode, Guid assignedByAdminId);

    /// <summary>
    /// Revoca un rol de un usuario.
    /// </summary>
    Task RevokeRoleAsync(Guid userId, string roleCode, Guid revokedByAdminId, string reason);

    /// <summary>
    /// Verifica si un usuario tiene un permiso específico.
    /// </summary>
    Task<bool> HasPermissionAsync(Guid userId, string permissionCode);

    /// <summary>
    /// Invalida la caché de permisos para un usuario específico.
    /// </summary>
    void InvalidateUserCache(Guid userId);

    /// <summary>
    /// Invalida toda la caché de permisos.
    /// </summary>
    void InvalidateAllCache();
}

/// <summary>
/// Implementación del servicio de políticas RBAC con caché en memoria.
/// </summary>
public class RbacPolicyService : IRbacPolicyService
{
    private readonly ClinicDbContext _dbContext;
    private readonly IMemoryCache _cache;
    private readonly IAuditService _auditService;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(30);

    private static readonly string UserPermissionsCachePrefix = "user_permissions_";

    public RbacPolicyService(
        ClinicDbContext dbContext,
        IMemoryCache cache,
        IAuditService auditService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
    }

    /// <summary>
    /// Verifica si un usuario puede acceder a un módulo con un nivel específico.
    /// </summary>
    public async Task<bool> CanAccessModuleAsync(Guid userId, ModuleCode module, PermissionLevel level)
    {
        var permissions = await GetUserPermissionsAsync(userId);
        var modulePermission = permissions.FirstOrDefault(p => p.Module == module);

        if (modulePermission == null)
            return false;

        return modulePermission.Level >= level;
    }

    /// <summary>
    /// Obtiene todos los permisos de un usuario con caché.
    /// </summary>
    public async Task<IEnumerable<ModulePermission>> GetUserPermissionsAsync(Guid userId)
    {
        var cacheKey = $"{UserPermissionsCachePrefix}{userId}";

        if (_cache.TryGetValue<IEnumerable<ModulePermission>>(cacheKey, out var cachedPermissions) && cachedPermissions != null)
        {
            return cachedPermissions;
        }

        // Cargar permisos desde la base de datos
        var userRoles = await _dbContext.UserRoles
            .Include(ur => ur.Role)
            .ThenInclude(r => r!.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .Where(ur => ur.UserId == userId && ur.RevokedAt == null)
            .ToListAsync();

        var permissions = new List<ModulePermission>();

        foreach (var userRole in userRoles)
        {
            var rolePermissions = userRole.Role?.RolePermissions;
            if (rolePermissions == null)
                continue;

            foreach (var rolePermission in rolePermissions)
            {
                if (rolePermission.Permission == null)
                    continue;

                var permission = rolePermission.Permission;
                var module = ParseModuleCode(permission.Module);
                var level = ParsePermissionLevel(permission.Level);

                var existingPermission = permissions.FirstOrDefault(p => p.Module == module);
                if (existingPermission != null)
                {
                    // Usar el nivel más alto
                    if (level > existingPermission.Level)
                    {
                        permissions.Remove(existingPermission);
                        permissions.Add(new ModulePermission { Module = module, Level = level });
                    }
                }
                else
                {
                    permissions.Add(new ModulePermission { Module = module, Level = level });
                }
            }
        }

        // Cachear los permisos
        _cache.Set(cacheKey, permissions, _cacheDuration);

        return permissions;
    }

    /// <summary>
    /// Asigna un rol a un usuario.
    /// </summary>
    public async Task AssignRoleAsync(Guid userId, string roleCode, Guid assignedByAdminId)
    {
        // Verificar que el rol existe
        var role = await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.Code == roleCode);

        if (role == null)
            throw new ArgumentException($"El rol '{roleCode}' no existe.", nameof(roleCode));

        // Verificar que el usuario no tenga ya este rol
        var existingAssignment = await _dbContext.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == role.Id && ur.RevokedAt == null);

        if (existingAssignment != null)
            throw new InvalidOperationException($"El usuario ya tiene el rol '{roleCode}'.");

        // Crear asignación
        var userRole = new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RoleId = role.Id,
            AssignedAt = DateTime.UtcNow,
            AssignedBy = assignedByAdminId
        };

        _dbContext.UserRoles.Add(userRole);
        await _dbContext.SaveChangesAsync();

        // Invalidar caché del usuario
        InvalidateUserCache(userId);

        // Registrar en auditoría
        await _auditService.LogInfoAsync(
            "RBAC",
            "ROLE_ASSIGNED",
            $"Rol '{roleCode}' asignado al usuario {userId} por admin {assignedByAdminId}",
            userId);
    }

    /// <summary>
    /// Revoca un rol de un usuario.
    /// </summary>
    public async Task RevokeRoleAsync(Guid userId, string roleCode, Guid revokedByAdminId, string reason)
    {
        // Verificar que el rol existe
        var role = await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.Code == roleCode);

        if (role == null)
            throw new ArgumentException($"El rol '{roleCode}' no existe.", nameof(roleCode));

        // Buscar la asignación activa
        var userRole = await _dbContext.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == role.Id && ur.RevokedAt == null);

        if (userRole == null)
            throw new InvalidOperationException($"El usuario no tiene el rol '{roleCode}' activo.");

        // Revocar el rol
        userRole.RevokedAt = DateTime.UtcNow;
        userRole.RevocationReason = reason;

        await _dbContext.SaveChangesAsync();

        // Invalidar caché del usuario
        InvalidateUserCache(userId);

        // Registrar en auditoría
        await _auditService.LogWarningAsync(
            "RBAC",
            "ROLE_REVOKED",
            $"Rol '{roleCode}' revocado del usuario {userId} por admin {revokedByAdminId}. Razón: {reason}",
            userId);
    }

    /// <summary>
    /// Verifica si un usuario tiene un permiso específico.
    /// </summary>
    public async Task<bool> HasPermissionAsync(Guid userId, string permissionCode)
    {
        var permissions = await GetUserPermissionsAsync(userId);
        
        // Parsear el código de permiso (ej: PATIENTS_READ)
        var parts = permissionCode.Split('_');
        if (parts.Length != 2)
            return false;

        var module = ParseModuleCode(parts[0]);
        var level = ParsePermissionLevel(parts[1]);

        var modulePermission = permissions.FirstOrDefault(p => p.Module == module);
        if (modulePermission == null)
            return false;

        return modulePermission.Level >= level;
    }

    /// <summary>
    /// Invalida la caché de permisos para un usuario específico.
    /// </summary>
    public void InvalidateUserCache(Guid userId)
    {
        var cacheKey = $"{UserPermissionsCachePrefix}{userId}";
        _cache.Remove(cacheKey);
    }

    /// <summary>
    /// Invalida toda la caché de permisos.
    /// </summary>
    public void InvalidateAllCache()
    {
        // En una implementación real, esto podría usar un método más eficiente
        // Por ahora, simplemente no hacemos nada ya que la caché tiene expiración
    }

    /// <summary>
    /// Parsea el código de módulo desde string.
    /// </summary>
    private static ModuleCode ParseModuleCode(string module)
    {
        return module.ToUpperInvariant() switch
        {
            "USERMANAGEMENT" => ModuleCode.UserManagement,
            "LICENSING" => ModuleCode.Licensing,
            "MEDICALRECORDS" => ModuleCode.MedicalRecords,
            "APPOINTMENTS" => ModuleCode.Appointments,
            "IMAGING" => ModuleCode.Imaging,
            "PRESCRIPTIONS" => ModuleCode.Prescriptions,
            "PHARMACY" => ModuleCode.Pharmacy,
            "WAREHOUSE" => ModuleCode.Warehouse,
            "FINANCE" => ModuleCode.Finance,
            "REPORTS" => ModuleCode.Reports,
            "AUDIT" => ModuleCode.Audit,
            "BACKUP" => ModuleCode.Backup,
            "SPECIALTIES" => ModuleCode.Specialties,
            _ => throw new ArgumentException($"Módulo desconocido: {module}")
        };
    }

    /// <summary>
    /// Parsea el nivel de permiso desde string.
    /// </summary>
    private static PermissionLevel ParsePermissionLevel(string level)
    {
        return level.ToUpperInvariant() switch
        {
            "READ" => PermissionLevel.Read,
            "WRITE" => PermissionLevel.Write,
            "DELETE" => PermissionLevel.Delete,
            "ADMIN" => PermissionLevel.Admin,
            _ => throw new ArgumentException($"Nivel de permiso desconocido: {level}")
        };
    }
}
