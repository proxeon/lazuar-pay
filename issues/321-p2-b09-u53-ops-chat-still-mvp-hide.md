---
number: "321"
id: B09-U53
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 321 — B09-U53 — Ops chat still `[MVP-HIDE]`

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U53 — Ops chat still `[MVP-HIDE]` (P2)

Not a bug. Listed so the next person does not remount it by accident and call `/ops/execute-action` from `ActionApprovalCard`.

## Evaluation (current tree, 2026-08-18)

### What the bug is
This is a guardrail ticket, not a product defect. Ops AI chat (`OpsChatWorkspace`, conversations, `ActionApprovalCard`) is intentionally disconnected with `[MVP-HIDE]` (ADR 023). `ActionApprovalCard` still POSTs `/ops/execute-action` with the model-proposed payload. If someone uncommented the route and added a sidebar link, merchants would get a chat that can execute backend tools (including broadcast-shaped payloads) from a card that looks like a normal approve button. The audit listed it so the next implementer does not “restore chat” as a drive-by.

### Still present?
**DOCS / HONESTY ONLY**

The hide is still in place. The only `[MVP-HIDE]` in ops `App.tsx` is chat:

```306:308:apps/lazuar-ops/src/App.tsx
        {/* [MVP-HIDE] ADR 023 — ops chat remains disconnected
        <Route path="/ops/chat" element={<OpsChatWorkspace />} />
        */}
```

`OpsChatWorkspace` is not imported by `App.tsx` (the JSX is inside a comment). The file-level comment still names the island (`App.tsx:217–218`). `Sidebar.tsx` has no `/ops/chat` link (grep empty). After 156, the catch-all is a real 404, not a redirect to Sales Insights:

```311:311:apps/lazuar-ops/src/App.tsx
      <Route path="*" element={<NotFoundPage />} />
```

The dangerous client is still on disk:

```18:23:apps/lazuar-ops/src/components/ActionApprovalCard.tsx
  const handleApprove = async () => {
    setIsExecuting(true);
    try {
      const { error } = await client.POST("/ops/execute-action", {
        body: action
      });
```

`ChatMessageBubble.tsx` still mounts `ActionApprovalCard`. `FormRegistry.ts` still maps `CreateProductCommand` to the chat product form (319). API routes `/ops/chat*` and `/ops/execute-action` still exist in `api-types-ts` and `ExecuteActionEndpoints.cs`. That is backend dark matter; it is not reachable from a painted ops button today.

### Related files
- `apps/lazuar-ops/src/App.tsx` — commented route + island list.
- `apps/lazuar-ops/src/components/OpsChatWorkspace.tsx` — `GET /ops/chat/conversations…`.
- `apps/lazuar-ops/src/components/ConversationsDirectory.tsx` — list/rename/delete conversations.
- `apps/lazuar-ops/src/components/ActionApprovalCard.tsx` — `POST /ops/execute-action`.
- `apps/lazuar-ops/src/components/chat/ChatMessageBubble.tsx` — mounts the approval card.
- `apps/lazuar-ops/src/hooks/use-chat-stream.ts` — `POST /ops/chat/stream`.
- `apps/lazuar-ops/src/components/chat/FormRegistry.ts` — chat product form.
- `apps/lazuar-api/Modules/Ops/Infrastructure/Endpoints/ExecuteActionEndpoints.cs` — execute-action backend.
- `docs/architecture-decision-log/023-pure-caas-mvp-ui-lobotomy.md` — `[MVP-HIDE]` methodology.
- `issues/156-p1-b09-u27-catch-all-erases-404.md` — `/ops/chat` is now 404 instead of dashboard.

### Tests
- No ops test that the chat route is absent or that Sidebar omits Chat.
- Nothing fails if someone uncomments the `<Route>`. CI will not catch a remount.
- First regression test (if you want a lock): grep/architecture test that `App.tsx` has no active `<Route path="/ops/chat"` and Sidebar links do not include `/ops/chat`. Do not add an execute-action e2e.

### Reproduction today
Arrange: logged-in merchant on `:3003`. Act: open `/ops/chat`. Assert: “404 / That page is not here,” not `OpsChatWorkspace`. Act: inspect Sidebar — no Chat item. Act: Network while using Products / Team — no `/ops/chat` or `/ops/execute-action`. To see the landmine: read `ActionApprovalCard.tsx` and `ExecuteActionEndpoints.cs`; do not uncomment the route.

### Blast radius
Today: none (unmounted). After a careless remount: any OrgAdmin (or whoever chat authorizes) could approve model-proposed writes, including `SendBroadcastCommand` preview/execute (`ActionApprovalCard.tsx:42–46`). That is ops/comms blast, not buyer card data. Frequency of the hide: permanent until a dedicated chat launch. Do not treat “restore chat” as part of 316–325.

### Suggested fix
Leave the comment in place. Do not remount. Do not delete the backend (ADR 023). If the file noise bothers you, add a one-line pointer in `App.tsx` (already present at 217–222) and stop. No TypeSpec regen. No Wave 5 / WhatsApp from this card.

### Evaluation notes
The audit said “Not a bug.” Keep status open only as a warning, or close as “will not fix / documented.” Severity P2 is a remount cost, not a current user-facing defect. Related: 156 (404), 319 (chat product form WhatsApp), 325 (no test that the hide holds).

