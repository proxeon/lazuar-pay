# Parked — Silent CHIP webhook registrar

**Do not start in 015.**  
**Analysis:** [00](../00-what-must-be-done.md) §5.1; C28; Hub `ChipWebhookRegistrar.cs`

---

- [ ] Do not `POST /webhooks/` into Ada’s CHIP account on PUT or boot
- [ ] Dashboard PEM paste is the 015 path
- [ ] A later **explicit** merchant button “register webhook” may steal list-before-create HTTP — not silent
- [ ] Public HTTPS predicate from Billplz still applies if that button ever exists
