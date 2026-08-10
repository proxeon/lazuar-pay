# S24 — Diagrams: product-lines, environments, second-app

**Track:** Docs diagrams · **Analysis:** `../01` FLW-WHICH-PRODUCT, CMP-NETWORK-HOPS, SEQ-SECOND-APP  
**Depends on:** S20  

---

## S24.1 Product lines (`guide/product-lines.md`)

- [x] Decision flowchart: when Payments cashier vs Commerce vs LHDN vs Paddle
- [x] Keep existing table as SSoT; diagram mirrors table (no new rules)
- [x] Do not collapse M2M and Commerce events

## S24.2 Environments (`integrations/environments.md`)

- [x] Network component: hop1 public Hub base, hop2 sample/app webhook URL
- [x] Local Pattern A (same machine) vs Pattern B (tunnels)
- [x] Billplz old-bill callback URL lock-in note
- [x] Canonical Hub port **8080** in new diagrams (S61 fixes prose drift)

## S24.3 Second-app checklist (`integrations/second-app-checklist.md`)

- [x] Proof sequence: provision non-aura product → BYOK → checkout → webhook → unlock → replay
- [x] Independence callouts: no Aura imports, no shared DB
- [x] Link payment-flow + architecture + run-sample (when ready)

## S24.4 Payments cashier page

- [x] Component diagram or short link: system context (app DB vs Hub vault vs gateway)
- [x] Prefer link to payment-flow for full E2E (avoid third diverging copy)

## S24.5 Exit

- [x] Docs build green
- [x] Product-line fence intact
