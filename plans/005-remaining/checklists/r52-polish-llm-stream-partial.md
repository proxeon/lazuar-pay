# R52 — LlmOrchestratorService stream partial

**Track:** Polish · **Analysis:** `../09-polish-godfiles-testsupport.md`  
**Note:** Preserve BinaryData / streaming fix history

---

## R52.1 Split

- [x] Move stream loop to `.Stream.cs` partial (or equivalent)
- [x] Keep non-stream path clear
- [x] No streaming behavior regression

## R52.2 Tests

- [x] Ops LLM tests green

## R52.3 Exit

- [x] Main file thinner; stream isolated
