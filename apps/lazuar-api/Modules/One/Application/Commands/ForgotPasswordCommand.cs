using BuildingBlocks.Application;

namespace Modules.One.Application.Commands;

public record ForgotPasswordCommand(string Email) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class ForgotPasswordCommandHandler : ICommandHandler<ForgotPasswordCommand>
{
    private readonly IOneRepository _repository;
    private readonly ITokenGeneratorService _tokenGenerator;

    public ForgotPasswordCommandHandler(IOneRepository repository, ITokenGeneratorService tokenGenerator)
    {
        _repository = repository;
        _tokenGenerator = tokenGenerator;
    }

    public async Task Handle(ForgotPasswordCommand request, CancellationToken ct)
    {
        var user = await _repository.GetUserByEmailAsync(request.Email, ct);
        if (user == null || !user.IsActive) return; // Silent fail for security

        var token = _tokenGenerator.GenerateSecureToken();
        var expiry = DateTime.UtcNow.AddHours(24);

        user.GeneratePasswordResetToken(token.TokenHash, token.PlainToken, expiry);

        await _repository.SaveChangesAsync(ct);
    }
}
