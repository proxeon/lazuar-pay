# G16 — Captured One `tenant.suspended` fixture

**Track:** G · **Depends:** K00  
**Analysis:** [`../04-inbound-webhooks.md`](../04-inbound-webhooks.md); [`../10-honesty-production-bar.md`](../10-honesty-production-bar.md)  
**Goal:** Pause is proven on product One envelope, not only a minted dialect.

**Why:** Pay verifier accepts split headers. Tests **mint** `{id,type,org_id|tenant_id}`. Live One may wrap tenant under `data` or use `event_type`. `PeekOrgId` would miss → process fallback or 503.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookEndpoints.cs` | `PeekOrgId` / `ReadOrgId` |
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookSignature.cs` | Dialect |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/OneWebhookTests.cs` | `Product_one_split_headers_suspend_charges` |
| Sibling One `WebhookEventPublisher` / catalog `tenant.suspended` | Envelope |

**Current (`6d730d15`):** Dialect closed in Pay source. Envelope uncaptured.

---

## G16.1

- [ ] Capture (sanitize) a real dispatcher POST from sibling One against a tunnel, **or** copy One’s documented envelope from **live** One source (`type` vs `event_type`, `tenant_id` location)
- [ ] Fixture file in Pay tests (no secrets)
- [ ] Test: that JSON + product headers → `ChargesPaused` true
- [ ] If envelope differs from `PeekOrgId`, **fix mapping** — do not import `Modules.One`

## G16.2 Must not

- [ ] Do not call this “production-proven” without the fixture
- [ ] Do not flip 011/11 from this phase

## G16.3 Exit

- [ ] Unblocked for G17
