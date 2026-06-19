using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Domain.Aggregates;

namespace Modules.Lhdn.Application.Commands;

public record GenerateApiKeyCommand(Guid OrganizationId, string Name, bool IsTestMode) : ICommand<string>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class GenerateApiKeyCommandHandler : ICommandHandler<GenerateApiKeyCommand, string>
{
    private readonly ILhdnRepository _repository;
    private readonly ITokenGeneratorService _tokenGenerator;

    public GenerateApiKeyCommandHandler(ILhdnRepository repository, ITokenGeneratorService tokenGenerator)
    {
        _repository = repository;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<string> Handle(GenerateApiKeyCommand request, CancellationToken ct)
    {
        var tokenPair = _tokenGenerator.GenerateSecureToken(40);
        var prefix = request.IsTestMode ? "sk_test_" : "sk_live_";
        
        var fullPlainToken = $"{prefix}{tokenPair.PlainToken}";
        var fullHash = _tokenGenerator.HashToken(fullPlainToken);

        var apiKey = new DeveloperApiKey(request.OrganizationId, request.Name, prefix, fullHash);
        
        _repository.AddDeveloperApiKey(apiKey);
        await _repository.SaveChangesAsync(ct);

        return fullPlainToken;
    }
}
