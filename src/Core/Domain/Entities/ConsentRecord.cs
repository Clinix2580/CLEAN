namespace OS.Domain.Entities;

public sealed class ConsentRecord
{
    public Guid Id { get; set; }
    public Guid SubjectId { get; set; }
    public string SubjectType { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string PrivacyNoticeVersion { get; set; } = string.Empty;
    public DateTime AcceptedAtUtc { get; set; }
    public string AcceptedByUserId { get; set; } = string.Empty;
    public string EvidenceHash { get; set; } = string.Empty;
    public DateTime? RevokedAtUtc { get; set; }
    public string? RevocationReason { get; set; }
}
