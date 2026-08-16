# W3-LP-167 — done

`one.AuditEvents` plus fire-and-forget `IAuditRecorder` (never fails the money path). Recorded after success: refund.created, subscriber.canceled, subscriber.payment_recorded, member.invited, member.removed, api_key.created, api_key.revoked. GET `/one/workspaces/{id}/audit` is IDOR-safe (membership or system admin). Ops Audit log under Workspace. Metadata has no API keys.

## Files

- `AuditEvent` + migration `20260820150000_AddAuditEvents`
- `IAuditRecorder` / `AuditRecorder`
- Hooks on refund / cancel / record-payment / invite / remove / api key
- Ops `AuditLogPage`

## Tests

- Invite records `member.invited`; foreign org GET 403; recorder throw does not propagate

Not committed. Not pushed.

Tracker `LP-167` **N → Y**.
