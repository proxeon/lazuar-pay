namespace Modules.CRM.Contracts;

public record ClientProfileDto(Guid Id, string FullName, string Email, string Phone);

public interface ICrmQueryService
{
    Task<ClientProfileDto?> GetClientProfileAsync(Guid profileId);
    
    // <-- Batch fetching for in-memory cross-module stitching
    Task<IEnumerable<ClientProfileDto>> GetClientProfilesAsync(IEnumerable<Guid> profileIds);
}
