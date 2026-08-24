# I10 — Second start returns stored hosted URL

**Track:** Idempotent start · **Depends:** A00  
**Analysis:** [`../10-honesty-frontend-risks.md`](../10-honesty-frontend-risks.md) P0-A; live `PublicPayEndpoints.Start`  
**IDs:** NP-CHK-004  
**Goal:** Ada’s second Pay click must not mint a second processor session.

---

## I10.1 Live today (must change)

- [ ] `Start` always calls `rail.CreateHostedUrlAsync(row, ct)`
- [ ] Then overwrites `PspRedirectUrl` and `ProviderSessionId` and `SaveChanges`
- [ ] Race: Pay → session A → land without `?status=verifying` → Pay again → session B → two charges, one `RCPT-`

## I10.2 Change

- [ ] After pause/open/email checks, if `row.Status == "open"` **and** `PspRedirectUrl` is non-whitespace: return `{ redirect_url: row.PspRedirectUrl }` **200**
- [ ] Do not 409 “already started” as the only option — the buyer must be able to continue to the processor
- [ ] Whitespace-only stored URL does not count as started (treat as first start)

## I10.3 Must not

- [ ] Do not return a stored URL when status is `paid` or `expired` (I13)
- [ ] Do not skip the pause check (I14)
- [ ] Do not call the PSP “to refresh” the URL
- [ ] Do not mint a new URL if SaveChanges failed last time **and** a URL is already stored

## I10.4 Exit

- [ ] Unblocked for I11, I12, G14
