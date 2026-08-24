import { useEffect, useState } from 'react'
import { Menu } from 'lucide-react'
import { Outlet, useLocation, useNavigate, useParams } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { pickApiBearerToken } from '../auth/bearerToken'
import { getWhoami, type Whoami, type WhoamiTenant } from '../lib/payApi'
import { canWriteMoney } from '../lib/roles'
import { setOrgHint } from '../lib/sessionKeys'
import {
  AppSidebar,
  AppSidebarLocationHeader,
} from '../ui/components/app-sidebar'
import { useIsMobile } from '../ui/hooks/use-mobile'
import { getPayNavGroups } from './nav'

export type OrgOutletContext = {
  orgId: string
  tenant: WhoamiTenant
  who: Whoami
  token: string
  write: boolean
}

export function OrgLayout() {
  const { orgId = '' } = useParams<{ orgId: string }>()
  const auth = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const token = pickApiBearerToken(auth.user)
  const isMobile = useIsMobile()
  const [sidebarOpen, setSidebarOpen] = useState(!isMobile)
  const [who, setWho] = useState<Whoami | null>(null)
  const [tenant, setTenant] = useState<WhoamiTenant | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    setSidebarOpen(!isMobile)
  }, [isMobile])

  useEffect(() => {
    setOrgHint(orgId)
    if (!token) return
    getWhoami(token, orgId)
      .then((body) => {
        setWho(body)
        const match = body.tenants.find((t) => t.id === orgId) ?? null
        setTenant(match)
        setError(match ? null : 'Not a member of this org')
      })
      .catch((err: unknown) => setError(err instanceof Error ? err.message : 'whoami failed'))
  }, [orgId, token])

  const write = canWriteMoney(tenant?.role)

  function titleFromPath() {
    const leaf = location.pathname.split('/').filter(Boolean).at(-1) ?? 'overview'
    if (leaf === orgId) return 'Overview'
    return leaf.charAt(0).toUpperCase() + leaf.slice(1)
  }

  if (error) {
    return (
      <div className="flex min-h-dvh items-center justify-center p-6">
        <p role="alert" className="text-sm text-red-600">
          {error}
        </p>
      </div>
    )
  }

  if (!who || !tenant || !token) {
    return (
      <div className="flex min-h-dvh items-center justify-center p-6 text-sm text-slate-500">
        Loading workspace…
      </div>
    )
  }

  const outlet: OrgOutletContext = { orgId, tenant, who, token, write }

  return (
    <div className="flex h-dvh overflow-hidden bg-white text-slate-900">
      {isMobile && sidebarOpen ? (
        <div
          className="fixed inset-0 z-30 bg-black/50 backdrop-blur-sm"
          onClick={() => setSidebarOpen(false)}
        />
      ) : null}

      <AppSidebar
        isOpen={sidebarOpen}
        onClose={() => setSidebarOpen(false)}
        pathname={location.pathname}
        onNavigate={(path) => navigate(path)}
        navGroups={getPayNavGroups(orgId)}
        user={{
          name: who.email ?? who.user_id,
          email: who.email ?? null,
          roleLabel: tenant.role ?? 'member',
        }}
        onLogout={() => void auth.signoutRedirect()}
        onSettingsClick={() => navigate(`/o/${orgId}/gateway`)}
        header={
          <AppSidebarLocationHeader
            orgName="Lazuar Pay"
            locationName={tenant.name ?? tenant.slug ?? orgId}
          />
        }
      />

      <div className="relative flex min-w-0 flex-1 flex-col overflow-hidden">
        <header className="flex h-14 shrink-0 items-center gap-2 border-b border-slate-200/80 bg-white/90 px-4 backdrop-blur md:px-6">
          <button
            type="button"
            className="inline-flex h-9 w-9 items-center justify-center rounded-md text-slate-700 hover:bg-slate-100 md:hidden"
            onClick={() => setSidebarOpen(true)}
            aria-label="Open navigation"
          >
            <Menu className="h-5 w-5" />
          </button>
          <h1 className="text-base font-semibold tracking-tight text-slate-900">{titleFromPath()}</h1>
        </header>
        <main className="flex-1 overflow-y-auto bg-slate-50/80">
          <Outlet context={outlet} />
        </main>
      </div>
    </div>
  )
}
