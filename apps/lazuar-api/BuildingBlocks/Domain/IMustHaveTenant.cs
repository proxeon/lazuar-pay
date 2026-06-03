namespace BuildingBlocks.Domain;

public interface IMustHaveTenant
{
    Guid OrganizationId { get; set; }
}
