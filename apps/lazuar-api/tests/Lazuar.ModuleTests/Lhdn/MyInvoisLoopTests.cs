using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using FluentAssertions;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Billing.Contracts;
using Modules.Billing.Contracts.Events;
using Modules.CRM.Contracts;
using Modules.Lhdn.Application;
using Modules.Lhdn.Application.Commands;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Application.Queries;
using Modules.Lhdn.Application.Services;
using Modules.Lhdn.Domain;
using Modules.Lhdn.Domain.Aggregates;
using Modules.Lhdn.Infrastructure.EventHandlers;
using Modules.Lhdn.Infrastructure.Services;
using Modules.Lhdn.Infrastructure.Services.Strategies;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Lhdn;

[TestFixture]
public class MyInvoisLoopTests
{
    [Test]
    public void InvoiceIssuedHandler_DoesNotSubmitStubTin()
    {
        var handler = new InvoiceIssuedIntegrationEventHandler(NullLogger<InvoiceIssuedIntegrationEventHandler>.Instance);
        var act = () => handler.HandleAsync(new InvoiceIssuedIntegrationEvent(
            Guid.CreateVersion7(), "INV-1", Guid.CreateVersion7(), 10m, "MYR", DateTime.UtcNow, DateTime.UtcNow));
        act.Should().NotThrowAsync();
    }

    [Test]
    public async Task B2bHandler_NoTin_DoesNotSubmit()
    {
        var mediator = Substitute.For<IMediator>();
        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileByEmailAsync(Arg.Any<Guid>(), Arg.Any<string>())
            .Returns(new ClientProfileDto { Email = "a@b.com", Full_name = "Ada", Phone = "1" });
        var lookup = Substitute.For<Modules.Commerce.Contracts.ICommerceDocumentLookup>();
        lookup.GetCustomerForDocumentAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new Modules.Commerce.Contracts.CommerceCustomerDisplay("Ada", "a@b.com"));

        var handler = new B2bTaxInvoiceRequestedIntegrationEventHandler(
            mediator, lookup, crm, NullLogger<B2bTaxInvoiceRequestedIntegrationEventHandler>.Instance);

        await handler.HandleAsync(new B2bTaxInvoiceRequestedIntegrationEvent(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "INV-2026-1", "tx", 100m, 0m, "MYR"));

        await mediator.DidNotReceive().Send(Arg.Any<SubmitTaxDocumentCommand>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task B2bHandler_RealTinAndId_SubmitsBuyerFromCrm()
    {
        var org = Guid.CreateVersion7();
        var mediator = Substitute.For<IMediator>();
        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileByEmailAsync(org, "buyer@example.com")
            .Returns(new ClientProfileDto
            {
                Email = "buyer@example.com",
                Full_name = "Person",
                Company_name = "Acme Sdn Bhd",
                Phone = "6012",
                Tin = "C9876543210",
                Id_type = "BRN",
                Id_value = "202001012345"
            });
        var lookup = Substitute.For<Modules.Commerce.Contracts.ICommerceDocumentLookup>();
        lookup.GetCustomerForDocumentAsync(org, "tx", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new Modules.Commerce.Contracts.CommerceCustomerDisplay("Person", "buyer@example.com"));

        var handler = new B2bTaxInvoiceRequestedIntegrationEventHandler(
            mediator, lookup, crm, NullLogger<B2bTaxInvoiceRequestedIntegrationEventHandler>.Instance);

        await handler.HandleAsync(new B2bTaxInvoiceRequestedIntegrationEvent(
            org, Guid.CreateVersion7(), "INV-2026-9", "tx", 100m, 0m, "MYR"));

        await mediator.Received(1).Send(
            Arg.Is<SubmitTaxDocumentCommand>(c =>
                c.Payload.Buyer_tin == "C9876543210"
                && c.Payload.Buyer_tin != MyInvoisBuyerRules.StubBuyerTin
                && c.Payload.Internal_id == "INV-2026-9"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public void StandardInvoiceXml_UsesTenantCity_NotMerdeka()
    {
        var org = Guid.CreateVersion7();
        var config = new LhdnTenantConfig(org, false, "C111", "BRN", "123");
        config.UpdateLegalAddress("Petaling Co", "Jalan Bukit", "Petaling Jaya", "10", "47800", "MYS");
        var renderer = new ScribanTemplateRendererService(NullLogger<ScribanTemplateRendererService>.Instance);
        var xml = new StandardInvoiceStrategy(renderer).Generate(SamplePayload("INV-X"), config, "1.0");

        xml.Should().Contain("Petaling Jaya");
        xml.Should().Contain("Jalan Bukit");
        xml.Should().NotContain("Bangunan Merdeka");
    }

    [Test]
    public void StandardInvoiceXml_IncludesSstScheme_WhenNumberProvided()
    {
        var org = Guid.CreateVersion7();
        var config = new LhdnTenantConfig(org, false, "C111", "BRN", "123");
        var renderer = new ScribanTemplateRendererService(NullLogger<ScribanTemplateRendererService>.Instance);
        var xml = new StandardInvoiceStrategy(renderer).Generate(SamplePayload("INV-SST"), config, "1.0", "W10-1808-12345678");
        xml.Should().Contain("schemeID=\"SST\"");
        xml.Should().Contain("W10-1808-12345678");
    }

    [Test]
    public async Task Submit_Type01InvalidTin_ThrowsAndDoesNotPersist()
    {
        var repo = Substitute.For<ILhdnRepository>();
        var org = Guid.CreateVersion7();
        repo.GetTenantConfigAsync(org, Arg.Any<CancellationToken>()).Returns(new LhdnTenantConfig(org, false, "C1", "BRN", "1"));
        var tin = Substitute.For<ITaxpayerValidationService>();
        tin.ValidateTinAsync(org, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TinValidationResponse(false, "C999", null));

        var handler = Handler(repo, tin: tin);
        var act = () => handler.Handle(new SubmitTaxDocumentCommand(org, "k", SamplePayload("INV-BAD")), CancellationToken.None);
        await act.Should().ThrowAsync<BusinessRuleValidationException>();
        repo.DidNotReceive().AddTaxDocument(Arg.Any<TaxDocument>());
    }

    [Test]
    public async Task Submit_GeneralPublic_DoesNotValidateTin()
    {
        var repo = Substitute.For<ILhdnRepository>();
        var org = Guid.CreateVersion7();
        repo.GetTenantConfigAsync(org, Arg.Any<CancellationToken>()).Returns(new LhdnTenantConfig(org, false, "C1", "BRN", "1"));
        var tin = Substitute.For<ITaxpayerValidationService>();
        var strategyFactory = Substitute.For<IDocumentStrategyFactory>();
        var strategy = Substitute.For<IUblDocumentStrategy>();
        strategy.Generate(Arg.Any<SubmitDocumentRequestDto>(), Arg.Any<LhdnTenantConfig>(), "1.0", Arg.Any<string?>())
            .Returns("<Invoice></Invoice>");
        strategyFactory.GetStrategy(Arg.Any<SubmitDocumentRequestDto>()).Returns(strategy);

        var payload = SamplePayload("B2C-CONS-1");
        payload.Buyer_tin = MyInvoisBuyerRules.GeneralPublicTin;
        payload.Buyer_id_value = "NA";

        var handler = Handler(repo, tin: tin, factory: strategyFactory);
        await handler.Handle(new SubmitTaxDocumentCommand(org, "cons", payload), CancellationToken.None);
        await tin.DidNotReceive().ValidateTinAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Submit_SigningOff_Explicit11_IsRejected()
    {
        var repo = Substitute.For<ILhdnRepository>();
        var org = Guid.CreateVersion7();
        repo.GetTenantConfigAsync(org, Arg.Any<CancellationToken>()).Returns(new LhdnTenantConfig(org, false, "C1", "BRN", "1"));
        var tin = Substitute.For<ITaxpayerValidationService>();
        tin.ValidateTinAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TinValidationResponse(true, "C9", "Ok"));
        var payload = SamplePayload("INV-11");
        payload.Document_version = "1.1";
        var handler = Handler(repo, tin: tin, signing: new LhdnSigningOptions { Signing = "Off" });
        var act = () => handler.Handle(new SubmitTaxDocumentCommand(org, "v11", payload), CancellationToken.None);
        await act.Should().ThrowAsync<BusinessRuleValidationException>();
        repo.DidNotReceive().AddTaxDocument(Arg.Any<TaxDocument>());
    }

    [Test]
    public async Task Submit_AutoNoCert_StaysUnsigned10()
    {
        var repo = Substitute.For<ILhdnRepository>();
        var org = Guid.CreateVersion7();
        repo.GetTenantConfigAsync(org, Arg.Any<CancellationToken>()).Returns(new LhdnTenantConfig(org, false, "C1", "BRN", "1"));
        var tin = Substitute.For<ITaxpayerValidationService>();
        tin.ValidateTinAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TinValidationResponse(true, "C9", "Ok"));
        var factory = Substitute.For<IDocumentStrategyFactory>();
        var strategy = Substitute.For<IUblDocumentStrategy>();
        strategy.Generate(Arg.Any<SubmitDocumentRequestDto>(), Arg.Any<LhdnTenantConfig>(), "1.0", Arg.Any<string?>())
            .Returns("<Invoice>unsigned</Invoice>");
        factory.GetStrategy(Arg.Any<SubmitDocumentRequestDto>()).Returns(strategy);
        TaxDocument? saved = null;
        repo.When(r => r.AddTaxDocument(Arg.Any<TaxDocument>())).Do(c => saved = c.Arg<TaxDocument>());

        var handler = Handler(repo, tin: tin, factory: factory, signing: new LhdnSigningOptions { Signing = "Auto" });
        await handler.Handle(new SubmitTaxDocumentCommand(org, "auto", SamplePayload("INV-10")), CancellationToken.None);
        saved.Should().NotBeNull();
        saved!.RawXmlContent.Should().Contain("unsigned");
        saved.RawXmlContent.Should().NotContain("SIGNATURE_PLACEHOLDER");
    }

    [Test]
    public void JsonSigner_WithSelfSignedCert_EmitsNonPlaceholderSignature()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=LazuarSign", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        var org = Guid.CreateVersion7();
        var config = new LhdnTenantConfig(org, false, "C1", "BRN", "1");
        config.UpdateLegalAddress("Co", "Line", "KL", "14", "50000", "MYS");
        var signed = new JsonUblDocumentSigner().SignJson(SamplePayload("INV-SIGN"), config, cert, null);

        signed.Format.Should().Be("JSON");
        signed.DocumentVersion.Should().Be("1.1");
        signed.Content.Should().Contain("SignatureValue");
        signed.Content.Should().NotContain("SIGNATURE_PLACEHOLDER");
        signed.HashHex.Should().HaveLength(64);
    }

    [Test]
    public async Task GetDocument_Pending_HasNoQr()
    {
        var repo = Substitute.For<ILhdnRepository>();
        var org = Guid.CreateVersion7();
        var doc = new TaxDocument(org, "INV-P", "hash", "<Invoice/>");
        repo.GetTaxDocumentByInternalIdAsync(org, "INV-P", Arg.Any<CancellationToken>()).Returns(doc);
        var links = Substitute.For<ILhdnLinkService>();
        links.GetPortalUrl().Returns("https://preprod.myinvois.hasil.gov.my");
        var handler = new GetLhdnDocumentStatusQueryHandler(repo, links);
        var result = await handler.Handle(new GetLhdnDocumentStatusQuery(org, "INV-P"), CancellationToken.None);
        result!.Qr_link.Should().BeNull();
    }

    [Test]
    public async Task GetDocument_Valid_HasShareQr()
    {
        var repo = Substitute.For<ILhdnRepository>();
        var org = Guid.CreateVersion7();
        var doc = new TaxDocument(org, "INV-V", "hash", "<Invoice/>");
        doc.MarkAsSubmitted("sub", "uuid-1");
        doc.MarkAsValid("long-1");
        repo.GetTaxDocumentByInternalIdAsync(org, "INV-V", Arg.Any<CancellationToken>()).Returns(doc);
        var links = Substitute.For<ILhdnLinkService>();
        links.GetPortalUrl().Returns("https://preprod.myinvois.hasil.gov.my");
        var handler = new GetLhdnDocumentStatusQueryHandler(repo, links);
        var result = await handler.Handle(new GetLhdnDocumentStatusQuery(org, "INV-V"), CancellationToken.None);
        result!.Qr_link.Should().Contain("/share/");
        result.Status.Should().Be("VALID");
    }

    [Test]
    public void DetectFormat_JsonVsXml()
    {
        MyInvoisBuyerRules.DetectSubmissionFormat("  {\"Invoice\":[]}").Should().Be("JSON");
        MyInvoisBuyerRules.DetectSubmissionFormat("<Invoice/>").Should().Be("XML");
    }

    private static SubmitTaxDocumentCommandHandler Handler(
        ILhdnRepository repo,
        ITaxpayerValidationService? tin = null,
        IDocumentStrategyFactory? factory = null,
        LhdnSigningOptions? signing = null)
    {
        if (factory == null)
        {
            factory = Substitute.For<IDocumentStrategyFactory>();
            var strategy = Substitute.For<IUblDocumentStrategy>();
            strategy.Generate(Arg.Any<SubmitDocumentRequestDto>(), Arg.Any<LhdnTenantConfig>(), Arg.Any<string>(), Arg.Any<string?>())
                .Returns("<Invoice></Invoice>");
            factory.GetStrategy(Arg.Any<SubmitDocumentRequestDto>()).Returns(strategy);
        }

        if (tin == null)
        {
            tin = Substitute.For<ITaxpayerValidationService>();
            tin.ValidateTinAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new TinValidationResponse(true, "C9", "Ok"));
        }

        var ctx = Substitute.For<IExecutionContextAccessor>();
        ctx.IsTestMode.Returns(true);
        var billing = Substitute.For<IBillingQueryService>();
        var costs = Substitute.For<ICreditCostService>();
        costs.GetCost(Arg.Any<CreditAction>()).Returns(0);

        return new SubmitTaxDocumentCommandHandler(
            repo,
            factory,
            Substitute.For<IUblValidatorService>(),
            ctx,
            billing,
            costs,
            Substitute.For<IMediator>(),
            Substitute.For<ILogger<SubmitTaxDocumentCommandHandler>>(),
            tin,
            Substitute.For<IDocumentSigner>(),
            Substitute.For<ICertificateVaultService>(),
            Options.Create(signing ?? new LhdnSigningOptions()));
    }

    private static SubmitDocumentRequestDto SamplePayload(string id) => new()
    {
        Internal_id = id,
        Document_type = SubmitDocumentRequestDtoDocument_type._01,
        Issue_date = DateTimeOffset.UtcNow,
        Buyer_name = "Buyer",
        Buyer_tin = "C9876543210",
        Buyer_id_type = SubmitDocumentRequestDtoBuyer_id_type.BRN,
        Buyer_id_value = "20200101",
        Buyer_address = new LhdnAddressDto
        {
            Line1 = "A",
            City = "KL",
            Postal_code = "50000",
            State_code = LhdnAddressDtoState_code._14,
            Country_code = "MYS"
        },
        Items = new List<LhdnItemDto>
        {
            new()
            {
                Description = "Item",
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
}
