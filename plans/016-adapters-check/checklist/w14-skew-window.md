# W14 — Reject stale `t`

**Track:** One HMAC · **Depends:** W11  
**Analysis:** One `toleranceSeconds = 300`  
**IDs:** —  
**Goal:** Replay of an old signed suspend must die.

---

## W14.1

- [ ] If `|now - t| > 300` seconds → 401 Invalid HMAC
- [ ] Use UTC unix. Inject clock only in tests if needed (`nowUnixSeconds` like One)

## W14.2 Must not

- [ ] Do not skip skew in Production
- [ ] Tests may pass `nowUnixSeconds` equal to fixture `t`

## W14.3 Exit

- [ ] W23 includes a stale-`t` 401 **or** a dedicated `OneWebhookTests.Stale_timestamp_is_401`
