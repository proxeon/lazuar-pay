# 10 — Program sequencing (D00–D06)

**Status:** analysis complete 2026-08-10  
**Program:** Sample app + docs diagrams/matrices for Hub multi-app cashier story  
**Folder shape:** this file + README + 01–09 analyses under `plans/006-sample/`

---

## 1. Goals

| ID | Goal |
|----|------|
| G1 | Integrator docs have accurate flow diagrams |
| G2 | Responsibility matrices M1–M7 published |
| G3 | Runnable Next.js sample under `examples/hub-cashier-next` |
| G4 | Sample verifies webhooks like `OutboundWebhookSignature` |
| G5 | Sample/docs teach envelope + raw body + no DIY gateway SDK |
| G6 | Packaging excludes sample from product CI risk |

### Non-goals

- Commerce / LHDN / Paddle in sample  
- Production deploy of sample  
- Dockerfile  
- Dual-run Aura modes  
- Fixing all TypeSpec honesty gaps (note only; envelope documented)  
- Completing 005 ops residual (keys table drop clocks, etc.)  

---

## 2. Relationship to 005-remaining

| 005 item | Blocks 006? |
|----------|-------------|
| Wave code closed (R99) | **No** |
| Ops residual key migrate / One-only | **No** for local sample |
| Webhook one-dispatcher | **No** — already SSoT for sample |
| TypeSpec dual DTO leftovers | **No** — sample uses snake_case runtime JSON |

**Decision: no 005 residual block.** Start D00 immediately.

---

## 3. Locked decisions (D00)

| Topic | Lock |
|-------|------|
| Sample path | `examples/hub-cashier-next` |
| Package name | `hub-cashier-next` |
| Hub API port in docs/sample | **8080** |
| Sample port | **3005** |
| Webhook payload | **Envelope + data** (runtime) |
| HTTP client | plain `fetch` |
| Shared types package | **none** |
| Dockerfile | **none** |
| CI build sample | **no** |
| Mermaid | prefer plugin; ASCII fallback OK |

---

## 4. Delivery phases D00–D06

### D00 — Align & freeze

**Outputs:**

- This folder complete (done when analyses accepted)  
- Decision table above acknowledged in PR description  

**DoD:**

- [ ] README + 01–10 present  
- [ ] No open product ambiguity on path/port/envelope  

**PR:** docs-only `plans/006-sample/**` optional commit on feature branch.

---

### D01 — Docs IA + matrices

**Depends on:** D00  

**Outputs:**

- `guide/architecture-who-does-what.md` (M1–M7)  
- `guide/hub-vs-diy.md`  
- Sidebar/nav updates  
- Homepage links  
- Embeds on create-checkout / webhooks / api-keys  
- Port 3002 Scalar fix  

**DoD:**

- [ ] `pnpm --filter lazuar-docs build`  
- [ ] Matrices paste match 02  
- [ ] No DIY tutorials  

**Analysis refs:** 02, 08, 09  

---

### D02 — Flow diagrams

**Depends on:** D00 (can parallel D01 after freeze)  

**Outputs:**

- Mermaid enablement **or** ASCII-only decision recorded  
- Diagrams on pages listed in 01  
- Optional `guide/payment-flow.md`  

**DoD:**

- [ ] Docs build green  
- [ ] Each diagram has prose/ASCII twin  
- [ ] Labels match paths/headers  

**Analysis refs:** 01, 08  

---

### D03 — Sample scaffold

**Depends on:** D00  

**Outputs:**

- `examples/hub-cashier-next` package  
- workspace + root script filters  
- `.env.example`, README skeleton  
- Empty/stub routes  

**DoD:**

- [ ] `pnpm install` links package  
- [ ] `pnpm --filter hub-cashier-next dev` starts  
- [ ] Root `pnpm build` does not require sample  
- [ ] No `@repo/*`, no gateway SDKs  

**Analysis refs:** 03, 07  

---

### D04 — Checkout + UI

**Depends on:** D03, contract freeze (04)  

**Outputs:**

- Orders store + pages  
- `POST /api/checkout` → Hub  
- Redirect UX  
- Error mapping  

**DoD:**

- [ ] With valid sk_ + BYOK, create returns `checkout_url`  
- [ ] Idempotency-Key `order:{id}`  
- [ ] Success page does not claim paid  

**Analysis refs:** 04, 03  

---

### D05 — Webhooks

**Depends on:** D03, algorithm freeze (05)  

**Outputs:**

- `lib/webhook-verify.ts`  
- `POST /api/webhooks/hub`  
- Fulfill order paid/failed  
- Local python/curl simulate instructions  

**DoD:**

- [ ] Valid signature → 200 + paid  
- [ ] Tampered body → 401  
- [ ] Replay → no double side effects  
- [ ] Raw body path only  

**Analysis refs:** 05, 02 M2  

---

### D06 — Runbooks + second-app green

**Depends on:** D01–D05  

**Outputs:**

- `integrations/run-sample-app.md`  
- Provision script outline  
- Second-app checklist points at sample  
- Manual test evidence notes  

**DoD:**

- [ ] Cold reader can go docs → sample → paid order  
- [ ] Checklist independence boxes can be ticked with sample  
- [ ] Homepage links sample  

**Analysis refs:** 06, 08, 10  

---

## 5. Dependency graph

```text
D00
├── D01 (docs matrices/IA)
├── D02 (diagrams)          // parallel with D01
└── D03 (sample scaffold)
     ├── D04 (checkout UI)
     └── D05 (webhooks)     // parallel with D04 after D03
          └── D06 (runbooks) // needs D01+D02+D04+D05 ideally
```

### Parallel bans

| Do not parallel | Why |
|-----------------|-----|
| D04 before D03 | package missing |
| D06 before D05 | runbook would lie |
| Rewriting signature algorithm mid-D05 | break verify |
| Adding `@repo/api-types-ts` mid-flight | packaging goal |

### Safe parallel

- D01 ∥ D02 ∥ D03 after D00  
- D04 ∥ D05 after D03  

---

## 6. Suggested PR list

| PR | Phase | Title sketch |
|----|-------|--------------|
| P0 | D00 | `docs(plans): 006-sample analysis` |
| P1 | D01 | `docs(lazuar-docs): who-does-what + hub-vs-diy matrices` |
| P2 | D02 | `docs(lazuar-docs): mermaid payment flow diagrams` |
| P3 | D03 | `chore(examples): scaffold hub-cashier-next` |
| P4 | D04 | `feat(examples): hub checkout create + order UI` |
| P5 | D05 | `feat(examples): hub webhook verify + fulfill` |
| P6 | D06 | `docs: run-sample-app + provision script` |

Keep PRs reviewable; avoid mega-PR of docs+sample.

---

## 7. Definition of Done (program)

### Docs

- [ ] Architecture page with M1–M7  
- [ ] Hub vs DIY condensed tables only  
- [ ] Payment flow page or equivalent section with E2E + dual-hop diagrams  
- [ ] Webhooks page shows envelope + raw body  
- [ ] Ports: API 8080; Scalar 3002  
- [ ] `pnpm --filter lazuar-docs build` green  

### Sample

- [ ] Lives in `examples/hub-cashier-next`  
- [ ] Creates checkout via Hub  
- [ ] Verifies Hub signature  
- [ ] Unlocks toy order only after `payment.completed`  
- [ ] No processor SDKs  
- [ ] Not required for monorepo product CI  

### Proof

- [ ] Manual e2e below green once on a developer machine  
- [ ] Second-app checklist can reference sample  

---

## 8. Manual test plan

### A. Curl-only (no browser gateway)

1. Start Hub API `:8080`.  
2. Provision workspace with `webhook_url=http://127.0.0.1:3005/api/webhooks/hub`.  
3. Configure BYOK test keys on workspace.  
4. Start sample with env.  
5. `POST /api/orders` → order id.  
6. `POST /api/checkout` `{order_id}` → `checkout_url`.  
7. Sign a fixture body with python (05) matching a pending order metadata → `POST /api/webhooks/hub`.  
8. `GET /api/orders/{id}` → `paid`.  
9. Replay webhook → still paid once.  
10. Tamper body → 401.

### B. Browser + sandbox gateway (full)

1. Same as A through step 6.  
2. Open `checkout_url` in browser.  
3. Pay with sandbox.  
4. Confirm Hop 1 reaches Hub (tunnel if needed).  
5. Confirm sample receives outbound webhook.  
6. UI shows paid without manual mark.  
7. Open success_url alone on unpaid order → still processing.

### C. Docs smoke

1. `pnpm --filter lazuar-docs dev`  
2. Click through Start → Who does what → Payment flow → Run sample.  
3. Confirm diagrams render (or ASCII present).  

### D. Packaging smoke

1. Root `pnpm build` (filtered) succeeds even if sample has intentional TS error (should not leave error; but filter means sample not required).  
2. `pnpm --filter hub-cashier-next build` succeeds when sample complete.  

---

## 9. Risks & mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Envelope vs TypeSpec flat model | Integrators verify wrong JSON | Document runtime envelope; sample parses `data` |
| Port 8090 confusion | Wrong HUB URL | Normalize to 8080 |
| In-memory order store lost on reload | False “webhook failed” | Document; log clearly |
| Hub worker not running | No outbound delivery | Document dispatcher; use signed curl for offline |
| Mermaid plugin break docs CI | Docs build red | ASCII fallback |
| Secrets committed | Security | `.env.example` only; gitignore |
| Sample pulled into turbo build | Noise / red main | Root filters (07) |
| Scope creep Commerce | Delay | Out of scope list in README |
| Clock skew local | 401 verify | 300s tolerance; injectable now in tests |
| BYOK forgotten | 422 only path | Docs + sample error mapping |

---

## 10. Effort sketch (rough)

| Phase | Eng days (order of magnitude) |
|-------|-------------------------------|
| D00 | 0.5 (analysis done) |
| D01 | 1–1.5 |
| D02 | 1–1.5 |
| D03 | 0.5–1 |
| D04 | 1 |
| D05 | 1 |
| D06 | 0.5–1 |
| **Total** | **~5–8 days** |

One engineer sequential; docs (D01/D02) parallelizable with sample (D03–D05).

---

## 11. README shape for `plans/006-sample` (compliance)

Must include:

1. Status line with date  
2. Goals / non-goals  
3. Index table of 01–10  
4. D00–D06 table  
5. Runtime SSoT anchors  
6. Envelope honesty note  
7. Link to this sequencing file for DoD  

**Current README matches this shape.**

---

## 12. Exit criteria for closing 006

Program closed when:

1. All phase DoDs checked  
2. Manual test B green **or** A green + documented tunnel limitation for B  
3. No open “must fix before sample” code dependency on 005 ops  
4. Optional: follow-up ticket for TypeSpec envelope honesty (not blocker)

After close: keep `plans/006-sample` as design archive; operational truth moves to `lazuar-docs` + `examples/hub-cashier-next`.
