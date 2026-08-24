import { useEffect, useState } from 'react'
import { Navigate } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { pickApiBearerToken } from '../auth/bearerToken'
import { dashboardPath } from '../lib/homePath'
import { getWhoami } from '../lib/payApi'
import { CreateWorkspaceForm } from './CreateWorkspaceForm'

export function CreateWorkspacePage() {
  const auth = useAuth()
  const token = pickApiBearerToken(auth.user)
  const [redirect, setRedirect] = useState<string | null>(null)
  const [ready, setReady] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!token) {
      void auth.signinRedirect()
      return
    }
    const ac = new AbortController()
    getWhoami(token)
      .then((body) => {
        if (ac.signal.aborted) return
        if (body.tenants.length > 0) {
          const dash = dashboardPath(body.tenants)
          const orgId = dash.match(/^\/o\/([^/]+)/)?.[1]
          setRedirect(orgId ? `/o/${orgId}/new` : dash)
          return
        }
        setReady(true)
      })
      .catch((err: unknown) => {
        if (!ac.signal.aborted) setError(err instanceof Error ? err.message : 'whoami failed')
      })
    return () => ac.abort()
  }, [token, auth])

  if (error) {
    return (
      <main className="mx-auto max-w-lg p-6">
        <p role="alert" className="text-sm text-red-600">
          {error}
        </p>
      </main>
    )
  }

  if (redirect) return <Navigate to={redirect} replace />

  if (!token || !ready) {
    return <p className="p-6 text-sm text-slate-500">Loading…</p>
  }

  return (
    <div className="min-h-dvh bg-slate-50/80">
      <header className="flex h-14 items-center border-b border-slate-200/80 bg-white/90 px-6">
        <p className="text-sm font-semibold tracking-tight text-slate-900">Lazuar Pay</p>
      </header>
      <CreateWorkspaceForm token={token} />
    </div>
  )
}
