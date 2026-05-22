namespace OS.Domain.Enums;

/// <summary>
/// Represents the operating mode of the system.
/// Determines feature availability and compliance requirements.
/// </summary>
public enum SystemMode
{
    /// <summary>
    /// Full functionality with all features enabled.
    /// </summary>
    Full,

    /// <summary>
    /// Demo mode with limited features and sample data.
    /// </summary>
    Demo
}

/// <summary>
/// Represents the type of system being run.
/// </summary>
public enum SystemType
{
    /// <summary>
    /// Healthcare management system (ClinicOS).
    /// </summary>
    ClinicOS,

    /// <summary>
    /// Commerce/retail management system (CommerceOS).
    /// </summary>
    CommerceOS
}

/// <summary>
/// Represents the compliance region and regulatory framework.
/// Determines which data protection and privacy regulations apply.
/// </summary>
public enum ComplianceRegion
{
    /// <summary>
    /// United States with HIPAA (Health Insurance Portability and Accountability Act) compliance.
    /// </summary>
    UsaHipaa,

    /// <summary>
    /// Mexico with LFPDPPP (Ley Federal de Protección de Datos Personales en Posesión de Particulares) compliance.
    /// </summary>
    MexicoLfpdppp
}

/// <summary>
/// Represents the system language.
/// </summary>
public enum Language
{
    /// <summary>
    /// Spanish language.
    /// </summary>
    Spanish,

    /// <summary>
    /// English language.
    /// </summary>
    English
}

/// <summary>
/// Represents the data access level for users.
/// Controls what operations users can perform on data.
/// </summary>
public enum DataAccessLevel
{
    /// <summary>
    /// Read-only access to data.
    /// </summary>
    ReadOnly,

    /// <summary>
    /// Read and write access to data.
    /// </summary>
    ReadWrite,

    /// <summary>
    /// Full administrative access including deletion.
    /// </summary>
    Admin
}

/// <summary>
/// Represents the status of an audit log.
/// </summary>
public enum AuditStatus
{
    /// <summary>
    /// Audit log has been successfully recorded.
    /// </summary>
    Recorded,

    /// <summary>
    /// Audit log integrity has been verified.
    /// </summary>
    Verified,

    /// <summary>
    /// Audit log integrity has been compromised.
    /// </summary>
    Compromised,

    /// <summary>
    /// Audit log has been archived.
    /// </summary>
    Archived
}

public enum LicenseType
{
    Lifetime = 00,
    Monthly = 01,
    Annual = 12,
    CreditBiweekly = 15
}

public enum LicenseEdition
{
    Demo,
    FullStandard,
    FullSubAdministrator,
    FullAdministrator,
    FullUnlimited
}

public enum LicenseMarket
{
    Mexico,
    USA
}

public enum LicenseStatus
{
    Missing,
    Active,
    ReadOnly,
    Expired,
    HardwareMismatch,
    ClockTampered,
    InvalidSignature,
    InvalidPin,
    Revoked
}
