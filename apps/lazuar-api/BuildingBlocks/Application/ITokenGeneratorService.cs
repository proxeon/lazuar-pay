namespace BuildingBlocks.Application;

public record GeneratedToken(string PlainToken, string TokenHash);

public interface ITokenGeneratorService
{
    GeneratedToken GenerateSecureToken(int length = 32);
    string HashToken(string plainToken);
}
