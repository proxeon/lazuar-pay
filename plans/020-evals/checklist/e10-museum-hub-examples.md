# E10 — Mark Hub sample museum

**Track:** E · **Depends:** K00  
**Analysis:** [`../09-spec-docs-sample.md`](../09-spec-docs-sample.md)  
**Goal:** Hub `examples/hub-cashier-next` cannot be mistaken for Pay.

**Why:** Root README still sells Hub CaaS and lists `examples/hub-cashier-next` as **the** integrator sample (`sk_`, port 3020, Hub 8080). A stranger following it never hits 8081.

**Related files**

| Path | Role today |
|------|------------|
| `README.md` | Lines ~106, 117, 179 — Hub sample |
| `examples/README.md` | If present |
| `examples/hub-cashier-next/` | Museum Next app |
| `docs/payments-integration-quickstart.md` | Hub runbook |
| `apps/lazuar-docs/docs/integrations/run-sample-app.md` | Hub |
| `plans/006-sample/README.md` | Done Hub program |

**Current (`6d730d15`):** Sample is Hub. Do not retarget base URL.

---

## E10.1

- [x] README at `examples/hub-cashier-next` (or package README): **museum**, Hub 8080, `sk_`, not 8081
- [x] Root README / Pay README that mention `examples/` point at Pay sample **or** say museum
- [x] Do not change Hub sample base URL to 8081

## E10.2 Exit

- [x] Unblocked for E11
