using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Lazuar.Api.Infrastructure.Data;

public abstract class PlatformDbContext : DbContext
{
    protected readonly IExecutionContextAccessor ExecutionContext;
    protected Guid TenantId => ExecutionContext.TenantId;

    protected PlatformDbContext(
        DbContextOptions options,
        IExecutionContextAccessor executionContext) : base(options)
    {
        ExecutionContext = executionContext;
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                if (entry.State == EntityState.Added)
                {
                    var orgIdProp = entry.Entity.GetType().GetProperty("OrganizationId");
                    if (orgIdProp != null && TenantId != Guid.Empty)
                    {
                        var currentVal = (Guid)orgIdProp.GetValue(entry.Entity)!;
                        if (currentVal == Guid.Empty)
                        {
                            orgIdProp.SetValue(entry.Entity, TenantId);
                        }
                    }
                }

                var recordedByProp = entry.Entity.GetType().GetProperty("RecordedBy");
                if (recordedByProp != null && recordedByProp.PropertyType == typeof(string))
                {
                    recordedByProp.SetValue(entry.Entity, ExecutionContext.AuditSignature);
                }

                var actorIdProp = entry.Entity.GetType().GetProperty("ActorId");
                if (actorIdProp != null && actorIdProp.PropertyType == typeof(string))
                {
                    actorIdProp.SetValue(entry.Entity, ExecutionContext.AuditSignature);
                }
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
