using BuildingBlocks.Infrastructure;
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

public static class DeadLetterRedriveEndpoints
{
    public static RouteGroupBuilder MapDeadLetterRedriveEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/dead-letters/redrive", async (
            IServiceProvider services,
            CancellationToken ct) =>
        {
            DbContext[] contexts =
            [
                services.GetRequiredService<OneDbContext>(),
                services.GetRequiredService<MessagingDbContext>(),
                services.GetRequiredService<PaymentsDbContext>(),
                services.GetRequiredService<CrmDbContext>(),
                services.GetRequiredService<OpsDbContext>(),
                services.GetRequiredService<BillingDbContext>(),
                services.GetRequiredService<LhdnDbContext>(),
                services.GetRequiredService<CommerceDbContext>(),
                services.GetRequiredService<CommunicationsDbContext>(),
            ];

            var reset = 0;
            foreach (var ctx in contexts)
            {
                reset += DeadLetterRedrive.Reset(ctx);
                if (ctx.ChangeTracker.HasChanges())
                {
                    await ctx.SaveChangesAsync(ct);
                }
            }

            return Results.Ok(new { reset });
        });

        return group;
    }
}
