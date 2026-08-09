using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lazuar.Api.Jobs.WebhookSubscriptionMigration;
using NUnit.Framework;

namespace Lazuar.ModuleTests.One;

[TestFixture]
public class LegacyWebhookSubscriptionMigratorTests
{
    [Test]
    public async Task Empty_Legacy_Is_Noop()
    {
        var store = new FakeWebhookSubscriptionMigrationStore();
        var migrator = new LegacyWebhookSubscriptionMigrator(store);
        var report = await migrator.RunAsync(LiveOptions());

        Assert.That(report.Processed, Is.EqualTo(0));
        Assert.That(report.Inserted, Is.EqualTo(0));
        Assert.That(store.Inserts, Is.Empty);
    }

    [Test]
    public async Task Copy_Active_Row_Inserts_Into_One_With_Preserved_Secret_And_Invoice_Events()
    {
        var orgId = Guid.CreateVersion7();
        var sourceId = Guid.CreateVersion7();
        var secret = "legacy-hmac-secret-abc";
        var store = new FakeWebhookSubscriptionMigrationStore();
        store.Orgs.Add(orgId);
        store.Legacy.Add(MakeRow(
            sourceId,
            orgId,
            url: "https://hooks.example.com/lhdn",
            secret: secret));

        var migrator = new LegacyWebhookSubscriptionMigrator(store);
        var report = await migrator.RunAsync(LiveOptions());

        Assert.That(report.Inserted, Is.EqualTo(1));
        Assert.That(store.Inserts, Has.Count.EqualTo(1));
        var inserted = store.Inserts[0];
        Assert.That(inserted.OrganizationId, Is.EqualTo(orgId));
        Assert.That(inserted.Url, Is.EqualTo("https://hooks.example.com/lhdn"));
        Assert.That(inserted.SecretKey, Is.EqualTo(secret));
        Assert.That(inserted.IsActive, Is.True);
        Assert.That(inserted.Id, Is.Not.EqualTo(sourceId)); // domain mints new Id

        var events = JsonSerializer.Deserialize<List<string>>(inserted.EnabledEventsJson);
        Assert.That(events, Is.EquivalentTo(new[] { "invoice.valid", "invoice.invalid" }));
        Assert.That(
            inserted.EnabledEventsJson,
            Is.EqualTo(JsonSerializer.Serialize(
                LegacyWebhookSubscriptionMigrator.LhdnInvoiceEnabledEvents.ToList())));
    }

    [Test]
    public async Task Rerun_Is_Idempotent_On_Org_And_Url()
    {
        var orgId = Guid.CreateVersion7();
        var sourceId = Guid.CreateVersion7();
        var store = new FakeWebhookSubscriptionMigrationStore();
        store.Orgs.Add(orgId);
        store.Legacy.Add(MakeRow(sourceId, orgId, "https://hooks.example.com/idem", "sec-1"));

        var migrator = new LegacyWebhookSubscriptionMigrator(store);
        var first = await migrator.RunAsync(LiveOptions());
        var second = await migrator.RunAsync(LiveOptions());

        Assert.That(first.Inserted, Is.EqualTo(1));
        Assert.That(second.Inserted, Is.EqualTo(0));
        Assert.That(second.AlreadyMigrated, Is.EqualTo(1));
        Assert.That(store.Inserts, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Existing_One_Endpoint_Same_Org_Url_Skips()
    {
        var orgId = Guid.CreateVersion7();
        var existingId = Guid.CreateVersion7();
        var store = new FakeWebhookSubscriptionMigrationStore();
        store.Orgs.Add(orgId);
        store.One.Add(new OneWebhookEndpointProbe
        {
            Id = existingId,
            OrganizationId = orgId,
            Url = "https://hooks.example.com/existing",
            IsActive = true
        });
        store.Legacy.Add(MakeRow(
            Guid.CreateVersion7(),
            orgId,
            "https://hooks.example.com/existing",
            "other-secret-not-compared"));

        var migrator = new LegacyWebhookSubscriptionMigrator(store);
        var report = await migrator.RunAsync(LiveOptions());

        Assert.That(report.AlreadyMigrated, Is.EqualTo(1));
        Assert.That(report.Inserted, Is.EqualTo(0));
        Assert.That(store.Inserts, Is.Empty);
        Assert.That(report.Outcomes[0].TargetId, Is.EqualTo(existingId));
    }

    [Test]
    public async Task Invalid_Url_Quarantines()
    {
        var orgId = Guid.CreateVersion7();
        var store = new FakeWebhookSubscriptionMigrationStore();
        store.Orgs.Add(orgId);
        store.Legacy.Add(MakeRow(
            Guid.CreateVersion7(),
            orgId,
            url: "http://evil.example.com/no-https",
            secret: "sec"));

        var migrator = new LegacyWebhookSubscriptionMigrator(store);
        var report = await migrator.RunAsync(LiveOptions());

        Assert.That(report.Quarantined, Is.EqualTo(1));
        Assert.That(report.Outcomes[0].Code, Is.EqualTo(MigrationRowCodes.QuarantineInvalidUrl));
        Assert.That(store.Inserts, Is.Empty);
    }

    [Test]
    public async Task Empty_Secret_Quarantines()
    {
        var orgId = Guid.CreateVersion7();
        var store = new FakeWebhookSubscriptionMigrationStore();
        store.Orgs.Add(orgId);
        store.Legacy.Add(MakeRow(
            Guid.CreateVersion7(),
            orgId,
            url: "https://hooks.example.com/ok",
            secret: "   "));

        var migrator = new LegacyWebhookSubscriptionMigrator(store);
        var report = await migrator.RunAsync(LiveOptions());

        Assert.That(report.Quarantined, Is.EqualTo(1));
        Assert.That(report.Outcomes[0].Code, Is.EqualTo(MigrationRowCodes.QuarantineEmptySecret));
        Assert.That(store.Inserts, Is.Empty);
    }

    [Test]
    public async Task Orphan_Org_Quarantines()
    {
        var missingOrg = Guid.CreateVersion7();
        var store = new FakeWebhookSubscriptionMigrationStore();
        store.Legacy.Add(MakeRow(
            Guid.CreateVersion7(),
            missingOrg,
            "https://hooks.example.com/orphan",
            "sec"));

        var migrator = new LegacyWebhookSubscriptionMigrator(store);
        var report = await migrator.RunAsync(LiveOptions());

        Assert.That(report.Quarantined, Is.EqualTo(1));
        Assert.That(report.Outcomes[0].Code, Is.EqualTo(MigrationRowCodes.QuarantineOrphanOrg));
        Assert.That(store.Inserts, Is.Empty);
    }

    [Test]
    public async Task DryRun_Does_Not_Insert()
    {
        var orgId = Guid.CreateVersion7();
        var store = new FakeWebhookSubscriptionMigrationStore();
        store.Orgs.Add(orgId);
        store.Legacy.Add(MakeRow(
            Guid.CreateVersion7(),
            orgId,
            "https://hooks.example.com/dry",
            "sec"));

        var migrator = new LegacyWebhookSubscriptionMigrator(store);
        var report = await migrator.RunAsync(new WebhookSubscriptionMigrationOptions
        {
            Enabled = true,
            DryRun = true,
            BatchSize = 500
        });

        Assert.That(report.DryRun, Is.True);
        Assert.That(report.WouldInsert, Is.EqualTo(1));
        Assert.That(report.Inserted, Is.EqualTo(0));
        Assert.That(store.Inserts, Is.Empty);
        Assert.That(store.One, Is.Empty);
    }

    [Test]
    public async Task Trims_Url_For_Idempotency_And_Insert()
    {
        var orgId = Guid.CreateVersion7();
        var store = new FakeWebhookSubscriptionMigrationStore();
        store.Orgs.Add(orgId);
        store.Legacy.Add(MakeRow(
            Guid.CreateVersion7(),
            orgId,
            url: "  https://hooks.example.com/trim  ",
            secret: "sec"));

        var migrator = new LegacyWebhookSubscriptionMigrator(store);
        var report = await migrator.RunAsync(LiveOptions());

        Assert.That(report.Inserted, Is.EqualTo(1));
        Assert.That(store.Inserts[0].Url, Is.EqualTo("https://hooks.example.com/trim"));
    }

    [Test]
    public async Task Domain_Ctor_Does_Not_Remint_Secret()
    {
        // Regression: CreateWebhookEndpointCommand mints whsec_; migrator must not.
        var orgId = Guid.CreateVersion7();
        var store = new FakeWebhookSubscriptionMigrationStore();
        store.Orgs.Add(orgId);
        const string preserved = "not-a-whsec-prefix-legacy";
        store.Legacy.Add(MakeRow(
            Guid.CreateVersion7(),
            orgId,
            "https://hooks.example.com/preserve",
            preserved));

        var migrator = new LegacyWebhookSubscriptionMigrator(store);
        await migrator.RunAsync(LiveOptions());

        Assert.That(store.Inserts[0].SecretKey, Is.EqualTo(preserved));
        Assert.That(store.Inserts[0].SecretKey.StartsWith("whsec_"), Is.False);
    }

    private static WebhookSubscriptionMigrationOptions LiveOptions() => new()
    {
        Enabled = true,
        DryRun = false,
        BatchSize = 500
    };

    private static LegacyWebhookSubscriptionRow MakeRow(
        Guid id,
        Guid organizationId,
        string url,
        string secret) =>
        new()
        {
            Id = id,
            OrganizationId = organizationId,
            Url = url,
            Secret = secret,
            IsActive = true,
            CreatedAt = new DateTime(2024, 6, 1, 10, 0, 0, DateTimeKind.Utc)
        };

    private sealed class FakeWebhookSubscriptionMigrationStore : IWebhookSubscriptionMigrationStore
    {
        public List<LegacyWebhookSubscriptionRow> Legacy { get; } = [];
        public List<OneWebhookEndpointProbe> One { get; } = [];
        public HashSet<Guid> Orgs { get; } = [];
        public List<MigratedTenantWebhookEndpointInsert> Inserts { get; } = [];

        public Task<IReadOnlyList<LegacyWebhookSubscriptionRow>> GetActiveLegacyBatchAsync(
            Guid? afterId,
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            var query = Legacy.Where(r => r.IsActive).OrderBy(r => r.Id).AsEnumerable();
            if (afterId is Guid after)
            {
                query = query.Where(r => r.Id.CompareTo(after) > 0);
            }

            IReadOnlyList<LegacyWebhookSubscriptionRow> page = query.Take(batchSize).ToList();
            return Task.FromResult(page);
        }

        public Task<OneWebhookEndpointProbe?> FindByOrgAndUrlAsync(
            Guid organizationId,
            string url,
            CancellationToken cancellationToken = default)
        {
            var hit = One.FirstOrDefault(e =>
                e.OrganizationId == organizationId
                && string.Equals(e.Url, url, StringComparison.Ordinal));
            return Task.FromResult(hit);
        }

        public Task<bool> OrganizationExistsAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Orgs.Contains(organizationId));

        public Task<bool> TryInsertAsync(
            MigratedTenantWebhookEndpointInsert row,
            CancellationToken cancellationToken = default)
        {
            if (One.Any(e =>
                    e.OrganizationId == row.OrganizationId
                    && string.Equals(e.Url, row.Url, StringComparison.Ordinal)))
            {
                return Task.FromResult(false);
            }

            Inserts.Add(row);
            One.Add(new OneWebhookEndpointProbe
            {
                Id = row.Id,
                OrganizationId = row.OrganizationId,
                Url = row.Url,
                IsActive = row.IsActive
            });
            return Task.FromResult(true);
        }
    }
}
