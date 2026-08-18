using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Modules.One.Infrastructure.Services;

public static class AuthCookie
{
    public const string MerchantName = "lazuar_auth";
    public const string AdminName = "lazuar_admin_auth";
    public const string AdminPath = "/api/v1/platform";

    public static CookieOptions MerchantOptions(bool isDev, DateTimeOffset? expires = null)
    {
        var options = BaseOptions(isDev);
        if (expires.HasValue)
            options.Expires = expires;
        return options;
    }

    public static CookieOptions AdminOptions(bool isDev, DateTimeOffset? expires = null)
    {
        var options = BaseOptions(isDev);
        options.Path = AdminPath;
        if (expires.HasValue)
            options.Expires = expires;
        return options;
    }

    public static bool IsDevelopment(HttpContext ctx) =>
        ctx.RequestServices?.GetService(typeof(IWebHostEnvironment)) is IWebHostEnvironment env
        && env.IsDevelopment();

    public static void DeleteMerchant(HttpContext ctx) =>
        ctx.Response.Cookies.Delete(MerchantName, MerchantOptions(IsDevelopment(ctx)));

    public static void DeleteAdmin(HttpContext ctx) =>
        ctx.Response.Cookies.Delete(AdminName, AdminOptions(IsDevelopment(ctx)));

    private static CookieOptions BaseOptions(bool isDev) => new()
    {
        HttpOnly = true,
        Secure = !isDev,
        SameSite = SameSiteMode.Lax,
        Domain = isDev ? null : ".lazuar.com"
    };
}
