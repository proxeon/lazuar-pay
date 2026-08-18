using System;
using BuildingBlocks.Infrastructure;
using MediatR;
using NUnit.Framework;

namespace Lazuar.ModuleTests.BuildingBlocks;

[TestFixture]
public class InboxNotificationRequirementTests
{
    [Test]
    public void Notification_Payload_Is_Returned()
    {
        var payload = new Note();
        var required = InboxNotificationRequirement.Require(payload, Guid.CreateVersion7());
        Assert.That(required, Is.SameAs(payload));
    }

    [Test]
    public void Non_Notification_Throws()
    {
        var id = Guid.CreateVersion7();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            InboxNotificationRequirement.Require(new NotANote(), id));
        Assert.That(ex!.Message, Does.Contain(id.ToString()));
        Assert.That(ex.Message, Does.Contain("INotification"));
    }

    [Test]
    public void Null_Payload_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            InboxNotificationRequirement.Require(null, Guid.CreateVersion7()));
    }

    private sealed class Note : INotification;

    private sealed class NotANote
    {
        public string Name { get; set; } = "x";
    }
}
