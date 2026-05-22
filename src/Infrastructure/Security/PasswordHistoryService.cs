using Microsoft.EntityFrameworkCore;
using OS.Domain.Entities;
using OS.Infrastructure.Data;

namespace OS.Infrastructure.Security;

/// <summary>
/// Servicio para gestionar el historial de contraseñas de usuarios.
/// Mantiene las últimas 5 contraseñas hasheadas para prevenir reutilización.
/// </summary>
public interface IPasswordHistoryService
{
    /// <summary>
    /// Agrega una nueva contraseña al historial del usuario.
    /// Mantiene solo las últimas 5 contraseñas.
    /// </summary>
    /// <param name="userId">ID del usuario</param>
    /// <param name="passwordHash">Hash de la nueva contraseña</param>
    Task AddPasswordToHistoryAsync(Guid userId, string passwordHash);

    /// <summary>
    /// Verifica si una contraseña ya ha sido usada anteriormente (últimas 5).
    /// </summary>
    /// <param name="userId">ID del usuario</param>
    /// <param name="passwordHash">Hash de la contraseña a verificar</param>
    /// <returns>true si la contraseña ya fue usada, false en caso contrario</returns>
    Task<bool> IsPasswordReusedAsync(Guid userId, string passwordHash);

    /// <summary>
    /// Obtiene el historial de contraseñas de un usuario.
    /// </summary>
    /// <param name="userId">ID del usuario</param>
    /// <returns>Lista de contraseñas en el historial (ordenadas por fecha descendente)</returns>
    Task<List<PasswordHistory>> GetPasswordHistoryAsync(Guid userId);

    /// <summary>
    /// Limpia el historial de contraseñas de un usuario.
    /// </summary>
    /// <param name="userId">ID del usuario</param>
    Task ClearPasswordHistoryAsync(Guid userId);
}

/// <summary>
/// Implementación del servicio de historial de contraseñas.
/// </summary>
public class PasswordHistoryService : IPasswordHistoryService
{
    private const int MaxPasswordHistory = 5;
    private readonly ClinicDbContext _dbContext;

    public PasswordHistoryService(ClinicDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Agrega una nueva contraseña al historial del usuario.
    /// </summary>
    public async Task AddPasswordToHistoryAsync(Guid userId, string passwordHash)
    {
        // Crear nuevo registro de historial
        var historyEntry = new PasswordHistory
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PasswordHash = passwordHash,
            ChangedAt = DateTime.UtcNow
        };

        _dbContext.PasswordHistories.Add(historyEntry);

        // Obtener historial actual del usuario
        var existingHistory = await _dbContext.PasswordHistories
            .Where(ph => ph.UserId == userId)
            .OrderByDescending(ph => ph.ChangedAt)
            .ToListAsync();

        // Si hay más de 5 contraseñas, eliminar las más antiguas
        if (existingHistory.Count >= MaxPasswordHistory)
        {
            var toRemove = existingHistory.Skip(MaxPasswordHistory - 1);
            _dbContext.PasswordHistories.RemoveRange(toRemove);
        }

        await _dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Verifica si una contraseña ya ha sido usada anteriormente.
    /// </summary>
    public async Task<bool> IsPasswordReusedAsync(Guid userId, string passwordHash)
    {
        var last5Passwords = await _dbContext.PasswordHistories
            .Where(ph => ph.UserId == userId)
            .OrderByDescending(ph => ph.ChangedAt)
            .Take(MaxPasswordHistory)
            .Select(ph => ph.PasswordHash)
            .ToListAsync();

        return last5Passwords.Any(hash => hash == passwordHash);
    }

    /// <summary>
    /// Obtiene el historial de contraseñas de un usuario.
    /// </summary>
    public async Task<List<PasswordHistory>> GetPasswordHistoryAsync(Guid userId)
    {
        return await _dbContext.PasswordHistories
            .Where(ph => ph.UserId == userId)
            .OrderByDescending(ph => ph.ChangedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Limpia el historial de contraseñas de un usuario.
    /// </summary>
    public async Task ClearPasswordHistoryAsync(Guid userId)
    {
        var history = await _dbContext.PasswordHistories
            .Where(ph => ph.UserId == userId)
            .ToListAsync();

        if (history.Any())
        {
            _dbContext.PasswordHistories.RemoveRange(history);
            await _dbContext.SaveChangesAsync();
        }
    }
}
