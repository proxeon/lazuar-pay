using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.One.Contracts;
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
    private readonly IAuditRecorder? _auditRecorder;

    public GenerateApiCredentialCommandHandler(
        IOneRepository repository,
        ITokenGeneratorService tokenGenerator,
        IAuditRecorder? auditRecorder = null)
    {
        _repository = repository;
        _tokenGenerator = tokenGenerator;
        _auditRecorder = auditRecorder;
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
        // null / empty / unknown → InvalidOperationException (400). Callers must send explicit scopes.
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

        if (_auditRecorder != null)
        {
            await _auditRecorder.RecordAsync(
                request.OrganizationId,
                "api_key.created",
                "api_credential",
                credential.Id.ToString(),
                new { name = credential.Name, prefix = credential.Prefix, hint = credential.KeyHint },
                request.CreatedByUserId,
                ct: ct);
        }

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
