using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using Modules.One.Domain;

namespace Modules.One.Application.Queries;

public record ListApiCredentialsQuery(Guid OrganizationId) : IQuery<IEnumerable<ApiKeyDto>>;

public class ListApiCredentialsQueryHandler : IQueryHandler<ListApiCredentialsQuery, IEnumerable<ApiKeyDto>>
{
    private readonly IOneRepository _repository;

    public ListApiCredentialsQueryHandler(IOneRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<ApiKeyDto>> Handle(ListApiCredentialsQuery request, CancellationToken ct)
    {
        var keys = await _repository.ListApiCredentialsAsync(request.OrganizationId, ct);

        return keys.Select(k => new ApiKeyDto
        {
            Id = k.Id.ToString(),
            Name = k.Name,
            Prefix = k.Prefix,
            Hint = k.KeyHint,
            Is_active = k.IsActive,
            Created_at = new DateTimeOffset(k.CreatedAt, TimeSpan.Zero),
            Scopes = PlatformApiScopes.Split(k.Scopes).ToList()
        });
    }
}
