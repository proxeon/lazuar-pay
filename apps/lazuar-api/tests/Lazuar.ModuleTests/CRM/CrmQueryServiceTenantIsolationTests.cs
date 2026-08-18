using System;
using System.Threading.Tasks;
using BuildingBlocks.Infrastructure;
using Lazuar.TestSupport;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.CRM.Domain;
using Modules.CRM.Infrastructure;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.CRM;

[TestFixture]
public class CrmQueryServiceTenantIsolationTests
{
    [Test]
    public async Task GetClientProfileAsync_Does_Not_Return_Another_Tenant()
    {
        var owner = Guid.CreateVersion7();
        var other = Guid.CreateVersion7();
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new CrmDbContext(
            options,
            FakeExecutionContextAccessor.EmptyTenant(),
            Substitute.For<IMediator>(),
            new DatabaseJobTrigger());

        var profile = new ClientProfileEntity
        {
            OrganizationId = owner,
            FullName = "Owner Buyer",
            Email = "owner@example.com",
            Tin = "C12345678901",
        };
        db.ClientProfiles.Add(profile);
        await db.SaveChangesAsync();

        var sut = new CrmQueryService(db);
        var leaked = await sut.GetClientProfileAsync(other, profile.Id);
        var owned = await sut.GetClientProfileAsync(owner, profile.Id);

        Assert.That(leaked, Is.Null);
        Assert.That(owned?.Tin, Is.EqualTo("C12345678901"));
    }
}
