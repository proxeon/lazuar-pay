# Parked — PublicDnsFallback

**Do not start in 016.**  
**Analysis:** [`../06-billplz-crosscheck.md`](../06-billplz-crosscheck.md); 015 `parked-dns-fallback.md`

---

- [ ] Do not port `PublicDnsFallback` / named client ConnectCallback
- [ ] Do not rewrite localhost to `lazuar-local-dev.com`
- [ ] Billplz create stays fail-closed on loopback / that host (fb15 tests it)
- [ ] If `www.billplz.com` actually fails DNS from this host, **amend A00** — do not copy 193 Hub lines “just in case”
- [ ] S17 greps the type name `PublicDnsFallback`. Do **not** grep `lazuar-local-dev.com` (block list contains it)
