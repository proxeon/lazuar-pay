# C17 — Hermetic CHIP start returns checkout_url

**Track:** CHIP · **Depends:** C12–C16, P17, P19  
**Analysis:** [00](../00-what-must-be-done.md) §7  
**IDs:** NP-CHK-005  
**Goal:** `POST /v1/pay/{token}/start` with active chip does not call the real CHIP API.

---

## C17.1

- [x] Test HttpMessageHandler stubs `POST …/purchases/` → 201 JSON `{ "id": "purch_1", "checkout_url": "https://gate.chip-in.asia/p/…" }`
- [x] Seed writer PUT chip creds + product/checkout + org_settings active_provider=chip
- [x] Start with name+email → 200 `{ redirect_url }` matching stub
- [x] Assert checkout.Provider=`chip`, ProviderSessionId=`purch_1`
- [x] Missing email → 400 (C30)
- [x] Do not use live network

## C17.2 Exit

- [x] Test green
- [x] Unblocked for C18
