using System;
using System.Globalization;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Billing.Application;
using Modules.Billing.Contracts;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Contracts.Events;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;
using Modules.Payments.Contracts;
using Modules.Payments.Contracts.Events;

namespace Modules.Billing.Infrastructure.EventHandlers;

public class GatewayPaymentCompletedHandler : IIntegrationEventHandler<GatewayPaymentCompletedIntegrationEvent>
{
    private readonly ILedgerRepository _repository;
    private readonly IMediator _mediator;
    private readonly IEventBus _eventBus;
    private readonly decimal _b2cIndividualThresholdMyr;

    public GatewayPaymentCompletedHandler(
        ILedgerRepository repository,
        IMediator mediator,
        [FromKeyedServices("BillingEventBus")] IEventBus eventBus,
        IConfiguration? configuration = null)
    {
        _repository = repository;
        _mediator = mediator;
        _eventBus = eventBus;
        _b2cIndividualThresholdMyr = configuration?.GetValue("Lhdn:B2cIndividualThresholdMyr", 10000m) ?? 10000m;
    }

    public async Task HandleAsync(GatewayPaymentCompletedIntegrationEvent @event)
    {
        // Platform utility credit top-ups are wallet + SYSTEM_CREDIT_TOPUP only
        // (PlatformTopUpEventHandler). Skip merchant GMV / revenue ledger path so the
        // same gateway txn is not dual-posted as creator sale revenue.
        if (@event.Metadata.TryGetValue("type", out var paymentType)
            && PlatformCheckoutTypes.IsPlatformCollected(paymentType))
            return;

        // $0 Stripe setup / 100% coupon vault is not GMV. Do not burn a RCPT number.
        if (@event.AmountPaid <= 0)
            return;

        var referenceType = LedgerReferenceTypes.GatewayPayment;
        var referenceId = @event.GatewayTransactionId;

        if (await _repository.HasEntryBeenProcessedAsync(referenceType, referenceId))
            return;

        var isB2b = @event.Metadata.TryGetValue("is_b2b_required", out var b2bFlag) && b2bFlag == "true";
        var taxAmount = ResolveTaxAmount(@event);
        var taxType = ResolveTaxType(@event, taxAmount);
        var msic = isB2b ? "022" : "004";
        var grossRevenue = @event.AmountPaid - taxAmount;
        LedgerEntry? entry = null;

        async Task PersistAsync(CancellationToken ct)
        {
            entry = new LedgerEntry(
                @event.OrganizationId,
                referenceType,
                referenceId,
                $"Gateway payment completed via {@event.Metadata.GetValueOrDefault("type", "UNKNOWN")}",
                isB2b ? "B2B" : "B2C");

            var baseCurrency = @event.BaseCurrency;
            var fxRate = @event.FxRate;

            entry.AddLine(AccountTypes.AssetCash, @event.NetAmount, @event.Currency, @event.NetAmount * fxRate, baseCurrency);

            if (@event.GatewayFee > 0)
            {
                entry.AddLine(AccountTypes.ExpenseGatewayFee, @event.GatewayFee, @event.Currency, @event.GatewayFee * fxRate, baseCurrency);
            }

            entry.AddLine(AccountTypes.RevenueGross, -grossRevenue, @event.Currency, -grossRevenue * fxRate, baseCurrency, taxType, msic);

            if (taxAmount > 0)
            {
                entry.AddLine(AccountTypes.LiabilityTaxPayable, -taxAmount, @event.Currency, -taxAmount * fxRate, baseCurrency, taxType, msic);
            }

            entry.ValidateBalanced();
            _repository.Add(entry);

            if (!isB2b)
            {
                var receiptNumber = await _mediator.Send(
                    new GenerateNextSequenceNumberCommand(@event.OrganizationId, DocumentSeries.ReceiptPrefix()), ct);
                entry.AssignB2cReceipt(receiptNumber);
                if (@event.AmountPaid > _b2cIndividualThresholdMyr)
                {
                    entry.MarkConsolidationNotRequired();
                    entry.UpdateLhdnStatus(null, LhdnValidationStatuses.NeedsBuyerTin);
                }
            }
            else
            {
                var invoiceNumber = await _mediator.Send(
                    new GenerateNextSequenceNumberCommand(@event.OrganizationId, DocumentSeries.InvoicePrefix()), ct);
                entry.AssignB2bInvoice(invoiceNumber);
            }

            await _repository.SaveChangesAsync(ct);
        }

        if (_repository is IBillingTransactional transactional)
        {
            await transactional.ExecuteInTransactionAsync(PersistAsync);
        }
        else
        {
            await PersistAsync(default);
        }

        var booked = entry!;
        var correlation = ResolveDocumentCorrelation(@event);
        if (!isB2b)
        {
            await _mediator.Send(new GenerateAndStoreDocumentCommand(
                @event.OrganizationId,
                booked.Id,
                "Official Receipt",
                CorrelationId: correlation
            ));
        }
        else
        {
            await _mediator.Send(new GenerateAndStoreDocumentCommand(
                @event.OrganizationId,
                booked.Id,
                "Invoice",
                CorrelationId: correlation
            ));

            await _eventBus.PublishAsync(new B2bTaxInvoiceRequestedIntegrationEvent(
                @event.OrganizationId,
                booked.Id,
                booked.CustomerDocumentNumber!,
                @event.GatewayTransactionId,
                grossRevenue,
                taxAmount,
                @event.Currency,
                correlation));
        }
    }

    private static string? ResolveDocumentCorrelation(GatewayPaymentCompletedIntegrationEvent @event)
    {
        if (@event.Metadata != null
            && @event.Metadata.TryGetValue("subscription_id", out var subscriptionId)
            && !string.IsNullOrWhiteSpace(subscriptionId))
        {
            return subscriptionId;
        }

        if (@event.Metadata != null
            && @event.Metadata.TryGetValue("receipt", out var receipt)
            && !string.IsNullOrWhiteSpace(receipt))
        {
            return receipt;
        }

        return null;
    }

    private static decimal ResolveTaxAmount(GatewayPaymentCompletedIntegrationEvent @event)
    {
        if (@event.TaxAmount > 0)
        {
            return @event.TaxAmount;
        }

        if (@event.Metadata != null
            && @event.Metadata.TryGetValue("sst_tax_amount", out var raw)
            && decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            && parsed > 0)
        {
            return parsed;
        }

        return 0m;
    }

    private static string ResolveTaxType(GatewayPaymentCompletedIntegrationEvent @event, decimal taxAmount)
    {
        if (@event.Metadata != null
            && @event.Metadata.TryGetValue("sst_tax_type", out var type)
            && type == "02"
            && taxAmount > 0)
        {
            return "02";
        }

        return "06";
    }
}
