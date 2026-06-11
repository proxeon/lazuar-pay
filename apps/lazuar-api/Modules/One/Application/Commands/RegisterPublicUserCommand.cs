using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.One.Domain;

namespace Modules.One.Application.Commands;

public record RegisterPublicUserCommand(string Email, string Password, string? Name) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class RegisterPublicUserCommandHandler : ICommandHandler<RegisterPublicUserCommand, Guid>
{
    private readonly IOneRepository _repository;
    private readonly IPasswordService _passwordService;

    public RegisterPublicUserCommandHandler(IOneRepository repository, IPasswordService passwordService)
    {
        _repository = repository;
        _passwordService = passwordService;
    }

    public async Task<Guid> Handle(RegisterPublicUserCommand request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        // 1. Check if user already exists
        var existingUser = await _repository.GetUserByEmailAsync(email, ct);
        if (existingUser != null)
        {
            throw new InvalidOperationException("An account with this email address already exists.");
        }

        // 2. Hash Password and Create User
        var passwordHash = _passwordService.Hash(request.Password);

        // Fallback to email local part if name is not provided
        var name = string.IsNullOrWhiteSpace(request.Name) ? email.Split('@')[0] : request.Name.Trim();

        var user = new GlobalUser(email, name, passwordHash, isSystemAdmin: false);

        _repository.AddGlobalUser(user);
        await _repository.SaveChangesAsync(ct);

        return user.Id;
    }
}
