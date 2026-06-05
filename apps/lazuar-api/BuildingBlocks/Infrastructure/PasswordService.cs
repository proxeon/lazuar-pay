using BuildingBlocks.Application;
using Microsoft.Extensions.Configuration;

namespace BuildingBlocks.Infrastructure;

public interface IPasswordService 
{ 
    string Hash(string password); 
    bool Verify(string password, string hash); 
}

public class PasswordService : IPasswordService
{
    private readonly int _workFactor;

    public PasswordService(IConfiguration configuration)
    {
        _workFactor = configuration.GetValue<int>("Security:PasswordWorkFactor", 11);
    }

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, workFactor: _workFactor);
    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}
