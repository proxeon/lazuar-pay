# H17 — POST /v1/checkouts is writer

**Track:** Harden · **Depends:** A00  
**Analysis:** [00](../00-what-must-be-done.md) §3.4; [014/00](../../014-evals/00-evaluation.md) P0-5  
**IDs:** NP-ONE-021  
**Goal:** `member` cannot mint a pay link via curl. UI already hides; API must match.

---

## H17.1 Live today

- [x] `CheckoutEndpoints.Create` uses `MemberGate.RequireMemberAsync`
- [x] Catalog create and gateway PUT already use `RequireWriterAsync` (`owner` | `admin` after member check)

## H17.2 Change

- [x] Switch create to `RequireWriterAsync`
- [x] GET `/v1/checkouts/{id}` stays `RequireMemberAsync`
- [x] Public `POST /v1/pay/{token}/start` stays **unauthenticated** (buyer)

## H17.3 Test

- [x] Next to `CatalogTests.Member_cannot_create_product`: member Bearer `POST /v1/checkouts` → **403**
- [x] Owner/admin still 201
- [x] Fake One: member `authz/check` allowed but whoami role `member`

## H17.4 Exit

- [x] Member 403 on create
- [x] Unblocked for H18
