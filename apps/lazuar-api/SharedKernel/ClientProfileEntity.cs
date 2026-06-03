using BuildingBlocks.Domain;

namespace SharedKernel;

public class ClientProfileEntity : Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public bool ConsentedToMarketing { get; set; } = false;
}
