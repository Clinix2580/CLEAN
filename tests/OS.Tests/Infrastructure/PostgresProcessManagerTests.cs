using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OS.Infrastructure.Data;
using Xunit;

namespace OS.Tests.Infrastructure;

public class PostgresProcessManagerTests
{
    [Fact]
    public async Task Start_Postgres_Process_When_Binaries_Available()
    {
        // This test runs only when explicitly enabled to avoid CI failures on machines without binaries.
        var run = Environment.GetEnvironmentVariable("RUN_POSTGRES_TESTS");
        if (string.IsNullOrWhiteSpace(run) || run != "1")
            return;

        var baseDir = AppContext.BaseDirectory;
        var sourceBin = Path.Combine(baseDir, "Database", "pg_bin");
        var sourceCert = Path.Combine(baseDir, "Database", "certificates");
        if (!Directory.Exists(sourceBin) || !Directory.Exists(sourceCert))
            throw new InvalidOperationException("PostgreSQL binaries or certificates not found in Database folder.");

        var tempRoot = Path.Combine(Path.GetTempPath(), "clinicos_test_" + Guid.NewGuid().ToString("N"));
        var binDir = Path.Combine(tempRoot, "pg_bin");
        var dataDir = Path.Combine(tempRoot, "pg_data");
        var certDir = Path.Combine(tempRoot, "certificates");

        Directory.CreateDirectory(tempRoot);
        // copy bins
        foreach (var d in Directory.GetDirectories(sourceBin, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(d.Replace(sourceBin, binDir));
        foreach (var f in Directory.GetFiles(sourceBin, "*", SearchOption.AllDirectories))
            File.Copy(f, f.Replace(sourceBin, binDir), overwrite: true);

        // copy certs
        Directory.CreateDirectory(certDir);
        foreach (var f in Directory.GetFiles(sourceCert, "*", SearchOption.AllDirectories))
            File.Copy(f, Path.Combine(certDir, Path.GetFileName(f)), overwrite: true);

        await using var mgr = new PostgresProcessManager(binDir, dataDir, certDir);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await mgr.StartAsync(cts.Token);

        // if we reached here, StartAsync didn't throw; dispose will stop the server
    }
}
