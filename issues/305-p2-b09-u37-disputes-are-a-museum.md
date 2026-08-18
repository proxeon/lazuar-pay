---
number: "305"
id: B09-U37
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 305 — B09-U37 — Disputes are a museum

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U37 — Disputes are a museum (P2)

`DisputesPage.tsx` entire file. Clickable nav, no action, no 403 chrome.

## Evaluation (current tree, 2026-08-18)

### What the bug is
Ops paints a first-class Commerce “Disputes” item and mounts `/commerce/disputes`. The page is a 50-row read-only table of `GET /admin/commerce/disputes`. There is no accept / counter / refund / cancel-subscription control, no row click, no link to the subscriber. Status is always amber. The query throws on any non-2xx and the page never reads `isError`, so a 403, 500, or empty list all collapse to “No open disputes.” The API matches the museum: `MapGet("/disputes")` only, OrgRead, no write endpoints. Commerce already records OPEN disputes and (as of 044) skips billing while `HasOpenDispute` is set, but the merchant cannot do anything from this screen.

### Still present?
**STILL BROKEN**

Nav and route are live:

```251:256:apps/lazuar-ops/src/components/Sidebar.tsx
                mod.id === "commerce" ? [
                  { label: "Dashboard", href: "/commerce/dashboard" },
                  { label: "Checkout Links", href: "/commerce/products" },
                  { label: "Subscribers", href: "/commerce/subscribers" },
                  { label: "Transaction Logs", href: "/commerce/transactions" },
                  { label: "Disputes", href: "/commerce/disputes" },
```

```282:282:apps/lazuar-ops/src/App.tsx
        <Route path="/commerce/disputes" element={<DisputesPage />} />
```

Page still has no action chrome and no error chrome:

```20:30:apps/lazuar-ops/src/modules/commerce/pages/DisputesPage.tsx
  const { data, isLoading } = useQuery({
    queryKey: ["commerce-disputes", activeWorkspaceId],
    queryFn: async () => {
      const res = await fetch(`${API_URL}/admin/commerce/disputes?page=1&limit=50`, {
        credentials: "include",
        headers: { "X-Tenant-Id": activeWorkspaceId },
      });
      if (!res.ok) throw new Error("Failed to load disputes");
      return (await res.json()) as { data: CommerceDispute[] };
    },
    enabled: !!activeWorkspaceId,
  });
```

```66:76:apps/lazuar-ops/src/modules/commerce/pages/DisputesPage.tsx
                <td className="px-4 py-2">
                  <span className="text-[10px] font-bold uppercase tracking-widest text-amber-700">{row.status}</span>
                </td>
              </tr>
            ))}
            {!isLoading && (data?.data.length ?? 0) === 0 && (
              <tr>
                <td colSpan={4} className="px-4 py-8 text-center text-[12px] text-[#71717a]">
                  No open disputes.
```

API is still list-only under OrgRead (`apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints.cs` 23, 67–77). `CommerceGatewayDisputeCreatedHandler` comment: “Does not cancel the subscription or book the dispute as a refund.”

### Related files
- `apps/lazuar-ops/src/modules/commerce/pages/DisputesPage.tsx` — the museum.
- `apps/lazuar-ops/src/components/Sidebar.tsx` — clickable Disputes item.
- `apps/lazuar-ops/src/App.tsx` — mounted route.
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints.cs` — GET only, OrgRead.
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceQueryService.CustomCheckouts.cs` `GetDisputesAsync` — SQL list, no mutation.
- `apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/CommerceGatewayDisputeCreatedHandler.cs` — persist OPEN; no merchant action.
- `apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/CommerceGatewayDisputeClosedHandler.cs` — webhook-driven resolve only.
- Issue 044 (`fix/044-skip-open-dispute-billing`) — billing now skips `HasOpenDispute`; still no ops action.

### Tests
- Existing tests that touch this path: none in ops. API tests around dispute *billing* live under issue 044’s tree (`HasOpenDispute` skip); `GetDisputesAsync` / `DisputesPage` have no dedicated test I could find.
- Whether any test would fail if the bug is still there: **No.**
- What a first regression test should assert: page renders a “read-only — respond in the processor dashboard” banner; `isError` paints “You cannot load disputes” not “No open disputes.”; no button calls a non-existent POST. Do **not** add a Stripe Billing `subscription.updated` or in-app win/loss workflow.

### Reproduction today
Arrange an OrgMember session. Click Commerce → Disputes. Assert: table or “No open disputes.”, no action buttons. Arrange a Viewer or a forced 403 on `GET /admin/commerce/disputes`. Assert: after the spinner, the empty sentence appears (React Query `data` is undefined, `isError` unread). Open Stripe/CHIP dashboard separately — that is still the only place to answer the chargeback.

### Blast radius
Merchants with a live card chargeback. Money is already in flight at the processor; Lazuar now pauses auto-debit (044) but the console implies a disputes product. Frequency is low (chargebacks) and high-severity when it happens. PII: gateway tx id + amount + subscription id prefix. Ops cannot tell “none” from “forbidden.”

### Suggested fix
Keep the list. Add `isError` chrome (same pattern Dashboard/Products already use for 403). Change empty copy to “No disputes recorded.” Add one honest sentence: respond in Stripe/CHIP/Xendit; Lazuar does not accept or fight chargebacks. Optional: link the subscription id to `/commerce/subscribers`. Do not implement accept/refund/cancel-from-dispute, Stripe Billing `subscription.updated`, or leftover-time credit (LP-059 is next-renewal-only). No TypeSpec regen unless you add a new write contract, which this ticket should not.

### Evaluation notes
Same bug as 008 “Disputes museum.” Adjacent: 044 (billing skip, resolved), 087 (`hasopendispute` / refunded overwrite), 059 (autocharge vs dispute). Severity still P2 as a lying nav surface; it is not P1 unless product claims in-app dispute handling. Not blocked. Do not start a Wave 5 / homemade dispute desk.

