using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using OS.Application.Interfaces;

namespace OS.Infrastructure.Security;

/// <summary>
/// Servicio de validación de certificados TLS para conexiones a PostgreSQL.
/// Valida el thumbprint del certificado del servidor y registra intentos no TLS.
/// </summary>
public interface ITlsCertificateValidator
{
    /// <summary>
    /// Valida que el certificado del servidor coincida con el thumbprint esperado.
    /// </summary>
    /// <param name="certificate">Certificado del servidor</param>
    /// <param name="expectedThumbprint">Thumbprint esperado (en formato hexadecimal sin espacios)</param>
    /// <param name="chain">Cadena de certificación</param>
    /// <param name="sslPolicyErrors">Errores de política SSL</param>
    /// <returns>true si el certificado es válido, false en caso contrario</returns>
    bool ValidateServerCertificate(
        X509Certificate? certificate,
        string expectedThumbprint,
        X509Chain? chain,
        System.Net.Security.SslPolicyErrors sslPolicyErrors);

    /// <summary>
    /// Obtiene el thumbprint de un certificado en formato hexadecimal sin espacios.
    /// </summary>
    /// <param name="certificate">Certificado</param>
    /// <returns>Thumbprint en formato hexadecimal</returns>
    string GetCertificateThumbprint(X509Certificate certificate);

    /// <summary>
    /// Registra un intento de conexión no TLS como evento de seguridad CRITICAL.
    /// </summary>
    /// <param name="connectionString">Cadena de conexión intentada</param>
    /// <param name="reason">Razón del fallo TLS</param>
    void LogNonTlsAttempt(string connectionString, string reason);
}

/// <summary>
/// Implementación de validador de certificados TLS.
/// </summary>
public class TlsCertificateValidator : ITlsCertificateValidator
{
    private readonly IAuditService _auditService;

    public TlsCertificateValidator(IAuditService auditService)
    {
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
    }

    /// <summary>
    /// Valida que el certificado del servidor coincida con el thumbprint esperado.
    /// </summary>
    public bool ValidateServerCertificate(
        X509Certificate? certificate,
        string expectedThumbprint,
        X509Chain? chain,
        System.Net.Security.SslPolicyErrors sslPolicyErrors)
    {
        // Si hay errores de SSL básicos, rechazar
        if (sslPolicyErrors != System.Net.Security.SslPolicyErrors.None)
        {
            LogNonTlsAttempt("PostgreSQL connection", $"SSL Policy Errors: {sslPolicyErrors}");
            return false;
        }

        // Si no hay certificado, rechazar
        if (certificate == null)
        {
            LogNonTlsAttempt("PostgreSQL connection", "No certificate provided by server");
            return false;
        }

        // Obtener el thumbprint del certificado del servidor
        var serverThumbprint = GetCertificateThumbprint(certificate);

        // Validar que coincida con el esperado
        if (!string.Equals(serverThumbprint, expectedThumbprint, StringComparison.OrdinalIgnoreCase))
        {
            LogNonTlsAttempt(
                "PostgreSQL connection",
                $"Certificate thumbprint mismatch. Expected: {expectedThumbprint}, Got: {serverThumbprint}");
            return false;
        }

        // Validar la cadena de certificación si está disponible
        if (chain != null)
        {
            chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
            
            if (!chain.Build(new X509Certificate2(certificate)))
            {
                LogNonTlsAttempt("PostgreSQL connection", "Certificate chain validation failed");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Obtiene el thumbprint de un certificado en formato hexadecimal sin espacios.
    /// </summary>
    public string GetCertificateThumbprint(X509Certificate certificate)
    {
        if (certificate == null)
            throw new ArgumentNullException(nameof(certificate));

        var thumbprint = certificate.GetCertHashString();
        // Remover espacios y convertir a mayúsculas
        return thumbprint.Replace(" ", "").ToUpperInvariant();
    }

    /// <summary>
    /// Registra un intento de conexión no TLS como evento de seguridad CRITICAL.
    /// </summary>
    public void LogNonTlsAttempt(string connectionString, string reason)
    {
        try
        {
            // Ocultar la contraseña en la cadena de conexión para el log
            var sanitizedConnectionString = SanitizeConnectionString(connectionString);

            _ = _auditService.LogCriticalAsync(
                "SECURITY",
                "TLS_CONNECTION_ATTEMPT_FAILED",
                $"Intento de conexión sin TLS o con certificado inválido. Razón: {reason}. Connection: {sanitizedConnectionString}",
                null)
                .ContinueWith(t =>
                {
                    // Ignorar cualquier error en el log de auditoría para no afectar la conexión.
                }, TaskContinuationOptions.OnlyOnFaulted);
        }
        catch
        {
            // Si falla el logging, no debe interrumpir el flujo de la aplicación
            // En producción, esto debería loggearse a un archivo de fallback
        }
    }

    /// <summary>
    /// Sanitiza una cadena de conexión ocultando la contraseña.
    /// </summary>
    private string SanitizeConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return "[empty]";

        var sb = new StringBuilder(connectionString);
        var passwordIndex = connectionString.IndexOf("Password=", StringComparison.OrdinalIgnoreCase);
        
        if (passwordIndex >= 0)
        {
            var startIndex = passwordIndex + "Password=".Length;
            var endIndex = connectionString.IndexOf(';', startIndex);
            
            if (endIndex >= 0)
            {
                sb.Remove(startIndex, endIndex - startIndex);
                sb.Insert(startIndex, "*****");
            }
            else
            {
                // Si es el último parámetro (sin punto y coma al final)
                sb.Remove(startIndex, sb.Length - startIndex);
                sb.Append("*****");
            }
        }

        return sb.ToString();
    }
}
