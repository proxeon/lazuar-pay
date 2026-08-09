using Lazuar.Api.Middleware;

namespace Lazuar.Api.Composition;

/// <summary>
/// Cross-cutting HTTP pipeline. <b>Order is load-bearing — do not reorder without security review.</b>
/// <list type="number">
/// <item><description>Exception handler (outermost unified errors)</description></item>
/// <item><description>Correlation ID (early logs)</description></item>
/// <item><description>CORS (preflight before auth)</description></item>
/// <item><description>JWT authentication (cookie / bearer)</description></item>
/// <item><description>API key authentication (may establish API_CLIENT principal)</description></item>
/// <item><description>Tenant security (TenantId + membership after both auth mechanisms)</description></item>
/// <item><description>Authorization</description></item>
/// </list>
/// </summary>
public static class MiddlewarePipelineExtensions
{
    public static WebApplication UseLazuarPipeline(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseCors();
        app.UseAuthentication();
        app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
        app.UseMiddleware<TenantSecurityMiddleware>();
        app.UseAuthorization();
        return app;
    }
}
