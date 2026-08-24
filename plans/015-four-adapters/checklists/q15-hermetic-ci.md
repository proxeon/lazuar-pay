# Q15 — CI stays hermetic

**Track:** Q · **Depends:** H21  
**Analysis:** [00](../00-what-must-be-done.md) §7  
**IDs:** —  
**Goal:** GitHub `pay` job does not call CHIP/Billplz/Xendit/Razorpay/Zitadel.

---

## Q15.1

- [ ] Tests use HttpMessageHandler / signed fixtures
- [ ] No live `gate.chip-in.asia` in CI
- [ ] IsolationTests still run

## Q15.2 Exit

- [ ] CI green without PSP credentials
