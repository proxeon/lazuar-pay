# M21 — Never `id_token` as Bearer

**Track:** Merchant · **Depends:** M12  
**Analysis:** [04](../04-merchant-frontend.md)  
**Goal:** Lock the picker: empty access + present `id_token` → `undefined`.

---

## M21.1 Tests

- [ ] Picker given `user.id_token` and **empty** `access_token` returns `undefined`
- [ ] Picker given opaque / JWE `access_token` returns `undefined` (no `id_token` fallback)
- [ ] Picker given JWT-like `access_token` returns that string

## M21.2 Docs

- [ ] README one line: copy **`access_token`**, not `id_token`
- [ ] Do not “heal” leftover opaque apps by sending `id_token`

## M21.3 Must not

- [ ] Do not reopen One issue 002 / M2M-14
- [ ] Do not parse `id_token` for membership (role SoT is whoami + `authz`)

## M21.4 Exit

- [ ] Tests fail if anyone wires `id_token` as Bearer
- [ ] Unblocked for M22
