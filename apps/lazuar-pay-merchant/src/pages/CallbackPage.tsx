import { Navigate } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { takeReturnTo } from '../lib/sessionKeys'

export function CallbackPage() {
  const auth = useAuth()

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

  if (auth.isLoading || !auth.isAuthenticated) {
    return <p>Completing sign-in…</p>
  }

  const returnTo = takeReturnTo()
  return <Navigate to={returnTo || '/'} replace />
}
