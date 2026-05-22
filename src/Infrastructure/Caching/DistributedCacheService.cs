using Microsoft.Extensions.Caching.Memory;

namespace OS.Infrastructure.Caching;

/// <summary>
/// Servicio de caché distribuido para permisos RBAC.
/// </summary>
public interface IDistributedCacheService
{
    /// <summary>
    /// Obtiene un valor del caché.
    /// </summary>
    Task<T?> GetAsync<T>(string key);

    /// <summary>
    /// Establece un valor en el caché.
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null);

    /// <summary>
    /// Elimina un valor del caché.
    /// </summary>
    Task RemoveAsync(string key);

    /// <summary>
    /// Invalida el caché por patrón de clave.
    /// </summary>
    Task InvalidatePatternAsync(string pattern);

    /// <summary>
    /// Limpia todo el caché.
    /// </summary>
    Task ClearAsync();
}

/// <summary>
/// Implementación del servicio de caché distribuido usando IMemoryCache.
/// En una implementación completa, esto usaría Redis u otra solución distribuida.
/// </summary>
public class DistributedCacheService : IDistributedCacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly Dictionary<string, DateTime> _cacheKeys = new();

    public DistributedCacheService(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
    }

    /// <summary>
    /// Obtiene un valor del caché.
    /// </summary>
    public Task<T?> GetAsync<T>(string key)
    {
        if (_memoryCache.TryGetValue(key, out T? value))
        {
            return Task.FromResult(value);
        }

        return Task.FromResult(default(T));
    }

    /// <summary>
    /// Establece un valor en el caché.
    /// </summary>
    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(30)
        };

        _memoryCache.Set(key, value, options);
        _cacheKeys[key] = DateTime.UtcNow.Add(options.AbsoluteExpirationRelativeToNow.Value);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Elimina un valor del caché.
    /// </summary>
    public Task RemoveAsync(string key)
    {
        _memoryCache.Remove(key);
        _cacheKeys.Remove(key);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Invalida el caché por patrón de clave.
    /// </summary>
    public Task InvalidatePatternAsync(string pattern)
    {
        var keysToRemove = _cacheKeys.Keys.Where(k => k.Contains(pattern, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var key in keysToRemove)
        {
            _memoryCache.Remove(key);
            _cacheKeys.Remove(key);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Limpia todo el caché.
    /// </summary>
    public Task ClearAsync()
    {
        foreach (var key in _cacheKeys.Keys.ToList())
        {
            _memoryCache.Remove(key);
        }

        _cacheKeys.Clear();

        return Task.CompletedTask;
    }
}
