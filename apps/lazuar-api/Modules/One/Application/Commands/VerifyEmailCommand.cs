using BuildingBlocks.Application;

namespace Modules.One.Application.Commands;

public record VerifyEmailCommand(string Email, string Token) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class VerifyEmailCommandHandler : ICommandHandler<VerifyEmailCommand>
{
    private readonly IOneRepository _repository;
    private readonly ITokenGeneratorService _tokenGenerator;

    public VerifyEmailCommandHandler(IOneRepository repository, ITokenGeneratorService tokenGenerator)
    {
        _repository = repository;
        _tokenGenerator = tokenGenerator;
    }

    public async Task Handle(VerifyEmailCommand request, CancellationToken ct)
    {
        var user = await _repository.GetUserByEmailAsync(request.Email, ct);
        if (user == null) throw new InvalidOperationException("Invalid verification request.");

        if (user.IsEmailVerified) return;

        if (string.IsNullOrEmpty(user.EmailVerificationTokenHash) || user.EmailVerificationExpiresAt < DateTime.UtcNow)
            throw new InvalidOperationException("Token is invalid or expired.");

        var inputHash = _tokenGenerator.HashToken(request.Token);
        if (user.EmailVerificationTokenHash != inputHash)
            throw new InvalidOperationException("Token is invalid or expired.");

        user.VerifyEmail();
        await _repository.SaveChangesAsync(ct);
    }
}
