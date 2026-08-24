# I11 — Do not call CreateHostedUrlAsync on resume

**Track:** Idempotent start · **Depends:** I10  
**Analysis:** P0-A; FakePsp `LastUri`  
**IDs:** —  
**Goal:** Idempotency is “no second HTTP to the PSP,” not only “return some URL.”

---

## I11.1

- [ ] When I10 hits the stored-URL branch, **do not** resolve `IHostedRail` create
- [ ] CHIP / Billplz / Xendit / Razorpay FakePsp send count stays 0 on the second POST
- [ ] Stripe.net is not called (no new `SessionService.CreateAsync`) — I16 is a belt on first create only

## I11.2 Must not

- [ ] Do not “verify the session still exists” with a GET to the PSP in this program
- [ ] Do not expire-and-recreate

## I11.3 Exit

- [ ] G14 asserts FakePsp send count == 1 after two starts (CHIP or Billplz fixture)
- [ ] Unblocked for I12
