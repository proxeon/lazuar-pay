using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Application.Services;
using Modules.Lhdn.Domain;

namespace Modules.Lhdn.Application.Queries;

public record GetLhdnDocumentStatusQuery(Guid OrganizationId, string InternalId) : IQuery<LhdnDocumentResponseDto?>;

public class GetLhdnDocumentStatusQueryHandler : IQueryHandler<GetLhdnDocumentStatusQuery, LhdnDocumentResponseDto?>
{
    private readonly ILhdnQueryService _queryService;
    private readonly ILhdnLinkService _linkService;

    public GetLhdnDocumentStatusQueryHandler(ILhdnQueryService queryService, ILhdnLinkService linkService)
    {
        _queryService = queryService;
        _linkService = linkService;
    }

    public async Task<LhdnDocumentResponseDto?> Handle(GetLhdnDocumentStatusQuery request, CancellationToken ct)
    {
        var submissions = await _queryService.GetRecentSubmissionsAsync(request.OrganizationId, 100, ct);
        var doc = submissions.FirstOrDefault(d => d.InternalReference == request.InternalId);

        if (doc == null) return null;

        var portalUrl = _linkService.GetPortalUrl();

        var qrLink = (!string.IsNullOrEmpty(doc.LhdnUuid) && !string.IsNullOrEmpty(doc.LongId))
            ? $"{portalUrl}/{doc.LhdnUuid}/share/{doc.LongId}"
            : null;

        return new LhdnDocumentResponseDto
        {
            Internal_id = doc.InternalReference,
            Lhdn_uuid = doc.LhdnUuid,
            Status = doc.Status,
            Qr_link = qrLink,
            Error_message = doc.ErrorMessage,
            Submitted_at = DateTimeOffset.Parse(doc.CreatedAt)
        };
    }
}

public record ListWebhooksQuery(Guid OrganizationId) : IQuery<IEnumerable<WebhookSubscriptionDto>>;

public class ListWebhooksQueryHandler : IQueryHandler<ListWebhooksQuery, IEnumerable<WebhookSubscriptionDto>>
{
    private readonly ILhdnRepository _repository;

    public ListWebhooksQueryHandler(ILhdnRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<WebhookSubscriptionDto>> Handle(ListWebhooksQuery request, CancellationToken ct)
    {
        var webhooks = await _repository.GetActiveWebhooksAsync(request.OrganizationId, ct);

        return webhooks.Select(w => new WebhookSubscriptionDto
        {
            Id = w.Id.ToString(),
            Url = w.Url,
            Events = new List<string> { "invoice.validated", "invoice.rejected" },
            Is_active = w.IsActive,
            Created_at = new DateTimeOffset(w.CreatedAt)
        });
    }
}

public record ListApiKeysQuery(Guid OrganizationId) : IQuery<IEnumerable<ApiKeyDto>>;

public class ListApiKeysQueryHandler : IQueryHandler<ListApiKeysQuery, IEnumerable<ApiKeyDto>>
{
    private readonly ILhdnRepository _repository;

    public ListApiKeysQueryHandler(ILhdnRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<ApiKeyDto>> Handle(ListApiKeysQuery request, CancellationToken ct)
    {
        var keys = await _repository.ListDeveloperApiKeysAsync(request.OrganizationId, ct);

        return keys.Select(k => new ApiKeyDto
        {
            Id = k.Id.ToString(),
            Name = k.Name,
            Prefix = k.Prefix,
            Hint = k.KeyHint,
            Is_active = k.IsActive,
            Created_at = new DateTimeOffset(k.CreatedAt, TimeSpan.Zero),
            Scopes = ApiKeyScopes.Split(k.Scopes).ToList()
        });
    }
}
