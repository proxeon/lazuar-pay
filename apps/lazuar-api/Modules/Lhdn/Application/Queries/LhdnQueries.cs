using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using Microsoft.Extensions.Options;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Application.Services;
using Modules.Lhdn.Domain;

namespace Modules.Lhdn.Application.Queries;

public record GetLhdnDocumentStatusQuery(Guid OrganizationId, string InternalId) : IQuery<LhdnDocumentResponseDto?>;

public class GetLhdnDocumentStatusQueryHandler : IQueryHandler<GetLhdnDocumentStatusQuery, LhdnDocumentResponseDto?>
{
    private readonly ILhdnRepository _repository;
    private readonly ILhdnLinkService _linkService;

    public GetLhdnDocumentStatusQueryHandler(ILhdnRepository repository, ILhdnLinkService linkService)
    {
        _repository = repository;
        _linkService = linkService;
    }

    public async Task<LhdnDocumentResponseDto?> Handle(GetLhdnDocumentStatusQuery request, CancellationToken ct)
    {
        var doc = await _repository.GetTaxDocumentByInternalIdAsync(request.OrganizationId, request.InternalId, ct);
        if (doc == null) return null;

        var portalUrl = _linkService.GetPortalUrl();

        var qrLink = (string.Equals(doc.ValidationStatus, "VALID", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(doc.LhdnUuid)
                && !string.IsNullOrEmpty(doc.LongId))
            ? $"{portalUrl}/{doc.LhdnUuid}/share/{doc.LongId}"
            : null;

        return new LhdnDocumentResponseDto
        {
            Internal_id = doc.InternalReferenceId,
            Lhdn_uuid = doc.LhdnUuid,
            Long_id = doc.LongId,
            Status = doc.ValidationStatus,
            Qr_link = qrLink,
            Error_message = doc.ErrorMessage,
            Is_test_mode = doc.IsTestMode,
            Submitted_at = new DateTimeOffset(doc.CreatedAt, TimeSpan.Zero),
            Validated_at = doc.ValidatedAt.HasValue
                ? new DateTimeOffset(doc.ValidatedAt.Value, TimeSpan.Zero)
                : null
        };
    }
}

public record GetLhdnTenantConfigQuery(Guid OrganizationId) : IQuery<LhdnTenantConfigDto?>;

public class GetLhdnTenantConfigQueryHandler : IQueryHandler<GetLhdnTenantConfigQuery, LhdnTenantConfigDto?>
{
    private readonly ILhdnRepository _repository;
    private readonly ISecretVault _secretVault;
    private readonly LhdnSigningOptions _signing;

    public GetLhdnTenantConfigQueryHandler(
        ILhdnRepository repository,
        ISecretVault secretVault,
        IOptions<LhdnSigningOptions> signing)
    {
        _repository = repository;
        _secretVault = secretVault;
        _signing = signing.Value;
    }

    public async Task<LhdnTenantConfigDto?> Handle(GetLhdnTenantConfigQuery request, CancellationToken ct)
    {
        var config = await _repository.GetTenantConfigAsync(request.OrganizationId, ct);
        if (config == null) return null;

        var hasSecret = !string.IsNullOrEmpty(config.MyInvoisClientSecret);
        var secretHint = _secretVault.HintLast4(config.MyInvoisClientSecret);

        var environment = string.Equals(config.Environment, "PROD", StringComparison.OrdinalIgnoreCase)
            ? LhdnTenantConfigDtoEnvironment.PROD
            : LhdnTenantConfigDtoEnvironment.SANDBOX;

        return new LhdnTenantConfigDto
        {
            Supplier_tin = config.SupplierTin,
            Id_type = config.IdType,
            Id_value = config.IdValue,
            Environment = environment,
            Msic_code = config.MsicCode,
            Intermediary_mode = config.IntermediaryMode,
            Myinvois_client_id = config.MyInvoisClientId,
            Has_client_secret = hasSecret,
            Client_secret_hint = secretHint,
            Has_certificate = !string.IsNullOrEmpty(config.EncryptedPfxBase64),
            Signing = _signing.Signing,
            Submission_kind = _signing.IsAuto && !string.IsNullOrEmpty(config.EncryptedPfxBase64)
                ? "signed_v1.1_json"
                : "unsigned_v1.0",
            Legal_name = config.LegalName,
            Address_line1 = config.AddressLine1,
            City = config.City,
            State = config.State,
            Postal = config.Postal,
            Country = config.Country
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
            Events = new List<string> { "invoice.valid", "invoice.invalid" },
            Is_active = w.IsActive,
            Created_at = new DateTimeOffset(w.CreatedAt)
        });
    }
}

/// <summary>
/// Obsolete LHDN-local query. Prefer <see cref="Modules.One.Contracts.IApiCredentialService"/>.
/// </summary>
[Obsolete("Platform credentials live in One. Use IApiCredentialService.ListAsync instead.")]
public record ListApiKeysQuery(Guid OrganizationId) : IQuery<IEnumerable<ApiKeyDto>>;

#pragma warning disable CS0618 // Obsolete façade intentionally retained for callers not yet migrated
public class ListApiKeysQueryHandler : IQueryHandler<ListApiKeysQuery, IEnumerable<ApiKeyDto>>
{
    private readonly Modules.One.Contracts.IApiCredentialService _credentials;

    public ListApiKeysQueryHandler(Modules.One.Contracts.IApiCredentialService credentials)
    {
        _credentials = credentials;
    }

    public async Task<IEnumerable<ApiKeyDto>> Handle(ListApiKeysQuery request, CancellationToken ct)
    {
        var keys = await _credentials.ListAsync(request.OrganizationId, ct);

        return keys.Select(k => new ApiKeyDto
        {
            Id = k.Id.ToString(),
            Name = k.Name,
            Prefix = k.Prefix,
            Hint = k.Hint,
            Is_active = k.IsActive,
            Created_at = new DateTimeOffset(k.CreatedAt, TimeSpan.Zero),
            Scopes = ApiKeyScopes.Split(k.Scopes).ToList()
        });
    }
}
#pragma warning restore CS0618
