# P18 — OpenTelemetry (parked)

**Track:** Parked  
**Analysis:** [`../06-host-production.md`](../06-host-production.md) §8 / §13.2 P2  
**Unpark when:** A public URL and a backup exist (K99b).

**Why parked:** Almost no logging today (good — no body leak). Adding Serilog “to look finished” is how CS/passwords hit logs (`MigrateAsync` `LogError(ex)` is the realistic leak).

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Program.cs` | Migrate `LogError` |
| `apps/lazuar-pay/src/Lazuar.Pay/Lazuar.Pay.csproj` | No OTel packages (verify) |
| Hub BuildingBlocks metrics | Isolation refuse |

**Current (`6d730d15`):** No OTel. Health is liveness.

---

## P18.1 When unparking

- [x] Console JSON; request id; route; status; duration; org **after** authz
- [x] No headers, bodies, CS, `whsec_`
- [x] Redact Npgsql exceptions
