# T17 — No LHDN / UBL in focused Pay

**Track:** Tax · **Depends:** A00  
**Analysis:** [00](../00-what-must-be-done.md) §4  
**IDs:** NP-XX-001  
**Goal:** Keep homemade MyInvois out of `apps/lazuar-pay`.

---

## T17.1 Grep

- [x] Grep `apps/lazuar-pay/src` for `Lhdn`, `MyInvois`, `UBL`, `XAdES`, `Irbm` — no matches
- [x] `Lazuar.Pay.csproj` has no LHDN SDK package
- [x] IsolationTests still ban `Modules.` (covers `Modules.Lhdn`)

## T17.2 Must not

- [x] Do not add a `tax_documents` table that pretends to be LHDN
- [x] Do not ProjectReference `packages/lhdn-sdk-dotnet`

## T17.3 Exit

- [x] Grep clean
- [x] Unblocked for A99 tax clause
