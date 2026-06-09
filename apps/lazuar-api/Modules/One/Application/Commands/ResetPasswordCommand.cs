using BuildingBlocks.Application;

namespace Modules.One.Application.Commands;

public record ResetPasswordCommand(string Email, string Token, string NewPassword) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class ResetPasswordCommandHandler : ICommandHandler<ResetPasswordCommand>
{
    private readonly IOneRepository _repository;
    private readonly ITokenGeneratorService _tokenGenerator;
    private readonly IPasswordService _passwordService;

    public ResetPasswordCommandHandler(IOneRepository repository, ITokenGeneratorService tokenGenerator, IPasswordService passwordService)
    {
        _repository = repository;
        _tokenGenerator = tokenGenerator;
        _passwordService = passwordService;
    }

    public async Task Handle(ResetPasswordCommand request, CancellationToken ct)
    {
        var user = await _repository.GetUserByEmailAsync(request.Email, ct);
        if (user == null || !user.IsActive) throw new InvalidOperationException("Invalid request.");

        if (string.IsNullOrEmpty(user.PasswordResetTokenHash) || user.PasswordResetExpiresAt < DateTime.UtcNow)
            throw new InvalidOperationException("Token is invalid or expired.");

        var inputHash = _tokenGenerator.HashToken(request.Token);
        if (user.PasswordResetTokenHash != inputHash)
            throw new InvalidOperationException("Token is invalid or expired.");

        var newHash = _passwordService.Hash(request.NewPassword);
        user.ResetPassword(newHash);

        await _repository.SaveChangesAsync(ct);
    }
}
