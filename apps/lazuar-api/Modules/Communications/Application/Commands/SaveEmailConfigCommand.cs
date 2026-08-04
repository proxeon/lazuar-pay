using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Modules.Communications.Domain.Aggregates;

namespace Modules.Communications.Application.Commands;

public record SaveEmailConfigCommand(
    Guid OrganizationId,
    string? ApiKey,
    string SenderEmail,
    bool IsActive) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class SaveEmailConfigCommandHandler : ICommandHandler<SaveEmailConfigCommand>
{
    private readonly ICommunicationsRepository _repository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISecretVault _secretVault;

    public SaveEmailConfigCommandHandler(
        ICommunicationsRepository repository,
        IHttpClientFactory httpClientFactory,
        ISecretVault secretVault)
    {
        _repository = repository;
        _httpClientFactory = httpClientFactory;
        _secretVault = secretVault;
    }

    public async Task Handle(SaveEmailConfigCommand request, CancellationToken ct)
    {
        var isSystemTenant = request.OrganizationId == Guid.Empty
                             || request.OrganizationId.ToString() == "00000000-0000-0000-0000-000000000001";

        if (isSystemTenant)
        {
            throw new BusinessRuleValidationException(new GenericBusinessRule("System tenant uses platform-level email configuration."));
        }

        var config = await _repository.GetEmailConfigAsync(request.OrganizationId, ct);
        var keepExistingKey = string.IsNullOrWhiteSpace(request.ApiKey);

        if (keepExistingKey && config == null)
        {
            throw new BusinessRuleValidationException(new GenericBusinessRule("API Key and Sender Email are required for first-time configuration."));
        }

        string plainKeyToValidate;
        if (keepExistingKey)
        {
            try
            {
                plainKeyToValidate = _secretVault.Decrypt(config!.ApiKey);
            }
            catch
            {
                // Legacy plaintext row
                plainKeyToValidate = config!.ApiKey;
            }
        }
        else
        {
            plainKeyToValidate = request.ApiKey!.Trim();
        }

        var client = _httpClientFactory.CreateClient("Resend");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", plainKeyToValidate);

        var response = await client.GetAsync("domains", ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new BusinessRuleValidationException(new GenericBusinessRule("Invalid Resend API Key or Domain not verified on Resend. Please check your credentials and try again."));
        }

        if (config == null)
        {
            var encrypted = _secretVault.Encrypt(plainKeyToValidate);
            config = new TenantEmailConfiguration(
                request.OrganizationId,
                encrypted,
                request.SenderEmail,
                request.IsActive);
            _repository.AddEmailConfig(config);
        }
        else if (keepExistingKey)
        {
            config.UpdateWithoutKey(request.SenderEmail, request.IsActive);
        }
        else
        {
            var encrypted = _secretVault.Encrypt(plainKeyToValidate);
            config.UpdateConfiguration(encrypted, request.SenderEmail, request.IsActive);
        }

        await _repository.SaveChangesAsync(ct);
    }
}
