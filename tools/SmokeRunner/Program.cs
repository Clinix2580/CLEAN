using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Diagnostics;
using System.Net.Http;
using System.IO.Compression;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OS.Infrastructure;
using OS.Infrastructure.Data.Configuration;
using SmokeRunner;

Console.WriteLine("ClinicOS Smoke Test Runner");
Console.WriteLine("===============================");
Console.WriteLine();

string? repoRoot = FindRepoRoot();
if (repoRoot == null)
{
    Console.WriteLine("Could not locate repository root. Ensure running from the workspace.");
    return;
}

var dbFolder = Path.Combine(repoRoot, "tools", "pgsql-portable");
if (!Directory.Exists(dbFolder))
{
    Console.WriteLine($"PostgreSQL portable folder not found at {dbFolder}");
    return;
}

var pgBin = Path.Combine(dbFolder, "bin");
var pgDataTemplate = Path.Combine(repoRoot, "src", "Infrastructure", "Database", "pg_data");
var certs = Path.Combine(dbFolder, "certificates");

Console.WriteLine($"Found Database folder: {dbFolder}");
Console.WriteLine($"pg_bin: {pgBin}");
Console.WriteLine($"pg_data template: {pgDataTemplate}");
Console.WriteLine($"certificates: {certs}");

if (!Directory.Exists(pgBin) || !Directory.EnumerateFileSystemEntries(pgBin).Any())
{
    Console.WriteLine("PostgreSQL portable bin appears empty or missing binaries. Place them into tools/pgsql-portable/bin and retry.");
    var envUrl = Environment.GetEnvironmentVariable("PG_BIN_URL");
    if (!string.IsNullOrEmpty(envUrl))
    {
        Console.WriteLine("pg_bin empty - PG_BIN_URL provided, attempting download...");
        try
        {
            var tmp = Path.Combine(Path.GetTempPath(), "pg_bin_download.zip");
            using (var http = new HttpClient())
            using (var s = await http.GetStreamAsync(envUrl).ConfigureAwait(false))
            using (var fs = File.Create(tmp))
            {
                await s.CopyToAsync(fs).ConfigureAwait(false);
            }
            ZipFile.ExtractToDirectory(tmp, pgBin);
            File.Delete(tmp);
            Console.WriteLine("Downloaded and extracted PG_BIN_URL to pg_bin.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to download/extract PG_BIN_URL: {ex.Message}");
        }
    }
    else
    {
        Console.WriteLine("pg_bin appears empty or missing PostgreSQL binaries. Place the portable pg binaries into src/Infrastructure/Database/pg_bin and retry.");
    }
    string files;
    if (Directory.Exists(pgBin))
    {
        var names = Directory.EnumerateFileSystemEntries(pgBin).Select(p => Path.GetFileName(p) ?? string.Empty);
        files = string.Join("\n", names);
    }
    else files = "(not present)";
    Console.WriteLine("pg_bin contents:\n" + files);
    Environment.ExitCode = 2;
    return;
}

// Locate postgres executable
string? postgresExe = Directory.EnumerateFiles(pgBin, "postgres*.exe", SearchOption.TopDirectoryOnly).FirstOrDefault()
    ?? Directory.EnumerateFiles(pgBin, "postgres", SearchOption.TopDirectoryOnly).FirstOrDefault();

if (postgresExe == null)
{
    Console.WriteLine("Could not find postgres executable in pg_bin.");
    Environment.ExitCode = 3;
    return;
}

var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
var targetBase = Path.Combine(localAppData, "SoftwareOS", "ClinicOS", "pg_instance");
var targetBin = Path.Combine(targetBase, "bin");
var targetData = Path.Combine(targetBase, "data");
Directory.CreateDirectory(targetBin);
Directory.CreateDirectory(targetData);

// Copy binaries (idempotent)
foreach (var file in Directory.EnumerateFiles(pgBin))
{
    var dest = Path.Combine(targetBin, Path.GetFileName(file));
    if (!File.Exists(dest)) File.Copy(file, dest);
}

// Copy data template if present
if (Directory.Exists(pgDataTemplate))
{
    CopyDirectory(pgDataTemplate, targetData);
}

// Copy certificates
if (Directory.Exists(certs))
{
    foreach (var f in Directory.EnumerateFiles(certs))
    {
        var dest = Path.Combine(targetData, Path.GetFileName(f));
        try { File.Copy(f, dest, overwrite: true); } catch { }
    }
}

var postgresPath = Path.Combine(targetBin, Path.GetFileName(postgresExe));
Console.WriteLine($"Starting postgres: {postgresPath} with data dir {targetData}");

var psi = new ProcessStartInfo(postgresPath, $"-D \"{targetData}\"")
{
    UseShellExecute = false,
    CreateNoWindow = true,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
};

var proc = Process.Start(psi);
if (proc == null)
{
    Console.WriteLine("Failed to start postgres process.");
    Environment.ExitCode = 4;
    return;
}

_ = Task.Run(() =>
{
    try
    {
        while (!proc.HasExited)
        {
            var outLine = proc.StandardOutput.ReadLine();
            if (outLine != null) Console.WriteLine(outLine);
        }
    }
    catch { }
});

// Wait for TCP port 5432
var ready = await WaitForPortAsync("127.0.0.1", 5432, TimeSpan.FromSeconds(60));
if (ready)
{
    Console.WriteLine("Postgres appears ready on 127.0.0.1:5432");
    Console.WriteLine();
    
    // Run comprehensive smoke tests
    try
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(repoRoot)
                    .AddJsonFile("src/Presentation/appsettings.json", optional: true)
                    .AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                services.AddInfrastructureServices(context.Configuration);
            })
            .Build();

        var smokeTestRunner = new SmokeTestRunner(host.Services, host.Services.GetRequiredService<IConfiguration>());
        var result = await smokeTestRunner.RunAllTestsAsync();
        
        // Set exit code based on results
        Environment.ExitCode = result.ErrorCount > 0 ? 1 : 0;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error running smoke tests: {ex.Message}");
        Console.WriteLine(ex.StackTrace);
        Environment.ExitCode = 6;
    }
    
    // Cleanup: stop PostgreSQL
    try
    {
        if (!proc.HasExited)
        {
            proc.Kill(entireProcessTree: true);
            await proc.WaitForExitAsync();
            Console.WriteLine("PostgreSQL process stopped.");
        }
    }
    catch { }
    
    Console.WriteLine("Smoke test execution completed.");
}
else
{
    Console.WriteLine("Timed out waiting for Postgres to accept connections.");
    Environment.ExitCode = 5;
}

static string? FindRepoRoot()
{
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    for (int i = 0; i < 10 && dir != null; i++)
    {
        if (Directory.Exists(Path.Combine(dir.FullName, "src")) && Directory.Exists(Path.Combine(dir.FullName, "src", "Infrastructure", "Database")))
            return dir.FullName;
        dir = dir.Parent;
    }
    return null;
}

static void CopyDirectory(string source, string destination)
{
    foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
    {
        Directory.CreateDirectory(dir.Replace(source, destination));
    }
    foreach (var file in Directory.GetFiles(source, "*.*", SearchOption.AllDirectories))
    {
        var dest = file.Replace(source, destination);
        if (!File.Exists(dest)) File.Copy(file, dest);
    }
}

static async Task<bool> WaitForPortAsync(string host, int port, TimeSpan timeout)
{
    var sw = Stopwatch.StartNew();
    while (sw.Elapsed < timeout)
    {
        try
        {
            using var client = new TcpClient();
            var task = client.ConnectAsync(host, port);
            var completed = await Task.WhenAny(task, Task.Delay(500));
            if (completed == task && client.Connected) return true;
        }
        catch { }
        await Task.Delay(500);
    }
    return false;
}
