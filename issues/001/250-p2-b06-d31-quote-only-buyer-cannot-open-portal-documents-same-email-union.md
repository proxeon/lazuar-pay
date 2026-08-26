---
number: "250"
id: B06-D31
severity: P2
status: resolved
resolved_branch: fix/250-quote-portal-token
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
---

# 250 — B06-D31 — Quote-only buyer cannot open portal documents; same-email union

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D31 — Quote-only buyer cannot open portal documents; same-email union (P2)

Portal documents require a subscription id from the magic-link token (`PortalDocumentQueryService.cs:44–52`). Quote-only buyers have no subscription. QuoteView “Open buyer portal” has no token (`QuoteView.tsx:96–98`).

Within a tenant, profiles that share an email are unioned (`57–63`) and all of those emails’ transaction logs become one table. Two clients of the same merchant who share a billing mailbox see each other’s documents. Not cross-tenant. HMAC download binds `tenantSlug + ledgerEntryId` (`PublicBillingEndpoints.cs:44–46`). Cross-tenant PDF theft via slug swap does not work without the JWT secret.

Public GET does **not** verify the ledger belongs to the tenant beyond the key path `vault/{tenantId}/documents/{id}.pdf`. Wrong-tenant GUID + valid HMAC for **this** slug would presign a missing object, not the other tenant’s file.

## Evaluation (current tree, 2026-08-18)

### What the bug is
Portal documents are loaded from a magic-link token whose subject is a **subscription id**. A paid custom quote is a `CheckoutSession` with `ProductId` null; it does not create a `Subscriptions` row. The quote CTA can now *request* a token, but minting still requires `FindSubscriptionIdForCheckoutSessionAsync` to find a subscription. A quote-only buyer therefore still lands on `/{slug}/portal` without `?token=` (or with a token for some other sub on the same CRM profile if one exists). Separately, once a token *does* resolve a profile, documents are unioned by email: every transaction log with that mailbox plus every other CRM profile that `GetClientProfileByEmailAsync` returns is one table. Two clients of the same merchant who share `billing@` see each other’s PDFs. HMAC download is bound to `tenantSlug + ledgerEntryId`; cross-tenant theft via slug swap needs the JWT secret.

### Still present?
**PARTIAL**

**138 / 021** attached a token when mint succeeds:

```34:45:apps/lazuar-portal/src/modules/checkout/components/QuoteView.tsx
  const [portalHref, setPortalHref] = useState(`/${tenantSlug}/portal`);
  useEffect(() => {
    if (!isCompleted) return;
    ...
    getCheckoutStatus(tenantSlug, checkout.id)
      .then((response) => {
        ...
        if (minted) {
          setPortalHref(`/${tenantSlug}/portal?token=${encodeURIComponent(minted)}`);
        }
```

Mint still requires a subscription (`PublicCheckoutEndpoints.cs:169–182`):

```181:182:apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicCheckoutEndpoints.cs
        var subscriptionId = await queryService.FindSubscriptionIdForCheckoutSessionAsync(organizationId, sessionId);
        return subscriptionId.HasValue ? tokenService.GenerateToken(subscriptionId.Value) : null;
```

Custom quotes have `ProductId` null. The join (`CommerceQueryService.Checkout.cs:43–54`) will pick **any** subscription on the same `ClientProfileId` if one exists (`c.ProductId IS NULL OR s.ProductId = c.ProductId`). A true quote-only profile still gets `token: null`. Portal list still returns empty without a subscription row:

```44:52:apps/lazuar-api/Modules/Commerce/Infrastructure/Services/PortalDocumentQueryService.cs
        const string profileSql = @"
            SELECT ""ClientProfileId"" FROM commerce.""Subscriptions""
            WHERE ""Id"" = @SubId AND ""OrganizationId"" = @OrgId LIMIT 1";
        var clientProfileId = await connection.QuerySingleOrDefaultAsync<Guid?>(
            profileSql, new { SubId = referenceSubscriptionId, OrgId = organizationId });
        if (clientProfileId == null)
            return new PortalDocumentsResponse { Items = new List<PortalDocumentDto>() };
```

Same-email union is unchanged (`PortalDocumentQueryService.cs:57–77` adds `GetClientProfileByEmailAsync` and selects logs by `CustomerEmail = @Email`). Quotes are now *included* for those profile ids (`quotesSql` at `:134–161`) — only after you already have a subscription token. HMAC GET still signs `tenantSlug` + id and presigns `vault/{tenantId}/documents/{ledgerEntryId}.pdf` without a ledger-row tenant check (`PublicBillingEndpoints.cs:35–61`).

### Related files
- `apps/lazuar-portal/src/modules/checkout/components/QuoteView.tsx` — CTA + optional token.
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicCheckoutEndpoints.cs` — mint only if a subscription exists.
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceQueryService.Checkout.cs` — loose ProductId-null join.
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Services/PortalDocumentQueryService.cs` — subscription gate + email union + quote rows.
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Security/MagicLinkTokenService.cs` — token subject is subscription id.
- `apps/lazuar-api/Modules/Billing/Infrastructure/Endpoints/PublicBillingEndpoints.cs` — HMAC download.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/GetCheckoutStatusTests.cs` — mint with subscription / pending null.
- `issues/138-p1-b09-u09-quote-settled-cta-and-custom-success-return-are-tokenless.md` — CTA now tries to mint.
- `issues/021-p0-b09-u01-checkout-success-never-receives-a-portal-token.md` — product success token.
- `issues/213-p2-b03-c25-portal-documents-merge-by-email-wider-than-arrearsaccess.md` — same-email union.

### Tests
- `GetCheckoutStatusTests.MintPortalTokenIfCompleted_CompletedWithSubscription_ReturnsToken` / `MintPortalTokenIfCompleted_Pending_IsNull`. There is **no** `CompletedWithoutSubscription_IsNull` test, and no test that a custom session with `ProductId` null mints nothing.
- **No** `PortalDocumentQueryService` tests exist (grep `PortalDocument` under `*Tests*.cs` is empty).
- `TenantIsolationArchitectureTests.DocumentLinkSigner_Draft_And_Final_Payloads_Differ` locks payload shape (`acme:{id}:{exp}`), not “ledger belongs to tenant.”
- `RequestPortalMagicLinkCommandHandlerTests` is email → subscription, not quote-only.
- No test would fail for a quote-only empty documents table or for the email union.
- First regression: completed custom checkout with no `Subscriptions` row → status `token` is null **and** documents list is empty. Second: two profiles, same org, same email, different ledger refs → one token must **not** return the other profile’s documents (or the product must document the union).

### Reproduction today
Create a custom quote for a brand-new email that has never subscribed. Pay it. Click “Open buyer portal.” Network: `GET /public/commerce/{slug}/checkout/{sessionId}/status` returns `token: null`. Portal is the magic-link form. “Email me a link” also needs a subscription. Repeat with two CRM profiles sharing `billing@acme.com` where at least one has a sub: one token lists both mailboxes’ logs.

### Blast radius
Quote-only B2B buyers cannot self-serve receipts (ops/email HMAC links still work). Same-email union is **intra-tenant PII** (two companies sharing a billing mailbox). Not cross-tenant. HMAC slug swap without the JWT secret does not steal another tenant’s object (wrong key → missing R2 object). Frequency: every quote-only pay; every shared billing inbox.

### Suggested fix
Do not mint a fake subscription. Either (a) mint a document-scoped token bound to `ClientProfileId` / session id and teach `ListForBuyerAsync` to accept that subject, or (b) drop “Open buyer portal” on quote-only and point at the HMAC PDF already on the page / email. Tighten the email union to the token’s profile id (leave 213 as the dedicated sibling if you only do (a)). Do not loosen HMAC to skip `tenantSlug`. No TypeSpec regen unless you add a new public token shape. No Stripe Billing portal.

### Evaluation notes
**138** and **021** fixed the CTA/poller for **subscription** sessions; they did not give quote-only buyers a subject. **213** is the same-email half (still open). **126** (CRM resolve merge) is a write-path sibling and is resolved; it does not remove this read-time union. Still P2 for the union; quote-only empty history is the daily UX miss. Not blocked by 161–200.

## Resolution

Completed quote-only checkout mints a portal token bound to `ClientProfileId`. Portal data + document list resolve that subject (no fake subscription). Email union stays gone (213). Quote rows list by profile id.

