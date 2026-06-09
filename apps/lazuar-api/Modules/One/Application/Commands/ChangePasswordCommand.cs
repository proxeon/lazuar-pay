using BuildingBlocks.Application;

namespace Modules.One.Application.Commands;

public record ChangePasswordCommand(Guid UserId, string CurrentPassword, string NewPassword) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class ChangePasswordCommandHandler : ICommandHandler<ChangePasswordCommand>
{
    private readonly IOneRepository _repository;
    private readonly IPasswordService _passwordService;

    public ChangePasswordCommandHandler(IOneRepository repository, IPasswordService passwordService)
    {
        _repository = repository;
        _passwordService = passwordService;
    }

    public async Task Handle(ChangePasswordCommand request, CancellationToken ct)
    {
        var user = await _repository.GetUserByIdAsync(request.UserId, ct);
        if (user == null || !user.IsActive) throw new InvalidOperationException("User not found or inactive.");

        if (!_passwordService.Verify(request.CurrentPassword, user.PasswordHash))
            throw new InvalidOperationException("Current password is incorrect.");

        var newHash = _passwordService.Hash(request.NewPassword);
        user.ChangePassword(newHash);

        await _repository.SaveChangesAsync(ct);
    }
}
