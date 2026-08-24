# Parked — PublicDnsFallback

**Do not start in 015.**  
**Analysis:** [00](../00-what-must-be-done.md) §5.2; B23; Hub `PublicDnsFallback.cs`

---

- [ ] Do not port `lazuar-local-dev.com` / custom DNS
- [ ] If `www.billplz.com` actually fails to resolve from the Pay host, amend A00 and add a **tiny** handler — do not copy 193 lines “just in case”
- [ ] Billplz localhost callback stays 400 (B15)
