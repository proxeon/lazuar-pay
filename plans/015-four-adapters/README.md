# 015 — Four remaining adapters on new Pay, no tax

**Date:** 24 August 2026  
**SHA at eval:** `ee2db8e5` (`main`)  
**Type:** Evaluation + implementation checklists. **Not** a flip of 011/11 cells until a phase Exit says so.

**Product decision:** hosted_link wraps for **CHIP, Billplz, Xendit, Razorpay** on top of live Stripe. **No tax** (no SST math, no SST fail-closed, no LHDN, no Tax Invoice).

| File | Job |
|------|-----|
| [00-what-must-be-done.md](./00-what-must-be-done.md) | Evaluation (what / why). Evidence. |
| [checklists/](./checklists/README.md) | **How to implement:** many small phases. One intent each. |
| [checklists/decisions.md](./checklists/decisions.md) | Freeze. Filled by A00. Do not change a row without amending A00. |

014 papers still name money-safety holes **all four rails inherit** if step-0 is skipped: [014/00](../014-evals/00-evaluation.md), [014/08](../014-evals/08-webhooks-secrets-fulfillment.md), [014/09](../014-evals/09-porting-architecture.md).

**Rule:** execute [checklists/](./checklists/README.md). Do not implement from `00` as a mega-PR. Do not copy `apps/lazuar-api/Modules/Payments`.
