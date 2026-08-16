using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Lazuar.ApiTypes;
using Lazuar.TestSupport;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Billing.Contracts.Commands;
using Modules.Lhdn.Application;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Infrastructure;
using Modules.Billing.Infrastructure.Commands;
using Modules.Lhdn.Application.Commands;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Application.Queries;
using Modules.Lhdn.Contracts;
using Modules.Lhdn.Domain.Aggregates;
using Modules.Lhdn.Infrastructure.Services;
using Modules.Lhdn.Infrastructure.Services.Strategies;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Billing.Commands;

[TestFixture]
public class TenantLegalProfileTests
{
    [Test]
    public async Task PutBillingProfile_PersistsFields()
    {
        var orgId = Guid.CreateVersion7();
        await using var db = CreateBillingDb(orgId);
        var mediator = Substitute.For<IMediator>();
        var handler = new UpdateTenantBillingProfileCommandHandler(db, mediator);

        await handler.Handle(new UpdateTenantBillingProfileCommand(
            orgId,
            "Acme Solutions Sdn Bhd",
            "C12345678901",
            "202401001234",
            "W10-1808-12345678",
            "https://cdn.example/logo.png",
            new TenantBillingAddressDto
            {
                Line1 = "12 Jalan Ampang",
                City = "Kuala Lumpur",
                Postal_code = "50450",
                State_code = "14",
                Country_code = "MYS"
            }), CancellationToken.None);

        var saved = await db.TenantBillingProfiles.IgnoreQueryFilters().SingleAsync();
        saved.LegalName.Should().Be("Acme Solutions Sdn Bhd");
        saved.Tin.Should().Be("C12345678901");
        saved.RegistrationNumber.Should().Be("202401001234");
        saved.SstRegistrationNumber.Should().Be("W10-1808-12345678");
        saved.LogoUrl.Should().Be("https://cdn.example/logo.png");
        saved.Address!.Line1.Should().Be("12 Jalan Ampang");
        saved.Address.StateCode.Should().Be("14");

        await mediator.Received(1).Send(
            Arg.Is<SyncSupplierStationeryCommand>(c =>
                c.OrganizationId == orgId
                && c.LegalName == "Acme Solutions Sdn Bhd"
                && c.Tin == "C12345678901"
                && c.AddressLine1 == "12 Jalan Ampang"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SyncStationery_UpdatesLhdnIdentity_PreservesSecret()
    {
        var orgId = Guid.CreateVersion7();
        var config = new LhdnTenantConfig(orgId, false, "OLDTIN", "BRN", "202001012345", "SANDBOX", "62010");
        config.UpdateApiCredentials("client-id", "encrypted-secret");
        config.UpdateLegalAddress("Old Name", "Lot 66", "Kuala Lumpur", "14", "50480", "MYS");

        var repo = Substitute.For<ILhdnRepository>();
        repo.GetTenantConfigAsync(orgId, Arg.Any<CancellationToken>()).Returns(config);

        var handler = new SyncSupplierStationeryCommandHandler(repo);
        await handler.Handle(new SyncSupplierStationeryCommand(
            orgId,
            "Acme Solutions Sdn Bhd",
            "C12345678901",
            "12 Jalan Ampang",
            "Kuala Lumpur",
            "14",
            "50450",
            "MYS"), CancellationToken.None);

        config.LegalName.Should().Be("Acme Solutions Sdn Bhd");
        config.SupplierTin.Should().Be("C12345678901");
        config.AddressLine1.Should().Be("12 Jalan Ampang");
        config.MyInvoisClientSecret.Should().Be("encrypted-secret");
        config.MyInvoisClientId.Should().Be("client-id");
        config.IdValue.Should().Be("202001012345");
        await repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SyncStationery_WhenNoLhdnConfig_IsNoOp()
    {
        var orgId = Guid.CreateVersion7();
        var repo = Substitute.For<ILhdnRepository>();
        repo.GetTenantConfigAsync(orgId, Arg.Any<CancellationToken>()).Returns((LhdnTenantConfig?)null);

        var handler = new SyncSupplierStationeryCommandHandler(repo);
        await handler.Handle(new SyncSupplierStationeryCommand(
            orgId, "Acme", "C1", "Line", "KL", "14", "50000", "MYS"), CancellationToken.None);

        await repo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetLhdnConfig_ReturnsPresenceFlags_NotRawSecret()
    {
        var orgId = Guid.CreateVersion7();
        var config = new LhdnTenantConfig(orgId, false, "C12345678901", "BRN", "202401001234", "SANDBOX");
        config.UpdateApiCredentials("my-client", "ciphertext-secret");

        var repo = Substitute.For<ILhdnRepository>();
        repo.GetTenantConfigAsync(orgId, Arg.Any<CancellationToken>()).Returns(config);

        var vault = Substitute.For<ISecretVault>();
        vault.Decrypt("ciphertext-secret").Returns("super-secret-value");

        var handler = new GetLhdnTenantConfigQueryHandler(
            repo,
            vault,
            Options.Create(new LhdnSigningOptions()));
        var dto = await handler.Handle(new GetLhdnTenantConfigQuery(orgId), CancellationToken.None);

        dto.Should().NotBeNull();
        dto!.Has_client_secret.Should().BeTrue();
        dto.Has_certificate.Should().BeFalse();
        dto.Myinvois_client_id.Should().Be("my-client");
        dto.Client_secret_hint.Should().Be("…alue");
        typeof(LhdnTenantConfigDto).GetProperty("Myinvois_client_secret").Should().BeNull();
    }

    [Test]
    public void StandardInvoice_UsesConfigAddress_NotBangunanMerdeka()
    {
        var orgId = Guid.CreateVersion7();
        var config = new LhdnTenantConfig(orgId, false, "C12345678901", "BRN", "202401001234", "SANDBOX", "62010");
        config.UpdateLegalAddress("Acme Solutions Sdn Bhd", "12 Jalan Ampang", "Kuala Lumpur", "14", "50450", "MYS");

        var request = new SubmitDocumentRequestDto
        {
            Internal_id = "INV-1",
            Document_type = SubmitDocumentRequestDtoDocument_type._01,
            Issue_date = DateTimeOffset.UtcNow,
            Buyer_name = "Buyer Sdn Bhd",
            Buyer_tin = "IG111",
            Buyer_id_type = SubmitDocumentRequestDtoBuyer_id_type.BRN,
            Buyer_id_value = "20200101",
            Buyer_address = new LhdnAddressDto
            {
                Line1 = "Buyer Street",
                City = "Petaling Jaya",
                Postal_code = "46000",
                State_code = LhdnAddressDtoState_code._10,
                Country_code = "MYS"
            },
            Items = new System.Collections.Generic.List<LhdnItemDto>
            {
                new()
                {
                    Description = "Service",
                    Classification_code = "022",
                    Quantity = 1,
                    Unit_price = 100,
                    Tax_rate = 0,
                    Tax_amount = 0,
                    Subtotal = 100,
                    Tax_type_code = LhdnItemDtoTax_type_code._06
                }
            },
            Total_excluding_tax = 100,
            Total_tax = 0,
            Total_including_tax = 100
        };

        var renderer = new ScribanTemplateRendererService(NullLogger<ScribanTemplateRendererService>.Instance);
        var xml = new StandardInvoiceStrategy(renderer).Generate(request, config, "1.0");

        xml.Should().NotContain("Bangunan Merdeka");
        xml.Should().NotContain("Lot 66");
        xml.Should().Contain("12 Jalan Ampang");
        xml.Should().Contain("C12345678901");
        xml.Should().Contain("Acme Solutions Sdn Bhd");
    }

    private static BillingDbContext CreateBillingDb(Guid orgId) =>
        new(
            InMemoryDb.CreateOptions<BillingDbContext>(),
            FakeExecutionContextAccessor.ForTenant(orgId),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());
}
