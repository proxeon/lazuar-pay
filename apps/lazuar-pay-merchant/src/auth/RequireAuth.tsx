import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'

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

  return children
}
