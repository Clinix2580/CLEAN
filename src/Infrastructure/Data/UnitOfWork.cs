using OS.Application.Interfaces;
using OS.Domain.Entities;

namespace OS.Infrastructure.Data;

/// <summary>
/// Implementación del patrón Unit of Work para Entity Framework Core.
/// Gestiona todas las operaciones de persistencia y transacciones.
/// Proporciona acceso a todos los repositorios del dominio.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly IApplicationDbContext _dbContext;
    private readonly Dictionary<Type, object> _repositories = [];
    private bool _disposed;

    // Repositorios lazy-loaded
    private IRepository<User>? _userRepository;
    private IRepository<Patient>? _patientRepository;
    private IRepository<Product>? _productRepository;
    private IRepository<AuditLog>? _auditLogRepository;

    public UnitOfWork(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Acceso al repositorio de usuarios.
    /// </summary>
    public IRepository<User> Users => _userRepository ??= GetRepository<User>();

    /// <summary>
    /// Acceso al repositorio de pacientes.
    /// </summary>
    public IRepository<Patient> Patients => _patientRepository ??= GetRepository<Patient>();

    /// <summary>
    /// Acceso al repositorio de productos.
    /// </summary>
    public IRepository<Product> Products => _productRepository ??= GetRepository<Product>();

    /// <summary>
    /// Acceso al repositorio de auditoría.
    /// </summary>
    public IRepository<AuditLog> AuditLogs => _auditLogRepository ??= GetRepository<AuditLog>();

    public IRepository<T> Repository<T>() where T : class => GetRepository<T>();

    /// <summary>
    /// Guarda todos los cambios pendientes en la base de datos.
    /// </summary>
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Inicia una transacción nueva.
    /// </summary>
    public async Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        return new EFCoreTransaction(transaction);
    }

    /// <summary>
    /// Libera los recursos del contexto.
    /// </summary>
    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        if (!_disposed)
        {
            _repositories.Clear();
            await _dbContext.DisposeAsync();
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Obtiene o crea un repositorio genérico para una entidad.
    /// </summary>
    private IRepository<T> GetRepository<T>() where T : class
    {
        var type = typeof(T);
        if (!_repositories.ContainsKey(type))
        {
            var repositoryType = typeof(Repository<>).MakeGenericType(type);
            var repository = Activator.CreateInstance(repositoryType, _dbContext)
                ?? throw new InvalidOperationException($"No se pudo crear el repositorio para {type.Name}");
            _repositories[type] = repository;
        }
        return (IRepository<T>)_repositories[type];
    }
}

/// <summary>
/// Adaptador de transacción de Entity Framework Core a la interfaz ITransaction.
/// </summary>
internal class EFCoreTransaction : ITransaction
{
    private readonly Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction _transaction;

    public EFCoreTransaction(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
    {
        _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
    }

    /// <summary>
    /// Confirma la transacción actual.
    /// </summary>
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await _transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Revierte la transacción actual.
    /// </summary>
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        await _transaction.RollbackAsync(cancellationToken);
    }

    /// <summary>
    /// Libera la transacción.
    /// </summary>
    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await _transaction.DisposeAsync();
    }
}
