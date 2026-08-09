# SharedKernel

**Status:** Intentional empty marker (Phase 15 / docs 002 + 009).

## What this project is

- Assembly anchor for architecture tests (`SharedKernelMarker`).
- Project reference from every module Domain for a future shared-VO cell.
- References only `BuildingBlocks.Domain`.

## What it is not

- Not a dumping ground for write-model entities or module aggregates.
- Not required to hold types “because the folder exists.”

## When to add types

Only when a **true** cross-module, domain-agnostic value object or ID appears (used by ≥2 modules without dragging write models). Until then, marker-only is correct.

See:

- [`docs/002-shared-kernel-vs-building-blocks.md`](../docs/002-shared-kernel-vs-building-blocks.md)
- [`docs/009-building-blocks-ownership.md`](../docs/009-building-blocks-ownership.md)
