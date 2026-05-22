using System.Diagnostics;
using OS.Infrastructure.Data;
using Serilog;

namespace OS.Infrastructure.Health;

public sealed class PostgresStartupHealthCheck
{
    private const int StartupTimeoutSeconds = 20;
    private readonly PostgresProcessManager _processManager;

    public PostgresStartupHealthCheck(PostgresProcessManager processManager)
    {
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
    }

    public async Task<PostgresStartupHealthCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var runtimeBinDirectory = Path.Combine(baseDirectory, "Database", "bin");
        var runtimePostgresPath = Path.Combine(runtimeBinDirectory, "postgres.exe");
        var runtimePgIsReadyPath = Path.Combine(runtimeBinDirectory, "pg_isready.exe");

        if (!File.Exists(runtimePostgresPath))
        {
            return PostgresStartupHealthCheckResult.Fail(
                "No se encontró postgres.exe en la carpeta de salida.",
                runtimePostgresPath);
        }

        if (!File.Exists(runtimePgIsReadyPath))
        {
            return PostgresStartupHealthCheckResult.Fail(
                "No se encontró pg_isready.exe en la carpeta de salida.",
                runtimePgIsReadyPath);
        }

        try
        {
            await _processManager.StartAsync(cancellationToken).ConfigureAwait(false);
            var ready = await WaitForReadinessAsync(runtimePgIsReadyPath, runtimeBinDirectory, cancellationToken)
                .ConfigureAwait(false);

            return ready
                ? PostgresStartupHealthCheckResult.Healthy(runtimePostgresPath)
                : PostgresStartupHealthCheckResult.Fail(
                    "PostgreSQL no respondió a pg_isready dentro del tiempo esperado.",
                    runtimePostgresPath);
        }
        catch (InvalidOperationException ex)
        {
            Log.Error(ex, "PostgreSQL portable failed startup validation.");
            return PostgresStartupHealthCheckResult.Fail(ex.Message, runtimePostgresPath);
        }
        catch (TimeoutException ex)
        {
            Log.Error(ex, "PostgreSQL portable readiness timed out.");
            return PostgresStartupHealthCheckResult.Fail(ex.Message, runtimePostgresPath);
        }
        catch (IOException ex)
        {
            Log.Error(ex, "PostgreSQL portable files are not accessible.");
            return PostgresStartupHealthCheckResult.Fail(ex.Message, runtimePostgresPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Error(ex, "PostgreSQL portable files cannot be accessed.");
            return PostgresStartupHealthCheckResult.Fail(ex.Message, runtimePostgresPath);
        }
    }

    private static async Task<bool> WaitForReadinessAsync(
        string pgIsReadyPath,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(StartupTimeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await ProbeAsync(pgIsReadyPath, workingDirectory, cancellationToken).ConfigureAwait(false))
                return true;

            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    private static async Task<bool> ProbeAsync(
        string pgIsReadyPath,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = pgIsReadyPath,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.StartInfo.ArgumentList.Add("-h");
        process.StartInfo.ArgumentList.Add("127.0.0.1");
        process.StartInfo.ArgumentList.Add("-p");
        process.StartInfo.ArgumentList.Add("5432");
        process.StartInfo.ArgumentList.Add("-q");

        if (!process.Start())
            return false;

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return process.ExitCode == 0;
    }
}

public sealed class PostgresStartupHealthCheckResult
{
    private PostgresStartupHealthCheckResult(bool isHealthy, string message, string postgresPath)
    {
        IsHealthy = isHealthy;
        Message = message;
        PostgresPath = postgresPath;
    }

    public bool IsHealthy { get; }
    public string Message { get; }
    public string PostgresPath { get; }

    public static PostgresStartupHealthCheckResult Healthy(string postgresPath) =>
        new(true, "PostgreSQL portable inició correctamente.", postgresPath);

    public static PostgresStartupHealthCheckResult Fail(string message, string postgresPath) =>
        new(false, message, postgresPath);
}
