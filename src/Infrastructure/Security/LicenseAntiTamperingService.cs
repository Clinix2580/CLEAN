using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace OS.Infrastructure.Security;

/// <summary>
/// Servicio de anti-tampering y anti-piratería para proteger el modelo de negocio.
/// Detecta intentos de manipulación del código, licencias y tiempo del sistema.
/// </summary>
public interface ILicenseAntiTamperingService
{
    /// <summary>
    /// Verifica la integridad del ensamblado actual.
    /// </summary>
    bool VerifyAssemblyIntegrity();

    /// <summary>
    /// Detecta si hay un depurador adjunto.
    /// </summary>
    bool IsDebuggerAttached();

    /// <summary>
    /// Verifica el tiempo del sistema con múltiples fuentes.
    /// </summary>
    bool VerifySystemTime();

    /// <summary>
    /// Genera un hash de integridad del archivo de licencia.
    /// </summary>
    string GenerateLicenseFileHash(string licenseFilePath);

    /// <summary>
    /// Verifica que el archivo de licencia no ha sido manipulado.
    /// </summary>
    bool VerifyLicenseFileIntegrity(string licenseFilePath, string expectedHash);

    /// <summary>
    /// Obtiene información del entorno para detectar virtualización/emulación.
    /// </summary>
    bool IsRunningInVirtualMachine();

    /// <summary>
    /// Valida que el proceso no está siendo analizado por herramientas de seguridad.
    /// </summary>
    bool IsBeingAnalyzed();
}

/// <summary>
/// Implementación del servicio de anti-tampering y anti-piratería.
/// </summary>
[SupportedOSPlatform("windows")]
public class LicenseAntiTamperingService : ILicenseAntiTamperingService
{
    private readonly ILogger<LicenseAntiTamperingService> _logger;
    private readonly string _expectedAssemblyHash;
    private readonly Dictionary<string, string> _fileHashes;

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemTime(ref SYSTEMTIME lpSystemTime);

    [DllImport("kernel32.dll")]
    private static extern bool SetSystemTime(ref SYSTEMTIME lpSystemTime);

    [DllImport("kernel32.dll")]
    private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

    [DllImport("kernel32.dll")]
    private static extern bool QueryPerformanceFrequency(out long lpFrequency);

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEMTIME
    {
        public short wYear;
        public short wMonth;
        public short wDayOfWeek;
        public short wDay;
        public short wHour;
        public short wMinute;
        public short wSecond;
        public short wMilliseconds;
    }

    public LicenseAntiTamperingService(ILogger<LicenseAntiTamperingService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Calcular hash esperado del ensamblado actual
        _expectedAssemblyHash = CalculateAssemblyHash();
        _fileHashes = new Dictionary<string, string>();
    }

    /// <summary>
    /// Verifica la integridad del ensamblado actual.
    /// </summary>
    public bool VerifyAssemblyIntegrity()
    {
        try
        {
            var currentHash = CalculateAssemblyHash();
            var isValid = currentHash == _expectedAssemblyHash;
            
            if (!isValid)
            {
                _logger.LogWarning("Integridad del ensamblado comprometida. Hash esperado: {Expected}, Hash actual: {Actual}",
                    _expectedAssemblyHash, currentHash);
            }
            
            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al verificar integridad del ensamblado");
            return false;
        }
    }

    /// <summary>
    /// Detecta si hay un depurador adjunto.
    /// </summary>
    public bool IsDebuggerAttached()
    {
        try
        {
            // Método 1: Debugger.IsAttached
            if (Debugger.IsAttached)
            {
                _logger.LogWarning("Depurador detectado mediante Debugger.IsAttached");
                return true;
            }

            // Método 2: Verificar si el proceso está siendo depurado
            using var currentProcess = Process.GetCurrentProcess();
            if (currentProcess.MainWindowHandle != IntPtr.Zero && IsDebuggerPresent())
            {
                _logger.LogWarning("Depurador detectado mediante IsDebuggerPresent");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al detectar depurador");
            return false;
        }
    }

    /// <summary>
    /// Verifica el tiempo del sistema con múltiples fuentes.
    /// </summary>
    public bool VerifySystemTime()
    {
        try
        {
            var systemTime = DateTime.UtcNow;
            var ntpTime = GetNtpTime();
            
            if (ntpTime.HasValue)
            {
                var difference = Math.Abs((systemTime - ntpTime.Value).TotalMinutes);
                
                if (difference > 60) // Más de 1 hora de diferencia
                {
                    _logger.LogWarning("Tiempo del sistema desincronizado. Sistema: {SystemTime}, NTP: {NtpTime}, Diferencia: {Difference} minutos",
                        systemTime, ntpTime.Value, difference);
                    return false;
                }
            }

            // Verificar que el tiempo no sea irrazonable (ej. año 1980 o 2100)
            if (systemTime.Year < 2020 || systemTime.Year > 2030)
            {
                _logger.LogWarning("Tiempo del sistema fuera de rango razonable: {SystemTime}", systemTime);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al verificar tiempo del sistema");
            return false;
        }
    }

    /// <summary>
    /// Genera un hash de integridad del archivo de licencia.
    /// </summary>
    public string GenerateLicenseFileHash(string licenseFilePath)
    {
        if (!File.Exists(licenseFilePath))
            throw new FileNotFoundException("Archivo de licencia no encontrado", licenseFilePath);

        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(licenseFilePath);
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Verifica que el archivo de licencia no ha sido manipulado.
    /// </summary>
    public bool VerifyLicenseFileIntegrity(string licenseFilePath, string expectedHash)
    {
        try
        {
            var currentHash = GenerateLicenseFileHash(licenseFilePath);
            var isValid = currentHash == expectedHash;
            
            if (!isValid)
            {
                _logger.LogWarning("Integridad del archivo de licencia comprometida. Hash esperado: {Expected}, Hash actual: {Actual}",
                    expectedHash, currentHash);
            }
            
            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al verificar integridad del archivo de licencia");
            return false;
        }
    }

    /// <summary>
    /// Obtiene información del entorno para detectar virtualización/emulación.
    /// </summary>
    public bool IsRunningInVirtualMachine()
    {
        try
        {
            // Verificar indicadores comunes de VM
            var vmIndicators = new[]
            {
                "VMWARE",
                "VIRTUALBOX",
                "QEMU",
                "XEN",
                "HYPER-V",
                "KVM"
            };

            var systemInfo = GetSystemInfo();
            var systemInfoUpper = systemInfo.ToUpperInvariant();

            foreach (var indicator in vmIndicators)
            {
                if (systemInfoUpper.Contains(indicator))
                {
                    _logger.LogWarning("Entorno de virtualización detectado: {Indicator}", indicator);
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al detectar entorno de virtualización");
            return false;
        }
    }

    /// <summary>
    /// Valida que el proceso no está siendo analizado por herramientas de seguridad.
    /// </summary>
    public bool IsBeingAnalyzed()
    {
        try
        {
            var suspiciousProcesses = new[]
            {
                "ida",
                "ida64",
                "x64dbg",
                "x32dbg",
                "ollydbg",
                "wireshark",
                "fiddler",
                "procmon",
                "procexp",
                "dnspy",
                "ilspy",
                "dotpeek",
                "reflector"
            };

            var currentProcesses = Process.GetProcesses();
            var currentProcessNames = currentProcesses.Select(p => p.ProcessName.ToLowerInvariant()).ToList();

            foreach (var suspicious in suspiciousProcesses)
            {
                if (currentProcessNames.Contains(suspicious))
                {
                    _logger.LogWarning("Herramienta de análisis detectada: {Process}", suspicious);
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al detectar herramientas de análisis");
            return false;
        }
    }

    [SuppressMessage("IL", "IL3000")]
    private static string CalculateAssemblyHash()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var assemblyLocation = assembly.Location;
            
            if (string.IsNullOrEmpty(assemblyLocation) || !File.Exists(assemblyLocation))
            {
                // Fallback: usar nombre del ensamblado
                return assembly.FullName?.GetHashCode().ToString("X16") ?? "UNKNOWN";
            }

            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(assemblyLocation);
            var hash = sha256.ComputeHash(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch
        {
            return "UNKNOWN";
        }
    }

    private static string GetSystemInfo()
    {
        try
        {
            var sb = new StringBuilder();
            
            // Información del sistema
            sb.AppendLine($"OS: {Environment.OSVersion}");
            sb.AppendLine($"MachineName: {Environment.MachineName}");
            sb.AppendLine($"ProcessorCount: {Environment.ProcessorCount}");
            
            // Información de BIOS (si es posible)
            try
            {
                var biosInfo = GetBiosInfo();
                if (!string.IsNullOrEmpty(biosInfo))
                {
                    sb.AppendLine($"BIOS: {biosInfo}");
                }
            }
            catch
            {
                // Ignorar errores de acceso a BIOS
            }

            return sb.ToString();
        }
        catch
        {
            return "UNKNOWN";
        }
    }

    private static string? GetBiosInfo()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS");
            using var results = searcher.Get();
            
            foreach (ManagementObject obj in results)
            {
                var serialNumber = obj["SerialNumber"]?.ToString();
                var manufacturer = obj["Manufacturer"]?.ToString();
                
                if (!string.IsNullOrEmpty(manufacturer) && !string.IsNullOrEmpty(serialNumber))
                {
                    return $"{manufacturer} - {serialNumber}";
                }
            }
        }
        catch
        {
            // Ignorar errores de WMI
        }

        return null;
    }

    private static DateTime? GetNtpTime()
    {
        try
        {
            // Implementación simplificada de consulta NTP
            // En producción, usar una biblioteca dedicada como NtpClient
            var ntpServer = "pool.ntp.org";
            var ntpPort = 123;
            
            using var udpClient = new System.Net.Sockets.UdpClient();
            var endPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Parse(ntpServer), ntpPort);
            udpClient.Connect(endPoint);
            
            var ntpData = new byte[48];
            ntpData[0] = 0x1B; // LI = 0, VN = 3, Mode = 3 (Client)
            
            udpClient.Send(ntpData, ntpData.Length);
            var response = udpClient.Receive(ref endPoint);
            
            if (response.Length >= 48)
            {
                var seconds = BitConverter.ToUInt32(response, 40);
                var ntpEpoch = new DateTime(1900, 1, 1);
                return ntpEpoch.AddSeconds(seconds);
            }
        }
        catch
        {
            // Ignorar errores de NTP
        }

        return null;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool IsDebuggerPresent();
}
