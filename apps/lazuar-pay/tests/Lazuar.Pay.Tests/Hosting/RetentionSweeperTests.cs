using Lazuar.Pay.Data;
using Lazuar.Pay.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

/// <summary>
/// plans/031/03: the retention sweep prunes the four append-only webhook/audit tables per
/// configured windows, in batches, and never touches the ledger. Tests drive the sweeper
/// directly against the factory's Postgres database with an in-memory config overlay.
/// </summary>
public class RetentionSweeperTests
{
    static string ConnectionString(PayApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<PayDbContext>().Database.GetConnectionString()!;
    }

    static async Task<PayDbContext> OpenAsync(PayApiFactory factory)
    {
        var options = new DbContextOptionsBuilder<PayDbContext>()
            .UseNpgsql(ConnectionString(factory))
            .Options;
        var db = new PayDbContext(options);
        await db.Database.OpenConnectionAsync();
        return db;
    }

    private static readonly DateTimeOffset Old = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Fresh = DateTimeOffset.UtcNow;

    static async Task SeedAllFourTablesAsync(PayDbContext db)
    {
        // One old + one fresh row in every sweep target table.
        db.PspWebhookEvents.AddRange(
            new PspWebhookEventRow { OrgId = "t1", Provider = "stripe", EventId = "old_1", ReceivedAt = Old },
            new PspWebhookEventRow { OrgId = "t1", Provider = "stripe", EventId = "fresh_1", ReceivedAt = Fresh });
        db.OneWebhookEvents.AddRange(
            new OneWebhookEventRow { Id = "one_old", DeliveryId = "del_old", EventType = "tenant.suspended", ReceivedAt = Old },
            new OneWebhookEventRow { Id = "one_fresh", DeliveryId = "del_fresh", EventType = "tenant.suspended", ReceivedAt = Fresh });
        db.OrgWebhookDeliveries.AddRange(
            new OrgWebhookDeliveryRow
            {
                Id = "dl_old", OrgId = "t1", EventId = "evt_old", EventType = "payment.completed",
                PayloadJson = "{}", Status = "succeeded", NextAttemptAt = Old, CreatedAt = Old
            },
            new OrgWebhookDeliveryRow
            {
                Id = "dl_fresh", OrgId = "t1", EventId = "evt_fresh", EventType = "payment.completed",
                PayloadJson = "{}", Status = "pending", NextAttemptAt = Fresh, CreatedAt = Fresh
            });
        db.AuditEvents.AddRange(
            new AuditEventRow { Id = "aud_old", OrgId = "t1", Action = "gateway.credentials.upsert", At = Old },
            new AuditEventRow { Id = "aud_fresh", OrgId = "t1", Action = "gateway.credentials.upsert", At = Fresh });
        await db.SaveChangesAsync();
    }

    [Test]
    public async Task Sweep_deletes_expired_rows_and_keeps_everything_within_retention()
    {
        await using var factory = new PayApiFactory();
        await using var db = await OpenAsync(factory);
        await SeedAllFourTablesAsync(db);

        var sweeper = new RetentionSweeper(db, new ConfigurationBuilder().Build());
        Assert.That(await sweeper.SweepAsync(CancellationToken.None), Is.EqualTo(4), "one expired row per table");

        Assert.That(await db.PspWebhookEvents.CountAsync(x => x.EventId == "old_1"), Is.EqualTo(0));
        Assert.That(await db.PspWebhookEvents.CountAsync(x => x.EventId == "fresh_1"), Is.EqualTo(1));
        Assert.That(await db.OneWebhookEvents.CountAsync(x => x.Id == "one_old"), Is.EqualTo(0));
        Assert.That(await db.OneWebhookEvents.CountAsync(x => x.Id == "one_fresh"), Is.EqualTo(1));
        Assert.That(await db.OrgWebhookDeliveries.CountAsync(x => x.Id == "dl_old"), Is.EqualTo(0));
        Assert.That(await db.OrgWebhookDeliveries.CountAsync(x => x.Id == "dl_fresh"), Is.EqualTo(1));
        Assert.That(await db.AuditEvents.CountAsync(x => x.Id == "aud_old"), Is.EqualTo(0));
        Assert.That(await db.AuditEvents.CountAsync(x => x.Id == "aud_fresh"), Is.EqualTo(1));
    }

    [Test]
    public async Task Retention_days_are_configurable_and_zero_disables_a_table()
    {
        await using var factory = new PayApiFactory();
        await using var db = await OpenAsync(factory);
        db.PspWebhookEvents.AddRange(
            new PspWebhookEventRow { OrgId = "t1", Provider = "stripe", EventId = "two_days", ReceivedAt = DateTimeOffset.UtcNow.AddDays(-2) },
            new PspWebhookEventRow { OrgId = "t1", Provider = "stripe", EventId = "twelve_hours", ReceivedAt = DateTimeOffset.UtcNow.AddHours(-12) });
        db.AuditEvents.Add(new AuditEventRow { Id = "aud_ancient", OrgId = "t1", Action = "x", At = Old });
        await db.SaveChangesAsync();

        var settings = new Dictionary<string, string?>
        {
            ["Pay:Retention:PspWebhookEventsDays"] = "1",   // override below the 90d default
            ["Pay:Retention:AuditEventsDays"] = "0",        // disabled — never swept
        };
        var sweeper = new RetentionSweeper(db, new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        await sweeper.SweepAsync(CancellationToken.None);

        Assert.That(await db.PspWebhookEvents.CountAsync(x => x.EventId == "two_days"), Is.EqualTo(0), "older than the 1-day override");
        Assert.That(await db.PspWebhookEvents.CountAsync(x => x.EventId == "twelve_hours"), Is.EqualTo(1), "inside the 1-day window");
        Assert.That(await db.AuditEvents.CountAsync(x => x.Id == "aud_ancient"), Is.EqualTo(1), "a disabled sweep must not delete");
    }

    [Test]
    public async Task Batched_delete_loops_until_the_table_is_drained()
    {
        await using var factory = new PayApiFactory();
        await using var db = await OpenAsync(factory);
        for (var i = 0; i < 5; i++)
        {
            db.AuditEvents.Add(new AuditEventRow { Id = "aud_b" + i, OrgId = "t1", Action = "x", At = Old });
        }
        await db.SaveChangesAsync();

        var settings = new Dictionary<string, string?>
        {
            ["Pay:Retention:BatchSize"] = "2",
            ["Pay:Retention:AuditEventsDays"] = "30",
        };
        var sweeper = new RetentionSweeper(db, new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        Assert.That(await sweeper.SweepAsync(CancellationToken.None), Is.EqualTo(5), "2 + 2 + 1 across three batched statements");
        Assert.That(await db.AuditEvents.CountAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task Ledger_and_replay_protection_tables_are_never_purged()
    {
        await using var factory = new PayApiFactory();
        await using var db = await OpenAsync(factory);
        db.Checkouts.Add(new CheckoutRow
        {
            Id = "c_keep", OrgId = "t1", PublicToken = "tok_c_keep",
            Amount = 10m, Currency = "MYR", Status = "paid", Provider = "test",
            CreatedAt = Old
        });
        db.Charges.Add(new ChargeRow
        {
            Id = "ch_keep", OrgId = "t1", CheckoutId = "c_keep",
            Provider = "test", Amount = 10m, Currency = "MYR", Status = "paid"
        });
        db.JournalEntries.Add(new JournalEntryRow { Id = "je_keep", OrgId = "t1", CheckoutId = "c_keep", Currency = "MYR", CreatedAt = Old });
        db.JournalLines.Add(new JournalLineRow { Id = "jl_keep", EntryId = "je_keep", Account = "cash", Dc = "D", Amount = 10m });
        db.Refunds.Add(new RefundRow
        {
            Id = "rf_keep", OrgId = "t1", CheckoutId = "c_keep", ChargeId = "ch_keep",
            Amount = 10m, Currency = "MYR", Status = "pending", Provider = "stripe",
            Reason = "late_pay", CreatedAt = Old
        });
        db.IdempotencyKeys.Add(new IdempotencyKeyRow { OrgId = "t1", Key = "k_keep", CheckoutId = "c_keep" });
        db.Documents.Add(new DocumentRow { Id = "doc_keep", OrgId = "t1", CheckoutId = "c_keep", Number = "RCPT-2020-00001", Title = "Official Receipt", CreatedAt = Old });
        db.DocumentSequences.Add(new DocumentSequenceRow { OrgId = "t1", Series = "RCPT", YearMyt = 2020, LastN = 1 });
        await db.SaveChangesAsync();

        var sweeper = new RetentionSweeper(db, new ConfigurationBuilder().Build());
        await sweeper.SweepAsync(CancellationToken.None);

        Assert.That(await db.Checkouts.CountAsync(x => x.Id == "c_keep"), Is.EqualTo(1));
        Assert.That(await db.Charges.CountAsync(x => x.Id == "ch_keep"), Is.EqualTo(1));
        Assert.That(await db.JournalLines.CountAsync(x => x.Id == "jl_keep"), Is.EqualTo(1));
        Assert.That(await db.Refunds.CountAsync(x => x.Id == "rf_keep"), Is.EqualTo(1));
        Assert.That(await db.IdempotencyKeys.CountAsync(x => x.Key == "k_keep"), Is.EqualTo(1));
        Assert.That(await db.Documents.CountAsync(x => x.Id == "doc_keep"), Is.EqualTo(1));
        Assert.That(await db.DocumentSequences.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task Pruning_frees_the_dedupe_key_for_a_hyper_late_redelivery()
    {
        // The documented tradeoff (plans/031/03): once the dedupe row is pruned, a
        // redelivery of the same event id inserts again instead of answering duplicate.
        // Safe by design — the 90-day window is ~30x Stripe's retry horizon and fulfillment
        // stays idempotent (unique charges.CheckoutId, CAS off "open").
        await using var factory = new PayApiFactory();
        await using var db = await OpenAsync(factory);
        db.PspWebhookEvents.Add(new PspWebhookEventRow
        {
            OrgId = "t1", Provider = "stripe", EventId = "evt_recycled", ReceivedAt = Old
        });
        await db.SaveChangesAsync();

        var sweeper = new RetentionSweeper(db, new ConfigurationBuilder().Build());
        await sweeper.SweepAsync(CancellationToken.None);
        Assert.That(await db.PspWebhookEvents.CountAsync(x => x.EventId == "evt_recycled"), Is.EqualTo(0));

        // The raw-SQL delete bypassed the tracker — a redelivery also arrives on a fresh
        // context, so clear before re-inserting.
        db.ChangeTracker.Clear();
        db.PspWebhookEvents.Add(new PspWebhookEventRow
        {
            OrgId = "t1", Provider = "stripe", EventId = "evt_recycled", ReceivedAt = Fresh
        });
        Assert.DoesNotThrowAsync(async () => await db.SaveChangesAsync());
    }
}
