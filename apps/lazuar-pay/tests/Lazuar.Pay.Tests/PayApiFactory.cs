using Lazuar.Pay.Data;
using Lazuar.Pay.One;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Lazuar.Pay.Tests;

public sealed class PayApiFactory : WebApplicationFactory<Program>
{
    public FakeOneHandler One { get; } = new();
    readonly string _dbName = "pay-" + Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            foreach (var d in services.Where(s => s.ServiceType == typeof(OneClient)).ToList())
            {
                services.Remove(d);
            }

            services.AddDbContext<PayDbContext>(o => o.UseInMemoryDatabase(_dbName));
            services.AddTransient(_ =>
            {
                var http = new HttpClient(One, disposeHandler: false)
                {
                    BaseAddress = new Uri("http://one.test/api/v1/"),
                    Timeout = TimeSpan.FromSeconds(2)
                };
                return new OneClient(http, Options.Create(new OneOptions
                {
                    BaseUrl = "http://one.test/api/v1",
                    TimeoutSeconds = 2
                }));
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<PayDbContext>().Database.EnsureCreated();
        return host;
    }
}
