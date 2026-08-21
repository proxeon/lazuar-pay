import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { pickApiBearerToken } from '../auth/bearerToken'
import { createTenant } from '../lib/oneApi'
import { setOrgHint } from '../lib/sessionKeys'

export function CreateWorkspacePage() {
  const auth = useAuth()
  const navigate = useNavigate()
  const token = pickApiBearerToken(auth.user)
  const [name, setName] = useState('')
  const [slug, setSlug] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    if (!token) {
      void auth.signinRedirect()
      return
    }
    setBusy(true)
    setError(null)
    try {
      const tenant = await createTenant(token, name.trim(), slug.trim())
      setOrgHint(tenant.id)
      void navigate(`/o/${tenant.id}`)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'create failed')
    } finally {
      setBusy(false)
    }
  }

  return (
    <main>
      <p className="kicker">Lazuar Pay</p>
      <h1>Create workspace</h1>
      <p>
        Calls One <code>POST /tenants</code>. The tenant id becomes Pay{' '}
        <code>org_id</code>. No Pay organizations table.
      </p>
      <form onSubmit={(e) => void onSubmit(e)}>
        <p>
          <label>
            Name{' '}
            <input
              value={name}
              onChange={(ev) => setName(ev.target.value)}
              required
              autoComplete="organization"
            />
          </label>
        </p>
        <p>
          <label>
            Slug{' '}
            <input
              value={slug}
              onChange={(ev) => setSlug(ev.target.value)}
              required
              pattern="[a-z0-9-]{1,64}"
            />
          </label>
        </p>
        {error && <p role="alert">{error}</p>}
        <button type="submit" disabled={busy}>
          Create
        </button>
      </form>
      <p>
        <Link to="/">Back</Link>
      </p>
    </main>
  )
}
