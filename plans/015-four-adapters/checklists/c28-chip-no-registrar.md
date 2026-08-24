# C28 — No silent CHIP webhook registrar

**Track:** CHIP · **Depends:** C11  
**Analysis:** [00](../00-what-must-be-done.md) §5.1 / §9  
**IDs:** —  
**Goal:** Do not `POST https://gate.chip-in.asia/api/v1/webhooks/` on PUT or boot.

---

## C28.1

- [x] Grep Pay src for `/webhooks/` toward `gate.chip-in.asia` — none
- [x] Do not port `ChipWebhookRegistrar.cs`
- [x] Merchant pastes PEM from CHIP dashboard (U12 copy)
- [x] Optional later: explicit “register webhook” **button** that the merchant clicks — **not this program** (parked-chip-registrar)

## C28.2 Exit

- [x] Grep clean
- [x] Unblocked for parked-chip-registrar to remain parked
