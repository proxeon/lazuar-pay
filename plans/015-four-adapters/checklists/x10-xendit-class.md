# X10 — XenditHosted class

**Track:** Xendit · **Depends:** P27, H12  
**Analysis:** [00](../00-what-must-be-done.md) §5.3  
**IDs:** NP-LAT-002  
**Goal:** Hosted invoice wrap. Reminder-only. Hub `XenditGatewayAdapter.cs` judgment only.

---

## X10.1

- [ ] `Gateways/XenditHosted.cs`, `Provider = "xendit"`
- [ ] HttpClient to `https://api.xendit.co`
- [ ] `CreateHostedUrlAsync` returns `invoice_url`
- [ ] No xenPlatform, no off-session, no refunds this program

## X10.2 Exit

- [ ] Class compiles
- [ ] Unblocked for X11
