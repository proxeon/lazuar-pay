# Phase 18 — Definition of done (maintenance track healthy)

**Goal:** Know when to stop “maintenance mode” and return to product work.  
**Use:** Revisit after each wave; not a single PR.

---

## 18.1 Safety

- [ ] No secrets/cookie jars tracked
- [ ] No uncompiled dead gen twins
- [ ] Dual API key path closed **or** dated dual-read with calendar
- [ ] Webhook story single **or** frozen special-case documented

## 18.2 Contracts

- [ ] TypeSpec P0 dual DTOs gone
- [ ] Path slash / broadcast honesty fixed
- [ ] `task gen` + CI contracts green
- [ ] api-spec README matches tree

## 18.3 Navigability

- [ ] One endpoints split to Commerce style
- [ ] Program.cs thinned
- [ ] At least provision or dunning split done if those areas were touch-heavy
- [ ] Messaging folder convention aligned

## 18.4 Quality loops

- [ ] CI runs same critical test projects as Taskfile (Ops included or excluded with reason)
- [ ] Architecture tests green on main
- [ ] Known cross-schema SQL leaks tracked as issues if not fixed

## 18.5 Structural debt accepted consciously

- [ ] No new modules without Phase 16 trigger
- [ ] BuildingBlocks thinning plan exists (even if incomplete)
- [ ] SharedKernel decision documented
- [ ] Migration squash **not** required for “done”

## 18.6 Product honesty

- [ ] Deferred revenue / WhatsApp / probes decided
- [ ] Community/Vault not taught as live backend modules

## 18.7 Stop criteria

When 18.1–18.6 are true:

- [ ] Close maintenance track as “healthy enough”
- [ ] Remaining items become normal backlog tickets, not a special program
