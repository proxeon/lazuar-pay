using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.One.Contracts;
using Modules.One.Domain;

namespace Modules.One.Application.Commands;

public record RegisterPublicUserCommand(string Email, string Password, string? Name, string WorkspaceName, string TenantSlug) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class RegisterPublicUserCommandHandler : ICommandHandler<RegisterPublicUserCommand, Guid>
{
    private readonly IOneRepository _repository;
    private readonly IPasswordService _passwordService;
    private readonly IEventBus _eventBus;
    
    private static readonly string[] CoreModules = { "OPS", "BILLING", "PAYMENTS", "CRM", "LHDN" };

    public RegisterPublicUserCommandHandler(
        IOneRepository repository, 
        IPasswordService passwordService,
        [FromKeyedServices("OneEventBus")] IEventBus eventBus)
    {
        _repository = repository;
        _passwordService = passwordService;
        _eventBus = eventBus;
    }

    public async Task<Guid> Handle(RegisterPublicUserCommand request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var slug = request.TenantSlug.Trim().ToLowerInvariant();

        var existingUser = await _repository.GetUserByEmailAsync(email, ct);
        if (existingUser != null)
        {
            throw new InvalidOperationException("An account with this email address already exists.");
        }

        var isSlugUnique = await _repository.IsSlugUniqueAsync(slug, ct);
        if (!isSlugUnique)
        {
            throw new InvalidOperationException("The requested workspace slug is already taken. Please choose another.");
        }

        var passwordHash = _passwordService.Hash(request.Password);
        var name = string.IsNullOrWhiteSpace(request.Name) ? email.Split('@')[0] : request.Name.Trim();

        var user = new GlobalUser(email, name, passwordHash, isSystemAdmin: false);
        _repository.AddGlobalUser(user);

        var organization = new Organization(request.WorkspaceName, slug);
        _repository.AddOrganization(organization);

        var membership = new TenantMembership(user.Id, organization.Id, "ADMIN");
        _repository.AddTenantMembership(membership);

        foreach (var module in CoreModules)
        {
            var entitlement = new TenantAppEntitlement(organization.Id, module);
            _repository.AddEntitlement(entitlement);
            
            await _eventBus.PublishAsync(new AppEntitlementGrantedIntegrationEvent(organization.Id, module));
        }

        await _repository.SaveChangesAsync(ct);

        return user.Id;
    }
}
