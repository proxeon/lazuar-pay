using BuildingBlocks.Domain;

namespace Modules.UserAccess.Domain;

public class UserEntity : Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "STAFF";
    public bool IsActive { get; set; } = true;
}
