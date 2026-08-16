# W2-LP-118 — done

Products can set SST type `06` or `02` + rate, only if Legal profile has an SST number. Checkout computes exclusive SST when registered and stamps metadata so Billplz `TaxAmount=0` still books `LIABILITY_TAX_PAYABLE` with type `02`. UBL supplier emits `schemeID="SST"` when the number is present. Untaxed lines stay `06`. PDF/ops label **SST** when type `02`.

## Tests run

- `SstTaxMathTests` 06 / 02+id / 02 without id — **ok**
- Standard invoice XML includes SST scheme when number set — **ok**

Not committed. Not pushed.

Tracker `LP-118` **P → Y** after a merchant marks a link as service tax with SST # on file.
