# F11 — `open` → `paid`

**Track:** Fulfillment · **Depends:** F10, D17  
**Analysis:** [07](../07-fulfillment-ledger-docs.md)  
**IDs:** NP-CHK-004  
**Goal:** Only an `open` checkout becomes `paid`. Do not double-fulfill.

---

## F11.1 CAS

- [ ] `UPDATE checkouts SET status = 'paid' … WHERE status = 'open'` (same `org_id`, same id)
- [ ] Zero rows updated → do not insert a second journal, seat, or `RCPT-`
- [ ] Already `paid` (same payment): HTTP 200 no-op
- [ ] `canceled` / unknown status: refuse

## F11.2 Expired-late (Bar C / Hub 036)

- [ ] Hub allowed late `EXPIRED` via `CanFulfillFromPayment` — read `apps/lazuar-api/Modules/Commerce/Domain/Aggregates/CheckoutSession.cs`; **read, do not copy project**
- [ ] Bar B **default: only `open`**. Do not fulfill `expired` in this program (refuse)
- [ ] Park expired-late as Bar C / Hub 036 — do not steal Hub `COMPLETED` naming; Pay status is `paid`

## F11.3 Must not

- [ ] Do not use create-time `Idempotency-Key` as the paid / journal key
- [ ] Do not leave status `open` after a successful fulfill commit

## F11.4 Exit

- [ ] Test: second fulfill does not flip a non-open row
- [ ] Unblocked for F12 and F13
