using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.Versioning;
using OS.Infrastructure.Security;

namespace OS.Infrastructure.Data.Configuration;

/// <summary>
/// Configuración de inyección de dependencias para los tres DbContext de PostgreSQL.
/// </summary>
[SupportedOSPlatform("windows")]
public static class MultiDbContextConfiguration
{
    /// <summary>
    /// Registra los tres DbContext (Clinic, License, Audit) en el contenedor de servicios.
    /// Requiere que estén configuradas las siguientes variables de entorno:
    /// - CLINICOS_DB_CONNECTION: Connection string para clinicos_db
    /// - LICENSES_DB_CONNECTION: Connection string para licenses_db (usar credenciales admin)
    /// - AUDIT_DB_CONNECTION: Connection string para audit_db (usar credenciales admin)
    /// 
    /// Las cadenas de conexión pueden estar cifradas con el prefijo "enc:" usando DPAPI.
    /// </summary>
    public static IServiceCollection AddMultiDbContexts(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Registrar DataProtectionService primero (no tiene dependencias)
        services.AddSingleton<IDataProtectionService, DataProtectionService>();

        // Usar una instancia local para descifrar cadenas de conexión sin construir un proveedor temporal.
        var dataProtectionService = new DataProtectionService();

        var clinicDbConnection = DecryptConnectionString(
            configuration.GetConnectionString("ClinicDb")
            ?? Environment.GetEnvironmentVariable("CLINICOS_DB_CONNECTION")
            ?? throw new InvalidOperationException("Missing ClinicDb connection string or CLINICOS_DB_CONNECTION environment variable"),
            dataProtectionService);

        var licenseDbConnection = DecryptConnectionString(
            configuration.GetConnectionString("LicenseDb")
            ?? Environment.GetEnvironmentVariable("LICENSES_DB_CONNECTION")
            ?? throw new InvalidOperationException("Missing LicenseDb connection string or LICENSES_DB_CONNECTION environment variable"),
            dataProtectionService);

        var auditDbConnection = DecryptConnectionString(
            configuration.GetConnectionString("AuditDb")
            ?? Environment.GetEnvironmentVariable("AUDIT_DB_CONNECTION")
            ?? throw new InvalidOperationException("Missing AuditDb connection string or AUDIT_DB_CONNECTION environment variable"),
            dataProtectionService);

        // Obtener el thumbprint del certificado TLS desde configuración
        var expectedCertThumbprint = configuration["PostgreSQL:CertificateThumbprint"] 
            ?? configuration.GetSection("ConnectionStrings")["CertificateThumbprint"];
        var normalizedThumbprint = expectedCertThumbprint?
            .Replace(":", string.Empty)
            .Replace(" ", string.Empty)
            .ToUpperInvariant();

        bool ValidateServerCertificate(System.Security.Cryptography.X509Certificates.X509Certificate? certificate, System.Net.Security.SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors != System.Net.Security.SslPolicyErrors.None)
                return false;

            if (certificate == null || string.IsNullOrWhiteSpace(normalizedThumbprint))
                return false;

            var actualThumbprint = certificate.GetCertHashString()
                .Replace(":", string.Empty)
                .Replace(" ", string.Empty)
                .ToUpperInvariant();

            return string.Equals(actualThumbprint, normalizedThumbprint, StringComparison.OrdinalIgnoreCase);
        }

        // ClinicDbContext - Base de datos de clínica
        services.AddDbContext<ClinicDbContext>(options =>
        {
            options.UseNpgsql(clinicDbConnection, postgresOptions =>
            {
                postgresOptions.EnableRetryOnFailure(3);
                
                // Configurar validación de certificado TLS si se proporciona thumbprint
                if (!string.IsNullOrEmpty(normalizedThumbprint))
                {
                    postgresOptions.RemoteCertificateValidationCallback(
                        (sender, certificate, chain, sslPolicyErrors) =>
                            ValidateServerCertificate(certificate, sslPolicyErrors));
                }
            });

#if DEBUG
            options.EnableDetailedErrors();
            options.EnableSensitiveDataLogging();
#endif
        });

        // LicenseDbContext - Base de datos de licencias
        services.AddDbContext<LicenseDbContext>(options =>
        {
            options.UseNpgsql(licenseDbConnection, postgresOptions =>
            {
                postgresOptions.EnableRetryOnFailure(3);
                
                // Configurar validación de certificado TLS si se proporciona thumbprint
                if (!string.IsNullOrEmpty(normalizedThumbprint))
                {
                    postgresOptions.RemoteCertificateValidationCallback(
                        (sender, certificate, chain, sslPolicyErrors) =>
                            ValidateServerCertificate(certificate, sslPolicyErrors));
                }
            });

#if DEBUG
            options.EnableDetailedErrors();
#endif
        });

        // AuditDbContext - Base de datos de auditoría
        services.AddDbContext<AuditDbContext>(options =>
        {
            options.UseNpgsql(auditDbConnection, postgresOptions =>
            {
                postgresOptions.EnableRetryOnFailure(3);
                
                // Configurar validación de certificado TLS si se proporciona thumbprint
                if (!string.IsNullOrEmpty(normalizedThumbprint))
                {
                    postgresOptions.RemoteCertificateValidationCallback(
                        (sender, certificate, chain, sslPolicyErrors) =>
                            ValidateServerCertificate(certificate, sslPolicyErrors));
                }
            });

#if DEBUG
            options.EnableDetailedErrors();
#endif
        });

        return services;
    }

    /// <summary>
    /// Descifra una cadena de conexión si está cifrada con el prefijo "enc:".
    /// </summary>
    private static string DecryptConnectionString(string connectionString, IDataProtectionService dataProtectionService)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        const string encryptionPrefix = "enc:";
        if (connectionString.StartsWith(encryptionPrefix, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var encryptedValue = connectionString.Substring(encryptionPrefix.Length);
                return dataProtectionService.UnprotectForMachine(encryptedValue);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Error al descifrar la cadena de conexión. Asegúrese de que fue cifrada en esta máquina usando DPAPI.",
                    ex);
            }
        }

        return connectionString;
    }

    /// <summary>
    /// Ejecuta las migraciones pendientes para todos los DbContext.
    /// Se debe llamar después de que la aplicación haya arrancado.
    /// </summary>
    public static async Task MigrateAllDatabasesAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();

        // Migrar ClinicDb
        try
        {
            var clinicDbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
#pragma warning disable IL3050 // EF Core migrations are isolated to the local PostgreSQL schema bootstrap path.
            await clinicDbContext.Database.MigrateAsync();
#pragma warning restore IL3050
            Console.WriteLine("✓ ClinicDb migrations applied successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error applying ClinicDb migrations: {ex.Message}");
            throw;
        }

        // Migrar LicenseDb
        try
        {
            var licenseDbContext = scope.ServiceProvider.GetRequiredService<LicenseDbContext>();
#pragma warning disable IL3050 // EF Core migrations are isolated to the local PostgreSQL schema bootstrap path.
            await licenseDbContext.Database.MigrateAsync();
#pragma warning restore IL3050
            Console.WriteLine("✓ LicenseDb migrations applied successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error applying LicenseDb migrations: {ex.Message}");
            throw;
        }

        // Migrar AuditDb
        try
        {
            var auditDbContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
#pragma warning disable IL3050 // EF Core migrations are isolated to the local PostgreSQL schema bootstrap path.
            await auditDbContext.Database.MigrateAsync();
#pragma warning restore IL3050
            Console.WriteLine("✓ AuditDb migrations applied successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error applying AuditDb migrations: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Valida que todas las bases de datos estén accesibles y tengan el esquema correcto.
    /// </summary>
    public static async Task<bool> ValidateAllDatabasesAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();

        try
        {
            // Validar ClinicDb
            var clinicDb = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            await clinicDb.Database.ExecuteSqlRawAsync("SELECT 1");
            Console.WriteLine("✓ ClinicDb is accessible");

            // Validar LicenseDb
            var licenseDb = scope.ServiceProvider.GetRequiredService<LicenseDbContext>();
            await licenseDb.Database.ExecuteSqlRawAsync("SELECT 1");
            Console.WriteLine("✓ LicenseDb is accessible");

            // Validar AuditDb
            var auditDb = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
            await auditDb.Database.ExecuteSqlRawAsync("SELECT 1");
            Console.WriteLine("✓ AuditDb is accessible");

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Database validation failed: {ex.Message}");
            return false;
        }
    }
}
