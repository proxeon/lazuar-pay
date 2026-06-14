using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BuildingBlocks.Application;
using Modules.One.Domain;
using Modules.One.Infrastructure.Configuration;

namespace Modules.One.Infrastructure.Workers;

/// <summary>
/// Executes once on startup to guarantee the System Tenant exists and securely 
/// upserts root administrators using BCrypt, fully replacing local bash scripts.
/// </summary>
public class SystemGenesisBootstrapperJob : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PlatformAdminSettings _settings;
    private readonly ILogger<SystemGenesisBootstrapperJob> _logger;

    public SystemGenesisBootstrapperJob(
        IServiceScopeFactory scopeFactory,
        IOptions<PlatformAdminSettings> settings,
        ILogger<SystemGenesisBootstrapperJob> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneDbContext>();
        var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();

        _logger.LogInformation("Verifying Genesis State...");

        // 1. Guarantee System Tenant exists using raw SQL to force the exact primitive Guid
        await db.Database.ExecuteSqlRawAsync(@"
            INSERT INTO one.""Organizations"" (""Id"", ""Name"", ""Slug"", ""IsActive"", ""CreatedAt"", ""UpdatedAt"")
            VALUES ('00000000-0000-0000-0000-000000000001', 'System Configuration', 'system', true, NOW(), NOW())
            ON CONFLICT (""Id"") DO NOTHING;
        ", cancellationToken);

        // 2. Upsert Platform Administrators
        if (!string.IsNullOrWhiteSpace(_settings.Emails) && !string.IsNullOrWhiteSpace(_settings.Password))
        {
            var emails = _settings.Emails.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var targetHash = passwordService.Hash(_settings.Password);

            foreach (var email in emails)
            {
                var normalizedEmail = email.ToLowerInvariant();
                var user = await db.GlobalUsers.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

                if (user == null)
                {
                    var name = normalizedEmail.Split('@')[0];
                    user = new GlobalUser(normalizedEmail, name, targetHash, isSystemAdmin: true, isEmailVerified: true);
                    db.GlobalUsers.Add(user);
                    _logger.LogInformation("Provisioned new Superadmin: {Email}", normalizedEmail);
                }
                else
                {
                    // Rotate password if the .env hash doesn't match the database hash
                    if (!passwordService.Verify(_settings.Password, user.PasswordHash))
                    {
                        user.ChangePassword(targetHash);
                        _logger.LogInformation("Rotated credentials for Superadmin: {Email}", normalizedEmail);
                    }

                    if (!user.IsSystemAdmin)
                    {
                        // Ensure legacy users are elevated if defined in the env array
                        await db.Database.ExecuteSqlRawAsync(
                            "UPDATE one.\"GlobalUsers\" SET \"IsSystemAdmin\" = true WHERE \"Id\" = {0}", user.Id);
                    }
                }
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            _logger.LogWarning("⚠️ PLATFORM_ADMIN_EMAILS or PLATFORM_ADMIN_PASSWORD is missing from environment. Superadmin accounts will not be seeded or updated.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
