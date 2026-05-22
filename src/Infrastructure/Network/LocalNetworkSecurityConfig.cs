using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
using OS.Infrastructure.Data;
using OS.Application.Interfaces;

namespace OS.Infrastructure.Network;

/// <summary>
/// Configuración de seguridad de red local.
/// </summary>
public interface ILocalNetworkSecurityConfig
{
    /// <summary>
    /// Configura NpgsqlOptions para forzar TLS.
    /// </summary>
    void ConfigureNpgsqlOptions(Npgsql.NpgsqlConnectionStringBuilder connectionStringBuilder);

    /// <summary>
    /// Valida el thumbprint del certificado del servidor.
    /// </summary>
    bool ValidateServerCertificate(X509Certificate2? certificate, string? expectedThumbprint);

    /// <summary>
    /// Registra un intento de conexión en los logs de auditoría.
    /// </summary>
    Task LogConnectionAttemptAsync(string connectionString, bool isTls, bool isSuccess, string? errorMessage = null);
}

/// <summary>
/// Implementación de la configuración de seguridad de red local.
/// </summary>
public class LocalNetworkSecurityConfig : ILocalNetworkSecurityConfig
{
    private readonly IAuditService _auditService;
    private readonly string _expectedServerThumbprint;

    public LocalNetworkSecurityConfig(IAuditService auditService, string? expectedServerThumbprint = null)
    {
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _expectedServerThumbprint = expectedServerThumbprint ?? string.Empty;
    }

    /// <summary>
    /// Configura NpgsqlOptions para forzar TLS.
    /// </summary>
    public void ConfigureNpgsqlOptions(Npgsql.NpgsqlConnectionStringBuilder connectionStringBuilder)
    {
        // Forzar SSL/TLS
        connectionStringBuilder.SslMode = Npgsql.SslMode.Require;

        // Configurar validación de certificado personalizada
        if (!string.IsNullOrWhiteSpace(_expectedServerThumbprint))
        {
            // Custom validation is handled by the TLS certificate validator in MultiDbContextConfiguration.
        }
    }

    /// <summary>
    /// Valida el thumbprint del certificado del servidor.
    /// </summary>
    public bool ValidateServerCertificate(X509Certificate2? certificate, string? expectedThumbprint)
    {
        if (certificate == null)
            return false;

        if (string.IsNullOrWhiteSpace(expectedThumbprint))
            return true; // Si no hay thumbprint esperado, aceptar cualquier certificado válido

        var actualThumbprint = certificate.Thumbprint?.Replace(":", "").ToUpperInvariant();
        var expected = expectedThumbprint.Replace(":", "").ToUpperInvariant();

        return string.Equals(actualThumbprint, expected, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Registra un intento de conexión en los logs de auditoría.
    /// </summary>
    public async Task LogConnectionAttemptAsync(string connectionString, bool isTls, bool isSuccess, string? errorMessage = null)
    {
        var level = isSuccess ? "INFO" : "ERROR";
        var message = $"Intento de conexión: TLS={isTls}, Éxito={isSuccess}";

        if (!isSuccess && !isTls)
        {
            message = "CRITICAL: Intento de conexión sin TLS bloqueado";
            await _auditService.LogErrorAsync("NETWORK", "CONNECTION_NO_TLS", message, Guid.Empty);
        }
        else if (!isSuccess)
        {
            message = $"ERROR: Conexión fallida: {errorMessage}";
            await _auditService.LogErrorAsync("NETWORK", "CONNECTION_FAILED", message, Guid.Empty);
        }
        else
        {
            await _auditService.LogInfoAsync("NETWORK", "CONNECTION_SUCCESS", message, Guid.Empty);
        }
    }
}
