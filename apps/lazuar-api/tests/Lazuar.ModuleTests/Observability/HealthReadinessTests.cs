using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Infrastructure.Observability;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Observability;

[TestFixture]
public class HealthReadinessTests
{
    [Test]
    public async Task Evaluate_Returns_Unhealthy_When_Db_Unreachable()
    {
        var collector = Substitute.For<IPlatformMetricsCollector>();
        collector.CanConnectAsync(Arg.Any<CancellationToken>()).Returns(false);

        var result = await HealthReadiness.EvaluateAsync(
            collector,
            new ObservabilityOptions { OutboxLagReadyThreshold = null });

        Assert.That(result.IsReady, Is.False);
        Assert.That(result.Status, Is.EqualTo("unhealthy"));
        Assert.That(result.DatabaseReachable, Is.False);
        Assert.That(result.Reason, Does.Contain("Database"));
    }

    [Test]
    public async Task Evaluate_Returns_Ready_When_Db_Up_And_No_Lag_Threshold()
    {
        var collector = Substitute.For<IPlatformMetricsCollector>();
        collector.CanConnectAsync(Arg.Any<CancellationToken>()).Returns(true);

        var result = await HealthReadiness.EvaluateAsync(
            collector,
            new ObservabilityOptions { OutboxLagReadyThreshold = null });

        Assert.That(result.IsReady, Is.True);
        Assert.That(result.Status, Is.EqualTo("ready"));
        Assert.That(result.DatabaseReachable, Is.True);
        await collector.DidNotReceive().CollectAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Evaluate_Returns_Unhealthy_When_Outbox_Lag_Exceeds_Threshold()
    {
        var collector = Substitute.For<IPlatformMetricsCollector>();
        collector.CanConnectAsync(Arg.Any<CancellationToken>()).Returns(true);
        collector.CollectAsync(Arg.Any<CancellationToken>()).Returns(new PlatformMetricsSnapshot
        {
            CollectedAtUtc = DateTime.UtcNow,
            OutboxLagSeconds = 600,
            DatabaseReachable = true,
            Schemas = Array.Empty<SchemaOutboxMetrics>()
        });

        var result = await HealthReadiness.EvaluateAsync(
            collector,
            new ObservabilityOptions { OutboxLagReadyThreshold = TimeSpan.FromMinutes(5) });

        Assert.That(result.IsReady, Is.False);
        Assert.That(result.Status, Is.EqualTo("unhealthy"));
        Assert.That(result.OutboxLagSeconds, Is.EqualTo(600));
        Assert.That(result.Reason, Does.Contain("Outbox lag"));
    }

    [Test]
    public async Task Evaluate_Returns_Ready_When_Outbox_Lag_Within_Threshold()
    {
        var collector = Substitute.For<IPlatformMetricsCollector>();
        collector.CanConnectAsync(Arg.Any<CancellationToken>()).Returns(true);
        collector.CollectAsync(Arg.Any<CancellationToken>()).Returns(new PlatformMetricsSnapshot
        {
            CollectedAtUtc = DateTime.UtcNow,
            OutboxLagSeconds = 30,
            DatabaseReachable = true,
            Schemas = Array.Empty<SchemaOutboxMetrics>()
        });

        var result = await HealthReadiness.EvaluateAsync(
            collector,
            new ObservabilityOptions { OutboxLagReadyThreshold = TimeSpan.FromMinutes(5) });

        Assert.That(result.IsReady, Is.True);
        Assert.That(result.Status, Is.EqualTo("ready"));
        Assert.That(result.OutboxLagSeconds, Is.EqualTo(30));
    }
}
