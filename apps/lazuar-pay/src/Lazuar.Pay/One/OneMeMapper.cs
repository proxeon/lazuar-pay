namespace Lazuar.Pay.One;

internal static class OneMeMapper
{
    public static WhoamiResponse? ToWhoami(OneMeResponse? me)
    {
        if (me is null || string.IsNullOrWhiteSpace(me.UserId))
        {
            return null;
        }

        var tenants = new List<WhoamiTenant>();
        foreach (var t in me.Tenants)
        {
            if (string.IsNullOrWhiteSpace(t.Id))
            {
                continue;
            }

            tenants.Add(new WhoamiTenant
            {
                Id = t.Id,
                Slug = t.Slug,
                Name = t.Name,
                Role = t.Role,
                Status = t.Status
            });
        }

        return new WhoamiResponse
        {
            UserId = me.UserId,
            Email = me.Email,
            IsPlatformAdmin = me.IsPlatformAdmin,
            ActiveOrgId = me.ActiveTenantId,
            Tenants = tenants
        };
    }
}
