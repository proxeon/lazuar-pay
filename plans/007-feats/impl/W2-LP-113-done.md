# W2-LP-113 — done

VALID events already carry `{portal}/{uuid}/share/{longId}`. GET `qr_link` is only set when status is VALID. Billing passes `QrLink` into `GenerateAndStoreDocument` for individual INV-/RCPT- rows (not every B2C-CONS receipt). Ops detail renders the share URL and QR. Official Receipts at pay time still have no QR.

## Tests run

- GET PENDING `qr_link` null; GET VALID contains `/share/` — **ok**
- VALID handler passes QrLink on individual INV- — **ok**

Not committed. Not pushed.

Tracker `LP-113` **B → Y** when a sandbox VALID PDF shows a scannable MyInvois QR.
