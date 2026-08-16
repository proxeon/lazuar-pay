using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Domain.Entities;
using Modules.One.Contracts;
using Modules.Payments.Contracts;
using Modules.Payments.Contracts.Events;

namespace Modules.Commerce.Application.Commands;

public class RecordRefundCommandHandler : ICommandHandler<RecordRefundCommand, string>
{
    private readonly ICommerceRepository _repository;
    private readonly IEventBus _eventBus;
    private readonly IAuditRecorder? _auditRecorder;

    public RecordRefundCommandHandler(
        ICommerceRepository repository,
        [FromKeyedServices("CommerceEventBus")] IEventBus eventBus,
        IAuditRecorder? auditRecorder = null)
    {
        _repository = repository;
        _eventBus = eventBus;
        _auditRecorder = auditRecorder;
    }

    public async Task<string> Handle(RecordRefundCommand request, CancellationToken ct)
    {
        var log = await _repository.GetTransactionLogByIdAsync(request.TransactionLogId, ct);
        if (log == null || log.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException("Transaction log not found.");
        }

        if (string.Equals(log.Status, CommerceTransactionLog.StatusRefunded, StringComparison.OrdinalIgnoreCase)
            || log.RemainingAmount <= 0)
        {
            throw new RefundRejectedException("ALREADY_REFUNDED", "Transaction is already refunded.");
        }

        if (string.Equals(log.Status, CommerceTransactionLog.StatusRefundPending, StringComparison.OrdinalIgnoreCase))
        {
            throw new RefundRejectedException("REFUND_PENDING", "A refund is already in progress.");
        }

        if (!IsRefundableSourceStatus(log.Status))
        {
            throw new RefundRejectedException("REFUND_NOT_ALLOWED", $"Transaction cannot be refunded from status '{log.Status}'.");
        }

        if (string.IsNullOrWhiteSpace(log.ExternalReference))
        {
            throw new RefundRejectedException("NO_GATEWAY_REFERENCE", "Transaction has no gateway reference; cannot refund.");
        }

        var remaining = log.RemainingAmount;
        var amount = request.Amount ?? remaining;
        if (amount <= 0)
        {
            throw new RefundRejectedException("INVALID_AMOUNT", "Refund amount must be greater than zero.");
        }

        if (amount > remaining)
        {
            throw new RefundRejectedException("AMOUNT_EXCEEDS_REMAINING", "Refund amount cannot exceed the remaining refundable amount.");
        }

        var gatewayName = ResolveGateway(request.GatewayName, log.GatewayName);
        var currency = string.IsNullOrWhiteSpace(log.Currency) ? "MYR" : log.Currency;
        var isFullRefund = amount == remaining;

        if (!string.Equals(log.GatewayName, gatewayName, StringComparison.OrdinalIgnoreCase))
        {
            log.SetGatewayName(gatewayName);
        }

        log.SetRefundReason(request.Reason);

        if (PaymentGatewayCapabilities.RequiresMarkRefunded(gatewayName))
        {
            if (!request.MarkRefunded)
            {
                throw new RefundRejectedException(
                    "MARK_REFUNDED_REQUIRED",
                    "This payment cannot be refunded in-product. Refund it at the processor or desk, then mark it refunded.");
            }

            log.ApplyRefund(amount);
            await _eventBus.PublishAsync(new GatewayRefundCompletedIntegrationEvent(
                OrganizationId: request.OrganizationId,
                SubscriptionId: request.SubscriptionId ?? Guid.Empty,
                PaymentRecordId: log.Id,
                GatewayTransactionId: log.ExternalReference,
                RefundedAmount: amount,
                Currency: currency,
                RefundedFee: 0m,
                NetRefundedAmount: amount,
                TaxAmount: request.TaxAmount,
                IsFullRefund: isFullRefund));
            await _repository.SaveChangesAsync(ct);
            await RecordAuditAsync(request, amount, "refunded", ct);
            return "refunded";
        }

        if (!PaymentGatewayCapabilities.SupportsApiRefund(gatewayName))
        {
            throw new RefundRejectedException("GATEWAY_REFUND_UNSUPPORTED", "This gateway does not support refunds.");
        }

        log.MarkRefundPending();
        await _eventBus.PublishAsync(new GatewayRefundRequestedIntegrationEvent(
            OrganizationId: request.OrganizationId,
            SubscriptionId: request.SubscriptionId ?? Guid.Empty,
            PaymentRecordId: log.Id,
            GatewayTransactionId: log.ExternalReference,
            Amount: amount,
            Currency: currency,
            GatewayName: gatewayName,
            TaxAmount: request.TaxAmount,
            IsFullRefund: isFullRefund));
        await _repository.SaveChangesAsync(ct);
        await RecordAuditAsync(request, amount, "refund_requested", ct);
        return "refund_requested";
    }

    private async Task RecordAuditAsync(RecordRefundCommand request, decimal amount, string status, CancellationToken ct)
    {
        if (_auditRecorder == null)
        {
            return;
        }

        await _auditRecorder.RecordAsync(
            request.OrganizationId,
            "refund.created",
            "transaction",
            request.TransactionLogId.ToString(),
            new { amount, status, reason = request.Reason },
            ct: ct);
    }

    private static bool IsRefundableSourceStatus(string status) =>
        status.Equals(CommerceTransactionLog.StatusConfirmed, StringComparison.OrdinalIgnoreCase)
        || status.Equals(CommerceTransactionLog.StatusPartiallyRefunded, StringComparison.OrdinalIgnoreCase)
        || status.Equals(CommerceTransactionLog.StatusRefundFailed, StringComparison.OrdinalIgnoreCase);

    private static string ResolveGateway(string? requestGateway, string? logGateway)
    {
        if (!string.IsNullOrWhiteSpace(requestGateway))
        {
            return requestGateway.Trim().ToUpperInvariant();
        }

        if (!string.IsNullOrWhiteSpace(logGateway))
        {
            return logGateway.Trim().ToUpperInvariant();
        }

        throw new RefundRejectedException(
            "GATEWAY_REQUIRED",
            "Gateway is required. Send gateway_name for this transaction.");
    }
}
