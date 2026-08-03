using System;
using BuildingBlocks.Infrastructure;
using NUnit.Framework;

namespace Lazuar.ModuleTests.BuildingBlocks;

[TestFixture]
public class MessageRetryPolicyTests
{
    [Test]
    public void MaxAttempts_Is_Five()
    {
        Assert.That(MessageRetryPolicy.MaxAttempts, Is.EqualTo(5));
    }

    [Test]
    public void GetBackoff_Uses_Exponential_Minutes_From_Attempt_Count()
    {
        Assert.That(MessageRetryPolicy.GetBackoff(1), Is.EqualTo(TimeSpan.FromMinutes(2)));
        Assert.That(MessageRetryPolicy.GetBackoff(2), Is.EqualTo(TimeSpan.FromMinutes(4)));
        Assert.That(MessageRetryPolicy.GetBackoff(3), Is.EqualTo(TimeSpan.FromMinutes(8)));
    }
}
