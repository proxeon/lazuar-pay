using BuildingBlocks.Application;

namespace Modules.One.Application.Commands;

public record UpdateProfileCommand(Guid UserId, string Name) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class UpdateProfileCommandHandler : ICommandHandler<UpdateProfileCommand>
{
    private readonly IOneRepository _repository;

    public UpdateProfileCommandHandler(IOneRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(UpdateProfileCommand request, CancellationToken ct)
    {
        var user = await _repository.GetUserByIdAsync(request.UserId, ct);
        if (user == null || !user.IsActive) throw new InvalidOperationException("User not found or inactive.");

        user.UpdateProfile(request.Name);

        await _repository.SaveChangesAsync(ct);
    }
}
