# F11 — Move LLM stack toward Ops (FW-3)

**Goal:** Ops owns LLM orchestration dependencies; BB keeps only true multi-module technical bits.  
**Depends on:** F10 recommended  
**Respect:** Sole orchestrator today is Ops

---

## F11.1 Inventory

- [ ] List BB LLM types: `IChatClientFactory`, policies, title generator, DI extensions
- [ ] List Ops consumers only vs other modules

## F11.2 Move

- [ ] Move Ops-only orchestration types into Ops.Infrastructure / Ops.Application
- [ ] Keep shared factory only if second consumer is real
- [ ] Fix DI in Ops module registration + host if needed
- [ ] Agent prompt/tool attributes: Ops.Contracts if appropriate

## F11.3 Tests

- [ ] Modules.Ops.Tests green
- [ ] Architecture tests green

## F11.4 Docs

- [ ] Update 009 ownership map

## F11.5 Exit

- [ ] BB no longer owns Ops-only LLM surface
