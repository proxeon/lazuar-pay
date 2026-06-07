using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.One.Contracts;
using Modules.One.Domain;

namespace Modules.One.Application.Commands;

public record CreateWorkspaceCommand(
    string Name, 
    string Slug, 
    string OwnerEmail, 
    string OwnerName, 
    List<string> ProvisionApps) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class CreateWorkspaceCommandHandler : ICommandHandler<CreateWorkspaceCommand, Guid>
{
    private readonly IOneRepository _repository;
    private readonly IPasswordService _passwordService;
    private readonly IEventBus _eventBus;

    public CreateWorkspaceCommandHandler(
        IOneRepository repository, 
        IPasswordService passwordService,
        [FromKeyedServices("OneEventBus")] IEventBus eventBus)
    {
        _repository = repository;
        _passwordService = passwordService;
        _eventBus = eventBus;
    }

    public async Task<Guid> Handle(CreateWorkspaceCommand request, CancellationToken ct)
    {
        // Step A: Create the Organization (Workspace)
        var organization = new Organization(request.Name, request.Slug);
        _repository.AddOrganization(organization);

        // Step B: Query database for GlobalUser matching OwnerEmail
        var email = request.OwnerEmail.Trim().ToLowerInvariant();
        var user = await _repository.GetUserByEmailAsync(email, ct);

        // Step C: If user doesn't exist, create a new GlobalUser with a secure random password
        string? generatedPassword = null;
        if (user == null)
        {
            generatedPassword = GenerateSecurePassword(12);
            var passwordHash = _passwordService.Hash(generatedPassword);
            
            user = new GlobalUser(email, passwordHash, isSystemAdmin: false);
            _repository.AddGlobalUser(user);
        }

        // Step D: Create TenantMembership linking User to Organization with role ADMIN/OWNER
        var membership = new TenantMembership(user.Id, organization.Id, "ADMIN");
        _repository.AddTenantMembership(membership);

        // Step E: Loop through ProvisionApps array and create TenantAppEntitlements
        foreach (var appId in request.ProvisionApps)
        {
            var cleanAppId = appId.Trim().ToUpperInvariant();
            var entitlement = new TenantAppEntitlement(organization.Id, cleanAppId);
            _repository.AddEntitlement(entitlement);
            
            // Publish event so modules (like Community) know to run JIT template seeding!
            await _eventBus.PublishAsync(new AppEntitlementGrantedIntegrationEvent(organization.Id, cleanAppId));
        }

        // Save everything atomically
        await _repository.SaveChangesAsync(ct);
        
        // (Phase 3 Note: The Customer Handoff Event will be published here in the next step!)

        return organization.Id;
    }

    // Helper to generate a secure random password
    private static string GenerateSecurePassword(int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%^&*";
        var result = new char[length];
        var randomData = new byte[length];
        
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomData);
        }
        
        for (int i = 0; i < length; i++)
        {
            result[i] = chars[randomData[i] % chars.Length];
        }
        return new string(result);
    }
}
