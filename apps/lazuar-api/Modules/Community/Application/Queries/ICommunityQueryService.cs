using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lazuar.ApiTypes;
using BuildingBlocks.Application;

namespace Modules.Community.Application.Queries;

public interface ICommunityQueryService
{
    Task<IEnumerable<PortalCommunitySpaceDto>> GetPortalSpacesAsync(Guid organizationId, IEnumerable<Guid> productIds);
}
