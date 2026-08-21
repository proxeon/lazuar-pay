# D12 — One connection string

**Track:** Database · **Depends:** D11  
**Analysis:** [03](../03-host-production-seams.md), [09](../09-data-migration.md)  
**Goal:** One name, bound in the host. Production cannot boot without it.  
**Lock:** `ConnectionStrings:Pay` (env `ConnectionStrings__Pay`). Not `Default` unless you amend this file.

---

## D12.1 Name

- [ ] Exactly one connection-string name: **`ConnectionStrings:Pay`**
- [ ] Document it in `apps/lazuar-pay/.env.example` (`Host=localhost;Port=5435;Database=lazuar_pay;…`)
- [ ] No `TenantConnection` / `MessagingConnection` / Hub trio
- [ ] Do not point this DSN at `lazuar` or `lazuar_mvp`

## D12.2 Bind

- [ ] Bind in the **host** (`apps/lazuar-pay/src/Lazuar.Pay`). No extra class library
- [ ] Production: **fail boot** if missing / empty
- [ ] Dev may no-op until D17 if memory store remains — **prefer fail** when `PAY_REQUIRE_DB=true`
- [ ] Do not copy Hub’s hand-rolled `.env` parser

## D12.3 Packages

- [ ] Npgsql (and EF only if D10 picked `PayDbContext`) on the **Pay** csproj
- [ ] Do not import Hub `Directory.Packages.props` / `Directory.Build.props`
- [ ] IsolationTests still ban MediatR

## D12.4 Exit

- [ ] Name is `Pay`, documented, Production fails closed
- [ ] Unblocked for D13 (D16 after D14)
