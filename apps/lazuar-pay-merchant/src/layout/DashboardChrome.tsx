import { useEffect, useState, type ReactNode } from 'react'
import { Menu } from 'lucide-react'
import { useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import type { Whoami, WhoamiTenant } from '../lib/payApi'
import { staffDisplay } from '../lib/staffDisplay'
import { AppSidebar } from '../ui/components/app-sidebar'
import { useIsMobile } from '../ui/hooks/use-mobile'
import { getPayNavGroups } from './nav'
import { WorkspaceSwitcher } from './WorkspaceSwitcher'

export function DashboardChrome({
  orgId,
  who,
  tenant,
  title,
  children,
}: {
  orgId: string
  who: Whoami
  tenant: WhoamiTenant
  title: string
  children: ReactNode
}) {
  const auth = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const isMobile = useIsMobile()
  const [sidebarOpen, setSidebarOpen] = useState(!isMobile)

  useEffect(() => {
    setSidebarOpen(!isMobile)
  }, [isMobile])

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
          ...staffDisplay(who, auth.user),
          roleLabel: tenant.role ?? 'member',
        }}
        onLogout={() => void auth.signoutRedirect()}
        onSettingsClick={() => navigate(`/o/${orgId}/gateway`)}
        header={<WorkspaceSwitcher orgId={orgId} tenants={who.tenants} />}
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
          <h1 className="text-base font-semibold tracking-tight text-slate-900">{title}</h1>
        </header>
        <main className="flex-1 overflow-y-auto bg-slate-50/80">{children}</main>
      </div>
    </div>
  )
}
