import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { pickApiBearerToken } from './bearerToken'

export function RequireAuth({ children }: { children: ReactNode }) {
  const auth = useAuth()
  const location = useLocation()

  if (auth.isLoading) {
    return <p>Checking session…</p>
  }

  if (auth.error) {
    return (
      <div role="alert">
        <p>{auth.error.message}</p>
        <button type="button" onClick={() => void auth.signinRedirect()}>
          Retry login
        </button>
      </div>
    )
  }

  if (!auth.isAuthenticated) {
    return (
      <Navigate
        to="/login"
        replace
        state={{
          from: { pathname: location.pathname, search: location.search },
        }}
      />
    )
  }

  if (!pickApiBearerToken(auth.user)) {
    return (
      <div role="alert" className="mx-auto max-w-lg space-y-3 p-6">
        <p className="text-sm text-red-600">
          This session has no JWT access token. Pay cannot call the API. Sign in
          again.
        </p>
        <button
          type="button"
          className="rounded-md bg-sky-600 px-4 py-2 text-sm font-medium text-white"
          onClick={() => void auth.signinRedirect()}
        >
          Sign in
        </button>
      </div>
    )
  }

  return children
}
