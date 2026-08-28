# D10 — Root README names focused Pay

**Track:** D · **Depends:** K00  
**Analysis:** [`../09-spec-docs-sample.md`](../09-spec-docs-sample.md)  
**Goal:** A stranger opening the repo finds 8081, not only Hub.

**Why:** Root README is Hub CaaS marketing (LHDN, dunning, portal.lazuar.com). Focused Pay is a subdirectory. Integrators never find `apps/lazuar-pay`.

**Related files**

| Path | Role today |
|------|------------|
| `README.md` | Hub watermark |
| `apps/lazuar-pay/README.md` | Focused Pay |
| `TODO.md` | Maybe Hub |
| E10 | Sample museum |

**Current (`6d730d15`):** Root does not lead with 8081.

---

## D10.1

- [x] Root README: Pay host 8081, merchant 5178, checkout 5179, One 8080
- [x] Hub museum called museum; do not set `VITE_API_URL` of ops to Pay
- [x] Link `apps/lazuar-pay/README.md`
- [x] Do not present `examples/hub-cashier-next` as Pay (E10)

## D10.2 Must not

- [x] Do not say production-ready
- [x] Do not say we have merchant webhooks until W21

## D10.3 Exit

- [x] Unblocked for D11
