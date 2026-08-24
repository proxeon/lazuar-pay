# K18 — Lock 400 does not mark paid

**Track:** Checkout · **Depends:** K11  
**Analysis:** 09 method 72  
**Goal:** 400 handling exists and does not set status paid.

---

- [ ] Grep 400 branch; must not assign `status: 'paid'`
- [ ] Optional: grep that conflated Billplz+email sentence is **gone** after K11
- [ ] Exit: green
