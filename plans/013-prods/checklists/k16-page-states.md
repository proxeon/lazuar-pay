# K16 — Page states: open / paid / expired / missing / verifying

**Track:** Buyer page · **Depends:** K15  
**Analysis:** [05](../05-checkout-frontend.md) §4.3  
**Goal:** Buyer-visible NP-CHK-004. Ship UI branches even if the host is still `open`-only until F11.  
**011:** NP-CHK-004

---

## K16.1 States

- [x] **open** — amount + Pay CTA (start / K12)
- [x] **paid** — thank you / “paid; this link cannot be paid again.” **Not** “you are a member”
- [x] **expired** — expired copy; no Pay button
- [x] **missing** — “This payment link is not valid.” No login form
- [x] **verifying** — after `success_url` return: spinner, poll public GET until `paid` or timeout (K19)

## K16.2 Honesty

- [x] UI has the branches **now** even if host cannot emit `paid`/`expired` until F11
- [x] Do not show paid because the query string says `success`
- [x] Do not title Tax Invoice; do not grant access on this pixel (NP-FUL-002 is the row)

## K16.3 Must not

- [x] No Hub IdentityBanner / “Use my Lazuar account”
- [x] No COMPLETED/ACTIVE/PENDING as the Pay enum — use `open`/`paid`/`expired`

## K16.4 Exit

- [x] Each state has a pixel
- [x] Unblocked for K19
