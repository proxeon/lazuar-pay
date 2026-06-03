using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Application;

namespace BuildingBlocks.Infrastructure;

public abstract class PlatformDbContext : DbContext
{
    protected readonly IExecutionContextAccessor ExecutionContext;

    protected PlatformDbContext(DbContextOptions options, IExecutionContextAccessor executionContext) : base(options)
    {
        ExecutionContext = executionContext;
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added)
            {
                var orgIdProp = entry.Entity.GetType().GetProperty("OrganizationId");
                if (orgIdProp != null && ExecutionContext.TenantId != Guid.Empty)
                {
                    var currentVal = (Guid)orgIdProp.GetValue(entry.Entity)!;
                    if (currentVal == Guid.Empty)
                    {
                        orgIdProp.SetValue(entry.Entity, ExecutionContext.TenantId);
                    }
                }
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
