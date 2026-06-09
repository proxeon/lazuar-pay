using System.Security.Claims;
using BuildingBlocks.Application;
using Microsoft.AspNetCore.Http;

namespace Lazuar.Api;

public class ExecutionContextAccessor : IExecutionContextAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ExecutionContextAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid TenantId
    {
        get
        {
            // Read dynamically from the context items (populated by TenantSecurityMiddleware)
            if (_httpContextAccessor.HttpContext?.Items.TryGetValue("TenantId", out var tenantIdObj) == true && tenantIdObj is Guid tenantId)
            {
                return tenantId;
            }
            return Guid.Empty;
        }
    }

    public Guid UserId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }
    }

    public string UserRole => _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value ?? "";

    public bool IsSystemAdmin => _httpContextAccessor.HttpContext?.User?.FindFirst("is_system_admin")?.Value == "true";
}
