---
number: "125"
id: B07-I26
severity: P1
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 125 — B07-I26 — API key 5-minute cache if revoke never consumes

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I26 — P1 — API key 5-minute cache if revoke never consumes

**Where.** `ApiKeyAuthenticationMiddleware.cs:51`; `RevokeApiCredentialCommand.cs:48–49`; `ApiKeyRevokedIntegrationEventHandler.cs:24–32`.

**What.** Happy path evicts. Outbox/inbox stall → stolen key lives until TTL. `Revoked_Key_After_Cache_Eviction_Returns_401` **simulates** an already-empty cache (`ApiKeyAuthenticationTests.cs:201–212`). It does not run the handler against a warm cache and then prove the next request 401s through SQL. Adjacent test `ApiKeyRevokedIntegrationEventHandlerTests` does evict. The pair is close to honest; the “revoked key” middleware test name overclaims (see §11).

