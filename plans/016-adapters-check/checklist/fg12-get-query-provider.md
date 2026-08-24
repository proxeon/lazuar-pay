# fg12 — GET `?provider=` does not change active

**Track:** Fill gateway · **Depends:** A00  
**Analysis:** 09 method 59; P15  
**Goal:** `GatewayTests.Get_optional_provider_query_does_not_change_active`

---

- [ ] PUT chip (active chip), PUT stripe (active stripe)
- [ ] GET `?provider=chip` → configured chip metadata
- [ ] GET without query → provider stripe
- [ ] `OrgSettings.ActiveProvider` still stripe
- [ ] Exit: green
