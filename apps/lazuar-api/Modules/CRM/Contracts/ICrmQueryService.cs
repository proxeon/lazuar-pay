namespace Modules.CRM.Contracts;

public record ClientProfileDto(Guid Id, string FullName, string Email, string Phone);

public interface ICrmQueryService
{
    Task<ClientProfileDto?> GetClientProfileAsync(Guid profileId);
    Task<IEnumerable<ClientProfileDto>> GetClientProfilesAsync(IEnumerable<Guid> profileIds);
    Task<ClientProfileDto?> GetClientProfileByEmailAsync(Guid organizationId, string email);
}
