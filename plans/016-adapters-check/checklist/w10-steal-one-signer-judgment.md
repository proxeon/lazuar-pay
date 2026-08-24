# W10 — Steal One signer judgment, not the type

**Track:** One HMAC · **Depends:** A00  
**Analysis:** Hub `OutboundWebhookSignature.cs`; live `OneWebhookEndpoints.cs`  
**IDs:** NP-XX-017 adjacent  
**Goal:** Pay verifies what One actually sends. Do not copy `Modules.One`.

---

## W10.1 Read (judgment only)

- [ ] Open `apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookSignature.cs`
- [ ] Algorithm: header `t={unix},v1={lowercase hex}` over payload `{unix}.{body}`, HMAC-SHA256, UTF-8 key and data, 300s skew, `FixedTimeEquals` on hex
- [ ] Header name One sends: confirm live One dispatcher (`X-Lazuar-Signature` vs `webhook-signature`). Pay currently reads `X-Lazuar-Signature` — keep that name unless live One uses another; if both, accept either

## W10.2 Implement in Pay

- [ ] New small helper under `apps/lazuar-pay/src/Lazuar.Pay/One/` (e.g. `OneWebhookSignature.cs`)
- [ ] Duplicate the **algorithm**, not the namespace `Modules.One`

## W10.3 Must not

- [ ] Do not ProjectReference Hub
- [ ] Do not `using Modules.One`
- [ ] IsolationTests must stay green (`Modules.One` is already banned)

## W10.4 Exit

- [ ] Helper exists in Pay
- [ ] Unblocked for W11
