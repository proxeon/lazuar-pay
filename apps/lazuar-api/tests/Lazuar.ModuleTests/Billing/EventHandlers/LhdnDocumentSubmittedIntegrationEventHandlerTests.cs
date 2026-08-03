using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Modules.Billing.Infrastructure.EventHandlers;
using Modules.Lhdn.Contracts.Events;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Billing.EventHandlers;

[TestFixture]
public class LhdnDocumentSubmittedIntegrationEventHandlerTests
{
    [Test]
    public async Task HandleAsync_CompletesWithoutWalletOrMediatorDependencies()
    {
        // A.8 / A.10: LHDN credit is owned solely by SubmitTaxDocumentCommand via ICreditCostService.
        // This handler is observability-only and must not deduct credits (prior double-charge bug).
        var logger = Substitute.For<ILogger<LhdnDocumentSubmittedIntegrationEventHandler>>();
        var handler = new LhdnDocumentSubmittedIntegrationEventHandler(logger);

        // Constructor accepts only ILogger — no IMediator / wallet / credit services.
        handler.Should().NotBeNull();

        await handler.HandleAsync(new LhdnDocumentSubmittedIntegrationEvent(
            OrganizationId: Guid.CreateVersion7(),
            InternalReferenceId: "INV-100",
            IsTestMode: false));

        // No throw = empty success path. Logger may be invoked at Debug; not asserted.
        Assert.Pass("LhdnDocumentSubmittedIntegrationEventHandler does not call wallet/DeductTenantCredit.");
    }

    [Test]
    public void HandlerType_HasNoMediatorOrBillingRepositoryConstructorDeps()
    {
        var ctors = typeof(LhdnDocumentSubmittedIntegrationEventHandler).GetConstructors();
        ctors.Should().HaveCount(1);
        var parameters = ctors[0].GetParameters();
        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be(typeof(ILogger<LhdnDocumentSubmittedIntegrationEventHandler>));
    }
}
