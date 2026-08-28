# G19 — Bake group `pay` is not Hub GHCR

**Track:** G · **Depends:** K00  
**Analysis:** [`../06-host-production.md`](../06-host-production.md) §13.2.7  
**Goal:** Pay images can ship without SSH Hub.

**Why:** `docker-bake.hcl` gained group `pay` (080) but file header is still “Hub → GHCR”. `ghcr.yml` deploys Hub. Shipping Pay via Hub SSH is refuse.

**Related files**

| Path | Role today |
|------|------------|
| `docker-bake.hcl` | Hub images + `pay` group |
| `.github/workflows/ghcr.yml` | Hub |
| `apps/lazuar-pay/Dockerfile` | API |
| `apps/lazuar-pay-merchant/Dockerfile` | Merchant |
| `apps/lazuar-pay-checkout/Dockerfile` | Checkout |

**Current (`6d730d15`):** Bake target exists; Hub workflow does not bake Pay as its own job.

---

## G19.1

- [x] Workflow or matrix that `docker buildx bake pay` **without** Hub deploy job
- [x] Image names `lazuar-pay`, `lazuar-pay-merchant`, `lazuar-pay-checkout`
- [x] Labels not “Lazuar Hub CaaS”
- [x] Do not add Pay to Hub `ghcr.yml` SSH path

## G19.2 Must not

- [x] Do not retarget Hub compose

## G19.3 Exit

- [x] Track G can close without deploy/Caddy (P18/parked kit)
