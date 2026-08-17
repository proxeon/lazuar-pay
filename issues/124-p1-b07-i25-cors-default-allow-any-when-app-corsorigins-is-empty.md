---
number: "124"
id: B07-I25
severity: P1
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 124 — B07-I25 — CORS default allow-any when `App:CorsOrigins` is empty

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I25 — P1 — CORS default allow-any when `App:CorsOrigins` is empty

**Where.** `AuthAndCorsExtensions.cs:196–212`. Empty → `AllowAnyOrigin` + any header/method (**no** credentials). Non-empty → listed origins + `AllowCredentials`.

**What.** Repo appsettings sets origins. `AppOptions.CorsOrigins` default is `""`. Production `env.example` sets `App__CorsOrigins=https://hub.lazuar.com`. Clearing the key in prod disables credentialed CORS (SPA cookie calls fail) **or**, if a client does not need credentials, opens the API to any origin. Misconfig foot-gun. 008 H10.

