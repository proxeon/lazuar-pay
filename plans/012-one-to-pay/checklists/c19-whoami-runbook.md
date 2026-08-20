# C19 — Live whoami runbook (docs only)

**Track:** Whoami · **Depends:** C16  
**Analysis:** [05](../05-local-topology.md), [02](../02-one-authn-tokens.md)  
**Goal:** A human can prove whoami against real One. **Not** a CI gate.

---

## C19.1 Process set (write into `apps/lazuar-pay/README.md`)

- [x] Start **One** stack so API is 8080 (bootstrap / `pnpm api:dev` per One README)
- [x] Do **not** start Hub `task dev` / compose `lazuar-api` (8080 collision)
- [x] Fingerprint One: not only `/health` `{status:ok}` — use One’s `/api/v1/` name or `GET /api/v1/me` 401 without token
- [x] `task pay:dev` on **8081**

## C19.2 Token

- [x] Document: log in on One `:5175` as demo user if present (`ada@acme.test` / `Password1!` — confirm against One README at implement time)
- [x] Document: copy **access_token**, not `id_token`
- [x] Document: `GET http://localhost:8081/v1/whoami` with `Authorization: Bearer …`
- [x] Document expected 401 without header; 401 if token is id_token and One rejects it

## C19.3 Must not document

- [x] Do not tell merchants to use `:5173` / `lazuar-admin`
- [x] Do not tell them to point ops `VITE_API_URL` at 8081
- [x] Do not tell them to put `ZITADEL_PAT` in Pay

## C19.4 Exit

- [x] README updated
- [x] Whoami track complete pending C99
- [x] Optional: operator actually ran the curl once (note date in the PR, not a tracker cell)
