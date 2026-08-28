import { useEffect, useState } from 'react'
import { Navigate } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { pickApiBearerToken } from '../auth/bearerToken'
import { orgIdFromPath, resolvePostLoginPath } from '../lib/homePath'
import { getWhoami } from '../lib/payApi'
import { setOrgHint, takeReturnToOnce } from '../lib/sessionKeys'

export function CallbackPage() {
  const auth = useAuth()
  const token = pickApiBearerToken(auth.user)
  const [path, setPath] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (auth.isLoading || auth.error || !auth.isAuthenticated) return
    if (!token) {
      void auth.signinRedirect()
      return
    }
    let stop = false
    const stored = takeReturnToOnce()
    getWhoami(token)
      .then((body) => {
        if (stop) return
        const next = resolvePostLoginPath(stored, body.tenants)
        const id = orgIdFromPath(next)
        if (id) setOrgHint(id)
        setPath(next)
      })
      .catch((err: unknown) => {
        if (stop) return
        const message = err instanceof Error ? err.message : 'whoami failed'
        if (message === 'unauthorized') {
          void auth.signinRedirect()
          return
        }
        setError(message)
      })
    return () => {
      stop = true
    }
  }, [auth, token])

  if (auth.error) {
    return (
      <main>
        <h1>Login failed</h1>
        <p role="alert">{auth.error.message}</p>
        <button type="button" onClick={() => void auth.signinRedirect()}>
          Try again
        </button>
      </main>
    )
  }

  if (error) {
    return (
      <main className="mx-auto max-w-lg space-y-4 p-6">
        <h1 className="text-2xl font-semibold tracking-tight">Whoami failed</h1>
        <p role="alert" className="text-sm text-red-600">
          {error}
        </p>
        <button type="button" onClick={() => void auth.signinRedirect()}>
          Try again
        </button>
      </main>
    )
  }

  if (auth.isLoading || !auth.isAuthenticated || !path) {
    return <p>Completing sign-in…</p>
  }

  return <Navigate to={path} replace />
}
