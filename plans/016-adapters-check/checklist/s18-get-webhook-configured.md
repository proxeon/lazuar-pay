# S18 — GET webhook_configured + audit org

**Track:** Strengthen · **Depends:** A00  
**Analysis:** 09 §9.9  
**IDs:** H23, P12  
**Goal:** Edit `Put_and_get_does_not_echo_secret`.

---

## S18.1 Add

- [ ] GET `webhook_configured == true`
- [ ] Audit `OrgId == "t1"` and Action `gateway.credentials.upsert` (already has Action)

## S18.2 Must not

- [ ] Do not echo PEM / `whsec_` / `sk_`

## S18.3 Exit

- [ ] Green
