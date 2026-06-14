using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;

namespace Modules.One.Application.Commands;

public record ResendVerificationEmailCommand(string Email) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class ResendVerificationEmailCommandHandler : ICommandHandler<ResendVerificationEmailCommand>
{
    private readonly IOneRepository _repository;
    private readonly ITokenGeneratorService _tokenGenerator;

    public ResendVerificationEmailCommandHandler(IOneRepository repository, ITokenGeneratorService tokenGenerator)
    {
        _repository = repository;
        _tokenGenerator = tokenGenerator;
    }

    public async Task Handle(ResendVerificationEmailCommand request, CancellationToken ct)
    {
        var user = await _repository.GetUserByEmailAsync(request.Email, ct);

        if (user == null || !user.IsActive || user.IsEmailVerified)
            return;

        var token = _tokenGenerator.GenerateSecureToken();
        var expiry = DateTime.UtcNow.AddHours(24);

        user.SetEmailVerificationToken(token.TokenHash, token.PlainToken, expiry);

        await _repository.SaveChangesAsync(ct);
    }
}
