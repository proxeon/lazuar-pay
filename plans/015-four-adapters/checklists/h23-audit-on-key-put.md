# H23 — Audit row on gateway PUT

**Track:** Harden · **Depends:** S17  
**Analysis:** [00](../00-what-must-be-done.md); 011 NP-AUD-003  
**IDs:** NP-AUD-003  
**Goal:** Key paste is a money-adjacent write. Same DB as credentials.

---

## H23.1 Live

- [ ] `Fulfillment` already inserts `audit_events` `checkout.paid` in its TX
- [ ] `GatewayEndpoints.Put` currently does not
- [ ] After successful Protect + SaveChanges, insert `AuditEventRow` `{ Action = "gateway.credentials.upsert", OrgId, At = UtcNow }`
- [ ] Same SaveChanges as the credential row (one commit)
- [ ] Do **not** put last4 or secret in the audit row

## H23.2 Must not

- [ ] Do not stand up `lazuar-audit` as a process
- [ ] Do not log ciphertext

## H23.3 Exit

- [ ] Hermetic: PUT then `AuditEvents` contains `gateway.credentials.upsert` for that org
