# X10 — XenditHosted class

**Track:** Xendit · **Depends:** P27, H12  
**Analysis:** [00](../00-what-must-be-done.md) §5.3  
**IDs:** NP-LAT-002  
**Goal:** Hosted invoice wrap. Reminder-only. Hub `XenditGatewayAdapter.cs` judgment only.

---

## X10.1

- [x] `Gateways/XenditHosted.cs`, `Provider = "xendit"`
- [x] HttpClient to `https://api.xendit.co`
- [x] `CreateHostedUrlAsync` returns `invoice_url`
- [x] No xenPlatform, no off-session, no refunds this program

## X10.2 Exit

- [x] Class compiles
- [x] Unblocked for X11
