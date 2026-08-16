# W1-LP-154 — Bounce / complaint suppression (Resend webhook exists — finish)

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 1 `LP-154`. Tracker: *Suppression (bounce / complaint)* — Lazuar **P**.  
**Not this ID:** BYO Resend (`LP-150` **Y**). Template variables (`LP-153`). WhatsApp STOP (`LP-155` refuse-until-reopen). Marketing blasts product (`LP-157` refuse).

**Invariant:** Hard bounce and spam complaint never get another email. **Unsubscribe** must not block receipts / dunning / magic-link (transactional). Resend’s signed webhook actually attributes the tenant.

---

## 0. Scope lock

In scope:

- `POST /public/communications/webhooks/resend` parse + verify
- `ISuppressionService` reason split
- `DispatchMessageIntegrationEventHandler` + `BroadcastFanoutJob` honor the split
- Tests against real-ish Resend JSON
- RFC 8058 one-click **POST** (header already advertised)

Out of scope:

- Suppression admin UI (nice; not required to close **P**)
- Changing Resend BYOK
- Soft-bounce retry policy
- SMS

---

## 1. Verdict

The **pipe is sketched**. Two product bugs keep the cell **P**:

1. **Parser likely misses Resend’s live payload** (`data.to` + `tags` as object).  
2. **One list blocks everything** — unsub from a broadcast (or a List-Unsubscribe on a receipt if we ever add it) kills dunning and receipts.

| Piece | Status |
|-------|--------|
| `SuppressionEntry` unique (org, email) | **Y** |
| GET unsubscribe HMAC | **Y** |
| Svix verify + fail-closed without secret in prod | **Y** |
| `ResendEmailService` tags `org` | **Y** (send shape: `[{ name, value }]`) |
| Webhook maps `email.bounced` / `email.complained` | **Y** if parse hits |
| Attribute without `org` tag | **Log + skip** (correct fail-closed) |
| Dispatch skip if suppressed | **Y** — **all** reasons |
| Broadcast skip if suppressed | **Y** |
| Transactional vs marketing | **N** |
| Webhook tests | **N** |
| POST one-click | **N** (header lies) |

---

## 2. Current files

| Path | Role |
|------|------|
| `Modules/Communications/Domain/Aggregates/SuppressionEntry.cs` | Reasons: `UNSUBSCRIBE`, `BOUNCE`, `COMPLAINT` (+ `ANONYMIZED` from GDPR) |
| `SuppressionService.cs` | Exists? any reason → suppressed |
| `PublicComplianceEndpoints.cs` | GET unsub; POST Resend |
| `ResendEmailService.cs` | Tag `org`; List-Unsubscribe **headers** when URL passed |
| `DispatchMessageIntegrationEventHandler.cs` | Skip all email if `IsSuppressedAsync` |
| `BroadcastFanoutJob.cs` | Same |
| `ClientProfileAnonymizedIntegrationEventHandler` (Communications) | `ANONYMIZED` |
| `tests/.../DispatchMessageIntegrationEventHandlerTests.cs` | Skip when service returns true |
| `tests/.../ResendEmailServiceTests.cs` | Header shape |

Resend webhook (2024+ typical):

```json
{
  "type": "email.bounced",
  "data": {
    "email_id": "…",
    "to": ["user@example.com"],
    "tags": { "org": "<guid>" }
  }
}
```

Current parser:

- `data.email.to[0]` then `data.recipient` — **not** `data.to`
- tags as **array** of `{name,value}` — **not** object map

Send API uses array tags; webhook may echo either. Handle **both**.

---

## 3. Gaps

### G1 — Bounce/complaint may never insert (P0)

Wrong JSON paths → 200 OK, no row, we keep mailing a hard bounce → Resend reputation burn.

### G2 — Unsub blocks transactional (P0 product)

Stripe-class: unsub = marketing only. Receipts / failed-pay / magic-link / dunning must send unless `BOUNCE` / `COMPLAINT` / `ANONYMIZED`.

### G3 — One-click POST missing

We send `List-Unsubscribe-Post: List-Unsubscribe=One-Click` but only implement GET. Mail clients POST the same URL and get 405.

### G4 — No webhook tests

---

## 4. Minimal changes

### 4.1 Must — parse

In `PublicComplianceEndpoints` Resend POST:

1. Recipient: `data.to[0]` **or** `data.email.to[0]` **or** `data.recipient`.  
2. Org tag: array `{name:org}` **or** object `tags.org`.  
3. Types: keep `email.bounced` → `BOUNCE`, `email.complained` → `COMPLAINT`. Ignore others 200.  
4. Still 200 on parse miss (Resend retries); **log** warning (already).

### 4.2 Must — reason split

| File | Change |
|------|--------|
| `ISuppressionService` | `IsSuppressedAsync(org, email, SuppressionLane lane)` where `lane` is `Transactional` \| `Marketing` |
| `SuppressionService` | Transactional blocked by `BOUNCE`, `COMPLAINT`, `ANONYMIZED`. Marketing blocked by those **plus** `UNSUBSCRIBE`. |
| Dispatch handler | `Transactional` |
| Broadcast fanout | `Marketing` |
| GET/POST unsub | still `UNSUBSCRIBE` |

Do not invent a second table.

### 4.3 Must — one-click POST

Same group: `POST /public/communications/unsubscribe` with the same `org`, `email`, `sig` query (RFC 8058 posts to the List-Unsubscribe URL). Return 200 empty. Reuse GET validation.

If Resend/List-Unsubscribe currently points at GET with query — keep that URL; allow POST.

### 4.4 Should

- Extract parse to `ResendWebhookParser` for tests.  
- Soft bounce (`bounce.type == Transient`) → do **not** suppress (optional; default stay suppress if type missing).

### 4.5 Do not

- Block transactional on unsub.  
- Platform-wide suppression across tenants.  
- Admin CRUD in this ticket.

---

## 5. Tests

New `ResendWebhookSuppressionTests.cs` + extend dispatch tests:

| Case | Expect |
|------|--------|
| Valid Svix + `data.to` + object tags | Row `BOUNCE` |
| Array tags `org` | Same |
| Missing org tag | No row; 200 |
| Bad signature (secret set) | 400 |
| No secret + Development | 200 (existing) |
| No secret + Production | 503 (existing) |
| Dispatch + `UNSUBSCRIBE` only | **sends** transactional |
| Dispatch + `BOUNCE` | skip |
| Broadcast + `UNSUBSCRIBE` | skip |
| POST unsub valid sig | `UNSUBSCRIBE` row |

---

## 6. Risks

| Risk | Mitigation |
|------|------------|
| Resend payload variants | Accept both shapes |
| Existing unsub users stop getting receipts after split | **Fix** — they should get receipts; marketing stays off |
| Tag name rename | Frozen `ResendEmailService.OrgTagName = "org"` |

---

## 7. Acceptance

1. A signed `email.bounced` with `data.to` + `tags.org` inserts `BOUNCE` and stops **all** mail.  
2. `UNSUBSCRIBE` stops broadcasts only.  
3. Complaint = bounce (all mail).  
4. POST unsubscribe works.  
5. Tests §5 pass.  
6. Tracker **P → Y**.

---

## 8. Implement order

1. Parser + tests  
2. Lane split + dispatch/broadcast  
3. POST unsub  
