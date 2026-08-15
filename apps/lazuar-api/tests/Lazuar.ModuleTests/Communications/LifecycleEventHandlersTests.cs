using System;
using BuildingBlocks.Application;
using FluentAssertions;
using Modules.Commerce.Contracts.Events;
using Modules.Communications.Infrastructure.EventHandlers;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Communications;

[TestFixture]
public class LifecycleEventHandlersTests
{
    [Test]
    public void SubscriptionSuspended_DoesNotDispatchPaymentFailed()
    {
        typeof(LifecycleEventHandlers)
            .Should().NotBeAssignableTo<IIntegrationEventHandler<SubscriptionSuspendedIntegrationEvent>>();
    }
}
