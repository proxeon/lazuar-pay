# H16 — Laptop defaults only in Development settings

**Track:** H · **Depends:** H13, H14  
**Goal:** Base appsettings is not a laptop trap for Production images.

**Why:** H13/H14 fix C# defaults. This phase audits json files so a later “helpful” appsettings.Production.json does not re-introduce localhost.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/appsettings.json` | Open and list One/CS keys |
| `apps/lazuar-pay/src/Lazuar.Pay/appsettings.Development.json` | Should own laptop |
| `apps/lazuar-pay/src/Lazuar.Pay/Hosting/PayCors.cs` | Production empty CORS throws |
| G15 | Compose profile honesty |

**Current (`6d730d15`):** Defaults live in C# more than json (H13/H14).

---

## H16.1

- [ ] Audit `appsettings.json` vs `appsettings.Development.json` vs `appsettings.Production.json`
- [ ] One BaseUrl laptop default only in Development
- [ ] CORS Production already throws if empty — keep
- [ ] Compose `--profile apps` documented as laptop in G15; do not “fix” it by retargeting Hub compose

## H16.2 Exit

- [ ] Track H complete when H10–H16 checked
