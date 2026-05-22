using System.Management;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using OS.Domain.Interfaces;

namespace OS.Infrastructure.Security;

/// <summary>
/// Representa un identificador único de hardware basado en CPU y Motherboard.
/// Se utiliza como semilla para la derivación de claves criptográficas.
/// </summary>
[SupportedOSPlatform("windows")]
public class HardwareId
{
    /// <summary>
    /// Identificador único del procesador.
    /// </summary>
    public string CpuId { get; }

    /// <summary>
    /// Identificador único de la placa madre.
    /// </summary>
    public string MotherboardId { get; }

    /// <summary>
    /// Hash combinado de CPU + Motherboard para uso como semilla.
    /// </summary>
    public string CombinedHash { get; }

    public HardwareId(string cpuId, string motherboardId)
    {
        CpuId = cpuId;
        MotherboardId = motherboardId;
        CombinedHash = ComputeHash($"{cpuId}:{motherboardId}");
    }

    private static string ComputeHash(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}

/// <summary>
/// Servicio para obtener identificadores únicos del hardware.
/// Utiliza WMI para leer CPU y Motherboard, con fallbacks para máquinas virtuales.
/// </summary>
public class HardwareIdentifier : IHardwareIdentifier
{
    /// <summary>
    /// Obtiene el identificador único del hardware de forma asíncrona.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
    public async Task<HardwareId> GetHardwareIdAsync()
    {
        return await Task.Run(() =>
        {
            var cpuId = Normalize(GetCpuId());
            var motherboardId = Normalize(GetMotherboardId());
            return new HardwareId(cpuId, motherboardId);
        });
    }

    /// <summary>
    /// Obtiene el identificador del procesador usando WMI.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
    private static string GetCpuId()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
            using var results = searcher.Get();
            foreach (ManagementObject obj in results)
            {
                var processorId = obj["ProcessorId"]?.ToString();
                if (!string.IsNullOrEmpty(processorId))
                    return processorId;
            }
        }
        catch
        {
            // Fallback: VM o sistema sin WMI
        }
        return GenerateFallbackId("CPU");
    }

    /// <summary>
    /// Obtiene el identificador de la placa madre usando WMI.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
    private static string GetMotherboardId()
    {
        var parts = new List<string>();

        // Intentar obtener número de serie de BaseBoard
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
            using var results = searcher.Get();
            foreach (ManagementObject obj in results)
            {
                var serialNumber = obj["SerialNumber"]?.ToString();
                if (!string.IsNullOrEmpty(serialNumber) && serialNumber != "To be filled by O.E.M.")
                {
                    parts.Add(serialNumber);
                }
            }
        }
        catch
        {
            // Continuar con fallback
        }

        // Obtener Machine GUID
        parts.Add(GetMachineGuid());

        // Obtener serial del volumen del sistema
        parts.Add(GetSystemDriveSerial());

        if (parts.Count > 0)
            return string.Join(":", parts);

        return GenerateFallbackId("MB");
    }

    /// <summary>
    /// Genera un ID de fallback basado en información del sistema.
    /// Se utiliza cuando WMI no está disponible (VMs, contenedores).
    /// </summary>
    private static string GenerateFallbackId(string prefix)
    {
        var parts = new[]
        {
            prefix,
            GetMachineGuid(),
            GetSystemDriveSerial()
        };

        var combined = string.Join(":", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        var bytes = Encoding.UTF8.GetBytes(combined);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash)[..32];
    }

    /// <summary>
    /// Obtiene el GUID único de la máquina del registro de Windows.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
    private static string GetMachineGuid()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            var value = key?.GetValue("MachineGuid")?.ToString();
            return !string.IsNullOrEmpty(value) ? value : "NO_MACHINE_GUID";
        }
        catch
        {
            return "NO_MACHINE_GUID";
        }
    }

    /// <summary>
    /// Obtiene el número de serie del volumen del sistema.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
    private static string GetSystemDriveSerial()
    {
        try
        {
            var systemDrive = Path.GetPathRoot(Environment.SystemDirectory)?.TrimEnd('\\') ?? "C:";
            using var searcher = new ManagementObjectSearcher($"SELECT VolumeSerialNumber FROM Win32_LogicalDisk WHERE DeviceID='{systemDrive}'");
            using var results = searcher.Get();
            foreach (ManagementObject obj in results)
            {
                var serial = obj["VolumeSerialNumber"]?.ToString();
                if (!string.IsNullOrWhiteSpace(serial))
                    return serial;
            }
        }
        catch
        {
            // Continuar con fallback
        }

        return "NO_VOLUME_SERIAL";
    }

    /// <summary>
    /// Normaliza cadenas para uso en identificadores.
    /// </summary>
    private static string Normalize(string value)
    {
        return value?.Trim().ToUpperInvariant() ?? "UNKNOWN";
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
    async Task<OS.Domain.ValueObjects.HardwareId> IHardwareIdentifier.GetHardwareIdAsync()
    {
        var hardwareId = await GetHardwareIdAsync();
        return new OS.Domain.ValueObjects.HardwareId(hardwareId.CpuId, hardwareId.MotherboardId);
    }

    string IHardwareIdentifier.GetCpuId() => GetCpuId();

    string IHardwareIdentifier.GetMotherboardId() => GetMotherboardId();
}
