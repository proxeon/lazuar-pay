using BuildingBlocks.Domain;
using Modules.One.Domain.Events;

namespace Modules.One.Domain;

public class GlobalUser : Entity, IAggregateRoot
{
    public Guid Id { get; private set; }
    public string Email { get; private set; }
    public string Name { get; private set; }
    public string PasswordHash { get; private set; }
    public Guid SecurityStamp { get; private set; }

    public bool IsSystemAdmin { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsEmailVerified { get; private set; }

    public string? EmailVerificationTokenHash { get; private set; }
    public DateTime? EmailVerificationExpiresAt { get; private set; }

    public string? PasswordResetTokenHash { get; private set; }
    public DateTime? PasswordResetExpiresAt { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private GlobalUser() { }
#pragma warning restore CS8618

    public GlobalUser(string email, string name, string passwordHash, bool isSystemAdmin = false, bool isEmailVerified = false)
    {
        Id = Guid.CreateVersion7();
        Email = email.Trim().ToLowerInvariant();
        Name = name.Trim();
        PasswordHash = passwordHash;
        SecurityStamp = Guid.CreateVersion7();
        IsSystemAdmin = isSystemAdmin;
        IsActive = true;
        IsEmailVerified = isEmailVerified;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new UserRegisteredDomainEvent(Id, Email, Name));
    }

    public void UpdateProfile(string name)
    {
        Name = name.Trim();
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new GlobalUserProfileUpdatedDomainEvent(Id, Email, Name));
    }

    public void ChangePassword(string newHash)
    {
        PasswordHash = newHash;
        SecurityStamp = Guid.CreateVersion7();
        UpdatedAt = DateTime.UtcNow;
    }

    public void GeneratePasswordResetToken(string tokenHash, string plainToken, DateTime expiry)
    {
        PasswordResetTokenHash = tokenHash;
        PasswordResetExpiresAt = expiry;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new PasswordResetRequestedDomainEvent(Id, Email, plainToken));
    }

    public void ResetPassword(string newHash)
    {
        PasswordHash = newHash;
        SecurityStamp = Guid.CreateVersion7();
        PasswordResetTokenHash = null;
        PasswordResetExpiresAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetEmailVerificationToken(string tokenHash, string plainToken, DateTime expiry)
    {
        EmailVerificationTokenHash = tokenHash;
        EmailVerificationExpiresAt = expiry;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new EmailVerificationRequestedDomainEvent(Id, Email, Name, plainToken));
    }

    public void VerifyEmail()
    {
        IsEmailVerified = true;
        EmailVerificationTokenHash = null;
        EmailVerificationExpiresAt = null;
        UpdatedAt = DateTime.UtcNow;
    }
}
