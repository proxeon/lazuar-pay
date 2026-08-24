# C17 — Hermetic CHIP start returns checkout_url

**Track:** CHIP · **Depends:** C12–C16, P17, P19  
**Analysis:** [00](../00-what-must-be-done.md) §7  
**IDs:** NP-CHK-005  
**Goal:** `POST /v1/pay/{token}/start` with active chip does not call the real CHIP API.

---

## C17.1

- [ ] Test HttpMessageHandler stubs `POST …/purchases/` → 201 JSON `{ "id": "purch_1", "checkout_url": "https://gate.chip-in.asia/p/…" }`
- [ ] Seed writer PUT chip creds + product/checkout + org_settings active_provider=chip
- [ ] Start with name+email → 200 `{ redirect_url }` matching stub
- [ ] Assert checkout.Provider=`chip`, ProviderSessionId=`purch_1`
- [ ] Missing email → 400 (C30)
- [ ] Do not use live network

## C17.2 Exit

- [ ] Test green
- [ ] Unblocked for C18
