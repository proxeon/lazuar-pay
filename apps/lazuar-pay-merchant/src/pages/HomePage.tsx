import { useEffect, useState } from 'react'
import { Navigate } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { pickApiBearerToken } from '../auth/bearerToken'
import { orgIdFromPath, resolvePostLoginPath } from '../lib/homePath'
import { getWhoami } from '../lib/payApi'
import { setOrgHint, takeReturnTo } from '../lib/sessionKeys'

export function HomePage() {
  const auth = useAuth()
  const token = pickApiBearerToken(auth.user)
  const [path, setPath] = useState<string | null>(null)
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
        const next = resolvePostLoginPath(takeReturnTo(), body.tenants)
        const id = orgIdFromPath(next)
        if (id) setOrgHint(id)
        setPath(next)
      })
      .catch((err: unknown) => {
        if (!ac.signal.aborted) {
          const message = err instanceof Error ? err.message : 'whoami failed'
          setError(message)
          if (message === 'unauthorized') void auth.signinRedirect()
        }
      })
    return () => ac.abort()
  }, [token, auth])

  if (error && error !== 'unauthorized') {
    return (
      <main className="mx-auto max-w-lg space-y-4 p-6">
        <h1 className="text-2xl font-semibold tracking-tight">Whoami failed</h1>
        <p role="alert" className="text-sm text-red-600">
          {error}
        </p>
      </main>
    )
  }

  if (!path) {
    return (
      <p className="p-6 text-sm text-slate-500">Opening workspace…</p>
    )
  }

  return <Navigate to={path} replace />
}
