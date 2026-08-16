using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Lazuar.TestSupport;
using Microsoft.EntityFrameworkCore;
using Modules.CRM.Contracts;
using Modules.CRM.Domain;
using Modules.CRM.Infrastructure;
using NUnit.Framework;

namespace Lazuar.ModuleTests.CRM;

[TestFixture]
public class ClientProfileCompanyNameTests
{
    [Test]
    public async Task Resolve_StoresCompanyNameAndTin_LeavesIdValueNull()
    {
        await using var db = CreateDb();
        var handler = new ResolveClientProfileCommandHandler(db);
        var orgId = Guid.CreateVersion7();

        var id = await handler.Handle(new ResolveClientProfileCommand(
            orgId,
            "Ada Buyer",
            "ada@example.com",
            "60123456789",
            Tin: "C12345678901",
            IdType: null,
            IdValue: null,
            CompanyName: "Acme Sdn Bhd"), CancellationToken.None);

        var profile = await db.ClientProfiles.IgnoreQueryFilters().SingleAsync(p => p.Id == id);
        profile.Tin.Should().Be("C12345678901");
        profile.CompanyName.Should().Be("Acme Sdn Bhd");
        profile.IdValue.Should().BeNull();
        profile.IdType.Should().BeNull();
    }

    [Test]
    public async Task Resolve_Enrich_FillsBlankTinAndCompany_DoesNotOverwriteExistingTin()
    {
        var orgId = Guid.CreateVersion7();
        await using var db = CreateDb();
        db.ClientProfiles.Add(new ClientProfileEntity
        {
            OrganizationId = orgId,
            FullName = "Ada Buyer",
            Email = "ada@example.com",
            Phone = "",
            Tin = "IG999",
            CompanyName = null
        });
        await db.SaveChangesAsync();

        var handler = new ResolveClientProfileCommandHandler(db);
        await handler.Handle(new ResolveClientProfileCommand(
            orgId,
            "Ada Buyer",
            "ada@example.com",
            "60111111111",
            Tin: "C111",
            CompanyName: "Acme Sdn Bhd"), CancellationToken.None);

        var profile = await db.ClientProfiles.IgnoreQueryFilters().SingleAsync();
        profile.Tin.Should().Be("IG999");
        profile.CompanyName.Should().Be("Acme Sdn Bhd");
        profile.Phone.Should().Be("60111111111");
    }

    [Test]
    public void Anonymize_ClearsCompanyName()
    {
        var profile = new ClientProfileEntity
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = Guid.CreateVersion7(),
            FullName = "Ada",
            Email = "ada@example.com",
            Phone = "601",
            CompanyName = "Acme Sdn Bhd",
            Tin = "C1"
        };

        profile.Anonymize();
        profile.CompanyName.Should().BeNull();
    }

    private static CrmDbContext CreateDb() =>
        new(
            InMemoryDb.CreateOptions<CrmDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());
}
