# Q15 — CI stays hermetic

**Track:** Q · **Depends:** H21  
**Analysis:** [00](../00-what-must-be-done.md) §7  
**IDs:** —  
**Goal:** GitHub `pay` job does not call CHIP/Billplz/Xendit/Razorpay/Zitadel.

---

## Q15.1

- [x] Tests use HttpMessageHandler / signed fixtures
- [x] No live `gate.chip-in.asia` in CI
- [x] IsolationTests still run

## Q15.2 Exit

- [x] CI green without PSP credentials
