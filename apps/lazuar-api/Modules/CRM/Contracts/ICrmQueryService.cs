using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lazuar.ApiTypes;

namespace Modules.CRM.Contracts;

public interface ICrmQueryService
{
    Task<ClientProfileDto?> GetClientProfileAsync(Guid organizationId, Guid profileId);
    Task<IEnumerable<ClientProfileDto>> GetClientProfilesAsync(Guid organizationId, IEnumerable<Guid> profileIds);
    Task<ClientProfileDto?> GetClientProfileByEmailAsync(Guid organizationId, string email);
}
