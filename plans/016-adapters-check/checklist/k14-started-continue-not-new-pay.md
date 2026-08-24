# K14 — Open + started → Continue, not a new Pay mint

**Track:** Checkout · **Depends:** I15, K13  
**Analysis:** P0-A SPA half  
**IDs:** —  
**Goal:** Refresh without `?status=verifying` must not look like first Pay.

---

## K14.1

- [ ] Extend `PayView` with `started?: boolean` and `redirect_url?: string` (I15)
- [ ] If `status === open` && `started`: primary button **Continue to processor** uses `redirect_url` if present, else POST start (I10 returns the same URL)
- [ ] Copy: “You already started this payment.”
- [ ] Do not show the empty first-time Pay as the only action

## K14.2 Must not

- [ ] Do not hide verifying (query still not paid)
- [ ] Do not offer a PSP picker

## K14.3 Exit

- [ ] Source branches on `started`
