# C28 — No silent CHIP webhook registrar

**Track:** CHIP · **Depends:** C11  
**Analysis:** [00](../00-what-must-be-done.md) §5.1 / §9  
**IDs:** —  
**Goal:** Do not `POST https://gate.chip-in.asia/api/v1/webhooks/` on PUT or boot.

---

## C28.1

- [ ] Grep Pay src for `/webhooks/` toward `gate.chip-in.asia` — none
- [ ] Do not port `ChipWebhookRegistrar.cs`
- [ ] Merchant pastes PEM from CHIP dashboard (U12 copy)
- [ ] Optional later: explicit “register webhook” **button** that the merchant clicks — **not this program** (parked-chip-registrar)

## C28.2 Exit

- [ ] Grep clean
- [ ] Unblocked for parked-chip-registrar to remain parked
