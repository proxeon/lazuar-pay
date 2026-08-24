# M20 — Vitest: CHIP PEM textarea

**Track:** Merchant · **Depends:** M10  
**Goal:** Grep lock so the widget cannot regress to `<input>`.

---

## M20.1

- [ ] `locks.test.ts`: when provider chip branch is in `WorkspacePage.tsx`, file contains `<textarea` near PEM copy **or** a dedicated test that the source includes `textarea` and `PEM`
- [ ] Source grep is enough (no RTL required)

## M20.2 Exit

- [ ] `pnpm --filter lazuar-pay-merchant test` green
