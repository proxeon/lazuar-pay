import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { pickApiBearerToken } from '../auth/bearerToken'
import { getWhoami, type Whoami } from '../lib/payApi'
import { setOrgHint } from '../lib/sessionKeys'

export function HomePage() {
  const auth = useAuth()
  const navigate = useNavigate()
  const token = pickApiBearerToken(auth.user)
  const [who, setWho] = useState<Whoami | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!token) {
      void auth.signinRedirect()
      return
    }
    const ac = new AbortController()
    getWhoami(token)
      .then((body) => {
        if (!ac.signal.aborted) setWho(body)
      })
      .catch((err: unknown) => {
        if (!ac.signal.aborted) {
          const message = err instanceof Error ? err.message : 'whoami failed'
          setError(message)
          if (message === 'unauthorized') {
            void auth.signinRedirect()
          }
        }
      })
    return () => ac.abort()
  }, [token, auth])

  if (error && error !== 'unauthorized') {
    return (
      <main>
        <h1>Whoami failed</h1>
        <p role="alert">{error}</p>
      </main>
    )
  }

  if (!who) {
    return <p>Loading workspaces…</p>
  }

  function openOrg(id: string) {
    setOrgHint(id)
    void navigate(`/o/${id}`)
  }

  return (
    <main>
      <p className="kicker">Lazuar Pay</p>
      <h1>Workspaces</h1>
      <p>
        {who.email ?? who.user_id}. One tenant id is Pay <code>org_id</code>.
      </p>
      {who.tenants.length === 0 ? (
        <p>
          No workspaces yet.{' '}
          <Link to="/workspaces/new">Create one in One</Link> (not a Pay org
          table).
        </p>
      ) : (
        <ul>
          {who.tenants.map((t) => (
            <li key={t.id}>
              <button type="button" onClick={() => openOrg(t.id)}>
                {t.name ?? t.slug ?? t.id}
              </button>{' '}
              <code>{t.role}</code> {t.status}
            </li>
          ))}
        </ul>
      )}
      <p>
        <Link to="/workspaces/new">Create workspace</Link>
      </p>
      <button type="button" onClick={() => void auth.signoutRedirect()}>
        Sign out
      </button>
    </main>
  )
}
