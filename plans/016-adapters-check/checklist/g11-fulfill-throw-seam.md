# G11 — Seam or real-TX store for fulfill throw

**Track:** Prove Beat 1 · **Depends:** G10  
**Analysis:** [`../09-tests-inventory.md`](../09-tests-inventory.md) §5.9 / §10.1 method 7  
**IDs:** H25  
**Goal:** Replace `Fulfillment` in tests **or** run webhook tests against SQLite/Postgres with real transactions.

---

## G11.1 Pick one (A00 already allows either)

- [ ] **Seam:** tiny `IFulfillPaid` (one method) registered in `Program`; tests replace with throwing decorator. **Not** a gateway factory (parked-factory)
- [ ] **Store:** test collection with SQLite (transactions on) or Testcontainers Postgres — heavier

## G11.2 Must not

- [ ] Do not add `IPaymentGatewayAdapter`
- [ ] Do not skip G12 without the G10 comment **and** an explicit “H25 unproven” note in WebhookTests

## G11.3 Exit

- [ ] Seam or store exists **or** G10 skip comment is complete and G12/G13 are marked n/a in A99
- [ ] Unblocked for G12
