using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.Extensions.Logging;
using Modules.Billing.Contracts;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Domain;
using Modules.Commerce.Contracts;
using Modules.CRM.Contracts;
using Modules.Lhdn.Application.Commands;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Domain.Aggregates;
using Modules.Lhdn.Infrastructure.Services;
using Modules.Payments.Contracts.Events;

namespace Modules.Lhdn.Infrastructure.EventHandlers;

public class GatewayRefundCompletedIntegrationEventHandler : IIntegrationEventHandler<GatewayRefundCompletedIntegrationEvent>
{
    private readonly ILhdnRepository _repository;
    private readonly IMediator _mediator;
    private readonly IBillingQueryService _billingQueryService;
    private readonly ICommerceDocumentLookup _commerceDocumentLookup;
    private readonly ICrmQueryService _crmQueryService;
    private readonly ILogger<GatewayRefundCompletedIntegrationEventHandler> _logger;

    public GatewayRefundCompletedIntegrationEventHandler(
        ILhdnRepository repository,
        IMediator mediator,
        IBillingQueryService billingQueryService,
        ICommerceDocumentLookup commerceDocumentLookup,
        ICrmQueryService crmQueryService,
        ILogger<GatewayRefundCompletedIntegrationEventHandler> logger)
    {
        _repository = repository;
        _mediator = mediator;
        _billingQueryService = billingQueryService;
        _commerceDocumentLookup = commerceDocumentLookup;
        _crmQueryService = crmQueryService;
        _logger = logger;
    }

    public async Task HandleAsync(GatewayRefundCompletedIntegrationEvent @event)
    {
        if (!@event.IsFullRefund)
        {
            _logger.LogInformation(
                "Skipping LHDN cancel/CN for partial refund PaymentRecordId {PaymentRecordId}.",
                @event.PaymentRecordId);
            return;
        }

        var originalDocument = await ResolveOriginalTaxDocumentAsync(@event);
        if (originalDocument == null || originalDocument.ValidationStatus != "VALID" || string.IsNullOrEmpty(originalDocument.LhdnUuid))
        {
            _logger.LogInformation(
                "No valid original LHDN document found to refund for PaymentRecordId {PaymentRecordId}.",
                @event.PaymentRecordId);
            return;
        }

        var hoursSinceValidation = (DateTime.UtcNow - originalDocument.ValidatedAt.GetValueOrDefault()).TotalHours;

        if (hoursSinceValidation <= 72)
        {
            await _mediator.Send(new CancelTaxDocumentCommand(
                @event.OrganizationId,
                originalDocument.InternalReferenceId,
                "Customer requested refund"));
            return;
        }

        var customer = await _commerceDocumentLookup.GetCustomerForDocumentAsync(
            @event.OrganizationId,
            @event.GatewayTransactionId,
            @event.SubscriptionId == Guid.Empty ? null : @event.SubscriptionId.ToString());

        ClientProfileDto? profile = null;
        if (!string.IsNullOrWhiteSpace(customer?.Email))
        {
            profile = await _crmQueryService.GetClientProfileByEmailAsync(@event.OrganizationId, customer.Email);
        }

        if (!LhdnBuyerMapper.TryCreatePayloadBuyer(
                profile,
                customer,
                out var buyerName,
                out var buyerTin,
                out var idType,
                out var idValue,
                out var address))
        {
            _logger.LogWarning(
                "Skipping type 02 credit note for PaymentRecordId {PaymentRecordId}: no real buyer TIN.",
                @event.PaymentRecordId);
            return;
        }

        var creditNoteNumber = await ResolveCreditNoteNumberAsync(@event);

        var tax = @event.TaxAmount;
        var gross = @event.RefundedAmount;
        var net = tax > 0 && gross >= tax ? gross - tax : gross;
        var taxRate = net == 0 || tax == 0 ? 0 : (double)Math.Round((tax / net) * 100m, 2);

        var payload = new SubmitDocumentRequestDto
        {
            Internal_id = creditNoteNumber,
            Document_type = SubmitDocumentRequestDtoDocument_type._02,
            Document_version = "1.0",
            Issue_date = DateTimeOffset.UtcNow,
            Original_lhdn_uuid = originalDocument.LhdnUuid,
            Adjustment_reason = "Customer requested refund",
            Buyer_name = buyerName,
            Buyer_tin = buyerTin,
            Buyer_id_type = idType,
            Buyer_id_value = idValue,
            Buyer_email = customer?.Email,
            Buyer_address = address,
            Items = new List<LhdnItemDto>
            {
                new()
                {
                    Description = "Refund",
                    Classification_code = "022",
                    Quantity = 1,
                    Unit_price = (double)net,
                    Tax_rate = taxRate,
                    Tax_amount = (double)tax,
                    Subtotal = (double)net,
                    Tax_type_code = tax > 0
                        ? LhdnItemDtoTax_type_code._02
                        : LhdnItemDtoTax_type_code._06
                }
            },
            Total_excluding_tax = (double)net,
            Total_tax = (double)tax,
            Total_including_tax = (double)gross
        };

        var idempotencyKey = $"cn:{@event.OrganizationId:N}:{@event.PaymentRecordId:N}:{@event.Id:N}";
        await _mediator.Send(new SubmitTaxDocumentCommand(@event.OrganizationId, idempotencyKey, payload));
    }

    private async Task<TaxDocument?> ResolveOriginalTaxDocumentAsync(GatewayRefundCompletedIntegrationEvent @event)
    {
        var payment = string.IsNullOrWhiteSpace(@event.GatewayTransactionId)
            ? null
            : await _billingQueryService.FindPaymentByGatewayTransactionAsync(
                @event.OrganizationId,
                @event.GatewayTransactionId);

        var candidates = new[]
        {
            payment?.CustomerDocumentNumber,
            payment?.LhdnDocumentUuid,
            payment?.TaxInvoiceId,
            @event.PaymentRecordId.ToString()
        }.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            var byInternal = await _repository.GetTaxDocumentByInternalIdAsync(@event.OrganizationId, candidate!);
            if (byInternal != null)
                return byInternal;

            var byUuid = await _repository.GetTaxDocumentByLhdnUuidAsync(@event.OrganizationId, candidate!);
            if (byUuid != null)
                return byUuid;
        }

        return null;
    }

    private async Task<string> ResolveCreditNoteNumberAsync(GatewayRefundCompletedIntegrationEvent @event)
    {
        var refundReference = @event.PaymentRecordId.ToString("N") + ":" + @event.Id.ToString("N");
        var refundLedger = await _billingQueryService.FindLedgerByReferenceAsync(
            @event.OrganizationId,
            LedgerReferenceTypes.GatewayRefund,
            refundReference);

        if (!string.IsNullOrWhiteSpace(refundLedger?.CustomerDocumentNumber))
            return refundLedger.CustomerDocumentNumber;

        return await _mediator.Send(
            new GenerateNextSequenceNumberCommand(@event.OrganizationId, DocumentSeries.CreditNotePrefix()));
    }
}
