using System.Collections.Generic;
using System.Linq;

namespace OS.Presentation.UI.Services;

/// <summary>
/// Servicio que proporciona descripciones detalladas de funciones por módulo
/// en español e inglés, para usar en diálogos del modo Demo ReadOnly.
/// </summary>
public interface IModuleDescriptionService
{
    /// <summary>
    /// Obtiene la descripción de un módulo específico en el idioma activo.
    /// </summary>
    string GetModuleDescription(string moduleName);

    /// <summary>
    /// Obtiene la descripción de una función específica dentro de un módulo.
    /// </summary>
    string GetFunctionDescription(string moduleName, string functionName);

    /// <summary>
    /// Obtiene todos los módulos disponibles con sus descripciones.
    /// </summary>
    IReadOnlyDictionary<string, string> GetAllModuleDescriptions();
}

/// <summary>
/// Implementación de IModuleDescriptionService con datos embebidos.
/// </summary>
public class ModuleDescriptionService : IModuleDescriptionService
{
    private readonly Dictionary<string, ModuleInfo> _modules;

    public ModuleDescriptionService()
    {
        _modules = new Dictionary<string, ModuleInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["Dashboard"] = new ModuleInfo
            {
                Name = "Dashboard",
                DescriptionEs = "Panel principal con resumen de actividad, estadísticas y widgets de monitoreo.",
                DescriptionEn = "Main dashboard with activity summary, statistics, and monitoring widgets.",
                Functions = new Dictionary<string, FunctionInfo>
                {
                    ["ViewStats"] = new FunctionInfo
                    {
                        Name = "ViewStats",
                        DescriptionEs = "Ver estadísticas de pacientes, citas y actividad del sistema.",
                        DescriptionEn = "View patient statistics, appointments, and system activity."
                    },
                    ["ViewAlerts"] = new FunctionInfo
                    {
                        Name = "ViewAlerts",
                        DescriptionEs = "Ver alertas y notificaciones del sistema.",
                        DescriptionEn = "View system alerts and notifications."
                    }
                }
            },
            ["Patients"] = new ModuleInfo
            {
                Name = "Patients",
                DescriptionEs = "Gestión de pacientes: registro, historial médico, expedientes clínicos.",
                DescriptionEn = "Patient management: registration, medical history, clinical records.",
                Functions = new Dictionary<string, FunctionInfo>
                {
                    ["CreatePatient"] = new FunctionInfo
                    {
                        Name = "CreatePatient",
                        DescriptionEs = "Registrar nuevo paciente con datos personales y médicos.",
                        DescriptionEn = "Register new patient with personal and medical data."
                    },
                    ["EditPatient"] = new FunctionInfo
                    {
                        Name = "EditPatient",
                        DescriptionEs = "Editar información de paciente existente.",
                        DescriptionEn = "Edit existing patient information."
                    },
                    ["ViewMedicalHistory"] = new FunctionInfo
                    {
                        Name = "ViewMedicalHistory",
                        DescriptionEs = "Ver historial médico completo del paciente.",
                        DescriptionEn = "View complete patient medical history."
                    }
                }
            },
            ["Appointments"] = new ModuleInfo
            {
                Name = "Appointments",
                DescriptionEs = "Programación y gestión de citas médicas.",
                DescriptionEn = "Medical appointment scheduling and management.",
                Functions = new Dictionary<string, FunctionInfo>
                {
                    ["ScheduleAppointment"] = new FunctionInfo
                    {
                        Name = "ScheduleAppointment",
                        DescriptionEs = "Programar nueva cita con doctor y paciente.",
                        DescriptionEn = "Schedule new appointment with doctor and patient."
                    },
                    ["CancelAppointment"] = new FunctionInfo
                    {
                        Name = "CancelAppointment",
                        DescriptionEs = "Cancelar o reagendar cita existente.",
                        DescriptionEn = "Cancel or reschedule existing appointment."
                    }
                }
            },
            ["MedicalRecords"] = new ModuleInfo
            {
                Name = "MedicalRecords",
                DescriptionEs = "Expedientes médicos electrónicos (EMR) con diagnósticos y tratamientos.",
                DescriptionEn = "Electronic Medical Records (EMR) with diagnoses and treatments.",
                Functions = new Dictionary<string, FunctionInfo>
                {
                    ["CreateRecord"] = new FunctionInfo
                    {
                        Name = "CreateRecord",
                        DescriptionEs = "Crear nuevo expediente médico con diagnóstico y tratamiento.",
                        DescriptionEn = "Create new medical record with diagnosis and treatment."
                    },
                    ["UpdateRecord"] = new FunctionInfo
                    {
                        Name = "UpdateRecord",
                        DescriptionEs = "Actualizar expediente médico existente.",
                        DescriptionEn = "Update existing medical record."
                    }
                }
            },
            ["Reports"] = new ModuleInfo
            {
                Name = "Reports",
                DescriptionEs = "Generación de reportes clínicos, administrativos y estadísticos.",
                DescriptionEn = "Clinical, administrative, and statistical report generation.",
                Functions = new Dictionary<string, FunctionInfo>
                {
                    ["GenerateReport"] = new FunctionInfo
                    {
                        Name = "GenerateReport",
                        DescriptionEs = "Generar reporte personalizado con filtros y parámetros.",
                        DescriptionEn = "Generate custom report with filters and parameters."
                    },
                    ["ExportReport"] = new FunctionInfo
                    {
                        Name = "ExportReport",
                        DescriptionEs = "Exportar reporte a PDF, Excel o formato compatible.",
                        DescriptionEn = "Export report to PDF, Excel, or compatible format."
                    }
                }
            },
            ["Settings"] = new ModuleInfo
            {
                Name = "Settings",
                DescriptionEs = "Configuración del sistema, usuarios, permisos y preferencias.",
                DescriptionEn = "System configuration, users, permissions, and preferences.",
                Functions = new Dictionary<string, FunctionInfo>
                {
                    ["ConfigureSystem"] = new FunctionInfo
                    {
                        Name = "ConfigureSystem",
                        DescriptionEs = "Configurar parámetros del sistema y preferencias.",
                        DescriptionEn = "Configure system parameters and preferences."
                    },
                    ["ManageUsers"] = new FunctionInfo
                    {
                        Name = "ManageUsers",
                        DescriptionEs = "Gestionar usuarios, roles y permisos de acceso.",
                        DescriptionEn = "Manage users, roles, and access permissions."
                    }
                }
            },
            ["Products"] = new ModuleInfo
            {
                Name = "Products",
                DescriptionEs = "Gestión de inventario de productos, medicamentos y suministros.",
                DescriptionEn = "Product inventory management, medications, and supplies.",
                Functions = new Dictionary<string, FunctionInfo>
                {
                    ["AddProduct"] = new FunctionInfo
                    {
                        Name = "AddProduct",
                        DescriptionEs = "Agregar nuevo producto al inventario.",
                        DescriptionEn = "Add new product to inventory."
                    },
                    ["UpdateStock"] = new FunctionInfo
                    {
                        Name = "UpdateStock",
                        DescriptionEs = "Actualizar niveles de stock y existencias.",
                        DescriptionEn = "Update stock levels and inventory."
                    }
                }
            }
        };
    }

    public string GetModuleDescription(string moduleName)
    {
        if (_modules.TryGetValue(moduleName, out var module))
        {
#if LANG_ES
            return module.DescriptionEs;
#else
            return module.DescriptionEn;
#endif
        }

        // Descripción genérica si el módulo no está registrado
#if LANG_ES
        return $"Módulo {moduleName}: Funcionalidad de gestión y administración.";
#else
        return $"Module {moduleName}: Management and administration functionality.";
#endif
    }

    public string GetFunctionDescription(string moduleName, string functionName)
    {
        if (_modules.TryGetValue(moduleName, out var module) &&
            module.Functions.TryGetValue(functionName, out var function))
        {
#if LANG_ES
            return function.DescriptionEs;
#else
            return function.DescriptionEn;
#endif
        }

        // Descripción genérica si la función no está registrada
#if LANG_ES
        return $"Función {functionName} en módulo {moduleName}.";
#else
        return $"Function {functionName} in module {moduleName}.";
#endif
    }

    public IReadOnlyDictionary<string, string> GetAllModuleDescriptions()
    {
        var result = new Dictionary<string, string>();
        foreach (var kvp in _modules)
        {
#if LANG_ES
            result[kvp.Key] = kvp.Value.DescriptionEs;
#else
            result[kvp.Key] = kvp.Value.DescriptionEn;
#endif
        }
        return result;
    }

    private class ModuleInfo
    {
        public string Name { get; set; } = string.Empty;
        public string DescriptionEs { get; set; } = string.Empty;
        public string DescriptionEn { get; set; } = string.Empty;
        public Dictionary<string, FunctionInfo> Functions { get; set; } = new();
    }

    private class FunctionInfo
    {
        public string Name { get; set; } = string.Empty;
        public string DescriptionEs { get; set; } = string.Empty;
        public string DescriptionEn { get; set; } = string.Empty;
    }
}
