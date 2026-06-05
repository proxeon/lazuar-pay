# 008 — Password Hashing Compatibility & Upgrade-on-Login Strategy

This document defines the architectural strategy and code implementation required to migrate legacy user credentials into the new platform. It allows legacy users to log in seamlessly without requiring a mass password reset.

---

## 1. The Challenge of Cryptographic Migration
The new Lazuar API platform uses BCrypt (managed by `PasswordService.cs` with a default work factor of `11`) for password hashing. The legacy platform, however, secured passwords using PBKDF2 with a SHA256 PRF (or a similar alternative).

Because cryptographic hashes are one-way functions, you cannot translate a legacy hash directly into a BCrypt hash. If you import legacy hashes directly into the new `PasswordHash` database column, `BCrypt.Verify()` will fail, locking all migrated users out of their accounts.

---

## 2. The Solution: "Upgrade-on-Login"

To transition users safely and transparently, we use the **Upgrade-on-Login** pattern. This process is executed at runtime when a user attempts to authenticate:

```
                     User Submits Login Credentials
                                   │
                                   ▼
                   Does Hash have "LEGACY_SHA256:" Prefix?
                                   │
                    ┌──────────────┴──────────────┐
                    ▼ YES                         ▼ NO
         Verify via Legacy SHA256            Verify via BCrypt
                    │                             │
         ┌──────────┴──────────┐                  ▼
         ▼ Success             ▼ Fail        Login Completed
   Hash raw input with BCrypt  Auth Failure
         │
         ▼
   Update DB, Strip Prefix
         │
         ▼
   Login Completed
```

### Steps of the Upgrade Flow:
1. **Import with Prefix:** During data migration, prefix all legacy hashes in the database with a distinct marker (e.g., `LEGACY_SHA256:`).
2. **Detect Algorithm:** During login, check if the retrieved password hash starts with `LEGACY_SHA256:`.
3. **Verify Legacy Hash:** If prefixed, run the raw password through the legacy verifier.
4. **Upgrade Inline:** If legacy verification succeeds, hash the raw password using the new `PasswordService.cs` (BCrypt), update the database record to overwrite the legacy hash, and complete the authentication.

---

## 3. Implementation Blueprint

Below is the concrete, production-ready implementation of this upgrade strategy to be integrated into your `UserAccess` module's authentication pipeline.

### Step A: Legacy Hash Verifier
Add the legacy verification helper class inside your infrastructure layer:

```csharp
using System.Security.Cryptography;

namespace Modules.UserAccess.Infrastructure.Security;

public static class LegacyPasswordVerifier
{
    private const int SaltSize = 16; // 128-bit salt
    private const int KeySize = 32;  // 256-bit key
    private const int Iterations = 10000;

    public static bool VerifyLegacyHash(string password, string legacyHash)
    {
        try
        {
            // Format expected: "salt_base64:hash_base64"
            var parts = legacyHash.Split(':');
            if (parts.Length != 2) return false;

            var salt = Convert.FromBase64String(parts[0]);
            var storedHash = Convert.FromBase64String(parts[1]);

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
            var computedHash = pbkdf2.GetBytes(KeySize);

            return CryptographicOperations.FixedTimeEquals(storedHash, computedHash);
        }
        catch
        {
            return false;
        }
    }
}
```

### Step B: Integrated Authentication Pipeline
Integrate the upgrade logic into your use-case handler (e.g., your authentication command handler). 

This code intercepts legacy passwords, verifies them, and upgrades them dynamically within the active database transaction boundary:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Modules.UserAccess.Infrastructure.Security;

namespace Modules.UserAccess.Application.Authentication;

public record AuthenticateCommand(string Email, string Password) : ICommand<string?>;

public class AuthenticateCommandHandler : ICommandHandler<AuthenticateCommand, string?>
{
    private readonly DbContext _dbContext; // Represents your UserAccess/Identity context
    private readonly IPasswordService _passwordService;
    private readonly IJwtService _jwtService;

    public AuthenticateCommandHandler(
        DbContext dbContext, 
        IPasswordService passwordService, 
        IJwtService jwtService)
    {
        _dbContext = dbContext;
        _passwordService = passwordService;
        _jwtService = jwtService;
    }

    public async Task<string?> Handle(AuthenticateCommand request, CancellationToken ct)
    {
        // Find user by email (assuming a direct db set lookup for simplicity)
        var user = await _dbContext.Set<Domain.UserEntity>()
            .FirstOrDefaultAsync(u => u.Email == request.Email.Trim().ToLowerInvariant() && u.IsActive, ct);

        if (user == null)
        {
            return null; // User not found
        }

        bool isPasswordValid;
        bool requiresUpgrade = false;

        // Check if the hash is legacy
        if (user.PasswordHash.StartsWith("LEGACY_SHA256:", StringComparison.OrdinalIgnoreCase))
        {
            var rawLegacyHash = user.PasswordHash.Substring("LEGACY_SHA256:".Length);
            
            isPasswordValid = LegacyPasswordVerifier.VerifyLegacyHash(request.Password, rawLegacyHash);
            requiresUpgrade = isPasswordValid;
        }
        else
        {
            // Standard validation using your new BCrypt PasswordService
            isPasswordValid = _passwordService.Verify(request.Password, user.PasswordHash);
        }

        if (!isPasswordValid)
        {
            return null; // Invalid credentials
        }

        // If verified successfully using legacy scheme, upgrade to BCrypt immediately
        if (requiresUpgrade)
        {
            var newHash = _passwordService.Hash(request.Password);
            user.PasswordHash = newHash; // Overwrite with new safe hash, stripping prefix automatically

            await _dbContext.SaveChangesAsync(ct);
        }

        // Generate and return JWT Token
        // var token = _jwtService.GenerateToken(...);
        return "JWT_TOKEN_PLACEHOLDER";
    }
}
```
