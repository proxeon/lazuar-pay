using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
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
    
    private static readonly string[] CoreModules = { "COMMUNITY", "OPS", "BILLING", "PAYMENTS", "CRM", "LHDN" };

    public RegisterPublicUserCommandHandler(IOneRepository repository, IPasswordService passwordService)
    {
        _repository = repository;
        _passwordService = passwordService;
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
        }

        // Commits everything atomically in one database transaction
        await _repository.SaveChangesAsync(ct);

        return user.Id;
    }
}
