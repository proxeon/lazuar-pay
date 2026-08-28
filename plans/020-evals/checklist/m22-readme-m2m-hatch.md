# M22 — Document the M2M hatch

**Track:** M · **Depends:** M14  
**Analysis:** [`../09-spec-docs-sample.md`](../09-spec-docs-sample.md); [`../02-machine-keys-m2m.md`](../02-machine-keys-m2m.md) §12  
**Goal:** A stranger can mint a One key and call Pay without reading 1227 lines.

**Why:** Host README’s only mint recipe is a human JWT curl. Root README hides Pay. Hub sample still looks like “the” integrator path.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/README.md` | JWT `POST /v1/checkouts` curl |
| `apps/lazuar-pay/.env.example` | No second-app key |
| `packages/pay-spec/main.tsp` | Checkouts.create doc: Bearer + writer, no `lzr_sk_` |
| `README.md` (repo root) | Hub-shaped |
| Sibling One `apps/lazuar-docs/docs/integrations/api-keys.md` | Mint UI (do not copy into Pay) |

**Current (`6d730d15`):** No `lzr_sk_` string under `apps/lazuar-pay`.

---

## M22.1 Host README

- [ ] Paragraph: mint key on One `POST /tenants/{id}/api-keys` with scopes `tenant:read` and `authz:check`
- [ ] Secret shown once; prefix `lzr_sk_`; never `VITE_*`; never git
- [ ] `Authorization: Bearer lzr_sk_…` on Pay **8081** `/v1/checkouts`
- [ ] Not Hub `sk_live_`. Not Stripe `sk_live_`. Not `whsec_`
- [ ] Curl example uses `$ORG_ID` = One tenant id
- [ ] Do not claim outbound webhooks until W21

## M22.2 `.env.example`

- [ ] Do **not** add `Pay__OneApiKey` as a merchant credential
- [ ] Comment: second apps hold their own `lzr_sk_`; Pay does not

## M22.3 pay-spec

- [ ] Doc comment on Checkouts.create: writer is One JWT owner/admin **or** bound `lzr_sk_`
- [ ] No new Map* (honesty unchanged)

## M22.4 Must not

- [ ] Do not document Mode M god-key
- [ ] Do not send merchants to `lazuar-ops`

## M22.5 Exit

- [ ] Unblocked for E16 (full second-app page after sample)
