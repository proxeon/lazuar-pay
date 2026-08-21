# F21 — Merchant UI: list + open receipt (`:5178`)

**Track:** Fulfillment · **Depends:** F19, F20, M24  
**Analysis:** [07](../07-fulfillment-ledger-docs.md)  
**Goal:** `:5178` shows the payments list and can open a receipt. `member` can see. Not ops `:3003`.

---

## F21.1 UI

- [x] Merchant Vite `:5178` calls Pay `/v1` (F19 + F20) with the One Bearer
- [x] List payments; open receipt (JSON view is enough)
- [x] `member` can see (M24: member read-only). `owner` / `admin` also see
- [x] Do not hide the receipt behind a VIEWER role One does not have

## F21.2 Must not

- [x] Not `lazuar-ops` `:3003`. Not `lazuar-admin` `:5173`
- [x] Do not set ops `VITE_API_URL` to 8081 (P60)
- [x] Do not copy ops modules / Hub `@repo/api-types-ts`
- [x] CORS still denies `:3003`

## F21.3 Exit

- [x] A member signed in on `:5178` can see a paid row and its `RCPT-`
- [x] Unblocked for B99 chrome (with O12)
