using BuildingBlocks.Domain;

namespace Modules.CRM.Domain;

public class ClientProfileEntity : Entity, IMustHaveTenant
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid OrganizationId { get; set; }
    public Guid? GlobalUserId { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public bool ConsentedToMarketing { get; set; } = false;

    public void Anonymize()
    {
        FullName = "Anonymized User";
        Email = $"deleted_{Id}@localhost";
        Phone = "";
        ConsentedToMarketing = false;
        GlobalUserId = null;
    }
}
