---
number: "064"
id: PAY-ONE-006
severity: P2
status: resolved
source: plans/019-evals/07-identity-authz-cors.md
head: "9f04ad58"
---

# 064 — One 400 / 429 on `authz/check` become Pay 503

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/019-evals/07-identity-authz-cors.md` B7
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`MemberGate` maps anything other than 401/403/200 to 503. Live One 400s non-GUID `object.id`. Rate limit 429 becomes 503. Operators chase “identity provider failed” for a bad URL org id (`/o/t1/...` in tests vs UUID in production One).

Hermetic tests use `"t1"`. Fake One never validates GUIDs. Against live One, `/v1/orgs/t1/ready` would 400 from One, which Pay maps to **503**.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/MemberGate.cs` **36–42**.
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneClient.cs`

## Reproduction

`GET /v1/orgs/not-a-uuid/ready` with a real Bearer against live One. Pay 503.

## Blast radius

Bad bookmarks, typos. 429 storms look like One is down.

## Suggested fix

Map One 400 → Pay 400 with `detail`. Map 429 → 429 or 503 with a distinct sentence. Do not change the member 403 spelling without 065.

## Tests

- Missing: Fake One 400 → Pay 400. Fake One 429 → documented status.

## Source reports

- `plans/019-evals/07-identity-authz-cors.md` §B7
