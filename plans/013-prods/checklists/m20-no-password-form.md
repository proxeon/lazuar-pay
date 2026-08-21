# M20 — No password form

**Track:** Merchant · **Depends:** M13  
**Analysis:** [04](../04-merchant-frontend.md)  
**Goal:** Fail this phase if a login form exists on `:5178`.  
**011:** NP-XX-007

---

## M20.1 Grep

- [ ] Grep merchant `src` for `password`, `/one/auth/login`, `lazuar_auth`
- [ ] Grep for Hub leftovers: `forgot-password`, `reset-password`, `verify-email`
- [ ] No email+password fields, no `POST /one/auth/login`, no cookie JWT

## M20.2 Lock

- [ ] Add a test or Isolation-style scan (Q10 may own the durable scan)
- [ ] **Fail the phase** if a login form exists
- [ ] Sign-in remains `signinRedirect()` only (M15)

## M20.3 Must not

- [ ] Do not port ops `LoginPage.tsx` / `ForgotPasswordPage` / `ResetPasswordPage`
- [ ] Do not stub `POST /v1/auth/login` on Pay to make a form work

## M20.4 Exit

- [ ] Grep/scan clean; no password UI on the merchant origin
- [ ] Unblocked for M21
