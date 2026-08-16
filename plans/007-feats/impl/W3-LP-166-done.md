# W3-LP-166 — done

Workspace invite Role is allow-listed `ADMIN|MEMBER|VIEWER`. Inviter must be membership ADMIN or SUPER_ADMIN (or platform system admin). Policies: `OrgAdmin` unchanged; `OrgMember` = SUPER_ADMIN|ADMIN|MEMBER; `OrgRead` = those plus VIEWER. Commerce admin GETs are OrgRead; mutations OrgMember; payment-config + subscriber anonymize stay OrgAdmin. Workspace invite/remove are OrgAdmin. Ops Team page under Workspace.

## Files

- `InviteUserToWorkspaceCommandHandler` + `WorkspaceStaffRoles`
- `AuthAndCorsExtensions` OrgMember / OrgRead
- Commerce endpoint policy split
- `TenantSecurityMiddleware` injects membership role when X-Tenant-Id is present
- Ops `TeamPage`

## Tests

- Invite allow-list; MEMBER cannot invite; MEMBER policy vs payment-config OrgAdmin; VIEWER cannot refund (OrgMember)

Not committed. Not pushed.

Tracker `LP-166` **P → Y**.
