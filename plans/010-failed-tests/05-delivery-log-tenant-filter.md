# 05 — Delivery-log tenant filter vs `DispatchMessageIntegrationEventHandlerTests`

## 1. Title, assigned tests, HEAD

**Title:** `MessageDeliveryLog` is now `IMustHaveTenant`. The eight assigned dispatch-handler tests write a row with a real `OrganizationId` under **empty ambient tenant**, then read it with `_db.MessageDeliveryLogs.SingleAsync()` (no `IgnoreQueryFilters()`). The global EF filter is `OrganizationId == ExecutionContext.TenantId` and `TenantId` is `Guid.Empty`, so the query returns zero rows and `SingleAsync` throws `InvalidOperationException: Sequence contains no elements`.

This is a **test-read bug** caused by the product fix for issue **179**. The handler still persists the log. The product filter is correct and must not be reverted.

**Assigned tests** (all in `DispatchMessageIntegrationEventHandlerTests`):

| # | Test method | What the test is actually checking (before the log read) | Log assertion that blows up |
|---|-------------|----------------------------------------------------------|-----------------------------|
| 1 | `HandleAsync_EmailChannel_WrapsBrandAndSendsViaIEmailService` | BYOK send + brand wrap; no WA; no credit deduct | `SENT` / `EMAIL` / `re_abc` |
| 2 | `HandleAsync_SuppressedAddress_SkipsEmailAndDoesNotSend` | suppression short-circuit; `IEmailService` not called | `SKIPPED` / contains `"suppressed"` |
| 3 | `HandleAsync_TenantEmail_InactiveByok_LogsFailedAndThrowsNoFallback` | inactive BYOK → throw `*No platform fallback*` | `FAILED` / contains `"No platform fallback"` |
| 4 | `HandleAsync_TenantEmail_NullByok_LogsFailedAndThrowsNoFallback` | null credentials → same throw | `Status == FAILED` |
| 5 | `HandleAsync_WhatsAppDisabled_CostTwo_DoesNotDeduct` | WA flag off + cost 2 → no send, no deduct | `SKIPPED` / `"WhatsApp channel disabled"` |
| 6 | `HandleAsync_WhatsAppDisabled_SkipsWhatsAppAndDoesNotCallIMessagingService` | WA flag off (default) → no send, no deduct | `SKIPPED` / `"WhatsApp channel disabled"` |
| 7 | `HandleAsync_WhatsAppEnabled_ConsoleTransport_DoesNotDeduct` | live `ConsoleMessagingService`, cost 2, `IsBillable=false` | `SENT` / `WHATSAPP` |
| 8 | `HandleAsync_WhatsAppEnabled_CostZero_SubstituteTransport_DoesNotDeduct` | substitute transport, cost 0 | `SENT` / `WHATSAPP` |

**File under test (tests):**
`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/Messaging/DispatchMessageIntegrationEventHandlerTests.cs`

**Entity:**
`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Messaging/Domain/MessageDeliveryLog.cs`

**HEAD (current branch `fix/180-unify-outbox-inbox`):**

```
4531f210f61b3d58d0332f1728b6a7889a1d2cad
fix(api): register every module outbox and inbox through one helper
```

Issue **179** itself landed earlier on this same line of work:

```
8237e1c6bbac5d494d14b5602438fdb55ab1efd0
fix(messaging): apply the tenant filter to MessageDeliveryLog

The log implements IMustHaveTenant so empty ambient no longer returns
every tenant's recipient addresses. PaymentWebhookLog stays global
for provider EventId idempotency.
```

That commit touched **four files only**:

1. `apps/lazuar-api/Modules/Messaging/Domain/MessageDeliveryLog.cs` — add `IMustHaveTenant`; make `OrganizationId` settable.
2. `apps/lazuar-api/tests/Lazuar.ArchitectureTests/TenantIsolationArchitectureTests.cs` — lock the interface (and lock `PaymentWebhookLog` as **not** tenant-filtered).
3. `issues/179-p1-b10-x23-child-log-tables-with-organizationid-or-session-id-and-no-tenant.md` — status `resolved`.
4. `issues/README.md` — row 179.

It did **not** update `DispatchMessageIntegrationEventHandlerTests`. That is why these eight methods fail on `HEAD` even though 179 is marked resolved.

`fix/180-unify-outbox-inbox` (`4531f210`) only unifies `AddModuleOutboxInbox`. It does not touch `MessageDeliveryLog` or these tests. The failures are inherited from 179 sitting in the branch history.

---

## 2. How the test fixture builds `MessagingDbContext` / execution context

The fixture is a worker-style InMemory module test. It is **intentional** that ambient tenant is empty: `DispatchMessageIntegrationEventHandler` is an inbox / event-bus handler, not an HTTP endpoint. Production workers have no `HttpContext`, so `ExecutionContextAccessor.TenantId` is `Guid.Empty` (see §3).

### 2.1 `SetUp` — one InMemory database per test, ambient tenant forced to empty

```34:62:apps/lazuar-api/tests/Lazuar.ModuleTests/Messaging/DispatchMessageIntegrationEventHandlerTests.cs
    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<MessagingDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;

        var executionContext = Substitute.For<IExecutionContextAccessor>();
        executionContext.TenantId.Returns(Guid.Empty);

        _db = new MessagingDbContext(
            options,
            executionContext,
            Substitute.For<IMediator>(),
            new DatabaseJobTrigger());

        _email = Substitute.For<IEmailService>();
        _messaging = Substitute.For<IMessagingService>();
        _billing = Substitute.For<IBillingQueryService>();
        _creditCost = Substitute.For<ICreditCostService>();
        _suppression = Substitute.For<ISuppressionService>();
        _comms = Substitute.For<ICommunicationsQueryService>();
        _mediator = Substitute.For<IMediator>();

        _creditCost.GetCost(CreditAction.WhatsAppSend).Returns(0);
        _suppression.IsSuppressedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<SuppressionLane>()).Returns(false);

        _sut = CreateSut();
    }
```

Facts that matter:

- **New InMemory database name per test** (`Guid.CreateVersion7()`). There is never more than one delivery-log row in that database after a single `HandleAsync`. `SingleAsync` is the right *cardinality* assertion — it is just applied to the **filtered** set.
- **NSubstitute `IExecutionContextAccessor`**, not `FakeExecutionContextAccessor`. The substitute is configured `TenantId.Returns(Guid.Empty)`. Even without that line, NSubstitute returns `default(Guid)` for a value-type property, which is also `Guid.Empty`. The explicit `Returns(Guid.Empty)` documents intent: this fixture is a **worker with empty ambient**.
- **`MessagingDbContext` is a `PlatformDbContext`**. The global `IMustHaveTenant` filter is therefore compiled into this InMemory model the first time the context is used. EF InMemory **does** apply query filters. That is proven by `TenantIsolationHardeningTests.Empty_Tenant_EF_Filter_Returns_Zero_Rows` (quoted in §3).
- The execution-context substitute is a **local** in `SetUp`. Tests cannot later flip `TenantId` to the event’s `orgId` without reaching back into that substitute. There is no field holding the accessor. That is another reason the right fix is `IgnoreQueryFilters()` on the read, not “set ambient in each test.”
- Default `_sut` is built with `Messaging:WhatsAppEnabled=false` (see `CreateSut` below). WhatsApp-enabled tests rebuild the handler.

### 2.2 `CreateSut` — same `_db`, optional transport / WA flag

```64:86:apps/lazuar-api/tests/Lazuar.ModuleTests/Messaging/DispatchMessageIntegrationEventHandlerTests.cs
    private DispatchMessageIntegrationEventHandler CreateSut(
        IMessagingService? messaging = null,
        bool whatsAppEnabled = false)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Messaging:WhatsAppEnabled"] = whatsAppEnabled ? "true" : "false"
            })
            .Build();

        return new DispatchMessageIntegrationEventHandler(
            _email,
            messaging ?? _messaging,
            _billing,
            _creditCost,
            _suppression,
            _comms,
            _db,
            _mediator,
            config,
            NullLogger<DispatchMessageIntegrationEventHandler>.Instance);
    }
```

The handler under test always receives the **same** `_db` instance that `SetUp` constructed with empty ambient. There is no second context with a different tenant.

### 2.3 Each test invents its own `orgId`

Every assigned test starts with:

```csharp
var orgId = Guid.CreateVersion7();
```

That guid is:

1. passed into `DispatchMessageIntegrationEvent.OrganizationId`,
2. used to stub `_comms.GetEmailConfigCredentialsAsync(orgId)` / `_suppression.IsSuppressedAsync(orgId, …)` / `_billing.HasSufficientCreditsAsync(orgId, …)`,
3. written onto `MessageDeliveryLog.OrganizationId` by the handler.

It is **never** copied onto `IExecutionContextAccessor.TenantId`.

So after `HandleAsync` the InMemory store contains a row whose `OrganizationId` is a random v7 guid, and the only compiled query filter is `OrganizationId == Guid.Empty`. Those two values never match.

### 2.4 What the fixture is *not*

- It does **not** use `FakeExecutionContextAccessor.EmptyTenant()` / `ForTenant(orgId)` from `Lazuar.TestSupport` (the newer helper). Behavior is the same as `EmptyTenant()`: `TenantId = Guid.Empty`.
- It does **not** seed `MessageDeliveryLogs` itself. The handler is the only writer.
- It does **not** call `IgnoreQueryFilters()` anywhere. That is the entire defect.

### 2.5 Why empty ambient is the right fixture, not a mistake

`DispatchMessageIntegrationEventHandler` is registered as an inbox consumer:

```57:74:apps/lazuar-api/Modules/Messaging/Infrastructure/DependencyInjection.cs
        services.AddTransient<DispatchMessageIntegrationEventHandler>();
        services.AddTransient<ClientProfileAnonymizedIntegrationEventHandler>();
        // ...
        eventBus.Subscribe<DispatchMessageIntegrationEvent, DispatchMessageIntegrationEventHandler>();
        eventBus.Subscribe<ClientProfileAnonymizedIntegrationEvent, ClientProfileAnonymizedIntegrationEventHandler>();
```

`InboxConsumerJob` creates a DI scope and publishes the inbox notification. It never stamps `HttpContext.Items["TenantId"]`. Production `ExecutionContextAccessor` therefore returns `Guid.Empty` on that path:

```16:26:apps/lazuar-api/src/Lazuar.Api/ExecutionContextAccessor.cs
    public Guid TenantId
    {
        get
        {
            if (_httpContextAccessor.HttpContext?.Items.TryGetValue("TenantId", out var tenantIdObj) == true && tenantIdObj is Guid tenantId)
            {
                return tenantId;
            }
            return Guid.Empty;
        }
    }
```

`FakeExecutionContextAccessor` documents the same convention for tests:

```18:22:apps/lazuar-api/tests/Lazuar.TestSupport/FakeExecutionContextAccessor.cs
    /// <summary>
    /// Empty tenant — matches most InMemory DbContext fixtures that seed with explicit OrganizationId
    /// and rely on fail-closed query filters (empty ambient tenant matches no rows until IgnoreQueryFilters).
    /// </summary>
    public static FakeExecutionContextAccessor EmptyTenant() => new();
```

So: empty ambient in this fixture is **modeling production**. The tests should keep it and **opt out of the filter when they inspect the write**, the same way sibling worker tests already do (Communications `AppEntitlementGrantedIntegrationEventHandlerTests`, `SuppressionLaneTests`, `ClientProfileAnonymizedSuppressionTests`).

---

## 3. `IMustHaveTenant` + `PlatformDbContext` filter (quotes)

### 3.1 The marker

```1:6:apps/lazuar-api/BuildingBlocks/Domain/IMustHaveTenant.cs
namespace BuildingBlocks.Domain;

public interface IMustHaveTenant
{
    Guid OrganizationId { get; set; }
}
```

The interface requires a **public setter**. That is why 179 also changed `MessageDeliveryLog.OrganizationId` from `{ get; private set; }` to `{ get; set; }` — the stamp in `SaveChangesAsync` must be able to write it.

### 3.2 The entity after 179

```10:21:apps/lazuar-api/Modules/Messaging/Domain/MessageDeliveryLog.cs
public class MessageDeliveryLog : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public string Channel { get; private set; } = "";
    public string Recipient { get; private set; } = "";
    public string Status { get; private set; } = "";
    public string? ProviderMessageId { get; private set; }
    public string? Error { get; private set; }
    public Guid? CorrelationEventId { get; private set; }
    public DateTime CreatedAt { get; private set; }
```

Constructor still **requires** an `organizationId` and assigns it immediately. Reads of `OrganizationId` after construct are never empty for the assigned tests (`orgId = Guid.CreateVersion7()`).

```26:44:apps/lazuar-api/Modules/Messaging/Domain/MessageDeliveryLog.cs
    public MessageDeliveryLog(
        Guid organizationId,
        string channel,
        string recipient,
        string status,
        string? providerMessageId = null,
        string? error = null,
        Guid? correlationEventId = null)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        Channel = channel;
        Recipient = recipient ?? "";
        Status = status;
        ProviderMessageId = providerMessageId;
        Error = error;
        CorrelationEventId = correlationEventId;
        CreatedAt = DateTime.UtcNow;
    }
```

**Before 179** (`8237e1c6^`):

```csharp
public class MessageDeliveryLog : Entity, IAggregateRoot
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
```

No `IMustHaveTenant` ⇒ `PlatformDbContext.OnModelCreating` did **not** attach a query filter ⇒ `_db.MessageDeliveryLogs.SingleAsync()` saw every row, including those written under empty ambient. That is the leak 179 closed, and the reason these tests used to pass.

### 3.3 How the filter is attached (every `PlatformDbContext`, including `MessagingDbContext`)

`MessagingDbContext` inherits `PlatformDbContext` and calls `base.OnModelCreating` first:

```8:27:apps/lazuar-api/Modules/Messaging/Infrastructure/MessagingDbContext.cs
public class MessagingDbContext : PlatformDbContext
{
    public DbSet<TenantReplica> TenantReplicas { get; set; } = null!;
    public DbSet<MessageDeliveryLog> MessageDeliveryLogs { get; set; } = null!;
    // ...
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
```

`PlatformDbContext.OnModelCreating` walks **every** entity on the model. If the CLR type implements `IMustHaveTenant`, it invokes the generic filter configurator:

```25:46:apps/lazuar-api/BuildingBlocks/Infrastructure/PlatformDbContext.cs
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IMustHaveTenant).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(PlatformDbContext)
                    .GetMethod(nameof(ConfigureGlobalFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.MakeGenericMethod(entityType.ClrType);
                method?.Invoke(this, new object[] { modelBuilder });
            }
        }
    }

    private void ConfigureGlobalFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, IMustHaveTenant
    {
        // Fail-closed: empty ambient TenantId matches no rows (workers must IgnoreQueryFilters + explicit org).
        modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
            e.OrganizationId == ExecutionContext.TenantId);
    }
```

The comment on lines 43–44 is the whole story for this analysis:

> Fail-closed: empty ambient TenantId matches no rows (workers must IgnoreQueryFilters + explicit org).

There is **no** special case for `Guid.Empty`. The predicate is a straight equality. `orgId == Guid.Empty` is false for every assigned test. The filtered `DbSet` is empty.

This is a **deliberate fail-closed** change from an older fail-open design (docs in `docs/001-gaps/14-tenant-isolation.md` still describe the old “filter bypass when TenantId empty” world). Current code does **not** bypass. Empty ambient seeing all tenants’ emails **is the bug 179 fixed**.

### 3.4 Write path is *not* filtered — only reads are

`SaveChangesAsync` on `PlatformDbContext`:

1. **Stamp** (lines 51–59): if an added `IMustHaveTenant` has empty `OrganizationId` **and** ambient `TenantId` is non-empty, copy ambient onto the entity. In these tests both sides of that `&&` are: entity already has `orgId` (skip), ambient is empty (skip). Stamp is a no-op.
2. **Write guard** (lines 62–76): refuse to persist `IMustHaveTenant` with empty `OrganizationId` after the stamp. Assigned tests pass a real v7 guid, so the guard does not fire. The row is saved.
3. Domain-event dispatch, then `base.SaveChangesAsync`, then `JobTrigger`.

```49:76:apps/lazuar-api/BuildingBlocks/Infrastructure/PlatformDbContext.cs
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // 1. Process multi-tenant assignments
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added && entry.Entity is IMustHaveTenant tenantEntity)
            {
                if (tenantEntity.OrganizationId == Guid.Empty && ExecutionContext.TenantId != Guid.Empty)
                {
                    tenantEntity.OrganizationId = ExecutionContext.TenantId;
                }
            }
        }

        // 1b. Write guard: never persist IMustHaveTenant without an organization after stamp.
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            if (entry.Entity is IMustHaveTenant tenantEntity && tenantEntity.OrganizationId == Guid.Empty)
            {
                throw new InvalidOperationException(
                    $"Cannot save {entry.Entity.GetType().Name} with empty OrganizationId. " +
                    "Set OrganizationId explicitly or ensure ambient TenantId is present for stamp.");
            }
        }
```

Query filters do **not** apply to `Add` / `SaveChanges`. The row is in the InMemory store. It is even still **tracked** on `_db`. `SingleAsync()` still applies the filter and returns nothing — EF does not satisfy `SingleAsync` from the tracker when the filter excludes the entity. (`Find(id)` would be a different API; these tests do not use it.)

### 3.5 InMemory proof that empty ambient hides rows

Same pattern, different module, already locked as a module test:

```45:73:apps/lazuar-api/tests/Lazuar.ModuleTests/TenantIsolation/TenantIsolationHardeningTests.cs
    public async Task Empty_Tenant_EF_Filter_Returns_Zero_Rows()
    {
        var orgA = Guid.CreateVersion7();
        var orgB = Guid.CreateVersion7();
        // ... CommerceDbContext + executionContext.TenantId.Returns(Guid.Empty) ...
        db.Products.Add(CreateProduct(orgA, "A", "a"));
        db.Products.Add(CreateProduct(orgB, "B", "b"));
        await db.SaveChangesAsync();

        // Fail-closed: empty ambient tenant matches no OrganizationId rows.
        var visible = await db.Products.ToListAsync();
        visible.Should().BeEmpty();

        var viaIgnore = await db.Products.IgnoreQueryFilters().ToListAsync();
        viaIgnore.Should().HaveCount(2);
    }
```

Substitute that `Products` for `MessageDeliveryLogs` and you have exactly what the eight assigned tests are doing — except they call `SingleAsync()` on the empty filtered set instead of asserting emptiness / ignoring the filter.

### 3.6 Architecture lock so 179 cannot silently regress

```128:138:apps/lazuar-api/tests/Lazuar.ArchitectureTests/TenantIsolationArchitectureTests.cs
    [Test]
    public void MessageDeliveryLog_Is_IMustHaveTenant_PaymentWebhookLog_Is_Allowlisted()
    {
        Assert.That(typeof(Modules.Messaging.Domain.MessageDeliveryLog)
            .GetInterfaces()
            .Any(i => i == typeof(BuildingBlocks.Domain.IMustHaveTenant)), Is.True);
        Assert.That(typeof(Modules.Payments.Domain.Entities.PaymentWebhookLog)
            .GetInterfaces()
            .Any(i => i == typeof(BuildingBlocks.Domain.IMustHaveTenant)), Is.False,
            "PaymentWebhookLog stays global for provider EventId idempotency.");
    }
```

Reverting 179 to make these eight tests green would fail this architecture test and re-open the P1 leak. Do not do that.

### 3.7 Issue 179 — why the filter exists

Audit text (still in the issue file even after `status: resolved`; the body was not rewritten):

> `MessageDeliveryLog` has `OrganizationId` and is **not** `IMustHaveTenant`. `GET /messaging/delivery-logs` filters by `ctx.TenantId` in LINQ. Any other `DbSet<MessageDeliveryLog>` query with empty ambient sees **all tenants’** recipient addresses.

Source: `issues/179-p1-b10-x23-child-log-tables-with-organizationid-or-session-id-and-no-tenant.md` lines 29–31 (same paragraph at `plans/009-bugs/10-tenancy-workers-contracts-tests.md` lines 792–794).

That is PII: `Recipient` is a live email or phone (`MessageDeliveryLog.cs` line 15). A worker, ops dump, or careless `ToListAsync()` under empty ambient used to return every tenant’s inbox addresses. After 179, that query is empty unless the caller:

- has ambient `TenantId` equal to the row’s `OrganizationId` (HTTP OrgAdmin path), **or**
- calls `IgnoreQueryFilters()` **and** predicates on `OrganizationId` explicitly (worker / anonymize path).

Empty ambient seeing all tenants’ emails is the bug. The tests must adapt to the filter, not the other way around.

---

## 4. How the handler writes logs (`OrganizationId` from the event)

The handler **never reads** `MessageDeliveryLogs`. It only `Add`s. It does **not** use ambient `TenantId` at all. Organization always comes from `DispatchMessageIntegrationEvent.OrganizationId`.

### 4.1 The event

```10:24:apps/lazuar-api/Modules/Messaging/Contracts/DispatchMessageIntegrationEvent.cs
public record DispatchMessageIntegrationEvent(
    Guid OrganizationId,
    string ToEmail,
    string? ToPhone,
    string Subject,
    string? HtmlEmailBody,
    string? PlainTextPhoneBody,
    string Channel = "EMAIL", // EMAIL, WHATSAPP, or ALL
    Guid? CreditHoldId = null,
    string? UnsubscribeUrl = null
) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
```

`Id` is the correlation id stored on the log (`CorrelationEventId`). Tests do not assert it.

### 4.2 Persist helper

```188:213:apps/lazuar-api/Modules/Messaging/Infrastructure/EventHandlers/DispatchMessageIntegrationEventHandler.cs
    private async Task LogDeliveryAsync(
        Guid organizationId,
        string channel,
        string recipient,
        string status,
        string? providerMessageId,
        string? error,
        Guid correlationEventId)
    {
        try
        {
            _dbContext.MessageDeliveryLogs.Add(new MessageDeliveryLog(
                organizationId,
                channel,
                recipient,
                status,
                providerMessageId,
                error,
                correlationEventId));
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist MessageDeliveryLog for {Channel}/{Recipient}/{Status}", channel, recipient, status);
        }
    }
```

Implications for these failures:

- `organizationId` is `@event.OrganizationId` at every call site. Assigned tests pass a non-empty v7 guid ⇒ write guard passes ⇒ `SaveChangesAsync` succeeds.
- Persist exceptions are **swallowed** (`LogWarning`). If a future test used `OrganizationId = Guid.Empty` (system tenant), the write guard would throw, the catch would swallow it, and even `IgnoreQueryFilters()` would see **zero rows**. That is **not** what is happening in the eight assigned tests. Do not “fix” these failures by changing `LogDeliveryAsync`.
- After a successful save the row is committed to the InMemory database of `_db`. A later `_db.MessageDeliveryLogs.IgnoreQueryFilters().SingleAsync()` would return it.

### 4.3 Every write site (all pass `@event.OrganizationId`)

| Handler lines | Status | Channel | When | Assigned tests that hit it |
|---------------|--------|---------|------|----------------------------|
| 69–75 | `SKIPPED` | `WHATSAPP` | `Messaging:WhatsAppEnabled=false` and the event wants WA | `WhatsAppDisabled_SkipsWhatsApp…`, `WhatsAppDisabled_CostTwo…` |
| 78–82 | `SKIPPED` | `EMAIL` | tenant address is suppressed (transactional lane) | `SuppressedAddress_SkipsEmail…` |
| 99–104 | `SKIPPED` | `WHATSAPP` | insufficient credits | none of the eight |
| 129–139 | `SENT` | `EMAIL` | `SendEmailAsync` returns a provider id | `EmailChannel_WrapsBrand…` |
| 141–144 | `FAILED` | `EMAIL` | `SendEmailAsync` throws; then rethrow | `TenantEmail_InactiveByok…`, `TenantEmail_NullByok…` |
| 152–154 | `SENT` | `WHATSAPP` | `SendMessageAsync` succeeds | `WhatsAppEnabled_ConsoleTransport…`, `WhatsAppEnabled_CostZero…` |
| 157–159 | `FAILED` | `WHATSAPP` | `SendMessageAsync` throws; then rethrow | none of the eight |

Call-site shape is always:

```csharp
await LogDeliveryAsync(@event.OrganizationId, "EMAIL"|"WHATSAPP", recipient, status, providerIdOrNull, errorOrNull, @event.Id);
```

Example — email success:

```127:145:apps/lazuar-api/Modules/Messaging/Infrastructure/EventHandlers/DispatchMessageIntegrationEventHandler.cs
            try
            {
                var htmlPayload = EmailTemplateBuilder.WrapWithBrandHtml(@event.HtmlEmailBody!, @event.UnsubscribeUrl);
                var providerId = await _emailService.SendEmailAsync(
                    @event.ToEmail,
                    @event.Subject,
                    htmlPayload,
                    @event.OrganizationId,
                    tenantApiKey,
                    tenantSenderEmail,
                    @event.UnsubscribeUrl);
                emailSent = true;
                await LogDeliveryAsync(@event.OrganizationId, "EMAIL", @event.ToEmail!, "SENT", providerId, null, @event.Id);
            }
            catch (Exception ex)
            {
                await LogDeliveryAsync(@event.OrganizationId, "EMAIL", @event.ToEmail!, "FAILED", null, ex.Message, @event.Id);
                throw;
            }
```

And the WA-disabled skip the two “disabled” tests hit first:

```69:76:apps/lazuar-api/Modules/Messaging/Infrastructure/EventHandlers/DispatchMessageIntegrationEventHandler.cs
        if (!whatsAppEnabled && wantsWhatsApp)
        {
            _logger.LogInformation(
                "WhatsApp channel disabled (Messaging:WhatsAppEnabled=false). Skipping WhatsApp for Organization {OrganizationId}, Channel {Channel}, Event {EventId}.",
                @event.OrganizationId, @event.Channel, @event.Id);
            await LogDeliveryAsync(@event.OrganizationId, "WHATSAPP", @event.ToPhone ?? "", "SKIPPED", null, "WhatsApp channel disabled", @event.Id);
            wantsWhatsApp = false;
        }
```

### 4.4 Why the *behavior* assertions still pass

NSubstitute / FluentAssertions checks that run **before** the log read do not touch `MessageDeliveryLogs`:

- `_email.Received` / `DidNotReceive`
- `_messaging.Received` / `DidNotReceive`
- `_mediator.DidNotReceive().Send(DeductTenantCreditCommand)`
- `_creditCost.Received` / `DidNotReceive`
- `act.Should().ThrowAsync<InvalidOperationException>()`

Those should be green. Failure is isolated to the log `SingleAsync`. That is important: this is not “email never sent” or “FAILED never written.” It is “written, then invisible.”

### 4.5 Related product note (not the cause of these eight failures)

`isSystemTenant` is true when `OrganizationId` is `Guid.Empty` or `00000000-0000-0000-0000-000000000001` (handler lines 57–58). A system-tenant dispatch that still calls `LogDeliveryAsync(Guid.Empty, …)` would now hit the write guard and be swallowed by the `LogWarning` catch. None of the assigned tests use a system org. Do not expand this ticket to change that persist path.

`TenantReplica` in the same DbContext is **not** `IMustHaveTenant` (it is keyed by `Id` = org id, no `OrganizationId` column). Irrelevant here.

---

## 5. Per-test: which assertion hits `SingleAsync` / `FirstAsync` without `IgnoreQueryFilters`

None of the eight tests use `FirstAsync`. All eight use `_db.MessageDeliveryLogs.SingleAsync()` with the global filter still on. There is no `Where(l => l.OrganizationId == orgId)` either — they rely on “exactly one row in this InMemory database,” which is true **unfiltered** and false **filtered**.

Exception from EF / LINQ:

```text
System.InvalidOperationException : Sequence contains no elements
```

at `EntityFrameworkQueryableExtensions.SingleAsync[TSource](IQueryable<TSource> source, CancellationToken cancellationToken)`.

Below: for each test, the org write, the expected row, the earlier assertions that should still pass, and the exact failing line.

### 5.1 `HandleAsync_EmailChannel_WrapsBrandAndSendsViaIEmailService` (lines 92–140)

- **orgId:** `Guid.CreateVersion7()` (line 94).
- **Event:** `Channel: "EMAIL"`, `ToEmail: "user@example.com"`, `HtmlEmailBody: "Line1\nLine2"`.
- **Stubs:** active BYOK (`tenant_key` / `from@tenant.test`); `_email.SendEmailAsync(…).Returns("re_abc")`.
- **Handler path:** `LogDeliveryAsync(orgId, "EMAIL", "user@example.com", "SENT", "re_abc", null, evt.Id)` at handler line 139.
- **Assertions that should pass:** `SendEmailAsync` received once with brand-wrapped HTML (`Line1<br/>Line2`, `"Powered by"`, `"Lazuar"`), org id, tenant key, tenant from; messaging not called; no `DeductTenantCreditCommand`; `GetCost(EmailSend)` not called (lines 120–134).
- **Failing line:**

```136:139:apps/lazuar-api/tests/Lazuar.ModuleTests/Messaging/DispatchMessageIntegrationEventHandlerTests.cs
        var log = await _db.MessageDeliveryLogs.SingleAsync();
        log.Status.Should().Be("SENT");
        log.Channel.Should().Be("EMAIL");
        log.ProviderMessageId.Should().Be("re_abc");
```

Row exists unfiltered: `OrganizationId=orgId`, `Status=SENT`, `Channel=EMAIL`, `ProviderMessageId=re_abc`. Filtered set is empty.

### 5.2 `HandleAsync_TenantEmail_InactiveByok_LogsFailedAndThrowsNoFallback` (lines 142–180)

- **orgId:** line 145.
- **Stubs:** credentials present but `IsActive: false`. `_email.SendEmailAsync` is configured to throw `InvalidOperationException("No platform fallback…")` only when the API key argument is null/whitespace (the handler leaves `tenantApiKey` null when inactive).
- **Handler path:** catch at lines 141–144 → `LogDeliveryAsync(orgId, "EMAIL", …, "FAILED", null, ex.Message, evt.Id)` **then rethrow**. Persist happens *before* the throw leaves `HandleAsync`.
- **Assertions that should pass:** `ThrowAsync<InvalidOperationException>().WithMessage("*No platform fallback*")` (lines 173–174).
- **Failing line:**

```176:179:apps/lazuar-api/tests/Lazuar.ModuleTests/Messaging/DispatchMessageIntegrationEventHandlerTests.cs
        var log = await _db.MessageDeliveryLogs.SingleAsync();
        log.Status.Should().Be("FAILED");
        log.Channel.Should().Be("EMAIL");
        log.Error.Should().Contain("No platform fallback");
```

The throw assertion is first. Typical NUnit output: the test fails on line 176, not on 173. The FAILED row is in the store.

### 5.3 `HandleAsync_TenantEmail_NullByok_LogsFailedAndThrowsNoFallback` (lines 182–214)

- **orgId:** line 185.
- **Stubs:** `GetEmailConfigCredentialsAsync` returns `(TenantEmailCredentials?)null`. Same throw-on-empty-key email stub as 5.2.
- **Handler path:** identical FAILED + rethrow.
- **Assertions that should pass:** `ThrowAsync` with `*No platform fallback*` (lines 210–211).
- **Failing line (inline):**

```213:213:apps/lazuar-api/tests/Lazuar.ModuleTests/Messaging/DispatchMessageIntegrationEventHandlerTests.cs
        (await _db.MessageDeliveryLogs.SingleAsync()).Status.Should().Be("FAILED");
```

Same empty filtered set. This is the most compact form of the bug — one expression, no `IgnoreQueryFilters`.

### 5.4 `HandleAsync_SuppressedAddress_SkipsEmailAndDoesNotSend` (lines 216–250)

- **orgId:** line 219.
- **Stubs:** `IsSuppressedAsync(orgId, "user@example.com", SuppressionLane.Transactional) → true`. Active BYOK is still stubbed but must not be used.
- **Handler path:** lines 78–82 → `LogDeliveryAsync(orgId, "EMAIL", "user@example.com", "SKIPPED", null, "Address suppressed", evt.Id)`; `wantsEmail = false`.
- **Assertions that should pass:** `SendEmailAsync` not received (lines 237–244).
- **Failing line:**

```246:249:apps/lazuar-api/tests/Lazuar.ModuleTests/Messaging/DispatchMessageIntegrationEventHandlerTests.cs
        var log = await _db.MessageDeliveryLogs.SingleAsync();
        log.Status.Should().Be("SKIPPED");
        log.Channel.Should().Be("EMAIL");
        log.Error.Should().Contain("suppressed");
```

Unfiltered row: `SKIPPED` / `EMAIL` / `Error = "Address suppressed"`.

### 5.5 `HandleAsync_WhatsAppDisabled_SkipsWhatsAppAndDoesNotCallIMessagingService` (lines 252–273)

- **orgId:** line 255.
- **Config:** default `_sut` ⇒ `Messaging:WhatsAppEnabled=false`.
- **Event:** `Channel: "WHATSAPP"`, `ToPhone: "+6012"`, `PlainTextPhoneBody: "hi"`.
- **Handler path:** lines 69–75 → `LogDeliveryAsync(orgId, "WHATSAPP", "+6012", "SKIPPED", null, "WhatsApp channel disabled", evt.Id)`; `wantsWhatsApp = false`. Cost / deduct never reached.
- **Assertions that should pass:** messaging not called; no `DeductTenantCreditCommand` (lines 267–268).
- **Failing line:**

```269:272:apps/lazuar-api/tests/Lazuar.ModuleTests/Messaging/DispatchMessageIntegrationEventHandlerTests.cs
        var log = await _db.MessageDeliveryLogs.SingleAsync();
        log.Status.Should().Be("SKIPPED");
        log.Channel.Should().Be("WHATSAPP");
        log.Error.Should().Contain("WhatsApp channel disabled");
```

### 5.6 `HandleAsync_WhatsAppEnabled_ConsoleTransport_DoesNotDeduct` (lines 275–308)

- **orgId:** line 278.
- **Config:** `CreateSut(messaging: console, whatsAppEnabled: true)` with a real `ConsoleMessagingService`. `IsBillable` is `false` (`ConsoleMessagingService.cs` line 18), so handler lines 87–88 force `whatsappCost = 0` even though `_creditCost.GetCost(WhatsAppSend)` returns 2.
- **Handler path:** WA send via console (no-op log to ILogger) → `LogDeliveryAsync(orgId, "WHATSAPP", "+6012", "SENT", null, null, evt.Id)` at handler line 154. `actualCost` stays 0 ⇒ no deduct.
- **Assertions that should pass:** type / `IsBillable`; `GetCost` received; **no** `DeductTenantCreditCommand` (including amount 2 or 0); `HasSufficientCreditsAsync` not called (lines 296–303).
- **Failing line:**

```305:307:apps/lazuar-api/tests/Lazuar.ModuleTests/Messaging/DispatchMessageIntegrationEventHandlerTests.cs
        var log = await _db.MessageDeliveryLogs.SingleAsync();
        log.Status.Should().Be("SENT");
        log.Channel.Should().Be("WHATSAPP");
```

### 5.7 `HandleAsync_WhatsAppEnabled_CostZero_SubstituteTransport_DoesNotDeduct` (lines 310–335)

- **orgId:** line 313.
- **Config:** `CreateSut(whatsAppEnabled: true)` using the NSubstitute `_messaging`. Cost already 0 from `SetUp` (and re-set on line 314).
- **Handler path:** `SendMessageAsync("+6012", "hi")` then `SENT` log at handler line 154.
- **Assertions that should pass:** messaging received once; no deduct; no credit-balance check (lines 328–330).
- **Failing line:**

```332:334:apps/lazuar-api/tests/Lazuar.ModuleTests/Messaging/DispatchMessageIntegrationEventHandlerTests.cs
        var log = await _db.MessageDeliveryLogs.SingleAsync();
        log.Status.Should().Be("SENT");
        log.Channel.Should().Be("WHATSAPP");
```

### 5.8 `HandleAsync_WhatsAppDisabled_CostTwo_DoesNotDeduct` (lines 337–361)

- **orgId:** line 340.
- **Config:** `CreateSut(whatsAppEnabled: false)` with `_creditCost.GetCost(WhatsAppSend) → 2`.
- **Handler path:** same skip as 5.5 (`SKIPPED` / `"WhatsApp channel disabled"`). Cost 2 is never consulted for deduct because `wantsWhatsApp` is cleared first. (`GetCost` is still invoked later in the handler at line 85, but deduct is gated on `actualCost > 0`.)
- **Assertions that should pass:** messaging not called; no deduct (lines 355–356).
- **Failing line:**

```358:360:apps/lazuar-api/tests/Lazuar.ModuleTests/Messaging/DispatchMessageIntegrationEventHandlerTests.cs
        var log = await _db.MessageDeliveryLogs.SingleAsync();
        log.Status.Should().Be("SKIPPED");
        log.Error.Should().Contain("WhatsApp channel disabled");
```

### 5.9 Summary table (failing queries only)

| Test | File line | Query | Filter hides row because |
|------|-----------|-------|--------------------------|
| EmailChannel_WrapsBrand… | 136 | `SingleAsync()` | `OrganizationId == orgId` ≠ `TenantId == Empty` |
| TenantEmail_InactiveByok… | 176 | `SingleAsync()` | same |
| TenantEmail_NullByok… | 213 | `SingleAsync()` | same |
| SuppressedAddress… | 246 | `SingleAsync()` | same |
| WhatsAppDisabled_Skips… | 269 | `SingleAsync()` | same |
| WhatsAppEnabled_Console… | 305 | `SingleAsync()` | same |
| WhatsAppEnabled_CostZero… | 332 | `SingleAsync()` | same |
| WhatsAppDisabled_CostTwo… | 358 | `SingleAsync()` | same |

No `FirstAsync` anywhere in this fixture. No other test in this class.

---

## 6. Recommended fix

**Tests should query `.IgnoreQueryFilters()` OR set ambient `TenantId` to the org used when writing the log.**

**Prefer `IgnoreQueryFilters` in this fixture.** Empty ambient is intentional (worker / inbox). Do **not** revert 179. Empty ambient seeing all tenants’ emails is the bug.

### 6.1 Preferred: keep empty ambient, ignore the filter on the *test* read

Matches:

- the comment on `PlatformDbContext.ConfigureGlobalFilter` (“workers must IgnoreQueryFilters + explicit org”),
- `FakeExecutionContextAccessor.EmptyTenant()` docs,
- sibling worker tests (`AppEntitlementGrantedIntegrationEventHandlerTests` lines 37 / 82 / 111; `SuppressionLaneTests` lines 45 / 59; `ClientProfileAnonymizedSuppressionTests` lines 32 / 52 / 67; and **this module’s own** `ClientProfileAnonymizedDeliveryLogTests` line 42).

Do **not** change:

- `MessageDeliveryLog` (keep `IMustHaveTenant`),
- `PlatformDbContext` filter / write guard,
- the handler’s `LogDeliveryAsync` (it already stamps org from the event),
- production `GET /messaging/delivery-logs` (HTTP has ambient tenant; see §8).

### 6.2 Alternative that also works, but is worse here

Per-test:

```csharp
executionContext.TenantId.Returns(orgId);
```

Problems:

1. The accessor is a **local** in `SetUp`. You would have to promote it to a field and reconfigure NSubstitute after each `orgId` is created.
2. Each test generates a **new** `orgId`. You cannot put a single `Returns(sharedOrg)` in `SetUp`.
3. It **stops modeling production**. Inbox workers do not have ambient tenant. The point of 179 is that those workers must not see other tenants’ rows by accident; tests that pretend ambient is the event org hide that contract.
4. EF query filters that close over `ExecutionContext.TenantId` are evaluated at query time on this codebase (hardening test proves it), so flipping `Returns` *would* work — it is just the wrong story.

A hybrid (`ForTenant(orgId)` + `IgnoreQueryFilters`) is what `ClientProfileAnonymizedSuppressionTests` does. Fine, but unnecessary once you ignore filters.

### 6.3 What not to do

| Anti-fix | Why not |
|----------|---------|
| Remove `IMustHaveTenant` from `MessageDeliveryLog` | Re-opens 179. Architecture test fails. Empty ambient leaks recipient PII again. |
| Special-case `Guid.Empty` in the global filter (`TenantId == Empty \|\| OrganizationId == TenantId`) | Restores fail-**open**. That *is* the bug. |
| Set ambient `TenantId = Guid.Empty` and also write logs with `OrganizationId = Guid.Empty` | Write guard throws; handler swallows; no row. Also lies about org. |
| Use `ChangeTracker.Entries<MessageDeliveryLog>()` in tests | Bypasses EF the wrong way; does not document the worker contract. |
| Second `MessagingDbContext` with `ForTenant(orgId)` just to read | Works, noisy, two models / two filters. Helper on the existing `_db` is enough. |
| Add `Where(l => l.OrganizationId == orgId)` **without** `IgnoreQueryFilters` | Still empty: the filter runs first. You need **both** if you want an explicit org predicate, or ignore-filters alone in a single-row InMemory db. |

Recommended assertion shape once the helper exists:

```csharp
var log = await Logs().SingleAsync();
```

Optionally also assert `log.OrganizationId.Should().Be(orgId)` — not required to go green, but it documents that the handler wrote the event org (the thing 179 now hides).

---

## 7. Concrete patch: helper `Logs()` ⇒ `_db.MessageDeliveryLogs.IgnoreQueryFilters()`

**Only file to edit for this failure:**
`apps/lazuar-api/tests/Lazuar.ModuleTests/Messaging/DispatchMessageIntegrationEventHandlerTests.cs`

The test file does not currently import `Modules.Messaging.Domain`. Add that usings line (or fully-qualify the helper’s return type).

### 7.1 Add the helper next to `CreateSut` / `TearDown`

```csharp
private IQueryable<MessageDeliveryLog> Logs() =>
    _db.MessageDeliveryLogs.IgnoreQueryFilters();
```

`Microsoft.EntityFrameworkCore` is already imported (line 5), so `IgnoreQueryFilters` resolves.

### 7.2 Replace every unfiltered `SingleAsync`

Eight identical substitutions:

| Line today | After |
|------------|--------|
| `var log = await _db.MessageDeliveryLogs.SingleAsync();` (136, 176, 246, 269, 305, 332, 358) | `var log = await Logs().SingleAsync();` |
| `(await _db.MessageDeliveryLogs.SingleAsync()).Status.Should().Be("FAILED");` (213) | `(await Logs().SingleAsync()).Status.Should().Be("FAILED");` |

No other lines in the fixture query the set. `Add` / `SaveChanges` stay as they are (they live in the handler).

### 7.3 Do not change `SetUp`

Leave `executionContext.TenantId.Returns(Guid.Empty)`. That is the worker contract. Optional cleanup (out of scope unless someone is already touching the fixture): replace the NSubstitute accessor with `FakeExecutionContextAccessor.EmptyTenant()` so the fixture matches Communications tests. Behavior is identical.

### 7.4 Suggested full helper + usings (for the implementer)

```csharp
using Modules.Messaging.Domain;
// ...
private IQueryable<MessageDeliveryLog> Logs() =>
    _db.MessageDeliveryLogs.IgnoreQueryFilters();
```

If a later test writes two channels (`Channel = "ALL"`), `SingleAsync` will be the wrong cardinality even after ignore-filters. None of the eight assigned tests do that. Do not “upgrade” to `FirstAsync` now.

### 7.5 Analog already in-tree (copy this habit, not these types)

Communications worker test, empty ambient, ignore-filters on the read:

```77:84:apps/lazuar-api/tests/Lazuar.ModuleTests/Communications/SuppressionLaneTests.cs
    private static CommunicationsDbContext CreateDb()
    {
        return new CommunicationsDbContext(
            InMemoryDb.CreateOptions<CommunicationsDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());
    }
```

and `await db.SuppressionEntries.IgnoreQueryFilters().SingleAsync()` at lines 45 and 59.

This module’s anonymize test (see §8) already inlines the same ignore-filters call on `MessageDeliveryLogs`.

---

## 8. Any other files that query `MessageDeliveryLogs` unsafely

Repo-wide `MessageDeliveryLogs` usages in `*.cs` (excluding migrations / snapshot / designer):

### 8.1 Tests

| File | Query | Safe? |
|------|-------|-------|
| `…/Messaging/DispatchMessageIntegrationEventHandlerTests.cs` | 8× `SingleAsync()` **without** ignore-filters | **UNSAFE — these eight failures** |
| `…/Messaging/ClientProfileAnonymizedDeliveryLogTests.cs` | `IgnoreQueryFilters().OrderBy(…).ToListAsync()` (line 42) | **Safe. Already ignore-filters.** |
| `…/Messaging/MessageDeliveryLogTests.cs` | **No DbContext.** Pure constructor / `Anonymize` unit tests. | **N/A. No query filter involved.** |

#### `ClientProfileAnonymizedDeliveryLogTests` — already `IgnoreQueryFilters`

```19:46:apps/lazuar-api/tests/Lazuar.ModuleTests/Messaging/ClientProfileAnonymizedDeliveryLogTests.cs
    [Test]
    public async Task HandleAsync_Scrubs_Matching_Recipient()
    {
        var orgId = Guid.CreateVersion7();
        var profileId = Guid.CreateVersion7();
        var options = new DbContextOptionsBuilder<MessagingDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;
        await using var db = new MessagingDbContext(
            options,
            Substitute.For<IExecutionContextAccessor>(),
            Substitute.For<IMediator>(),
            new DatabaseJobTrigger());

        db.MessageDeliveryLogs.Add(new MessageDeliveryLog(orgId, "EMAIL", "buyer@example.com", "SENT", "re_1"));
        db.MessageDeliveryLogs.Add(new MessageDeliveryLog(orgId, "EMAIL", "other@example.com", "SENT", "re_2"));
        await db.SaveChangesAsync();

        var handler = new ClientProfileAnonymizedIntegrationEventHandler(db);
        await handler.HandleAsync(new ClientProfileAnonymizedIntegrationEvent(
            orgId, profileId, "Buyer@Example.com", null));

        var rows = await db.MessageDeliveryLogs.IgnoreQueryFilters().OrderBy(l => l.ProviderMessageId).ToListAsync();
        rows[0].Recipient.Should().Be($"deleted_{profileId}@localhost");
        rows[0].ProviderMessageId.Should().Be("re_1");
        rows[1].Recipient.Should().Be("other@example.com");
    }
```

Notes:

- Ambient is also empty: `Substitute.For<IExecutionContextAccessor>()` with no `Returns` ⇒ `TenantId == Guid.Empty`. Same worker fixture as dispatch tests.
- Line 42 already has `.IgnoreQueryFilters()`. This test stays green after 179.
- Introduced in `3b934a95` (`fix(messaging): scrub delivery-log inboxes on anonymize…`, 2026-08-18 08:51), **before** 179 (`8237e1c6`, 10:06 the same day). `IgnoreQueryFilters` was therefore in place *before* the filter existed (a no-op then, required now). The anonymize **handler** also used ignore-filters from day one, so the test matched production.
- Writes via `db.MessageDeliveryLogs.Add(...)` + `SaveChangesAsync` succeed for the same reason as the dispatch handler: filter is read-only; `OrganizationId` is set in the constructor.

**Verdict:** `ClientProfileAnonymizedDeliveryLogTests` already did the right thing. Do not touch it for this ticket.

#### `MessageDeliveryLogTests` — no query, no filter

```9:62:apps/lazuar-api/tests/Lazuar.ModuleTests/Messaging/MessageDeliveryLogTests.cs
public class MessageDeliveryLogTests
{
    [Test]
    public void Constructor_SetsSentFields() { /* new MessageDeliveryLog(...) ; assert fields */ }

    [Test]
    public void Constructor_SetsFailedAndSkipped() { /* no DbContext */ }

    [Test]
    public void Anonymize_Replaces_Recipient_Keeps_Provider_Id() { /* log.Anonymize(profileId) */ }
}
```

No `MessagingDbContext`, no `SingleAsync`, no `IgnoreQueryFilters` needed. Adding `IMustHaveTenant` / making `OrganizationId` settable does not change these tests.

**Verdict:** already fine. Do not touch.

`MessagingEndpointsAuthorizationTests` only inspects endpoint metadata for `POST /messaging/notify`. It never opens a DbContext.

No other test project queries `MessageDeliveryLogs`.

### 8.2 Production

| File | What it does | Safe? |
|------|----------------|-------|
| `Modules/Messaging/Infrastructure/EventHandlers/DispatchMessageIntegrationEventHandler.cs` | `Add` + `SaveChangesAsync` only (line 199). No query. | Safe. Writes are unfiltered. Org comes from the event. |
| `Modules/Messaging/Infrastructure/EventHandlers/ClientProfileAnonymizedIntegrationEventHandler.cs` | Worker read: `IgnoreQueryFilters()` **and** `OrganizationId == @event.OrganizationId` (lines 36–47). | **Safe. Canonical worker pattern.** |
| `Modules/Messaging/Infrastructure/Endpoints.cs` | HTTP `GET /messaging/delivery-logs`: filter left on + `Where(l => l.OrganizationId == ctx.TenantId)` (lines 36–38). | **Safe for HTTP.** Fail-closed if ambient is empty (returns `[]`, no PII leak). |

Anonymize handler (the pattern dispatch **tests** should copy):

```36:47:apps/lazuar-api/Modules/Messaging/Infrastructure/EventHandlers/ClientProfileAnonymizedIntegrationEventHandler.cs
        var rows = await _dbContext.MessageDeliveryLogs
            .IgnoreQueryFilters()
            .Where(l => l.OrganizationId == @event.OrganizationId && l.Recipient == email)
            .ToListAsync();

        if (rows.Count == 0)
        {
            var lowered = email.ToLowerInvariant();
            rows = await _dbContext.MessageDeliveryLogs
                .IgnoreQueryFilters()
                .Where(l => l.OrganizationId == @event.OrganizationId && l.Recipient.ToLower() == lowered)
                .ToListAsync();
        }
```

Support GET (defense in depth; do **not** add `IgnoreQueryFilters` here):

```29:53:apps/lazuar-api/Modules/Messaging/Infrastructure/Endpoints.cs
        // Support: list recent delivery attempts for the current tenant.
        group.MapGet("/delivery-logs", async Task<Ok<IReadOnlyList<MessageDeliveryLogDto>>> (
            [FromServices] IExecutionContextAccessor ctx,
            [FromServices] MessagingDbContext db,
            [FromQuery] int? limit) =>
        {
            var take = Math.Clamp(limit ?? 50, 1, 200);
            var rows = await db.MessageDeliveryLogs
                .AsNoTracking()
                .Where(l => l.OrganizationId == ctx.TenantId)
                .OrderByDescending(l => l.CreatedAt)
                .Take(take)
                // ...
```

After 179 the explicit `Where` is redundant **when** ambient is the calling org (the global filter already restricts to `ctx.TenantId`). Keep it. If someone later slaps `IgnoreQueryFilters()` on this endpoint and drops the `Where`, that is a new leak. Not this ticket.

There is no `IMessageDeliveryLogRepository`. No other module references the `DbSet`.

### 8.3 Unsafe-query verdict

The **only** remaining unsafe `MessageDeliveryLogs` queries in the tree are the eight `SingleAsync()` calls in `DispatchMessageIntegrationEventHandlerTests`. Production worker code that *reads* the table already ignores filters and predicates on org. Production HTTP code relies on ambient tenant (plus an explicit `Where`). Domain unit tests never query.

---

## 9. Files to change later

Implement the test-only patch. Do not implement a product change for these eight failures.

### 9.1 Change (this ticket)

| Path | What |
|------|------|
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Messaging/DispatchMessageIntegrationEventHandlerTests.cs` | Add `using Modules.Messaging.Domain;`. Add `Logs()` helper (`_db.MessageDeliveryLogs.IgnoreQueryFilters()`). Replace eight `_db.MessageDeliveryLogs.SingleAsync()` with `Logs().SingleAsync()`. Leave `SetUp` ambient `Guid.Empty`. |

### 9.2 Do not change (for this failure)

| Path | Why |
|------|-----|
| `apps/lazuar-api/Modules/Messaging/Domain/MessageDeliveryLog.cs` | 179 is correct. Keep `IMustHaveTenant` and the public `OrganizationId` setter. |
| `apps/lazuar-api/BuildingBlocks/Infrastructure/PlatformDbContext.cs` | Fail-closed filter stays. |
| `apps/lazuar-api/Modules/Messaging/Infrastructure/EventHandlers/DispatchMessageIntegrationEventHandler.cs` | Writes org from the event. Persist-swallow is unrelated. |
| `apps/lazuar-api/Modules/Messaging/Infrastructure/EventHandlers/ClientProfileAnonymizedIntegrationEventHandler.cs` | Already ignore-filters + explicit org. |
| `apps/lazuar-api/Modules/Messaging/Infrastructure/Endpoints.cs` | HTTP + ambient + explicit `Where`. Do not ignore filters. |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Messaging/ClientProfileAnonymizedDeliveryLogTests.cs` | Already `IgnoreQueryFilters()` at line 42. |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Messaging/MessageDeliveryLogTests.cs` | No DbContext. |
| `apps/lazuar-api/tests/Lazuar.ArchitectureTests/TenantIsolationArchitectureTests.cs` | Keep the 179 lock. |
| `issues/179-…md` | Already `resolved`. Body text still says “is **not** `IMustHaveTenant`” (stale audit paste). Optional docs cleanup, not a test fix. |

### 9.3 Optional follow-ups (not required to go green)

1. **Assert `log.OrganizationId == orgId`** on the eight tests after `Logs().SingleAsync()`, so a future handler that writes `Guid.Empty` (and then gets swallowed by the write guard) fails loudly.
2. **Swap NSubstitute accessor for `FakeExecutionContextAccessor.EmptyTenant()`** so the fixture matches Communications worker tests and the TestSupport comment.
3. **System-tenant persist:** `LogDeliveryAsync(Guid.Empty, …)` now trips the write guard and is swallowed. If system / platform mails should still leave a support row, that is a separate product ticket (stamp a reserved system org, or exempt the log). Not these eight tests.
4. **`GET /messaging/delivery-logs` module test** that proves empty ambient returns `[]` and org-A ambient cannot see org-B rows. Would lock 179 at the HTTP boundary. Does not exist today (`MessagingEndpointsAuthorizationTests` only covers `POST /notify`).
5. Refresh the 179 issue body so it no longer claims the entity “is **not** `IMustHaveTenant`.”

### 9.4 How to confirm after the patch

From `apps/lazuar-api`:

```text
dotnet test tests/Lazuar.ModuleTests/Lazuar.ModuleTests.csproj --filter FullyQualifiedName~DispatchMessageIntegrationEventHandlerTests
```

Expected: eight assigned tests pass. Also keep green:

```text
dotnet test tests/Lazuar.ModuleTests/Lazuar.ModuleTests.csproj --filter FullyQualifiedName~ClientProfileAnonymizedDeliveryLogTests
dotnet test tests/Lazuar.ModuleTests/Lazuar.ModuleTests.csproj --filter FullyQualifiedName~MessageDeliveryLogTests
dotnet test tests/Lazuar.ArchitectureTests/Lazuar.ArchitectureTests.csproj --filter FullyQualifiedName~MessageDeliveryLog_Is_IMustHaveTenant
dotnet test tests/Lazuar.ModuleTests/Lazuar.ModuleTests.csproj --filter FullyQualifiedName~Empty_Tenant_EF_Filter_Returns_Zero_Rows
```

### 9.5 One-paragraph cause (for the tracker)

Issue 179 (`8237e1c6`) made `MessageDeliveryLog : IMustHaveTenant`, so `PlatformDbContext` attaches `HasQueryFilter(e => e.OrganizationId == ExecutionContext.TenantId)`. `DispatchMessageIntegrationEventHandlerTests` builds `MessagingDbContext` with `TenantId = Guid.Empty` (correct for an inbox worker) and writes logs with `OrganizationId = Guid.CreateVersion7()` from the event (correct). It then calls `_db.MessageDeliveryLogs.SingleAsync()` eight times without `IgnoreQueryFilters()`. The filtered set is empty → `Sequence contains no elements`. Sibling tests (`ClientProfileAnonymizedDeliveryLogTests`, `MessageDeliveryLogTests`) do not fail: the first already ignores filters; the second never queries. Fix the fixture with `Logs() => _db.MessageDeliveryLogs.IgnoreQueryFilters()`. Do not revert 179.

---

*Analysis only. No product or test code was changed in this pass.*
