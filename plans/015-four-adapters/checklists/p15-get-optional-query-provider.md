# P15 — Optional GET ?provider=

**Track:** Provider door · **Depends:** P14  
**Analysis:** [00](../00-what-must-be-done.md) §3.4  
**IDs:** —  
**Goal:** Support can inspect a non-active row without making it active.

---

## P15.1

- [x] `GET /v1/orgs/{orgId}/gateway?provider=chip` loads that PK if present
- [x] Unknown provider query → 400 (P10 allow-list)
- [x] Missing row → `{ configured: false, provider }`
- [x] Does **not** change `active_provider`
- [x] Still never returns ciphertext

## P15.2 Must not

- [x] Do not list all five rails in one payload in this program (optional later). Default GET is **active** only (P14)

## P15.3 Exit

- [x] Query param works
- [x] Unblocked for U21
