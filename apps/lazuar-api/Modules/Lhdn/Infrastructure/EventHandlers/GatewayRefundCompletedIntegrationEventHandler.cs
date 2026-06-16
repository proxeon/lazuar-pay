using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using Microsoft.Extensions.Logging;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Application.Services;
using Modules.Lhdn.Domain.Aggregates;
using Modules.Lhdn.Infrastructure.Serialization;
using Modules.Payments.Contracts.Events;

namespace Modules.Lhdn.Infrastructure.EventHandlers;

public class GatewayRefundCompletedIntegrationEventHandler : IIntegrationEventHandler<GatewayRefundCompletedIntegrationEvent>
{
    private readonly ILhdnRepository _repository;
    private readonly ILhdnGatewayAdapter _gateway;
    private readonly IDocumentStrategyFactory _strategyFactory;
    private readonly ILogger<GatewayRefundCompletedIntegrationEventHandler> _logger;

    public GatewayRefundCompletedIntegrationEventHandler(
        ILhdnRepository repository,
        ILhdnGatewayAdapter gateway,
        IDocumentStrategyFactory strategyFactory,
        ILogger<GatewayRefundCompletedIntegrationEventHandler> logger)
    {
        _repository = repository;
        _gateway = gateway;
        _strategyFactory = strategyFactory;
        _logger = logger;
    }

    public async Task HandleAsync(GatewayRefundCompletedIntegrationEvent @event)
    {
        var originalDocument = await _repository.GetTaxDocumentByInternalIdAsync(@event.OrganizationId, @event.PaymentRecordId.ToString());

        if (originalDocument == null || originalDocument.ValidationStatus != "VALID" || string.IsNullOrEmpty(originalDocument.LhdnUuid))
        {
            _logger.LogWarning("No valid original LHDN document found to refund for PaymentRecordId {PaymentRecordId}.", @event.PaymentRecordId);
            return;
        }

        var config = await _repository.GetTenantConfigAsync(@event.OrganizationId);
        if (config == null || string.IsNullOrWhiteSpace(config.MyInvoisClientId) || string.IsNullOrWhiteSpace(config.MyInvoisClientSecret))
        {
            _logger.LogWarning("Tenant LHDN config missing. Cannot process refund for {PaymentRecordId}.", @event.PaymentRecordId);
            return;
        }

        var hoursSinceValidation = (DateTime.UtcNow - originalDocument.ValidatedAt.GetValueOrDefault()).TotalHours;

        if (hoursSinceValidation <= 72)
        {
            var token = await _gateway.GetTokenAsync(config.OrganizationId, config.MyInvoisClientId, config.MyInvoisClientSecret, config.IntermediaryMode, config.SupplierTin);
            var cancelResult = await _gateway.CancelDocumentAsync(config.MyInvoisClientId, token, originalDocument.LhdnUuid, "Customer requested refund", config.IntermediaryMode, config.SupplierTin);

            if (cancelResult.Success)
            {
                originalDocument.Cancel();
                await _repository.SaveChangesAsync();
                _logger.LogInformation("Cancelled LHDN Document {Uuid} successfully within 72h window.", originalDocument.LhdnUuid);
            }
            else
            {
                _logger.LogError("Failed to cancel LHDN Document {Uuid}: {Error}", originalDocument.LhdnUuid, cancelResult.ErrorMessage);
            }
        }
        else
        {
            var creditNoteInternalId = $"CN-{@event.PaymentRecordId}";
            var documentVersion = "1.0"; 
            
            var payload = new SubmitDocumentRequestDto
            {
                Internal_id = creditNoteInternalId,
                Document_type = SubmitDocumentRequestDtoDocument_type._02,
                Document_version = documentVersion,
                Issue_date = DateTimeOffset.UtcNow,
                Original_lhdn_uuid = originalDocument.LhdnUuid,
                Adjustment_reason = "Customer requested refund",
                Buyer_name = "Resolved via CRM",
                Buyer_tin = "IG1234567890",
                Buyer_id_type = SubmitDocumentRequestDtoBuyer_id_type.BRN,
                Buyer_id_value = "202001012345",
                Buyer_address = new LhdnAddressDto 
                { 
                    Line1 = "Address Line 1", 
                    City = "Kuala Lumpur", 
                    Postal_code = "50000", 
                    State_code = LhdnAddressDtoState_code._14, 
                    Country_code = "MYS" 
                },
                Items = new List<LhdnItemDto>
                {
                    new LhdnItemDto 
                    { 
                        Description = "Refund", 
                        Classification_code = "022", 
                        Quantity = 1, 
                        Unit_price = (double)@event.RefundedAmount, 
                        Tax_rate = 0, 
                        Tax_amount = 0, 
                        Subtotal = (double)@event.RefundedAmount, 
                        Tax_type_code = LhdnItemDtoTax_type_code._06 
                    }
                },
                Total_excluding_tax = (double)@event.RefundedAmount,
                Total_tax = 0,
                Total_including_tax = (double)@event.RefundedAmount
            };

            var strategy = _strategyFactory.GetStrategy(payload);
            var jsonDocument = strategy.Generate(payload, config, documentVersion);
            
            var rawJsonString = JsonSerializer.Serialize(jsonDocument, LhdnJsonOptions.Instance);

            var documentHashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawJsonString));
            var documentHashHex = Convert.ToHexString(documentHashBytes).ToLowerInvariant();

            var creditNoteDoc = new TaxDocument(
                @event.OrganizationId,
                creditNoteInternalId,
                documentHashHex,
                rawJsonString
            );

            _repository.AddTaxDocument(creditNoteDoc);
            await _repository.SaveChangesAsync();

            _logger.LogInformation("Generated Credit Note {CreditNoteId} for original invoice {Uuid} (>72h).", creditNoteInternalId, originalDocument.LhdnUuid);
        }
    }
}
