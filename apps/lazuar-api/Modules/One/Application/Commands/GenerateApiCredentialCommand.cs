using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.One.Domain;

namespace Modules.One.Application.Commands;

public record GenerateApiCredentialResult(
    Guid Id,
    string Name,
    string Prefix,
    string Hint,
    DateTime CreatedAt,
    string PlainKey,
    string Scopes);

public record GenerateApiCredentialCommand(
    Guid OrganizationId,
    string Name,
    bool IsTestMode,
    Guid? CreatedByUserId = null,
    IReadOnlyList<string>? Scopes = null) : ICommand<GenerateApiCredentialResult>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class GenerateApiCredentialCommandHandler : ICommandHandler<GenerateApiCredentialCommand, GenerateApiCredentialResult>
{
    private readonly IOneRepository _repository;
    private readonly ITokenGeneratorService _tokenGenerator;

    public GenerateApiCredentialCommandHandler(IOneRepository repository, ITokenGeneratorService tokenGenerator)
    {
        _repository = repository;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<GenerateApiCredentialResult> Handle(GenerateApiCredentialCommand request, CancellationToken ct)
    {
        var tokenPair = _tokenGenerator.GenerateSecureToken(40);
        var prefix = request.IsTestMode ? "sk_test_" : "sk_live_";

        var fullPlainToken = $"{prefix}{tokenPair.PlainToken}";
        var fullHash = _tokenGenerator.HashToken(fullPlainToken);
        var keyHint = fullPlainToken.Length >= 4
            ? fullPlainToken[^4..]
            : fullPlainToken;
        // null scopes → LHDN document default; empty/unknown → InvalidOperationException (400).
        var scopes = PlatformApiScopes.NormalizeAndValidate(request.Scopes);

        var credential = new ApiCredential(
            request.OrganizationId,
            request.Name,
            prefix,
            fullHash,
            keyHint,
            scopes,
            request.CreatedByUserId);

        _repository.AddApiCredential(credential);
        await _repository.SaveChangesAsync(ct);

        return new GenerateApiCredentialResult(
            credential.Id,
            credential.Name,
            credential.Prefix,
            credential.KeyHint,
            credential.CreatedAt,
            fullPlainToken,
            credential.Scopes);
    }
}
