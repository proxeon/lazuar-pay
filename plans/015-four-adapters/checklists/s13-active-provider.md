# S13 — org_settings.active_provider

**Track:** Schema · **Depends:** A00  
**Analysis:** [00](../00-what-must-be-done.md) §3.2 / §3.4  
**IDs:** NP-GW-007  
**Goal:** One rail the org charges with. Buyer has no dropdown.

---

## S13.1 Column

- [ ] Add nullable `ActiveProvider` on `OrgSettingsRow`
- [ ] Values: `stripe` | `chip` | `billplz` | `xendit` | `razorpay` or null (not configured)
- [ ] PUT `/v1/orgs/{orgId}/gateway` **sets** this to the provider just saved (P13)
- [ ] Public start **reads** this (P17) unless the checkout already has `Provider` from a previous start

## S13.2 Must not

- [ ] Do not pick `stripe` in code when the org only configured CHIP
- [ ] Do not store five “active” flags
- [ ] Do not put the picker on `:5179`

## S13.3 Exit

- [ ] Column on the row type
- [ ] Unblocked for P13
