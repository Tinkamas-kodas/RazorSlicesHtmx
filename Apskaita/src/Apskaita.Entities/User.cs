namespace Apskaita.Entities;

/// <summary>
/// Application role assigned to a user by an admin. A null role means access has not been assigned yet.
/// Stored in MySQL as a native <c>enum</c> column via <c>HasConversion&lt;string&gt;()</c>.
/// </summary>
public enum UserRole
{
    User = 1,
    Admin = 2
}

/// <summary>
/// An application user identified by the email asserted by an external sign-in provider
/// (Microsoft Entra ID or Google). The email is the unique business identity key; <see cref="SubjectId"/>
/// stores the provider's stable subject claim (<c>oid</c> for Entra, <c>sub</c> for Google) for audit only.
/// A user is auto-provisioned on first sign-in with <see cref="Role"/> = null; an admin then assigns a role.
/// </summary>
public class User
{
    public Guid Id { get; set; }

    /// <summary>Provider-agnostic stable subject id (<c>oid</c> for Entra, <c>sub</c> for Google). Not the identity key.</summary>
    public string SubjectId { get; set; } = string.Empty;

    /// <summary>The sign-in provider that last authenticated this user: <c>entra</c> or <c>google</c>.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Normalized (trimmed, lowercased) email asserted by the provider. Unique identity key.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Assigned role: <c>admin</c> or <c>user</c>. Null until an admin assigns one (or a bootstrap admin self-provisions).</summary>
    public UserRole? Role { get; set; }

    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
}
