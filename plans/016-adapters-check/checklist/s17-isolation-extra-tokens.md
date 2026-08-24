# S17 — Isolation extra banned tokens

**Track:** Strengthen · **Depends:** A00  
**Analysis:** 09 §9.8  
**IDs:** H21, H22, T17, C28, B23  
**Goal:** Append to `BannedSrc` in the **same** method.

---

## S17.1 Add tokens

- [ ] `application_fee`, `TransferData`, `transfer_data`
- [ ] `ChipWebhookRegistrar`, `PublicDnsFallback`
- [ ] `Lhdn`, `MyInvois`, `UBL`, `XAdES`, `Irbm`

## S17.2 Must not

- [ ] Do **not** add `lazuar-local-dev.com` (Billplz block list)
- [ ] Do **not** add `/webhooks/` (Pay’s own route)

## S17.3 Exit

- [ ] IsolationTests green
