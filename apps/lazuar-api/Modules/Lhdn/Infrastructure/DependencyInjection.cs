using Microsoft.Extensions.DependencyInjection;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Application.Services;
using Modules.Lhdn.Infrastructure.Gateways;
using Modules.Lhdn.Infrastructure.Services;

namespace Modules.Lhdn.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddLhdnModule(this IServiceCollection services)
    {
        services.AddScoped<ICertificateVaultService, CertificateVaultService>();
        services.AddScoped<IXmlSignatureService, XmlSignatureService>();
        services.AddScoped<IUblXmlGenerator, UblXmlGenerator>();
        services.AddScoped<ILhdnGatewayAdapter, LhdnGatewayAdapter>();

        return services;
    }
}
