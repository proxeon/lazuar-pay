# S31 — Sample Next.js scaffold

**Track:** Sample packaging · **Analysis:** `../03-sample-app-architecture.md`, `../07`  
**Depends on:** S00, S30  
**Goal:** App boots with shell routes; no Hub calls required yet.

---

## S31.1 Create package

- [ ] Directory: `examples/hub-cashier-next/`
- [ ] `package.json`: private; name `@examples/hub-cashier-next`
- [ ] Scripts: `dev` on port **3020**, `build`, `start`, optional `lint`
- [ ] Deps: `next@16.2.x`, `react@19.2.x`, `react-dom` (align portal family)
- [ ] DevDeps: typescript, @types/node, @types/react, @types/react-dom, eslint-config-next optional
- [ ] **No** `@repo/api-types-ts`, `@repo/ui`, stripe SDK, billplz SDK

## S31.2 Config files

- [ ] `tsconfig.json`
- [ ] `next.config.ts` (no basePath required)
- [ ] `next-env.d.ts` as needed
- [ ] Optional minimal CSS (no full design system required)

## S31.3 App shell routes

- [ ] `app/layout.tsx` — minimal shell; badge “Sample · not production”
- [ ] `app/page.tsx` — landing: what this proves + link to /pay
- [ ] Placeholder routes (can  stub):
  - [ ] `app/pay/page.tsx`
  - [ ] `app/pay/success/page.tsx`
  - [ ] `app/pay/cancel/page.tsx`
  - [ ] `app/orders/page.tsx` optional
  - [ ] `app/api/checkout/route.ts` stub (501 or TODO)
  - [ ] `app/api/webhooks/hub/payments/route.ts` **or** `app/webhooks/hub/payments/route.ts` — pick path and document (doc-faithful: `/webhooks/hub/payments` preferred)

## S31.4 Runtime defaults

- [ ] Webhook route file includes `export const runtime = "nodejs"` when implemented (note in stub)
- [ ] Disclaimer in README (S40 can flesh)

## S31.5 Verify

- [ ] `pnpm install` at repo root (lockfile)
- [ ] `pnpm --filter @examples/hub-cashier-next dev` serves http://localhost:3020
- [ ] Product `pnpm build` still excludes sample (S30 filters)

## S31.6 Exit

- [ ] Scaffold PR is shell-only; no secrets; no Hub dependency for smoke UI
