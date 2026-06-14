using Microsoft.Extensions.DependencyInjection;
using Modules.Lhdn.Application.Services;
using Modules.Lhdn.Infrastructure.Services;
using System.Security.Cryptography.Xml;

namespace Modules.Lhdn.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddLhdnModule(this IServiceCollection services)
    {
        services.AddScoped<ICertificateVaultService, CertificateVaultService>();
        services.AddScoped<IXmlSignatureService, XmlSignatureService>();
        services.AddScoped<IUblXmlGenerator, UblXmlGenerator>();

        return services;
    }
}
