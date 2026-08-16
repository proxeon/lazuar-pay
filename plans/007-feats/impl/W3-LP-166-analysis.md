# W3-LP-166 — Staff roles beyond admin

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 3 `LP-166`. Tracker: *Staff roles beyond admin* — Lazuar **P**.  
**Not this ID:** API key scopes (`LP-131`). Platform `SUPER_ADMIN`. HitPay cashier/locations. Custom permission matrix. CRM seats.

**Invariant:** A workspace can invite **Member** (operate commerce, cannot touch keys/gateways/billing profile/members) and **Viewer** (GET only). `ADMIN` stays owner-shaped. Invite `Role` is an allow-list, not a free string. Ops has a Team page on the existing members API.

---

## 0. Scope lock

In scope:

- Roles: `ADMIN` \| `MEMBER` \| `VIEWER` (workspace membership)  
- Authorization policies split  
- `InviteUserToWorkspace` allow-list  
- Workspace **Team** page (list / invite / remove)  
- Ops nav hide for Viewer (mutations)

Out of scope:

- Per-route fine grain (20 checkboxes)  
- `CLIENT` as staff (that is buyer-ish auth noise — do not use)  
- Impersonation  
- Audit log (LP-167)

---

## 1. Verdict

API: `GET/POST/DELETE` members + invites exist. Handler stores **any** `req.Role`. Inviter must be `ADMIN` (string compare — workspace `SUPER_ADMIN` membership cannot invite). Human `OrgAdmin` policy is `SUPER_ADMIN|ADMIN` only — every `/admin/*` commerce route is that policy. There is **no Team page**. Everyone who can open ops is an admin.

**P** is correct: invite plumbing without roles.

---

## 2. Current files

| Path | Role |
|------|------|
| `TenantMembership.Role` | Free string |
| `InviteUserToWorkspaceCommand` | No allow-list; inviter `== "ADMIN"` |
| `AuthAndCorsExtensions` | `OrgAdmin` = admin only |
| `WorkspaceEndpoints` | members + invites |
| `GeneralSettingsPage.tsx` | Name/slug only |
| Ops `App.tsx` / Sidebar | No Team |

---

## 3. Exact gaps

| # | Gap |
|---|-----|
| G1 | No MEMBER/VIEWER semantics on HTTP |
| G2 | Invite accepts `"banana"` |
| G3 | No Team UI |
| G4 | Inviter check misses workspace `SUPER_ADMIN` (minor) |

---

## 4. Recommended model

| Role | Commerce write | Refund / record-pay | Keys, gateways, email BYOK, legal profile | Members |
|------|----------------|---------------------|------------------------------------------|---------|
| ADMIN | Y | Y | Y | Y |
| MEMBER | Y | Y | N | N |
| VIEWER | N (GET ok) | N | N | N |

Policies:

- Keep `OrgAdmin` for money-config + members + keys.  
- Add `OrgMember` = `ADMIN|MEMBER` for `/admin/commerce` mutations that are not config.  
- Add `OrgViewer` or reuse authenticated+membership for GETs.

Minimal mapping: attach `OrgMember` to product/subscriber/coupon/dunning/refund/record-payment writes; leave payment-config, communications email, api keys, workspace update, invites on `OrgAdmin`. GET routes: any of the three roles.

Frontend: hide settings that 403; Team page under Workspace.

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| `InviteUserToWorkspaceCommandHandler` | Allow-list; inviter `ADMIN` or `SUPER_ADMIN` |
| `AuthAndCorsExtensions` | `OrgMember`, `OrgRead` |
| Commerce/Billing/Comms endpoints | Split RequireAuthorization |
| TypeSpec workspace | `role` enum |
| New `TeamPage.tsx` + sidebar | List, invite, revoke |
| Agent tools | MEMBER cannot invite |

Must not: location-scoped cashiers; rename JWT `CLIENT`.

---

## 6. Tests

| Case | Expect |
|------|--------|
| Invite `MEMBER` | Membership role MEMBER |
| Invite `HACKER` | 400 |
| MEMBER `POST` product | 200 |
| MEMBER `PUT` payment-config | 403 |
| VIEWER `POST` refund | 403 |
| VIEWER `GET` subscribers | 200 |
| MEMBER cannot invite | 403 |

Extend One invite tests + endpoint auth tests.

---

## 7. Acceptance

1. Owner invites a bookkeeper as Viewer; they see subscribers and cannot refund.  
2. Member can enroll / record-payment and cannot rotate API keys.  
3. Team page is the only staff UX.  
4. Existing single-admin workspaces unchanged (`ADMIN`).

Tracker **P → Y** after 1–3.

---

## 8. Order

1. Allow-list + policies  
2. Retag write routes  
3. Team page  
4. Tests  

Do **not** implement from this file.
