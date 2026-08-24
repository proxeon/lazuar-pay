# J11 — Fallback join via stored plink_

**Track:** Razorpay join · **Depends:** J10, I12  
**Analysis:** [`../08-razorpay-crosscheck.md`](../08-razorpay-crosscheck.md); P0-C  
**IDs:** —  
**Goal:** If Razorpay does not copy link notes onto `payment.entity`, we still find the checkout.

---

## J11.1

- [ ] After notes miss, look at payment payload for a payment_link id if present (`payload.payment_link.entity.id` or payment `notes` / entity fields — read Razorpay payload shape; do not guess extra event types)
- [ ] Then: `Checkouts.FirstOrDefault(x => x.OrgId == orgId && x.Provider == razorpay && x.ProviderSessionId == plinkId)`
- [ ] Handler currently parses **before** db checkout load — fallback that needs DB belongs in `RazorpayWebhook.Parse` only if you pass db (don’t), **or** in `WebhookEndpoints` after parse: if provider is razorpay and CheckoutId null, join by `ProviderSessionId == parsed.ProviderRef` is **wrong** (ProviderRef is `pay_`). Store plink on parse as optional `parsed.HostedSessionId` **or** join in the handler using a new parse field
- [ ] Prefer: parse sets `CheckoutId` from notes; if null, sets `HostedSessionId` from payment_link id; handler looks up by `ProviderSessionId`
- [ ] Start already persists `id` from payment_links JSON (`plink_…`) — I12 must not clobber it

## J11.2 Must not

- [ ] Do not fulfill `payment_link.paid` as cash (J13)
- [ ] Do not Guid-invent checkout id
- [ ] Do not scan all org checkouts by amount

## J11.3 Exit

- [ ] Unblocked for J12, J16
