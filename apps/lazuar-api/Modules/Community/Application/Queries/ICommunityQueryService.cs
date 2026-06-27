using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lazuar.ApiTypes;

namespace Modules.Community.Application.Queries;

public interface ICommunityQueryService
{
    Task<IEnumerable<PortalCommunitySpaceDto>> GetPortalSpacesAsync(Guid organizationId, IEnumerable<Guid> productIds);
    Task<IEnumerable<AdminCommunitySpaceDto>> GetAdminSpacesAsync(Guid organizationId);
}
