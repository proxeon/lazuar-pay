# S13 — org_settings.active_provider

**Track:** Schema · **Depends:** A00  
**Analysis:** [00](../00-what-must-be-done.md) §3.2 / §3.4  
**IDs:** NP-GW-007  
**Goal:** One rail the org charges with. Buyer has no dropdown.

---

## S13.1 Column

- [x] Add nullable `ActiveProvider` on `OrgSettingsRow`
- [x] Values: `stripe` | `chip` | `billplz` | `xendit` | `razorpay` or null (not configured)
- [x] PUT `/v1/orgs/{orgId}/gateway` **sets** this to the provider just saved (P13)
- [x] Public start **reads** this (P17) unless the checkout already has `Provider` from a previous start

## S13.2 Must not

- [x] Do not pick `stripe` in code when the org only configured CHIP
- [x] Do not store five “active” flags
- [x] Do not put the picker on `:5179`

## S13.3 Exit

- [x] Column on the row type
- [x] Unblocked for P13
