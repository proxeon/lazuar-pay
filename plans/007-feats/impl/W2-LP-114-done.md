# W2-LP-114 — done

`Lhdn:B2cIndividualThresholdMyr` default `10000` (from 1 Jan 2026). B2C receipts above the threshold are `NOT_REQUIRED` / `NEEDS_BUYER_TIN` and never enter `B2C-CONS` XML. Job has the same defense-in-depth. Below-threshold B2C still batches on the 28th / catch-up. Ops Tax Invoices shows last `B2C-CONS-*` + LHDN status and does not claim “filed by the 7th”.

## Tests run

- `B2cConsolidationJobTests` including RM 10000.01 excluded — **ok**

Not committed. Not pushed.

Tracker `LP-114` **B → P** (worker + threshold). **Y** when merchants see last-run status (banner shipped).
