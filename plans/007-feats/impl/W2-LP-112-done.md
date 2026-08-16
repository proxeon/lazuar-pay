# W2-LP-112 — done

B2B checkout collects TIN + ID type + ID value and calls `POST /public/commerce/{slug}/validate-tin` (tenant MyInvois creds, no integrator scope). Invalid pairs block pay. `SubmitTaxDocument` type `01` (not General Public `EI00000000010`) re-validates via cache/gateway and refuses invalid/stub pairs. Legal profile has a Check TIN button. Integrator `POST /lhdn/taxpayer/validate` unchanged.

## Tests run

- Submit invalid TIN → no TaxDocument; consolidation General Public skips validate — **ok** (`MyInvoisLoopTests`)

Not committed. Not pushed.

Tracker `LP-112` **B → Y** on sandbox checkout with a real TIN/ID pair.
