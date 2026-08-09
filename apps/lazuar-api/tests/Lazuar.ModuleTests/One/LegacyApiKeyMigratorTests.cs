using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lazuar.Api.Jobs.ApiKeyMigration;
using Modules.One.Domain;
using NUnit.Framework;

namespace Lazuar.ModuleTests.One;

[TestFixture]
public class LegacyApiKeyMigratorTests
{
    [Test]
    public async Task Empty_Legacy_Is_Noop()
    {
        var store = new FakeApiKeyMigrationStore();
        var migrator = new LegacyApiKeyMigrator(store);
        var report = await migrator.RunAsync(LiveOptions());

        Assert.That(report.Processed, Is.EqualTo(0));
        Assert.That(report.Inserted, Is.EqualTo(0));
        Assert.That(store.Inserts, Is.Empty);
    }

    [Test]
    public async Task Copy_Row_Inserts_Into_One()
    {
        var orgId = Guid.CreateVersion7();
        var sourceId = Guid.CreateVersion7();
        var store = new FakeApiKeyMigrationStore();
        store.Orgs.Add(orgId);
        store.Legacy.Add(MakeRow(
            sourceId,
            orgId,
            keyHash: "hash-abc",
            scopes: PlatformApiScopes.DefaultDocumentScopes,
            isActive: true));

        var migrator = new LegacyApiKeyMigrator(store);
        var report = await migrator.RunAsync(LiveOptions());

        Assert.That(report.Inserted, Is.EqualTo(1));
        Assert.That(store.Inserts, Has.Count.EqualTo(1));
        var inserted = store.Inserts[0];
        Assert.That(inserted.Id, Is.EqualTo(sourceId));
        Assert.That(inserted.OrganizationId, Is.EqualTo(orgId));
        Assert.That(inserted.KeyHash, Is.EqualTo("hash-abc"));
        Assert.That(inserted.Scopes, Is.EqualTo(PlatformApiScopes.DefaultDocumentScopes));
        Assert.That(inserted.IsActive, Is.True);
        Assert.That(inserted.Name, Is.EqualTo("Test Key"));
        Assert.That(inserted.Prefix, Is.EqualTo("sk_live_"));
        Assert.That(inserted.KeyHint, Is.EqualTo("wxyz"));
    }

    [Test]
    public async Task Rerun_Is_Idempotent_On_KeyHash()
    {
        var orgId = Guid.CreateVersion7();
        var sourceId = Guid.CreateVersion7();
        var store = new FakeApiKeyMigrationStore();
        store.Orgs.Add(orgId);
        store.Legacy.Add(MakeRow(sourceId, orgId, "hash-idem", PlatformApiScopes.DefaultDocumentScopes));

        var migrator = new LegacyApiKeyMigrator(store);
        var first = await migrator.RunAsync(LiveOptions());
        var second = await migrator.RunAsync(LiveOptions());

        Assert.That(first.Inserted, Is.EqualTo(1));
        Assert.That(second.Inserted, Is.EqualTo(0));
        Assert.That(second.AlreadyMigrated, Is.EqualTo(1));
        Assert.That(store.Inserts, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Hash_Collision_Different_Org_Skips_With_Warning_Code()
    {
        var orgA = Guid.CreateVersion7();
        var orgB = Guid.CreateVersion7();
        var sourceId = Guid.CreateVersion7();
        var existingId = Guid.CreateVersion7();
        var store = new FakeApiKeyMigrationStore();
        store.Orgs.Add(orgA);
        store.Orgs.Add(orgB);
        store.One.Add(new OneCredentialProbe
        {
            Id = existingId,
            KeyHash = "shared-hash",
            OrganizationId = orgA
        });
        store.Legacy.Add(MakeRow(sourceId, orgB, "shared-hash", PlatformApiScopes.DefaultDocumentScopes));

        var migrator = new LegacyApiKeyMigrator(store);
        var report = await migrator.RunAsync(LiveOptions());

        Assert.That(report.HashCollisionDifferentOrg, Is.EqualTo(1));
        Assert.That(report.Inserted, Is.EqualTo(0));
        Assert.That(store.Inserts, Is.Empty);
        Assert.That(report.Outcomes[0].Code, Is.EqualTo(MigrationRowCodes.HashCollisionDifferentOrg));
    }

    [Test]
    public async Task Unknown_Scopes_Only_Quarantines()
    {
        var orgId = Guid.CreateVersion7();
        var sourceId = Guid.CreateVersion7();
        var store = new FakeApiKeyMigrationStore();
        store.Orgs.Add(orgId);
        store.Legacy.Add(MakeRow(sourceId, orgId, "hash-unk", "legacy.bogus:scope not.a.scope"));

        var migrator = new LegacyApiKeyMigrator(store);
        var report = await migrator.RunAsync(LiveOptions());

        Assert.That(report.Quarantined, Is.EqualTo(1));
        Assert.That(report.Outcomes[0].Code, Is.EqualTo(MigrationRowCodes.QuarantineUnknownScopesOnly));
        Assert.That(store.Inserts, Is.Empty);
    }

    [Test]
    public async Task Partial_Unknown_Scopes_Keeps_Known()
    {
        var orgId = Guid.CreateVersion7();
        var sourceId = Guid.CreateVersion7();
        var store = new FakeApiKeyMigrationStore();
        store.Orgs.Add(orgId);
        store.Legacy.Add(MakeRow(
            sourceId,
            orgId,
            "hash-partial",
            $"{PlatformApiScopes.LhdnDocumentsRead} weird.unknown:scope {PlatformApiScopes.LhdnDocumentsWrite}"));

        var migrator = new LegacyApiKeyMigrator(store);
        var report = await migrator.RunAsync(LiveOptions());

        Assert.That(report.Inserted, Is.EqualTo(1));
        Assert.That(report.PartialScopes, Is.EqualTo(1));
        Assert.That(store.Inserts[0].Scopes, Is.EqualTo(
            $"{PlatformApiScopes.LhdnDocumentsRead} {PlatformApiScopes.LhdnDocumentsWrite}"));
        Assert.That(report.Outcomes[0].Detail, Does.Contain("dropped_scopes:"));
        Assert.That(report.Outcomes[0].Detail, Does.Contain("weird.unknown:scope"));
    }

    [Test]
    public async Task Orphan_Org_Quarantines()
    {
        var missingOrg = Guid.CreateVersion7();
        var sourceId = Guid.CreateVersion7();
        var store = new FakeApiKeyMigrationStore();
        // no Orgs.Add
        store.Legacy.Add(MakeRow(sourceId, missingOrg, "hash-orphan", PlatformApiScopes.DefaultDocumentScopes));

        var migrator = new LegacyApiKeyMigrator(store);
        var report = await migrator.RunAsync(LiveOptions());

        Assert.That(report.Quarantined, Is.EqualTo(1));
        Assert.That(report.Outcomes[0].Code, Is.EqualTo(MigrationRowCodes.QuarantineOrphanOrg));
        Assert.That(store.Inserts, Is.Empty);
    }

    [Test]
    public async Task DryRun_Does_Not_Insert()
    {
        var orgId = Guid.CreateVersion7();
        var sourceId = Guid.CreateVersion7();
        var store = new FakeApiKeyMigrationStore();
        store.Orgs.Add(orgId);
        store.Legacy.Add(MakeRow(sourceId, orgId, "hash-dry", PlatformApiScopes.DefaultDocumentScopes));

        var migrator = new LegacyApiKeyMigrator(store);
        var report = await migrator.RunAsync(new ApiKeyMigrationOptions
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
    public async Task Inactive_Row_Preserves_IsActive_False()
    {
        var orgId = Guid.CreateVersion7();
        var sourceId = Guid.CreateVersion7();
        var store = new FakeApiKeyMigrationStore();
        store.Orgs.Add(orgId);
        store.Legacy.Add(MakeRow(
            sourceId,
            orgId,
            "hash-inactive",
            PlatformApiScopes.DefaultDocumentScopes,
            isActive: false));

        var migrator = new LegacyApiKeyMigrator(store);
        var report = await migrator.RunAsync(LiveOptions());

        Assert.That(report.Inserted, Is.EqualTo(1));
        Assert.That(store.Inserts[0].IsActive, Is.False);
    }

    [Test]
    public async Task Empty_Hash_Quarantines()
    {
        var orgId = Guid.CreateVersion7();
        var store = new FakeApiKeyMigrationStore();
        store.Orgs.Add(orgId);
        store.Legacy.Add(MakeRow(Guid.CreateVersion7(), orgId, "   ", PlatformApiScopes.DefaultDocumentScopes));

        var migrator = new LegacyApiKeyMigrator(store);
        var report = await migrator.RunAsync(LiveOptions());

        Assert.That(report.Quarantined, Is.EqualTo(1));
        Assert.That(report.Outcomes[0].Code, Is.EqualTo(MigrationRowCodes.QuarantineEmptyHash));
    }

    [Test]
    public async Task Id_Collision_Different_Hash_Remaps_To_New_Id()
    {
        var orgId = Guid.CreateVersion7();
        var sharedId = Guid.CreateVersion7();
        var store = new FakeApiKeyMigrationStore();
        store.Orgs.Add(orgId);
        store.One.Add(new OneCredentialProbe
        {
            Id = sharedId,
            KeyHash = "other-hash",
            OrganizationId = orgId
        });
        store.Legacy.Add(MakeRow(sharedId, orgId, "new-hash", PlatformApiScopes.DefaultDocumentScopes));

        var migrator = new LegacyApiKeyMigrator(store);
        var report = await migrator.RunAsync(LiveOptions());

        Assert.That(report.Inserted, Is.EqualTo(1));
        Assert.That(report.IdRemapped, Is.EqualTo(1));
        Assert.That(store.Inserts[0].Id, Is.Not.EqualTo(sharedId));
        Assert.That(store.Inserts[0].KeyHash, Is.EqualTo("new-hash"));
        Assert.That(report.Outcomes[0].IdRemapped, Is.True);
    }

    private static ApiKeyMigrationOptions LiveOptions() => new()
    {
        Enabled = true,
        DryRun = false,
        BatchSize = 500
    };

    private static LegacyDeveloperApiKeyRow MakeRow(
        Guid id,
        Guid organizationId,
        string keyHash,
        string scopes,
        bool isActive = true) =>
        new()
        {
            Id = id,
            OrganizationId = organizationId,
            Name = "Test Key",
            Prefix = "sk_live_",
            KeyHash = keyHash,
            KeyHint = "wxyz",
            Scopes = scopes,
            IsActive = isActive,
            CreatedAt = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc)
        };

    private sealed class FakeApiKeyMigrationStore : IApiKeyMigrationStore
    {
        public List<LegacyDeveloperApiKeyRow> Legacy { get; } = [];
        public List<OneCredentialProbe> One { get; } = [];
        public HashSet<Guid> Orgs { get; } = [];
        public List<MigratedApiCredentialInsert> Inserts { get; } = [];

        public Task<IReadOnlyList<LegacyDeveloperApiKeyRow>> GetLegacyBatchAsync(
            Guid? afterId,
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            var query = Legacy.OrderBy(r => r.Id).AsEnumerable();
            if (afterId is Guid after)
            {
                query = query.Where(r => r.Id.CompareTo(after) > 0);
            }

            IReadOnlyList<LegacyDeveloperApiKeyRow> page = query.Take(batchSize).ToList();
            return Task.FromResult(page);
        }

        public Task<OneCredentialProbe?> FindByKeyHashAsync(
            string keyHash,
            CancellationToken cancellationToken = default)
        {
            var hit = One.FirstOrDefault(c =>
                string.Equals(c.KeyHash, keyHash, StringComparison.Ordinal));
            return Task.FromResult(hit);
        }

        public Task<OneCredentialProbe?> FindByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var hit = One.FirstOrDefault(c => c.Id == id);
            return Task.FromResult(hit);
        }

        public Task<bool> OrganizationExistsAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Orgs.Contains(organizationId));

        public Task<bool> TryInsertAsync(
            MigratedApiCredentialInsert row,
            CancellationToken cancellationToken = default)
        {
            if (One.Any(c => string.Equals(c.KeyHash, row.KeyHash, StringComparison.Ordinal)))
            {
                return Task.FromResult(false);
            }

            if (One.Any(c => c.Id == row.Id))
            {
                // Simulate PK collision as failure (migrator should remap before insert).
                return Task.FromResult(false);
            }

            Inserts.Add(row);
            One.Add(new OneCredentialProbe
            {
                Id = row.Id,
                KeyHash = row.KeyHash,
                OrganizationId = row.OrganizationId
            });
            return Task.FromResult(true);
        }
    }
}
