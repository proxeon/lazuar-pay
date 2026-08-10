# S24 — Diagrams: product-lines, environments, second-app

**Track:** Docs diagrams · **Analysis:** `../01` FLW-WHICH-PRODUCT, CMP-NETWORK-HOPS, SEQ-SECOND-APP  
**Depends on:** S20  

---

## S24.1 Product lines (`guide/product-lines.md`)

- [ ] Decision flowchart: when Payments cashier vs Commerce vs LHDN vs Paddle
- [ ] Keep existing table as SSoT; diagram mirrors table (no new rules)
- [ ] Do not collapse M2M and Commerce events

## S24.2 Environments (`integrations/environments.md`)

- [ ] Network component: hop1 public Hub base, hop2 sample/app webhook URL
- [ ] Local Pattern A (same machine) vs Pattern B (tunnels)
- [ ] Billplz old-bill callback URL lock-in note
- [ ] Canonical Hub port **8080** in new diagrams (S61 fixes prose drift)

## S24.3 Second-app checklist (`integrations/second-app-checklist.md`)

- [ ] Proof sequence: provision non-aura product → BYOK → checkout → webhook → unlock → replay
- [ ] Independence callouts: no Aura imports, no shared DB
- [ ] Link payment-flow + architecture + run-sample (when ready)

## S24.4 Payments cashier page

- [ ] Component diagram or short link: system context (app DB vs Hub vault vs gateway)
- [ ] Prefer link to payment-flow for full E2E (avoid third diverging copy)

## S24.5 Exit

- [ ] Docs build green
- [ ] Product-line fence intact
