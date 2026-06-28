using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Application.Queries;

namespace Modules.Commerce.Infrastructure.Services;

public partial class CommerceQueryService : ICommerceQueryService
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly IMagicLinkTokenService _tokenService;

    public CommerceQueryService(
        [FromKeyedServices("CommerceSqlConnectionFactory")] ISqlConnectionFactory connectionFactory,
        IMagicLinkTokenService tokenService)
    {
        _connectionFactory = connectionFactory;
        _tokenService = tokenService;
    }
}
