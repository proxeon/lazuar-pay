using BuildingBlocks.Application;

namespace BuildingBlocks.Infrastructure;

public interface IPasswordService 
{ 
    string Hash(string password); 
    bool Verify(string password, string hash); 
}

public class PasswordService : IPasswordService
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);
    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}
