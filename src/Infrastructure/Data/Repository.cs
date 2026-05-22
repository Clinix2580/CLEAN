using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using OS.Application.Interfaces;

namespace OS.Infrastructure.Data;

/// <summary>
/// Implementación genérica del patrón Repository usando Entity Framework Core.
/// Proporciona operaciones CRUD estándar para todas las entidades del dominio.
/// Soporta soft delete automático para entidades que lo implementan.
/// </summary>
/// <typeparam name="TEntity">El tipo de entidad a gestionar</typeparam>
public class Repository<TEntity> : IRepository<TEntity> where TEntity : class
{
    private readonly IApplicationDbContext _dbContext;
    private readonly DbSet<TEntity> _dbSet;

    public Repository(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _dbSet = _dbContext.Set<TEntity>();
    }

    public async Task<TEntity?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync([id], cancellationToken: cancellationToken);
    }

    public async Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<TEntity>> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(predicate).ToListAsync(cancellationToken);
    }

    public async Task<TEntity> AddAsync(TEntity entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        await _dbSet.AddAsync(entity);
        return entity;
    }

    public async Task<IEnumerable<TEntity>> AddRangeAsync(IEnumerable<TEntity> entities)
    {
        var entityList = entities?.ToList() ?? throw new ArgumentNullException(nameof(entities));

        if (entityList.Count == 0)
            throw new ArgumentException("La colección de entidades no puede estar vacía.", nameof(entities));

        await _dbSet.AddRangeAsync(entityList);
        return entityList;
    }

    public async Task UpdateAsync(TEntity entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        _dbSet.Update(entity);
        await Task.CompletedTask;
    }

    public async Task RemoveAsync(TEntity entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        _dbSet.Remove(entity);
        await Task.CompletedTask;
    }

    public Task DeleteAsync(TEntity entity) => RemoveAsync(entity);

    public async Task RemoveRangeAsync(IEnumerable<TEntity> entities)
    {
        var entityList = entities?.ToList() ?? throw new ArgumentNullException(nameof(entities));

        if (entityList.Count == 0)
            throw new ArgumentException("La colección de entidades no puede estar vacía.", nameof(entities));

        _dbSet.RemoveRange(entityList);
        await Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.AnyAsync(predicate, cancellationToken);
    }

    public async Task<int> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        if (predicate == null)
            return await _dbSet.CountAsync(cancellationToken);

        return await _dbSet.CountAsync(predicate, cancellationToken);
    }
}
