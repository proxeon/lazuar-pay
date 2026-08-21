# M20 — No password form

**Track:** Merchant · **Depends:** M13  
**Analysis:** [04](../04-merchant-frontend.md)  
**Goal:** Fail this phase if a login form exists on `:5178`.  
**011:** NP-XX-007

---

## M20.1 Grep

- [x] Grep merchant `src` for `password`, `/one/auth/login`, `lazuar_auth`
- [x] Grep for Hub leftovers: `forgot-password`, `reset-password`, `verify-email`
- [x] No email+password fields, no `POST /one/auth/login`, no cookie JWT

## M20.2 Lock

- [x] Add a test or Isolation-style scan (Q10 may own the durable scan)
- [x] **Fail the phase** if a login form exists
- [x] Sign-in remains `signinRedirect()` only (M15)

## M20.3 Must not

- [x] Do not port ops `LoginPage.tsx` / `ForgotPasswordPage` / `ResetPasswordPage`
- [x] Do not stub `POST /v1/auth/login` on Pay to make a form work

## M20.4 Exit

- [x] Grep/scan clean; no password UI on the merchant origin
- [x] Unblocked for M21
