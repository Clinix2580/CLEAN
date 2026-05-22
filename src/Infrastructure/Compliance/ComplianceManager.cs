using OS.Application.Interfaces;
using OS.Domain.Entities;
using OS.Domain.Enums;

namespace OS.Infrastructure.Compliance;

public class ComplianceManager : IComplianceManager
{
    public ComplianceSettings Settings { get; private set; } = new();

    public bool IsAuditEnabled => Settings.AuditEnabled;
    public int AutoLogoutMinutes => Settings.AutoLogoutMinutes;
    public bool AreArcoRightsEnabled => Settings.ArcoRightsEnabled;

    public void InitializeForRegion(ComplianceRegion region, Language language)
    {
        Settings = new ComplianceSettings
        {
            Region = region,
            Language = language,
            AuditEnabled = true,
            DataEncryptionEnabled = true,
            AutoLogoutMinutes = region == ComplianceRegion.UsaHipaa ? 15 : 30,
            ArcoRightsEnabled = region == ComplianceRegion.MexicoLfpdppp,
            PrivacyNoticeVersion = "1.0"
        };
    }

    public string GetPrivacyNoticeText()
    {
        return Settings.Region == ComplianceRegion.UsaHipaa
            ? GetHipaaPrivacyNotice()
            : GetLfpdpppPrivacyNotice();
    }

    public string GetEulaText()
    {
        return Settings.Region == ComplianceRegion.UsaHipaa
            ? GetHipaaEula()
            : GetLfpdpppEula();
    }

    public string GetResponsibilityDisclaimerText()
    {
        return Settings.Region == ComplianceRegion.UsaHipaa
            ? GetHipaaDisclaimer()
            : GetLfpdpppDisclaimer();
    }

    public string GetComplianceFrameworkName()
    {
        return Settings.Region == ComplianceRegion.UsaHipaa
            ? "HIPAA (Health Insurance Portability and Accountability Act)"
            : "LFPDPPP (Ley Federal de Protección de Datos Personales en Posesión de los Particulares)";
    }

    private static string GetHipaaPrivacyNotice()
    {
        return @"
PRIVACY NOTICE - HIPAA COMPLIANCE

This software complies with the Health Insurance Portability and Accountability Act (HIPAA).

Your Responsibilities:
• Maintain confidentiality of Protected Health Information (PHI)
• Report any suspected breaches immediately
• Log out when not actively using the system
• Do not share credentials with others

Audit Trail:
All actions are logged and monitored. Attempts to access unauthorized data will be recorded.

Auto-Logout: 15 minutes of inactivity
Data Encryption: AES-256
";
    }

    private static string GetLfpdpppPrivacyNotice()
    {
        return @"
AVISO DE PRIVACIDAD - LFPDPPP

Este software cumple con la Ley Federal de Protección de Datos Personales en Posesión de los Particulares.

Responsable: SoftwareOS
Finalidad: Gestión de datos personales de pacientes/clientes

Derechos ARCO:
• Acceso: Conocer qué datos personales tenemos
• Rectificación: Corregir datos inexactos
• Cancelación: Solicitar eliminación de datos
• Oposición: Negar uso de datos para fines específicos

Contacto para ejercer derechos: privacidad@softwareos.com

Cierre automático: 30 minutos de inactividad
Encriptación: AES-256
";
    }

    private static string GetHipaaEula()
    {
        return @"
END USER LICENSE AGREEMENT - HIPAA COMPLIANCE

By using this software, you agree to:
1. Comply with all HIPAA regulations
2. Maintain appropriate security safeguards
3. Report security incidents within 24 hours
4. Use the software only for authorized purposes
5. Accept audit logging of all activities

Violation of these terms may result in termination of access and legal action.
";
    }

    private static string GetLfpdpppEula()
    {
        return @"
CONTRATO DE LICENCIA DE USUARIO FINAL - LFPDPPP

Al usar este software, usted acepta:
1. Cumplir con la LFPDPPP y regulaciones aplicables
2. Mantener salvaguardas de seguridad apropiadas
3. Reportar incidentes de seguridad dentro de 24 horas
4. Usar el software solo para fines autorizados
5. Aceptar el registro de auditoría de todas las actividades

El incumplimiento de estos términos puede resultar en la terminación del acceso y acciones legales.
";
    }

    private static string GetHipaaDisclaimer()
    {
        return @"
HIPAA RESPONSIBILITY DISCLAIMER

ClinicOS is a technical safeguard platform. It does not replace legal advice, compliance policies, user training, Business Associate Agreements, breach notification procedures, backup policies or administrative controls required by HIPAA.

The operator is responsible for configuring users, roles, access permissions, retention, audit review and lawful handling of Protected Health Information.
";
    }

    private static string GetLfpdpppDisclaimer()
    {
        return @"
DESCARGO DE RESPONSABILIDAD LFPDPPP

ClinicOS es una plataforma tecnica de salvaguardas, consentimiento, auditoria y cifrado local. No sustituye asesoria legal, politicas internas, capacitacion, contratos, gestion documental ni respuesta formal ante solicitudes ARCO.

El responsable del tratamiento debe configurar finalidades, avisos de privacidad, permisos de usuarios, plazos de conservacion y procedimientos para atender derechos de Acceso, Rectificacion, Cancelacion y Oposicion.
";
    }
}
