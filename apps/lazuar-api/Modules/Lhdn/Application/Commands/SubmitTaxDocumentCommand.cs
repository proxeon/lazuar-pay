// apps/lazuar-api/Modules/Lhdn/Application/Commands/SubmitTaxDocumentCommand.cs
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Billing.Contracts;
using Modules.Billing.Contracts.Commands;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Application.Services;
using Modules.Lhdn.Domain;
using Modules.Lhdn.Domain.Aggregates;
using Modules.Lhdn.Domain.Entities;

namespace Modules.Lhdn.Application.Commands;

public record SubmitTaxDocumentCommand(Guid OrganizationId, string IdempotencyKey, SubmitDocumentRequestDto Payload) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class SubmitTaxDocumentCommandHandler : ICommandHandler<SubmitTaxDocumentCommand, Guid>
{
    private readonly ILhdnRepository _repository;
    private readonly IDocumentStrategyFactory _strategyFactory;
    private readonly IUblValidatorService _validatorService;
    private readonly IExecutionContextAccessor _executionContext;
    private readonly IBillingQueryService _billingQueryService;
    private readonly ICreditCostService _creditCostService;
    private readonly IMediator _mediator;
    private readonly ILogger<SubmitTaxDocumentCommandHandler> _logger;
    private readonly ITaxpayerValidationService _taxpayerValidation;
    private readonly IDocumentSigner _documentSigner;
    private readonly ICertificateVaultService _certificateVault;
    private readonly LhdnSigningOptions _signingOptions;

    public SubmitTaxDocumentCommandHandler(
        ILhdnRepository repository, 
        IDocumentStrategyFactory strategyFactory,
        IUblValidatorService validatorService,
        IExecutionContextAccessor executionContext,
        IBillingQueryService billingQueryService,
        ICreditCostService creditCostService,
        IMediator mediator,
        ILogger<SubmitTaxDocumentCommandHandler> logger,
        ITaxpayerValidationService taxpayerValidation,
        IDocumentSigner documentSigner,
        ICertificateVaultService certificateVault,
        IOptions<LhdnSigningOptions> signingOptions)
    {
        _repository = repository;
        _strategyFactory = strategyFactory;
        _validatorService = validatorService;
        _executionContext = executionContext;
        _billingQueryService = billingQueryService;
        _creditCostService = creditCostService;
        _mediator = mediator;
        _logger = logger;
        _taxpayerValidation = taxpayerValidation;
        _documentSigner = documentSigner;
        _certificateVault = certificateVault;
        _signingOptions = signingOptions.Value;
    }

    public async Task<Guid> Handle(SubmitTaxDocumentCommand request, CancellationToken ct)
    {
        var isTestMode = _executionContext.IsTestMode;
        var lhdnCost = _creditCostService.GetCost(CreditAction.LhdnSubmit);
        var shouldMeter = !isTestMode && lhdnCost > 0;

        if (shouldMeter)
        {
            var hasCredits = await _billingQueryService.HasSufficientCreditsAsync(request.OrganizationId, lhdnCost);
            if (!hasCredits)
            {
                throw new BusinessRuleValidationException(new GenericBusinessRule($"402: Insufficient API Credits ({lhdnCost} required). Please top up your balance."));
            }
        }

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existingLog = await _repository.GetIdempotencyLogAsync(request.OrganizationId, request.IdempotencyKey, ct);
            if (existingLog != null)
            {
                if (Guid.TryParse(existingLog.ResponseBody, out var existingDocId))
                {
                    return existingDocId;
                }
                throw new InvalidOperationException("Duplicate idempotency key detected but response format was unresolvable.");
            }
        }

        var config = await _repository.GetTenantConfigAsync(request.OrganizationId, ct);
        if (config == null)
        {
            throw new InvalidOperationException("LHDN Tenant Configuration is missing.");
        }

        await EnsureBuyerTinValidAsync(request.OrganizationId, request.Payload, ct);

        var profile = await _billingQueryService.GetBillingProfileAsync(request.OrganizationId);
        var supplierSst = string.IsNullOrWhiteSpace(profile?.Sst_registration_number)
            ? null
            : profile.Sst_registration_number.Trim();

        var requestedVersion = string.IsNullOrWhiteSpace(request.Payload.Document_version)
            ? null
            : request.Payload.Document_version.Trim();

        var (content, documentHashHex) = RenderDocument(request.Payload, config, requestedVersion, supplierSst);

        if (shouldMeter)
        {
            var deductionKey = !string.IsNullOrWhiteSpace(request.IdempotencyKey)
                ? $"lhdn:{request.IdempotencyKey}"
                : $"lhdn:{Guid.CreateVersion7()}";

            await _mediator.Send(new DeductTenantCreditCommand(
                request.OrganizationId,
                lhdnCost,
                $"LHDN submission ({request.Payload.Document_type})",
                deductionKey), ct);
        }

        var taxDocument = new TaxDocument(
            request.OrganizationId,
            request.Payload.Internal_id,
            documentHashHex,
            content,
            isTestMode
        );

        _repository.AddTaxDocument(taxDocument);

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var log = new IdempotencyLog(request.OrganizationId, request.IdempotencyKey, 201, taxDocument.Id.ToString());
            _repository.AddIdempotencyLog(log);
        }

        try
        {
            await _repository.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex.InnerException?.Message.Contains("IX_IdempotencyLogs") == true)
        {
            var concurrentLog = await _repository.GetIdempotencyLogAsync(request.OrganizationId, request.IdempotencyKey, ct);
            if (concurrentLog != null && Guid.TryParse(concurrentLog.ResponseBody, out var concurrentDocId))
            {
                return concurrentDocId;
            }
            throw new InvalidOperationException("Concurrent idempotency collision unresolvable.");
        }

        return taxDocument.Id;
    }

    private async Task EnsureBuyerTinValidAsync(Guid organizationId, SubmitDocumentRequestDto payload, CancellationToken ct)
    {
        var isType01 = payload.Document_type == SubmitDocumentRequestDtoDocument_type._01;
        if (!isType01 || MyInvoisBuyerRules.IsGeneralPublic(payload.Buyer_tin, payload.Buyer_id_value))
        {
            return;
        }

        if (MyInvoisBuyerRules.IsStubTin(payload.Buyer_tin))
        {
            throw new BusinessRuleValidationException(new GenericBusinessRule(
                "Buyer TIN is a reserved stub and cannot be submitted to MyInvois."));
        }

        if (string.IsNullOrWhiteSpace(payload.Buyer_id_value))
        {
            throw new BusinessRuleValidationException(new GenericBusinessRule(
                "Buyer ID type and ID value are required before submitting a type 01 e-invoice."));
        }

        TinValidationResponse result;
        try
        {
            result = await _taxpayerValidation.ValidateTinAsync(
                organizationId,
                payload.Buyer_tin,
                payload.Buyer_id_type.ToString(),
                payload.Buyer_id_value,
                ct);
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleValidationException(new GenericBusinessRule(ex.Message));
        }

        if (!result.IsValid)
        {
            throw new BusinessRuleValidationException(new GenericBusinessRule(
                "Buyer TIN / ID pair is not valid in MyInvois. Type 01 was not created."));
        }
    }

    private (string Content, string HashHex) RenderDocument(
        SubmitDocumentRequestDto payload,
        LhdnTenantConfig config,
        string? requestedVersion,
        string? supplierSst)
    {
        var explicitV11 = string.Equals(requestedVersion, "1.1", StringComparison.Ordinal);
        var canAutoSign = _signingOptions.IsAuto && _documentSigner.CanSign(config);

        if (explicitV11 && !canAutoSign)
        {
            throw new BusinessRuleValidationException(new GenericBusinessRule(
                "Document version 1.1 requires a stored .p12 and Lhdn:Signing=Auto. Submit unsigned 1.0 or upload a certificate."));
        }

        if (canAutoSign)
        {
            try
            {
                var cert = _certificateVault.GetDecryptedCertificate(config.EncryptedPfxBase64!, config.PfxPasswordCiphertext!);
                var signed = _documentSigner.SignJson(payload, config, cert, supplierSst);
                if (signed.Content.Contains("SIGNATURE_PLACEHOLDER", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Signer produced a placeholder signature.");
                }

                return (signed.Content.Replace("\r\n", "\n"), signed.HashHex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "JSON 1.1 signing failed for {InternalId}; falling back to unsigned XML 1.0.", payload.Internal_id);
                if (explicitV11)
                {
                    throw new BusinessRuleValidationException(new GenericBusinessRule(
                        $"Signed 1.1 submit failed: {ex.Message}"));
                }
            }
        }

        var strategy = _strategyFactory.GetStrategy(payload);
        var rawXml = strategy.Generate(payload, config, "1.0", supplierSst).Replace("\r\n", "\n");
        if (rawXml.Contains("SIGNATURE_PLACEHOLDER", StringComparison.Ordinal))
        {
            throw new BusinessRuleValidationException(new GenericBusinessRule(
                "Unsigned 1.0 XML contained a signature placeholder and was not submitted."));
        }

        try
        {
            _validatorService.Validate(rawXml, payload.Document_type.ToString());
        }
        catch (Exception ex) when (ex is not BusinessRuleValidationException)
        {
            throw new BusinessRuleValidationException(new GenericBusinessRule($"XML Schema Validation Error: {ex.Message}"));
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawXml))).ToLowerInvariant();
        return (rawXml, hash);
    }
}
