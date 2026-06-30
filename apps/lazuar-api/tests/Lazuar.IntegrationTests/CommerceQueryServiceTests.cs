using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.Commerce.Infrastructure;
using Modules.Commerce.Infrastructure.Services;
using Modules.CRM.Contracts;
using NSubstitute;
using NUnit.Framework;
using Testcontainers.PostgreSql;

namespace Lazuar.IntegrationTests.Commerce;

[TestFixture]
public class CommerceQueryServiceTests
{
    private PostgreSqlContainer _dbContainer = null!;
    private CommerceDbContext _dbContext = null!;
    private CommerceQueryService _queryService = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // 1. Spin up an ephemeral PostgreSQL database specifically for this test run
#pragma warning disable CS0618
        _dbContainer = new PostgreSqlBuilder()
            .WithDatabase("lazuar_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
#pragma warning restore CS0618

        await _dbContainer.StartAsync();

        var connectionString = _dbContainer.GetConnectionString();

        var options = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "commerce");
            })
            .Options;

        // Mock required dependencies for the DbContext
        var executionContext = Substitute.For<IExecutionContextAccessor>();
        executionContext.TenantId.Returns(Guid.CreateVersion7());

        var mediator = Substitute.For<IMediator>();
        var jobTrigger = new DatabaseJobTrigger();

        _dbContext = new CommerceDbContext(options, executionContext, mediator, jobTrigger);

        // 2. Apply the exact, current Entity Framework schema to the test container
        await _dbContext.Database.MigrateAsync();

        var connectionFactory = new NpgsqlConnectionFactory(connectionString);
        var tokenService = Substitute.For<IMagicLinkTokenService>();
        var crmQueryService = Substitute.For<ICrmQueryService>();

        _queryService = new CommerceQueryService(connectionFactory, tokenService, crmQueryService);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _dbContext.DisposeAsync();
        await _dbContainer.DisposeAsync();
    }

    [Test]
    public void DapperQueries_ShouldMatchEntityFrameworkSchema()
    {
        var orgId = Guid.CreateVersion7();

        // 3. Execute all read models. If any column selected in the raw Dapper SQL 
        // does not exist in the EF Core migrated schema, it will throw an NpgsqlException.
        Assert.DoesNotThrowAsync(async () => await _queryService.GetProductsAsync(orgId));
        Assert.DoesNotThrowAsync(async () => await _queryService.GetCouponsAsync(orgId));
        Assert.DoesNotThrowAsync(async () => await _queryService.GetSubscribersAsync(orgId, 1, 50, null));
        Assert.DoesNotThrowAsync(async () => await _queryService.GetTransactionsAsync(orgId, 1, 50, null, null, null));
        Assert.DoesNotThrowAsync(async () => await _queryService.GetDunningCampaignsAsync(orgId));
        Assert.DoesNotThrowAsync(async () => await _queryService.GetStatsAsync(orgId));
        Assert.DoesNotThrowAsync(async () => await _queryService.GetPortalDataAsync(orgId, Guid.CreateVersion7()));
        Assert.DoesNotThrowAsync(async () => await _queryService.GetCustomCheckoutsAsync(orgId, 1, 50));
    }
}
