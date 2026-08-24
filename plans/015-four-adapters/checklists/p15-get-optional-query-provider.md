# P15 — Optional GET ?provider=

**Track:** Provider door · **Depends:** P14  
**Analysis:** [00](../00-what-must-be-done.md) §3.4  
**IDs:** —  
**Goal:** Support can inspect a non-active row without making it active.

---

## P15.1

- [ ] `GET /v1/orgs/{orgId}/gateway?provider=chip` loads that PK if present
- [ ] Unknown provider query → 400 (P10 allow-list)
- [ ] Missing row → `{ configured: false, provider }`
- [ ] Does **not** change `active_provider`
- [ ] Still never returns ciphertext

## P15.2 Must not

- [ ] Do not list all five rails in one payload in this program (optional later). Default GET is **active** only (P14)

## P15.3 Exit

- [ ] Query param works
- [ ] Unblocked for U21
