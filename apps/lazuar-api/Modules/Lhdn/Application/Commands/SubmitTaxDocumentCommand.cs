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
using Modules.Billing.Contracts;
using Modules.Billing.Contracts.Commands;
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
    private readonly ICreditCostService _creditCostService;
    private readonly IMediator _mediator;
    private readonly ILogger<SubmitTaxDocumentCommandHandler> _logger;

    public SubmitTaxDocumentCommandHandler(
        ILhdnRepository repository, 
        IDocumentStrategyFactory strategyFactory,
        IUblValidatorService validatorService,
        IExecutionContextAccessor executionContext,
        IBillingQueryService billingQueryService,
        ICreditCostService creditCostService,
        IMediator mediator,
        ILogger<SubmitTaxDocumentCommandHandler> logger)
    {
        _repository = repository;
        _strategyFactory = strategyFactory;
        _validatorService = validatorService;
        _executionContext = executionContext;
        _billingQueryService = billingQueryService;
        _creditCostService = creditCostService;
        _mediator = mediator;
        _logger = logger;
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
        catch (Exception ex) when (ex.InnerException?.Message.Contains("IX_IdempotencyLogs") == true)
        {
            var concurrentLog = await _repository.GetIdempotencyLogAsync(request.OrganizationId, request.IdempotencyKey, ct);
            if (concurrentLog != null && Guid.TryParse(concurrentLog.ResponseBody, out var concurrentDocId))
            {
                return concurrentDocId;
            }
            throw new InvalidOperationException("Concurrent idempotency collision unresolvable.");
        }

        // Deduct credits for the submission. Idempotent on the LHDN idempotency key (or document id),
        // so a retried command cannot double-charge. Test mode and a configured cost of 0 skip
        // Deduct (domain forbids Deduct(0)). The document is already persisted; a deduction
        // failure is logged rather than failing the submission.
        if (shouldMeter)
        {
            try
            {
                var deductionKey = !string.IsNullOrWhiteSpace(request.IdempotencyKey)
                    ? $"lhdn:{request.IdempotencyKey}"
                    : $"lhdn:{taxDocument.Id}";

                await _mediator.Send(new DeductTenantCreditCommand(
                    request.OrganizationId,
                    lhdnCost,
                    $"LHDN submission ({request.Payload.Document_type})",
                    deductionKey), ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LHDN document {DocId} saved for tenant {OrganizationId} but credit deduction failed.", taxDocument.Id, request.OrganizationId);
            }
        }

        return taxDocument.Id;
    }
}
