# R52 — LlmOrchestratorService stream partial (notes)

**Date:** 2026-08-09  
**Track:** Polish  
**Checklist:** `checklists/r52-polish-llm-stream-partial.md`  
**Analysis:** `09-polish-godfiles-testsupport.md` §2.3  
**No commit** (per task).

---

## Summary

| Concern | State |
|---------|--------|
| Stream loop location | **Moved** to `LlmOrchestratorService.Stream.cs` |
| Non-stream path | **Clear** on main: ctor + `ProcessChatAsync` + helpers |
| BinaryData / streaming history comment | **Preserved** on Stream partial header |
| Behavior | Pure move — no stream/tool-loop logic change |
| DI / public type | Unchanged (`partial class LlmOrchestratorService`) |
| Ops LLM tests | **Green** (5/5 `Modules.Ops.Tests`) |

---

## Layout after R52

```
Modules/Ops/Infrastructure/Services/
  LlmOrchestratorService.cs           # ~99 LOC — fields, ctor, ProcessChatAsync, GetValidatedTenantId, TrackAndLogCost
  LlmOrchestratorService.Stream.cs    # ~357 LOC — ProcessChatStreamAsync entire body + BinaryData history block
  LlmOrchestratorService.Prompts.cs   # keep — BuildInitialMessages, BuildChatOptions
  LlmOrchestratorService.Tools.cs     # keep — BuildProposedAction, ExecuteReadToolAsync
  ToolCallAccumulator.cs              # keep — MemoryStream byte accumulation (do not “simplify”)
```

---

## Move rules followed

- [x] `ILlmOrchestratorService` surface unchanged
- [x] Did **not** rewrite BinaryData → string accumulation
- [x] Kept `_maxIterations` / tool failure budget semantics
- [x] Same public type + ctor (Modules.Ops.Tests mocks still valid)
- [x] No Conversation.cs extract (optional; setup duplication left as-is)

---

## Files

| Action | Path |
|--------|------|
| New | `apps/lazuar-api/Modules/Ops/Infrastructure/Services/LlmOrchestratorService.Stream.cs` |
| Edit | `apps/lazuar-api/Modules/Ops/Infrastructure/Services/LlmOrchestratorService.cs` (thinned; pointer to Stream history) |
| Edit | `plans/005-remaining/checklists/r52-polish-llm-stream-partial.md` |
| Edit | `plans/005-remaining/FULL-CHECKLIST.md` (R52 section) |
| New | `plans/005-remaining/r52-notes.md` |

---

## Verification

```bash
dotnet test apps/lazuar-api/tests/Modules.Ops.Tests/Modules.Ops.Tests.csproj --nologo
# Passed!  - Failed: 0, Passed: 5, Skipped: 0, Total: 5
```

---

## Explicit non-goals

- Conversation setup shared helper partial
- Stream tool-loop behavior changes
- ToolCallAccumulator “simplification”
- Commit (deferred per task)
