using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Application.Queries;
using Modules.CRM.Contracts;

namespace Modules.Commerce.Infrastructure.Services;

public partial class CommerceQueryService : ICommerceQueryService
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly IMagicLinkTokenService _tokenService;
    private readonly ICrmQueryService _crmQueryService;

    public CommerceQueryService(
        [FromKeyedServices("CommerceSqlConnectionFactory")] ISqlConnectionFactory connectionFactory,
        IMagicLinkTokenService tokenService,
        ICrmQueryService crmQueryService)
    {
        _connectionFactory = connectionFactory;
        _tokenService = tokenService;
        _crmQueryService = crmQueryService;
    }
}
