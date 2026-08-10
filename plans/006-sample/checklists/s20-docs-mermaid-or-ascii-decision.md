# S20 — Mermaid vs ASCII decision

**Track:** Docs diagrams · **Analysis:** `../01-docs-flow-diagrams.md` §4  
**Depends on:** S00  
**Goal:** Choose rendering strategy once; record decision.

---

## S20.1 Options

| Option | When |
|--------|------|
| **A ASCII-only** | Ship diagrams immediately; no plugin risk |
| **B Mermaid plugin** | Better sequence diagrams; needs dep + theme |
| **C Mermaid + ASCII details fallback** | Migration period |

## S20.2 Decision record

- [ ] Choose A / B / C: ________
- [ ] Write decision in `plans/006-sample/README.md` or `D00` notes (one sentence)
- [ ] If B/C: pick plugin approach (`vitepress-plugin-mermaid` or equivalent)

## S20.3 If Mermaid enabled

- [ ] Add deps to `apps/lazuar-docs/package.json`
- [ ] Wire `docs/.vitepress/config.ts` (and theme if required)
- [ ] Pin mermaid major version
- [ ] Verify dark/light readable
- [ ] `pnpm --filter lazuar-docs build` green with a test mermaid fence

## S20.4 If ASCII-only

- [ ] Document that Phase B Mermaid is optional follow-up
- [ ] Still require prose summary under every diagram (a11y)

## S20.5 Exit

- [ ] Decision locked; subsequent S21–S24 use chosen format only
