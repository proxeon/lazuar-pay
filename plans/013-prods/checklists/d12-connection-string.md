# D12 — One connection string

**Track:** Database · **Depends:** D11  
**Analysis:** [03](../03-host-production-seams.md), [09](../09-data-migration.md)  
**Goal:** One name, bound in the host. Production cannot boot without it.  
**Lock:** `ConnectionStrings:Pay` (env `ConnectionStrings__Pay`). Not `Default` unless you amend this file.

---

## D12.1 Name

- [x] Exactly one connection-string name: **`ConnectionStrings:Pay`**
- [x] Document it in `apps/lazuar-pay/.env.example` (`Host=localhost;Port=5435;Database=lazuar_pay;…`)
- [x] No `TenantConnection` / `MessagingConnection` / Hub trio
- [x] Do not point this DSN at `lazuar` or `lazuar_mvp`

## D12.2 Bind

- [x] Bind in the **host** (`apps/lazuar-pay/src/Lazuar.Pay`). No extra class library
- [x] Production: **fail boot** if missing / empty
- [x] Dev may no-op until D17 if memory store remains — **prefer fail** when `PAY_REQUIRE_DB=true`
- [x] Do not copy Hub’s hand-rolled `.env` parser

## D12.3 Packages

- [x] Npgsql (and EF only if D10 picked `PayDbContext`) on the **Pay** csproj
- [x] Do not import Hub `Directory.Packages.props` / `Directory.Build.props`
- [x] IsolationTests still ban MediatR

## D12.4 Exit

- [x] Name is `Pay`, documented, Production fails closed
- [x] Unblocked for D13 (D16 after D14)
