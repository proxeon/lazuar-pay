# K99b — Go-live definition of done (Job B)

**Track:** Program  
**Depends:** H10–H14, G10 (on the **one** dogfood rail), G16, G17  
**Analysis:** [`../11-what-next.md`](../11-what-next.md) §4  
**Goal:** We can boot and charge ourselves without laptop-shaped lies. Not “platform.”

**Why:** Job A can close while Production still boots on localhost CS. This close is **us** on HTTPS.

**Related files**

| Path | Must be true |
|------|----------------|
| `HealthEndpoints.cs` | H10 bool |
| `Program.cs` / `OneOptions.cs` | H12–H14 fail-boot |
| Dogfood rail hosted class | G10–G13 as needed |
| `OneWebhookTests` + fixture | G16 |
| `docker-compose.pay.yml` | G14 volume; G15 laptop honesty |

**Current (`6d730d15`):** Cashier hermetic green; process still laptop-shaped.

---

## K99b.1 Process

- [ ] `/ready` is 503 when Postgres cannot connect
- [ ] Production empty WrapKey / Pay CS / laptop One URL **fail boot**
- [ ] Compose profile apps documented as laptop; volume on pay-db
- [ ] Root Hub compose still museum

## K99b.2 Money (one rail)

- [ ] Pick Stripe test **or** CHIP — not five dogfoods
- [ ] Persist-before-PSP or processor idempotency on **that** rail (G10–G13 as needed)
- [ ] Buyer on public checkout origin; success URL is not paid
- [ ] One `RCPT-` ; webhook retry no-ops
- [ ] Captured One `tenant.suspended` pauses charges (G16)
- [ ] Ops runbook: One registers Pay URL; per-org `whsec_` PUT (G17)

## K99b.3 Still not claimed

- [ ] Not kernel if K99a open
- [ ] Not SST / e-invoice
- [ ] Not refunds unless P20/P10 staffed
- [ ] Not OTel required

## K99b.4 Exit

- [ ] README may say first-party sandbox dogfood on HTTPS. Must not say production-ready platform.
