using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Application.Services;
using Modules.Lhdn.Infrastructure.Gateways;
using Modules.Lhdn.Infrastructure.Repositories;
using Modules.Lhdn.Infrastructure.Services;
using Modules.Lhdn.Infrastructure.Workers;

namespace Modules.Lhdn.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddLhdnModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default") ?? throw new InvalidOperationException("Default connection string not found.");

        services.AddDbContext<LhdnDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "lhdn");
            }));

        services.AddKeyedScoped<IEventBus, OutboxEventBus<LhdnDbContext>>("LhdnEventBus");

        services.AddScoped<ILhdnRepository, LhdnRepository>();
        services.AddScoped<ICertificateVaultService, CertificateVaultService>();
        services.AddScoped<IXmlSignatureService, XmlSignatureService>();
        services.AddScoped<IUblXmlGenerator, UblXmlGenerator>();
        services.AddScoped<ILhdnGatewayAdapter, LhdnGatewayAdapter>();

        services.AddHostedService<LhdnSubmissionJob>();
        services.AddHostedService<LhdnStatusPollingJob>();

        return services;
    }
}
