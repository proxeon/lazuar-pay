using Microsoft.EntityFrameworkCore;
using Modules.Billing.Infrastructure;
using Modules.Commerce.Infrastructure;
using Modules.Communications.Infrastructure;
using Modules.CRM.Infrastructure;
using Modules.Lhdn.Infrastructure;
using Modules.Messaging.Infrastructure;
using Modules.One.Infrastructure;
using Modules.Ops.Infrastructure;
using Modules.Payments.Infrastructure;

namespace Lazuar.Api.Composition;

/// <summary>
/// Applies EF migrations for every module schema at boot (first boot / empty Neon).
/// <para>
/// <b>Multi-instance note:</b> Concurrent hosts each run MigrateAsync against the same database.
/// EF's migration history table usually serializes safely, but multi-instance deploys still race
/// on schema apply and can amplify lock contention. Prefer a single migrate job / init container
/// before scaling replicas (follow-up; not required for this phase).
/// </para>
/// </summary>
public static class DatabaseMigrationExtensions
{
    public static async Task MigrateAllModuleDatabasesAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var migratorLog = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMigrator");

        DbContext[] contexts =
        [
            sp.GetRequiredService<OneDbContext>(),
            sp.GetRequiredService<MessagingDbContext>(),
            sp.GetRequiredService<PaymentsDbContext>(),
            sp.GetRequiredService<CrmDbContext>(),
            sp.GetRequiredService<OpsDbContext>(),
            sp.GetRequiredService<BillingDbContext>(),
            sp.GetRequiredService<LhdnDbContext>(),
            sp.GetRequiredService<CommerceDbContext>(),
            sp.GetRequiredService<CommunicationsDbContext>(),
        ];

        foreach (var ctx in contexts)
        {
            var name = ctx.GetType().Name;
            migratorLog.LogInformation("Applying EF migrations for {DbContext}...", name);
            try
            {
                await ctx.Database.MigrateAsync();
                migratorLog.LogInformation("Migrations applied for {DbContext}", name);
            }
            catch (Exception ex)
            {
                migratorLog.LogError(ex, "MigrateAsync failed for {DbContext}", name);
                throw;
            }
        }
    }
}
