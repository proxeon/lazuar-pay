using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Modules.One.Contracts;

namespace Modules.Commerce.Infrastructure.EventHandlers;

public class TenantUpdatedUnpublishProductsHandler : IIntegrationEventHandler<TenantUpdatedIntegrationEvent>
{
    private readonly CommerceDbContext _db;

    public TenantUpdatedUnpublishProductsHandler(CommerceDbContext db)
    {
        _db = db;
    }

    public async Task HandleAsync(TenantUpdatedIntegrationEvent @event)
    {
        if (@event.IsActive)
            return;

        var products = await _db.Products
            .IgnoreQueryFilters()
            .Where(p => p.OrganizationId == @event.TenantId && p.IsActive)
            .ToListAsync();

        foreach (var product in products)
            product.Archive();

        if (products.Count > 0)
            await _db.SaveChangesAsync();
    }
}
