# 012 — Implementation checklists (One → new Pay)

**Date:** 20 August 2026  
**Branch:** `feat/012-one-to-pay`  
**Style:** Many **small** phase files. One phase ≈ one commit (or a tightly scoped PR).  
**How-to evidence:** parent [`../01`](../01-one-http-surface.md)–[`../10`](../10-dogfood-and-tests.md). Do not treat this folder as a substitute for those papers.  
**Freeze:** [`decisions.md`](./decisions.md) (locked in C00).

**This program is “Pay trusts One over HTTP.”** It is not S1 money, not old Hub compatibility, not an One-repo feature.

## Rule: not one mega-PR

| Do | Don’t |
|----|--------|
| One intent per phase | Options + whoami + authz + TypeSpec + live curl in one tip |
| Fake One with `HttpMessageHandler` in `task pay:test` | Require Zitadel / One compose for CI |
| Forward the caller’s Bearer | Invent Pay users / cookie JWT |
| Keep listen **8081** | Bind 8080 or steal Hub `task dev` |
| Leave `lazuar-one` unchanged for C-phases | Seed SPA, SCIM, FGA `payment` “for Pay” |

## Track map

```text
C00 Align & freeze
  │
  ├─ Track Whoami (serial) ── C10 → C11 → C12 → C13 → C14 → C15 → C16 → C17 → C18 → C19
  │
  └─ Track Authz (after C16) ── C20 → C21 → C22 → C23 → C24
C99 Connected definition of done

Parked (do not start until C99, except notes):
  P10 SPA / OIDC     P20 machine key     P30 One webhooks
  P40 One repo       P50 money           P60 old ops/portal
```

**Serial inside Whoami.** Authz may start after **C16** (hermetic whoami tests green), not after C19.

## Phase index

### Program

| ID | File | Intent |
|----|------|--------|
| C00 | [c00-align-freeze.md](./c00-align-freeze.md) | Lock paths, JSON, anti-goals. No product code. |
| C99 | [c99-connected-done.md](./c99-connected-done.md) | Honest close of *connected* (not S0, not S1) |

### Track Whoami

| ID | File | Intent |
|----|------|--------|
| C10 | [c10-one-options.md](./c10-one-options.md) | `OneOptions` BaseUrl + Timeout only |
| C11 | [c11-httpclient.md](./c11-httpclient.md) | Named `HttpClient` to One, no endpoints yet |
| C12 | [c12-me-projection.md](./c12-me-projection.md) | Plain types: One `/me` JSON → Pay whoami DTO |
| C13 | [c13-whoami-endpoint.md](./c13-whoami-endpoint.md) | `GET /v1/whoami` forwards Bearer to One `/me` |
| C14 | [c14-whoami-errors.md](./c14-whoami-errors.md) | 401 / 503 mapping; no 200 on One failure |
| C15 | [c15-health-never-calls-one.md](./c15-health-never-calls-one.md) | `/health` and `/v1/health` stay One-free |
| C16 | [c16-whoami-tests.md](./c16-whoami-tests.md) | Hermetic tests: 200 / 401 / timeout |
| C17 | [c17-isolation-tests.md](./c17-isolation-tests.md) | Widen IsolationTests; still no cathedral |
| C18 | [c18-pay-spec-whoami.md](./c18-pay-spec-whoami.md) | `packages/pay-spec` grows whoami only |
| C19 | [c19-whoami-runbook.md](./c19-whoami-runbook.md) | README: Hub off, curl against live One |

### Track Authz

| ID | File | Intent |
|----|------|--------|
| C20 | [c20-dummy-org-ready.md](./c20-dummy-org-ready.md) | `GET /v1/orgs/{orgId}/ready` + `authz/check member` |
| C21 | [c21-authz-errors.md](./c21-authz-errors.md) | One 403, `{allowed:false}`, 503 |
| C22 | [c22-authz-tests.md](./c22-authz-tests.md) | Hermetic allow / deny tests |
| C23 | [c23-header-is-hint.md](./c23-header-is-hint.md) | Path SoT; `X-Lazuar-Tenant-Id` cannot authorize |
| C24 | [c24-viewer-honesty.md](./c24-viewer-honesty.md) | Document One has no staff VIEWER; do not fake it |

### Parked (ideas captured, not this program)

| ID | File | Intent |
|----|------|--------|
| P10 | [p10-spa-oidc.md](./p10-spa-oidc.md) | Pay browser origin + One app/redirects |
| P20 | [p20-machine-key.md](./p20-machine-key.md) | Scoped `lzr_sk_` for jobs |
| P30 | [p30-one-webhooks.md](./p30-one-webhooks.md) | HMAC; `tenant.suspended` before live charges |
| P40 | [p40-one-repo.md](./p40-one-repo.md) | When (rarely) One must change |
| P50 | [p50-money.md](./p50-money.md) | `/v1/checkouts` after connected |
| P60 | [p60-old-frontends.md](./p60-old-frontends.md) | Do not point ops/portal at 8081 |

## How to execute

1. Complete **C00** and fill [`decisions.md`](./decisions.md).
2. C10→C19 in order. Do not skip C15 or C16.
3. C20→C24 after C16.
4. C99 only when Whoami **and** Authz checklists are done.
5. Do not flip [011/11](../../011-new-lazuar-pay/11-checklist.md) cells except the NP-ONE rows listed in each phase **Exit**.
6. Do not start P-phases in the same PR as C-phases.
