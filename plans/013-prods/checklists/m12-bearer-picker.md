# M12 — `pickApiBearerToken`

**Track:** Merchant · **Depends:** M11  
**Analysis:** [04](../04-merchant-frontend.md)  
**Goal:** Copy One’s picker. JWT `access_token` only. Never `id_token`.  
**011:** NP-ONE-003

---

## M12.1 Copy from One

- [x] Copy `pickApiBearerToken` from One `apps/lazuar-app/src/auth/bearerToken.ts` (and its tests)
- [x] Same policy as `examples/vite-spa` — do not invent a fourth rule
- [x] Returns JWT-like `access_token` only (`three` non-empty `.` parts)

## M12.2 Reject

- [x] Opaque / JWE / empty / missing `access_token` → `undefined`
- [x] `id_token` is **never** returned (even if it looks like a JWT)

## M12.3 Wire

- [x] Unit test in the merchant app
- [x] `getAccessToken` is **synchronous** (`() => pickApiBearerToken(auth.user)`), not `useEffect`
- [x] First paint must not 401 because the token was applied one tick late

## M12.4 Exit

- [x] Picker + tests in merchant; sync wire documented for M13
- [x] Unblocked for M13
