using System.Collections.Concurrent;

namespace OS.Infrastructure.Background;

/// <summary>
/// Tarea de fondo programada.
/// </summary>
public class BackgroundJob
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Func<Task> Action { get; set; } = null!;
    public TimeSpan Interval { get; set; }
    public DateTime LastRun { get; set; }
    public DateTime NextRun { get; set; }
    public bool IsRunning { get; set; }
}

/// <summary>
/// Servicio de tareas de fondo.
/// </summary>
public interface IBackgroundJobService
{
    /// <summary>
    /// Registra una tarea de fondo.
    /// </summary>
    void RegisterJob(string name, Func<Task> action, TimeSpan interval);

    /// <summary>
    /// Inicia el servicio de tareas de fondo.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Detiene el servicio de tareas de fondo.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Ejecuta una tarea inmediatamente.
    /// </summary>
    Task ExecuteJobAsync(string name);
}

/// <summary>
/// Implementación del servicio de tareas de fondo.
/// En una implementación completa, esto usaría Hangfire o Quartz.NET.
/// </summary>
public class BackgroundJobService : IBackgroundJobService, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, BackgroundJob> _jobs = new();
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _executionTask;

    /// <summary>
    /// Registra una tarea de fondo.
    /// </summary>
    public void RegisterJob(string name, Func<Task> action, TimeSpan interval)
    {
        var job = new BackgroundJob
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Action = action,
            Interval = interval,
            LastRun = DateTime.MinValue,
            NextRun = DateTime.UtcNow.Add(interval),
            IsRunning = false
        };

        _jobs.TryAdd(name, job);
    }

    /// <summary>
    /// Inicia el servicio de tareas de fondo.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_executionTask != null)
            return;

        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _executionTask = ExecuteJobsAsync(_cancellationTokenSource.Token);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Detiene el servicio de tareas de fondo.
    /// </summary>
    public async Task StopAsync()
    {
        if (_cancellationTokenSource != null)
        {
            _cancellationTokenSource.Cancel();
            await (_executionTask ?? Task.CompletedTask);
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
            _executionTask = null;
        }
    }

    /// <summary>
    /// Ejecuta una tarea inmediatamente.
    /// </summary>
    public async Task ExecuteJobAsync(string name)
    {
        if (_jobs.TryGetValue(name, out var job))
        {
            await ExecuteJobAsync(job);
        }
    }

    /// <summary>
    /// Ejecuta las tareas de fondo en un ciclo.
    /// </summary>
    private async Task ExecuteJobsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;

            foreach (var job in _jobs.Values)
            {
                if (now >= job.NextRun && !job.IsRunning)
                {
                    _ = Task.Run(() => ExecuteJobAsync(job), cancellationToken);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
    }

    /// <summary>
    /// Ejecuta una tarea específica.
    /// </summary>
    private async Task ExecuteJobAsync(BackgroundJob job)
    {
        job.IsRunning = true;
        job.LastRun = DateTime.UtcNow;

        try
        {
            await job.Action();
        }
        catch (Exception ex)
        {
            // Log error
            Console.WriteLine($"Error executing job {job.Name}: {ex.Message}");
        }
        finally
        {
            job.IsRunning = false;
            job.NextRun = DateTime.UtcNow.Add(job.Interval);
        }
    }

    /// <summary>
    /// Libera recursos.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
