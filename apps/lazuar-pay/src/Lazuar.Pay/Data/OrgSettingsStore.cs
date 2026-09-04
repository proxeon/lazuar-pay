using Microsoft.EntityFrameworkCore;

namespace Lazuar.Pay.Data;

internal static class OrgSettingsStore
{
    public static async Task<OrgSettingsRow> GetOrCreateAsync(
        PayDbContext db, string orgId, CancellationToken ct)
    {
        var settings = await db.OrgSettings.FindAsync([orgId], ct);
        if (settings is not null)
        {
            return settings;
        }

        settings = new OrgSettingsRow { OrgId = orgId };
        db.OrgSettings.Add(settings);
        try
        {
            await db.SaveChangesAsync(ct);
            return settings;
        }
        catch (DbUpdateException)
        {
            db.Entry(settings).State = EntityState.Detached;
            return await db.OrgSettings.FindAsync([orgId], ct)
                ?? throw new InvalidOperationException("org settings insert raced and vanished");
        }
    }
}
