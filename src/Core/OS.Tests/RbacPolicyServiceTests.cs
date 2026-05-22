using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using OS.Infrastructure.Data;
using OS.Infrastructure.Security;
using OS.Application.Interfaces;
using OS.Domain.Entities;

namespace OS.Tests;

/// <summary>
/// Tests unitarios para RbacPolicyService.
/// Verifica la matriz de permisos (roles × módulos).
/// </summary>
public class RbacPolicyServiceTests : IDisposable
{
    private readonly ClinicDbContext _dbContext;
    private readonly IRbacPolicyService _rbacPolicyService;
    private readonly Mock<IAuditService> _auditServiceMock;

    public RbacPolicyServiceTests()
    {
        var options = new DbContextOptionsBuilder<ClinicDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ClinicDbContext(options);

        var cache = new MemoryCache(new MemoryCacheOptions());
        _auditServiceMock = new Mock<IAuditService>();
        _auditServiceMock
            .Setup(a => a.LogInfoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _auditServiceMock
            .Setup(a => a.LogWarningAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _rbacPolicyService = new RbacPolicyService(_dbContext, cache, _auditServiceMock.Object);

        SeedTestData();
    }

    private void SeedTestData()
    {
        // Crear roles
        var adminRole = new Role { Id = Guid.NewGuid(), Code = "ADMIN", Name = "Administrator", Description = "Full access", CreatedAt = DateTime.UtcNow };
        var doctorRole = new Role { Id = Guid.NewGuid(), Code = "DOCTOR", Name = "Doctor", Description = "Medical records access", CreatedAt = DateTime.UtcNow };
        var nurseRole = new Role { Id = Guid.NewGuid(), Code = "NURSE", Name = "Nurse", Description = "Limited access", CreatedAt = DateTime.UtcNow };

        _dbContext.Roles.AddRange(adminRole, doctorRole, nurseRole);

        // Crear permisos
        var permissions = new List<Permission>
        {
            new Permission { Id = Guid.NewGuid(), Code = "USER_MANAGEMENT_READ", Module = "UserManagement", Level = "Read", Description = "Read users", CreatedAt = DateTime.UtcNow },
            new Permission { Id = Guid.NewGuid(), Code = "USER_MANAGEMENT_WRITE", Module = "UserManagement", Level = "Write", Description = "Write users", CreatedAt = DateTime.UtcNow },
            new Permission { Id = Guid.NewGuid(), Code = "USER_MANAGEMENT_DELETE", Module = "UserManagement", Level = "Delete", Description = "Delete users", CreatedAt = DateTime.UtcNow },
            new Permission { Id = Guid.NewGuid(), Code = "MEDICAL_RECORDS_READ", Module = "MedicalRecords", Level = "Read", Description = "Read medical records", CreatedAt = DateTime.UtcNow },
            new Permission { Id = Guid.NewGuid(), Code = "MEDICAL_RECORDS_WRITE", Module = "MedicalRecords", Level = "Write", Description = "Write medical records", CreatedAt = DateTime.UtcNow },
            new Permission { Id = Guid.NewGuid(), Code = "MEDICAL_RECORDS_DELETE", Module = "MedicalRecords", Level = "Delete", Description = "Delete medical records", CreatedAt = DateTime.UtcNow },
            new Permission { Id = Guid.NewGuid(), Code = "APPOINTMENTS_READ", Module = "Appointments", Level = "Read", Description = "Read appointments", CreatedAt = DateTime.UtcNow },
            new Permission { Id = Guid.NewGuid(), Code = "APPOINTMENTS_WRITE", Module = "Appointments", Level = "Write", Description = "Write appointments", CreatedAt = DateTime.UtcNow },
            new Permission { Id = Guid.NewGuid(), Code = "WAREHOUSE_READ", Module = "Warehouse", Level = "Read", Description = "Read warehouse", CreatedAt = DateTime.UtcNow },
            new Permission { Id = Guid.NewGuid(), Code = "WAREHOUSE_WRITE", Module = "Warehouse", Level = "Write", Description = "Write warehouse", CreatedAt = DateTime.UtcNow }
        };

        _dbContext.Permissions.AddRange(permissions);

        // Asignar permisos a roles
        // Admin: todos los permisos
        foreach (var permission in permissions)
        {
            _dbContext.RolePermissions.Add(new RolePermission
            {
                Id = Guid.NewGuid(),
                RoleId = adminRole.Id,
                PermissionId = permission.Id,
                AssignedAt = DateTime.UtcNow,
                AssignedBy = Guid.Empty
            });
        }

        // Doctor: MedicalRecords (Read, Write), Appointments (Read, Write)
        var doctorPermissions = permissions.Where(p => 
            (p.Module == "MedicalRecords" && (p.Level == "Read" || p.Level == "Write")) ||
            (p.Module == "Appointments" && (p.Level == "Read" || p.Level == "Write"))).ToList();
        foreach (var permission in doctorPermissions)
        {
            _dbContext.RolePermissions.Add(new RolePermission
            {
                Id = Guid.NewGuid(),
                RoleId = doctorRole.Id,
                PermissionId = permission.Id,
                AssignedAt = DateTime.UtcNow,
                AssignedBy = Guid.Empty
            });
        }

        // Nurse: MedicalRecords (Read), Appointments (Read)
        var nursePermissions = permissions.Where(p => 
            (p.Module == "MedicalRecords" && p.Level == "Read") ||
            (p.Module == "Appointments" && p.Level == "Read")).ToList();
        foreach (var permission in nursePermissions)
        {
            _dbContext.RolePermissions.Add(new RolePermission
            {
                Id = Guid.NewGuid(),
                RoleId = nurseRole.Id,
                PermissionId = permission.Id,
                AssignedAt = DateTime.UtcNow,
                AssignedBy = Guid.Empty
            });
        }

        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task Admin_ShouldHaveFullAccessToAllModules()
    {
        // Arrange
        var adminRole = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Code == "ADMIN");
        var adminUser = new User { Id = Guid.NewGuid(), Username = "admin", PasswordHash = "hash", FullName = "Admin", IsActive = true, CreatedAt = DateTime.UtcNow };
        _dbContext.Users.Add(adminUser);
        _dbContext.UserRoles.Add(new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = adminUser.Id,
            RoleId = adminRole!.Id,
            AssignedAt = DateTime.UtcNow,
            AssignedBy = Guid.Empty
        });
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        Assert.True(await _rbacPolicyService.CanAccessModuleAsync(adminUser.Id, ModuleCode.UserManagement, PermissionLevel.Read));
        Assert.True(await _rbacPolicyService.CanAccessModuleAsync(adminUser.Id, ModuleCode.UserManagement, PermissionLevel.Write));
        Assert.True(await _rbacPolicyService.CanAccessModuleAsync(adminUser.Id, ModuleCode.UserManagement, PermissionLevel.Delete));
        Assert.True(await _rbacPolicyService.CanAccessModuleAsync(adminUser.Id, ModuleCode.MedicalRecords, PermissionLevel.Read));
        Assert.True(await _rbacPolicyService.CanAccessModuleAsync(adminUser.Id, ModuleCode.MedicalRecords, PermissionLevel.Write));
        Assert.True(await _rbacPolicyService.CanAccessModuleAsync(adminUser.Id, ModuleCode.MedicalRecords, PermissionLevel.Delete));
        Assert.True(await _rbacPolicyService.CanAccessModuleAsync(adminUser.Id, ModuleCode.Appointments, PermissionLevel.Read));
        Assert.True(await _rbacPolicyService.CanAccessModuleAsync(adminUser.Id, ModuleCode.Appointments, PermissionLevel.Write));
        Assert.True(await _rbacPolicyService.CanAccessModuleAsync(adminUser.Id, ModuleCode.Warehouse, PermissionLevel.Read));
        Assert.True(await _rbacPolicyService.CanAccessModuleAsync(adminUser.Id, ModuleCode.Warehouse, PermissionLevel.Write));
    }

    [Fact]
    public async Task Doctor_ShouldHaveMedicalRecordsAndAppointmentsAccess()
    {
        // Arrange
        var doctorRole = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Code == "DOCTOR");
        var doctorUser = new User { Id = Guid.NewGuid(), Username = "doctor", PasswordHash = "hash", FullName = "Doctor", IsActive = true, CreatedAt = DateTime.UtcNow };
        _dbContext.Users.Add(doctorUser);
        _dbContext.UserRoles.Add(new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = doctorUser.Id,
            RoleId = doctorRole!.Id,
            AssignedAt = DateTime.UtcNow,
            AssignedBy = Guid.Empty
        });
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        Assert.False(await _rbacPolicyService.CanAccessModuleAsync(doctorUser.Id, ModuleCode.UserManagement, PermissionLevel.Read));
        Assert.False(await _rbacPolicyService.CanAccessModuleAsync(doctorUser.Id, ModuleCode.UserManagement, PermissionLevel.Write));
        Assert.True(await _rbacPolicyService.CanAccessModuleAsync(doctorUser.Id, ModuleCode.MedicalRecords, PermissionLevel.Read));
        Assert.True(await _rbacPolicyService.CanAccessModuleAsync(doctorUser.Id, ModuleCode.MedicalRecords, PermissionLevel.Write));
        Assert.False(await _rbacPolicyService.CanAccessModuleAsync(doctorUser.Id, ModuleCode.MedicalRecords, PermissionLevel.Delete));
        Assert.True(await _rbacPolicyService.CanAccessModuleAsync(doctorUser.Id, ModuleCode.Appointments, PermissionLevel.Read));
        Assert.True(await _rbacPolicyService.CanAccessModuleAsync(doctorUser.Id, ModuleCode.Appointments, PermissionLevel.Write));
        Assert.False(await _rbacPolicyService.CanAccessModuleAsync(doctorUser.Id, ModuleCode.Warehouse, PermissionLevel.Read));
        Assert.False(await _rbacPolicyService.CanAccessModuleAsync(doctorUser.Id, ModuleCode.Warehouse, PermissionLevel.Write));
    }

    [Fact]
    public async Task Nurse_ShouldHaveLimitedMedicalRecordsAndAppointmentsAccess()
    {
        // Arrange
        var nurseRole = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Code == "NURSE");
        var nurseUser = new User { Id = Guid.NewGuid(), Username = "nurse", PasswordHash = "hash", FullName = "Nurse", IsActive = true, CreatedAt = DateTime.UtcNow };
        _dbContext.Users.Add(nurseUser);
        _dbContext.UserRoles.Add(new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = nurseUser.Id,
            RoleId = nurseRole!.Id,
            AssignedAt = DateTime.UtcNow,
            AssignedBy = Guid.Empty
        });
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        Assert.False(await _rbacPolicyService.CanAccessModuleAsync(nurseUser.Id, ModuleCode.UserManagement, PermissionLevel.Read));
        Assert.False(await _rbacPolicyService.CanAccessModuleAsync(nurseUser.Id, ModuleCode.UserManagement, PermissionLevel.Write));
        Assert.True(await _rbacPolicyService.CanAccessModuleAsync(nurseUser.Id, ModuleCode.MedicalRecords, PermissionLevel.Read));
        Assert.False(await _rbacPolicyService.CanAccessModuleAsync(nurseUser.Id, ModuleCode.MedicalRecords, PermissionLevel.Write));
        Assert.False(await _rbacPolicyService.CanAccessModuleAsync(nurseUser.Id, ModuleCode.MedicalRecords, PermissionLevel.Delete));
        Assert.True(await _rbacPolicyService.CanAccessModuleAsync(nurseUser.Id, ModuleCode.Appointments, PermissionLevel.Read));
        Assert.False(await _rbacPolicyService.CanAccessModuleAsync(nurseUser.Id, ModuleCode.Appointments, PermissionLevel.Write));
        Assert.False(await _rbacPolicyService.CanAccessModuleAsync(nurseUser.Id, ModuleCode.Warehouse, PermissionLevel.Read));
        Assert.False(await _rbacPolicyService.CanAccessModuleAsync(nurseUser.Id, ModuleCode.Warehouse, PermissionLevel.Write));
    }

    [Fact]
    public async Task GetUserPermissionsAsync_ShouldReturnCorrectPermissions()
    {
        // Arrange
        var doctorRole = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Code == "DOCTOR");
        var doctorUser = new User { Id = Guid.NewGuid(), Username = "doctor", PasswordHash = "hash", FullName = "Doctor", IsActive = true, CreatedAt = DateTime.UtcNow };
        _dbContext.Users.Add(doctorUser);
        _dbContext.UserRoles.Add(new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = doctorUser.Id,
            RoleId = doctorRole!.Id,
            AssignedAt = DateTime.UtcNow,
            AssignedBy = Guid.Empty
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var permissions = await _rbacPolicyService.GetUserPermissionsAsync(doctorUser.Id);

        // Assert
        Assert.DoesNotContain(permissions, p => p.Module == ModuleCode.MedicalRecords && p.Level == PermissionLevel.Delete);
        Assert.Contains(permissions, p => p.Module == ModuleCode.MedicalRecords && p.Level == PermissionLevel.Write);
        Assert.Contains(permissions, p => p.Module == ModuleCode.Appointments && p.Level == PermissionLevel.Write);
        Assert.DoesNotContain(permissions, p => p.Module == ModuleCode.UserManagement);
        Assert.DoesNotContain(permissions, p => p.Module == ModuleCode.Warehouse);
    }

    [Fact]
    public async Task AssignRoleAsync_ShouldAddRoleToUser()
    {
        // Arrange
        var nurseRole = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Code == "NURSE");
        var doctorRole = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Code == "DOCTOR");
        var user = new User { Id = Guid.NewGuid(), Username = "user", PasswordHash = "hash", FullName = "User", IsActive = true, CreatedAt = DateTime.UtcNow };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        await _rbacPolicyService.AssignRoleAsync(user.Id, "DOCTOR", Guid.Empty);

        // Assert
        var userRoles = await _dbContext.UserRoles.Where(ur => ur.UserId == user.Id).ToListAsync();
        Assert.Single(userRoles);
        Assert.Equal(doctorRole!.Id, userRoles.First().RoleId);
    }

    [Fact]
    public async Task RevokeRoleAsync_ShouldRemoveRoleFromUser()
    {
        // Arrange
        var doctorRole = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Code == "DOCTOR");
        var user = new User { Id = Guid.NewGuid(), Username = "user", PasswordHash = "hash", FullName = "User", IsActive = true, CreatedAt = DateTime.UtcNow };
        _dbContext.Users.Add(user);
        _dbContext.UserRoles.Add(new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            RoleId = doctorRole!.Id,
            AssignedAt = DateTime.UtcNow,
            AssignedBy = Guid.Empty
        });
        await _dbContext.SaveChangesAsync();

        // Act
        await _rbacPolicyService.RevokeRoleAsync(user.Id, "DOCTOR", Guid.Empty, "Test revocation");

        // Assert
        var userRoles = await _dbContext.UserRoles.Where(ur => ur.UserId == user.Id).ToListAsync();
        Assert.All(userRoles, ur => Assert.NotNull(ur.RevokedAt));
    }

    [Fact]
    public async Task InvalidateUserCache_ShouldClearCachedPermissions()
    {
        // Arrange
        var adminRole = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Code == "ADMIN");
        var user = new User { Id = Guid.NewGuid(), Username = "user", PasswordHash = "hash", FullName = "User", IsActive = true, CreatedAt = DateTime.UtcNow };
        _dbContext.Users.Add(user);
        _dbContext.UserRoles.Add(new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            RoleId = adminRole!.Id,
            AssignedAt = DateTime.UtcNow,
            AssignedBy = Guid.Empty
        });
        await _dbContext.SaveChangesAsync();

        // Act - First call should cache
        await _rbacPolicyService.GetUserPermissionsAsync(user.Id);
        
        // Invalidate cache
        _rbacPolicyService.InvalidateUserCache(user.Id);

        // Assert - Second call should rebuild cache
        var permissions = await _rbacPolicyService.GetUserPermissionsAsync(user.Id);
        Assert.NotNull(permissions);
        Assert.NotEmpty(permissions);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
