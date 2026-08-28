import { useEffect, useState } from 'react'
import { Outlet, useLocation, useParams } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { pickApiBearerToken } from '../auth/bearerToken'
import { getWhoami, type Whoami, type WhoamiTenant } from '../lib/payApi'
import { canWriteMoney } from '../lib/roles'
import { setOrgHint, setReturnTo } from '../lib/sessionKeys'
import { DashboardChrome } from './DashboardChrome'

export type OrgOutletContext = {
  orgId: string
  tenant: WhoamiTenant
  who: Whoami
  token: string
  write: boolean
}

function titleFromPath(pathname: string, orgId: string) {
  const leaf = pathname.split('/').filter(Boolean).at(-1) ?? 'overview'
  if (leaf === orgId || leaf === 'overview') return 'Overview'
  if (leaf === 'gateway') return 'Processor'
  if (leaf === 'checkouts') return 'Pay links'
  if (leaf === 'payments') return 'Payments'
  if (leaf === 'receipts') return 'Receipts'
  if (leaf === 'new') return 'Create workspace'
  return leaf.charAt(0).toUpperCase() + leaf.slice(1)
}

export function OrgLayout() {
  const { orgId = '' } = useParams<{ orgId: string }>()
  const auth = useAuth()
  const location = useLocation()
  const token = pickApiBearerToken(auth.user)
  const [who, setWho] = useState<Whoami | null>(null)
  const [tenant, setTenant] = useState<WhoamiTenant | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    setOrgHint(orgId)
    if (!token) {
      setReturnTo(`${location.pathname}${location.search}`)
      void auth.signinRedirect()
      return
    }
    let stop = false
    getWhoami(token, orgId)
      .then((body) => {
        if (stop) return
        setWho(body)
        const match = body.tenants.find((t) => t.id === orgId) ?? null
        setTenant(match)
        setError(match ? null : 'Not a member of this org')
      })
      .catch((err: unknown) => {
        if (stop) return
        const message = err instanceof Error ? err.message : 'whoami failed'
        if (message === 'unauthorized') {
          setReturnTo(`${location.pathname}${location.search}`)
          void auth.signinRedirect()
          return
        }
        setError(message)
      })
    return () => {
      stop = true
    }
  }, [orgId, token, auth, location.pathname, location.search])

  const write = canWriteMoney(tenant?.role)

  if (error) {
    return (
      <div className="flex min-h-dvh items-center justify-center p-6">
        <p role="alert" className="text-sm text-red-600">
          {error}
        </p>
      </div>
    )
  }

  if (!token) {
    return (
      <div className="flex min-h-dvh items-center justify-center p-6 text-sm text-slate-500">
        Signing in…
      </div>
    )
  }

  if (!who || !tenant) {
    return (
      <div className="flex min-h-dvh items-center justify-center p-6 text-sm text-slate-500">
        Loading workspace…
      </div>
    )
  }

  const outlet: OrgOutletContext = { orgId, tenant, who, token, write }

  return (
    <DashboardChrome orgId={orgId} who={who} tenant={tenant} title={titleFromPath(location.pathname, orgId)}>
      <Outlet context={outlet} />
    </DashboardChrome>
  )
}
