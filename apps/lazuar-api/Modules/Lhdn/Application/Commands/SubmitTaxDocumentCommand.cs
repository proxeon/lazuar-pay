using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Lazuar.ApiTypes;
using Microsoft.EntityFrameworkCore;
using Modules.Billing.Contracts;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Application.Services;
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

    public SubmitTaxDocumentCommandHandler(
        ILhdnRepository repository, 
        IDocumentStrategyFactory strategyFactory,
        IUblValidatorService validatorService,
        IExecutionContextAccessor executionContext,
        IBillingQueryService billingQueryService)
    {
        _repository = repository;
        _strategyFactory = strategyFactory;
        _validatorService = validatorService;
        _executionContext = executionContext;
        _billingQueryService = billingQueryService;
    }

    public async Task<Guid> Handle(SubmitTaxDocumentCommand request, CancellationToken ct)
    {
        var isTestMode = _executionContext.IsTestMode;

        // Pre-Flight Check: Disallow real submissions if the wallet is empty
        if (!isTestMode)
        {
            var hasCredits = await _billingQueryService.HasPositiveCreditBalanceAsync(request.OrganizationId);
            if (!hasCredits)
            {
                throw new BusinessRuleValidationException(new GenericBusinessRule("402: Insufficient API Credits. Please top up your balance."));
            }
        }

        // Idempotency Check
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

        // Force Sandbox routing for SK_TEST keys regardless of user UI configuration
        if (isTestMode)
        {
            config.UpdateProfile(config.SupplierTin, config.IdType, config.IdValue, "SANDBOX", config.MsicCode, config.IntermediaryMode);
        }

        var documentVersion = string.IsNullOrWhiteSpace(request.Payload.Document_version) ? "1.0" : request.Payload.Document_version;
        var strategy = _strategyFactory.GetStrategy(request.Payload);
        var rawXmlString = strategy.Generate(request.Payload, config, documentVersion);
        var normalizedXmlString = rawXmlString.Replace("\r\n", "\n");

        try
        {
            _validatorService.Validate(normalizedXmlString, request.Payload.Document_type.ToString());
        }
        catch (Exception ex) when (ex is not BusinessRuleValidationException)
        {
            throw new BusinessRuleValidationException(new GenericBusinessRule($"XML Schema Validation Error: {ex.Message}"));
        }

        var xmlBytes = Encoding.UTF8.GetBytes(normalizedXmlString);
        var hashBytes = SHA256.HashData(xmlBytes);
        var documentHashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();

        var taxDocument = new TaxDocument(
            request.OrganizationId,
            request.Payload.Internal_id,
            documentHashHex,
            normalizedXmlString,
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
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_IdempotencyLogs") == true)
        {
            // Another thread beat us to the idempotency insert. Return the cached result safely.
            var concurrentLog = await _repository.GetIdempotencyLogAsync(request.OrganizationId, request.IdempotencyKey, ct);
            if (concurrentLog != null && Guid.TryParse(concurrentLog.ResponseBody, out var concurrentDocId))
            {
                return concurrentDocId;
            }
            throw new InvalidOperationException("Concurrent idempotency collision unresolvable.");
        }

        return taxDocument.Id;
    }
}
