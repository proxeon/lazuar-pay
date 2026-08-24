import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { createTenant } from '../lib/oneApi'
import { setOrgHint } from '../lib/sessionKeys'
import { PageCanvas, PageHeader } from '../layout/PageHeader'
import { Button } from '../ui/components/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../ui/components/card'
import { Input } from '../ui/components/input'
import { Label } from '../ui/components/label'

export function slugFromName(name: string): string {
  return name
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 64)
}

export function CreateWorkspaceForm({ token }: { token: string }) {
  const navigate = useNavigate()
  const [name, setName] = useState('')
  const [slug, setSlug] = useState('')
  const [slugTouched, setSlugTouched] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  function onName(value: string) {
    setName(value)
    if (!slugTouched) setSlug(slugFromName(value))
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      const tenant = await createTenant(token, name.trim(), slug.trim())
      setOrgHint(tenant.id)
      void navigate(`/o/${tenant.id}/overview`)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'create failed')
    } finally {
      setBusy(false)
    }
  }

  return (
    <PageCanvas>
      <PageHeader
        title="Create workspace"
        subtitle="One tenant id becomes Pay org_id. Pay does not store organizations."
      />
      <Card>
        <CardHeader>
          <CardTitle>Workspace</CardTitle>
          <CardDescription>Name is what staff see. Slug is the URL-safe handle in One.</CardDescription>
        </CardHeader>
        <CardContent>
          <form className="space-y-4" onSubmit={(e) => void onSubmit(e)}>
            <div className="space-y-2">
              <Label htmlFor="workspace_name">Name</Label>
              <Input
                id="workspace_name"
                value={name}
                onChange={(ev) => onName(ev.target.value)}
                required
                autoComplete="organization"
                placeholder="Acme"
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="workspace_slug">Slug</Label>
              <Input
                id="workspace_slug"
                value={slug}
                onChange={(ev) => {
                  setSlugTouched(true)
                  setSlug(ev.target.value)
                }}
                required
                pattern="[a-z0-9\\-]{1,64}"
                placeholder="acme"
              />
              <p className="text-xs text-slate-500">Lowercase letters, numbers, and hyphens. Max 64.</p>
            </div>
            {error ? (
              <p role="alert" className="text-sm text-red-600">
                {error}
              </p>
            ) : null}
            <Button type="submit" disabled={busy}>
              Create workspace
            </Button>
          </form>
        </CardContent>
      </Card>
    </PageCanvas>
  )
}
