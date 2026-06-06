using BuildingBlocks.Domain;

namespace Modules.One.Domain;

public class GlobalUser : Entity, IAggregateRoot
{
    public Guid Id { get; private set; }
    public string Email { get; private set; }
    public string PasswordHash { get; private set; }
    public bool IsSystemAdmin { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

#pragma warning disable CS8618
    private GlobalUser() { }
#pragma warning restore CS8618

    public GlobalUser(string email, string passwordHash, bool isSystemAdmin = false)
    {
        Id = Guid.CreateVersion7();
        Email = email.Trim().ToLowerInvariant();
        PasswordHash = passwordHash;
        IsSystemAdmin = isSystemAdmin;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdatePassword(string newHash)
    {
        PasswordHash = newHash;
    }
}
