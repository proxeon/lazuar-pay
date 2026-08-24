# K12 — Start 503 shows host detail

**Track:** Checkout · **Depends:** K11  
**Analysis:** every 503 → `rail not configured`; host also “CHIP rejected the org key”  
**IDs:** K16  
**Goal:** Operators can tell missing rail from bad keys.

---

## K12.1

- [ ] Parse `detail` on 503
- [ ] Fallback `rail not configured` only if body missing

## K12.2 Exit

- [ ] Source uses detail
