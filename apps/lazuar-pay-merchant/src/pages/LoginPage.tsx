import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { setReturnTo } from '../lib/sessionKeys'

export function LoginPage() {
  const auth = useAuth()
  const location = useLocation()
  const fromLoc = (
    location.state as { from?: { pathname?: string; search?: string } } | null
  )?.from
  const from = `${fromLoc?.pathname || '/'}${fromLoc?.search || ''}`

  if (auth.isLoading) {
    return <p>Loading…</p>
  }

  if (auth.isAuthenticated) {
    return <Navigate to={from} replace />
  }

  const clientConfigured = Boolean(import.meta.env.VITE_ZITADEL_CLIENT_ID)

  function startSignIn() {
    if (from && from !== '/') {
      setReturnTo(from)
    }
    void auth.signinRedirect()
  }

  return (
    <main className="mx-auto max-w-lg space-y-4 p-6">
      <p className="text-xs font-semibold uppercase tracking-wider text-slate-500">Lazuar Pay</p>
      <h1 className="text-2xl font-semibold tracking-tight">Merchant</h1>
      <p className="text-sm text-slate-600">
        Sign-in uses One product login at <code>:5175</code>. This page is not a
        password form. Not <code>lazuar-ops</code> (<code>:3003</code>), not
        staff admin (<code>:5173</code>).
      </p>
      {!clientConfigured && (
        <p role="alert" className="text-sm text-red-600">
          Missing <code>VITE_ZITADEL_CLIENT_ID</code>. Register the SPA with{' '}
          <code>scripts/register-spa.sh</code>.
        </p>
      )}
      <button
        type="button"
        className="rounded-md bg-sky-600 px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
        onClick={startSignIn}
        disabled={!clientConfigured}
      >
        Sign in
      </button>
    </main>
  )
}
