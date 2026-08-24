# B23 — Do not port PublicDnsFallback

**Track:** Billplz · **Depends:** B10  
**Analysis:** [00](../00-what-must-be-done.md) §5.2 / §9  
**IDs:** —  
**Goal:** 193-line Hub DNS folklore stays museum unless CHIP/Billplz actually fail to resolve **from this host**.

---

## B23.1

- [x] Grep Pay src for `PublicDnsFallback`, `lazuar-local-dev.com`, custom `Dns.GetHostEntry` — none
- [x] Standard `HttpClient` to `www.billplz.com` / sandbox
- [x] If DNS actually fails in dogfood, **amend A00** before adding a tiny handler — do not sneak it into B13

## B23.2 Exit

- [x] Grep clean
- [x] parked-dns-fallback remains parked
