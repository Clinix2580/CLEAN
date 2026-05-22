using OS.Domain.Common;

namespace OS.Domain.Entities;

/// <summary>
/// Represents a user in the system with authentication and authorization information.
/// </summary>
public class User : AggregateRoot
{
    /// <summary>
    /// Gets or sets the username for the user.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full name of the user.
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the email address of the user.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the hashed password of the user.
    /// Should only be set through secure password hashing methods.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the role assigned to the user (e.g., "Admin", "Doctor", "Staff").
    /// </summary>
    public string Role { get; set; } = "User";

    /// <summary>
    /// Gets or sets the date and time of the user's last login.
    /// </summary>
    public DateTime? LastLogin { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user account is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the list of permissions granted to the user.
    /// </summary>
    public List<string> Permissions { get; set; } = new();

    /// <summary>
    /// Records the user's last login time.
    /// </summary>
    public void RecordLogin()
    {
        LastLogin = DateTime.UtcNow;
    }

    /// <summary>
    /// Deactivates the user account.
    /// </summary>
    /// <param name="deactivatedBy">The ID of the user who deactivated this account.</param>
    public void Deactivate(string deactivatedBy)
    {
        IsActive = false;
        UpdatedBy = deactivatedBy;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Reactivates a deactivated user account.
    /// </summary>
    /// <param name="reactivatedBy">The ID of the user who reactivated this account.</param>
    public void Reactivate(string reactivatedBy)
    {
        IsActive = true;
        UpdatedBy = reactivatedBy;
        UpdatedAt = DateTime.UtcNow;
    }
}
