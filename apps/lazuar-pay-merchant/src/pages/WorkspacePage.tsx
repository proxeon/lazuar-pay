import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { pickApiBearerToken } from '../auth/bearerToken'
import { getWhoami, payApi, type WhoamiTenant } from '../lib/payApi'
import { canWriteMoney } from '../lib/roles'
import { setOrgHint } from '../lib/sessionKeys'

export function WorkspacePage() {
  const { orgId = '' } = useParams<{ orgId: string }>()
  const auth = useAuth()
  const token = pickApiBearerToken(auth.user)
  const [tenant, setTenant] = useState<WhoamiTenant | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    setOrgHint(orgId)
    if (!token) return
    getWhoami(token, orgId)
      .then((who) => {
        const match = who.tenants.find((t) => t.id === orgId) ?? null
        setTenant(match)
        if (!match) setError('Not a member of this org')
      })
      .catch((err: unknown) =>
        setError(err instanceof Error ? err.message : 'whoami failed'),
      )
  }, [orgId, token])

  const write = canWriteMoney(tenant?.role)

  return (
    <main>
      <p className="kicker">Lazuar Pay</p>
      <h1>{tenant?.name ?? orgId}</h1>
      <p>
        Role <code>{tenant?.role ?? '…'}</code>. Path org id is authorization
        SoT; the tenant header is a hint only.
      </p>
      {error && <p role="alert">{error}</p>}
      <ul>
        <li>
          Products {write ? '(create)' : '(read)'} — catalog lands in CAT13
        </li>
        <li>
          Gateway keys {write ? '(paste)' : '(hidden — member cannot change)'}
        </li>
        <li>Payments and receipts (member can see)</li>
      </ul>
      <p>
        Pay API <code>{payApi}</code>
      </p>
      <p>
        <Link to="/">All workspaces</Link>
      </p>
    </main>
  )
}
