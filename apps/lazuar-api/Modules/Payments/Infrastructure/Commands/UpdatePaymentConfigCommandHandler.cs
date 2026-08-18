using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Modules.Payments.Contracts.Commands;
using Modules.Payments.Domain;
using Modules.Payments.Domain.Aggregates;
using Modules.Payments.Infrastructure.Gateways;

namespace Modules.Payments.Infrastructure.Commands;

public class UpdatePaymentConfigCommandHandler : ICommandHandler<UpdatePaymentConfigCommand>
{
    private readonly PaymentsDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ISecretVault _secretVault;

    public UpdatePaymentConfigCommandHandler(
        PaymentsDbContext context,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ISecretVault secretVault)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _secretVault = secretVault;
    }

    public async Task Handle(UpdatePaymentConfigCommand request, CancellationToken ct)
    {
        var gatewayType = request.GatewayType.ToUpperInvariant();
        var config = await _context.TenantPaymentConfigurations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.OrganizationId == request.OrganizationId && c.GatewayType == gatewayType, ct);

        var keepApiKey = SecretVaultExtensions.IsKeepExistingSecret(request.ApiKey);
        var keepWebhook = SecretVaultExtensions.IsKeepExistingSecret(request.WebhookSecret);
        var keepSecretKey = SecretVaultExtensions.IsKeepExistingSecret(request.SecretKey);

        // Stripe stores secret_key in ApiKey column; others use api_key.
        string? resolvedPlainApiKey;
        if (gatewayType == "STRIPE")
        {
            if (keepSecretKey && keepApiKey)
            {
                resolvedPlainApiKey = config?.ApiKey is null
                    ? null
                    : _secretVault.DecryptOrPlaintext(config.ApiKey);
            }
            else if (!keepSecretKey)
            {
                resolvedPlainApiKey = request.SecretKey!.Trim();
            }
            else
            {
                resolvedPlainApiKey = request.ApiKey!.Trim();
            }
        }
        else
        {
            if (keepApiKey)
            {
                resolvedPlainApiKey = config?.ApiKey is null
                    ? null
                    : _secretVault.DecryptOrPlaintext(config.ApiKey);
            }
            else
            {
                resolvedPlainApiKey = request.ApiKey!.Trim();
            }
        }

        string? resolvedPlainWebhook;
        if (keepWebhook)
        {
            resolvedPlainWebhook = config?.WebhookSecret is null
                ? null
                : _secretVault.DecryptOrPlaintext(config.WebhookSecret);
        }
        else
        {
            resolvedPlainWebhook = request.WebhookSecret!.Trim();
        }

        var finalMerchantId = string.IsNullOrWhiteSpace(request.MerchantId) || request.MerchantId.Contains("••••", StringComparison.Ordinal)
            ? config?.MerchantId
            : request.MerchantId.Trim();

        var isActive = request.IsActive ?? config?.IsActive ?? true;

        var environment = request.Environment;
        if (string.IsNullOrWhiteSpace(environment))
        {
            environment = PaymentGatewayEnvironment.InferFromStripeShapedKey(resolvedPlainApiKey)
                ?? config?.Environment
                ?? PaymentGatewayEnvironment.Test;
        }

        // Automatically fetch RSA Public Key and register webhooks when a new CHIP key is supplied
        if (gatewayType == "CHIP" &&
            !SecretVaultExtensions.IsKeepExistingSecret(request.ApiKey) &&
            !string.IsNullOrEmpty(resolvedPlainApiKey))
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", resolvedPlainApiKey);

                var apiBaseUrl = _configuration["App:ApiBaseUrl"]?.TrimEnd('/') ?? "http://localhost:8080/api/v1";
                var webhookUrl = $"{apiBaseUrl}/webhooks/payments/chip/{request.OrganizationId}";

                if (webhookUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase))
                {
                    webhookUrl = webhookUrl.Replace("localhost", "lazuar-local-dev.com", StringComparison.OrdinalIgnoreCase);
                }

                resolvedPlainWebhook = await ChipWebhookRegistrar.EnsureRegisteredAsync(client, webhookUrl, ct);
            }
            catch (Exception ex)
            {
                throw new BusinessRuleValidationException(
                    new GenericBusinessRule($"Failed to setup CHIP Collect. Please verify your API Key. Detail: {ex.Message}"));
            }
        }

        if (config == null && string.IsNullOrEmpty(resolvedPlainApiKey))
        {
            throw new BusinessRuleValidationException(
                new GenericBusinessRule("API key (or Stripe secret key) is required for first-time gateway configuration."));
        }

        var encryptedApiKey = string.IsNullOrEmpty(resolvedPlainApiKey)
            ? null
            : _secretVault.Encrypt(resolvedPlainApiKey);

        var encryptedWebhook = string.IsNullOrEmpty(resolvedPlainWebhook)
            ? null
            : _secretVault.Encrypt(resolvedPlainWebhook);

        if (config == null)
        {
            config = new TenantPaymentConfiguration(
                request.OrganizationId,
                gatewayType,
                encryptedApiKey,
                encryptedWebhook,
                finalMerchantId,
                isActive,
                environment);
            _context.TenantPaymentConfigurations.Add(config);
        }
        else
        {
            config.UpdateCredentials(
                gatewayType,
                encryptedApiKey ?? config.ApiKey,
                encryptedWebhook ?? config.WebhookSecret,
                finalMerchantId,
                isActive,
                environment);
        }

        await _context.SaveChangesAsync(ct);
    }
}
