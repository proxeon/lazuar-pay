import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useOutletContext } from 'react-router-dom'
import {
  createPayWorkerKey,
  listApiKeys,
  revokeApiKey,
  type OneApiKey,
} from '../../lib/oneApi'
import type { OrgOutletContext } from '../../layout/OrgLayout'
import { PageCanvas, PageHeader } from '../../layout/PageHeader'
import { Button } from '../../ui/components/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '../../ui/components/dialog'
import { Input } from '../../ui/components/input'
import { Label } from '../../ui/components/label'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../../ui/components/table'

const SCOPE_LABEL: Record<string, string> = {
  'tenant:read': 'Workspace',
  'authz:check': 'Check permissions',
  'members:read': 'Members',
  'apps:read': 'Applications',
  'keys:read': 'API keys',
  'webhooks:read': 'Webhooks',
  'webhooks:write': 'Manage webhooks',
  admin: 'Workspace admin',
  '*': 'Everything',
}

function scopeLabel(id: string): string {
  return SCOPE_LABEL[id] ?? id
}

export function ApiKeysPage() {
  const { orgId, token, write, tenant } = useOutletContext<OrgOutletContext>()
  const suspended = tenant.status === 'suspended'
  const [items, setItems] = useState<OneApiKey[]>([])
  const [error, setError] = useState<string | null>(null)
  const [formError, setFormError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [loaded, setLoaded] = useState(false)
  const [formOpen, setFormOpen] = useState(false)
  const [name, setName] = useState('Pay worker')
  const [shownSecret, setShownSecret] = useState<string | null>(null)
  const [shownPrefix, setShownPrefix] = useState<string | null>(null)
  const [copied, setCopied] = useState(false)
  const secretInputRef = useRef<HTMLInputElement>(null)

  async function refresh(opts?: { ignore?: () => boolean }) {
    const rows = await listApiKeys(token, orgId)
    if (opts?.ignore?.()) return
    setItems(rows)
    setLoaded(true)
  }

  useEffect(() => {
    let stop = false
    setLoaded(false)
    setError(null)
    void refresh({ ignore: () => stop }).catch((err: unknown) => {
      if (stop) return
      setError(err instanceof Error ? err.message : 'One unreachable')
      setLoaded(true)
    })
    return () => {
      stop = true
    }
  }, [orgId, token])

  useEffect(() => {
    if (!shownSecret) return
    secretInputRef.current?.focus()
    secretInputRef.current?.select()
  }, [shownSecret])

  function openForm() {
    setFormError(null)
    setName('Pay worker')
    setFormOpen(true)
  }

  function closeForm() {
    if (busy) return
    setFormOpen(false)
    setFormError(null)
  }

  function closeSecret() {
    setShownSecret(null)
    setShownPrefix(null)
    setCopied(false)
  }

  async function onCreate(e: FormEvent) {
    e.preventDefault()
    if (!write || suspended) return
    const next = name.trim()
    if (!next) return
    setBusy(true)
    setFormError(null)
    setShownSecret(null)
    try {
      const created = await createPayWorkerKey(token, orgId, next)
      setFormOpen(false)
      setShownSecret(created.secret)
      setShownPrefix(created.prefix ?? null)
      await refresh()
    } catch (err: unknown) {
      setFormError(err instanceof Error ? err.message : 'One unreachable')
    } finally {
      setBusy(false)
    }
  }

  async function onRevoke(key: OneApiKey) {
    if (!write || suspended) return
    if (!window.confirm(`Revoke “${key.name}”? That secret stops working on Pay.`)) return
    setBusy(true)
    setError(null)
    try {
      await revokeApiKey(token, orgId, key.id)
      await refresh()
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'One unreachable')
    } finally {
      setBusy(false)
    }
  }

  async function copySecret() {
    if (!shownSecret) return
    try {
      await navigator.clipboard.writeText(shownSecret)
      setCopied(true)
      window.setTimeout(() => setCopied(false), 2000)
    } catch {
      setCopied(false)
      secretInputRef.current?.select()
    }
  }

  return (
    <PageCanvas>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <PageHeader subtitle="Your server sends this as Bearer to Pay. Workspace key (lzr_sk_), not a Stripe secret, not the webhook whsec_." />
        {loaded && write && !suspended ? (
          <Button type="button" onClick={openForm}>
            Create key
          </Button>
        ) : null}
        {!write ? <p className="text-sm text-slate-500">Member cannot mint keys.</p> : null}
      </div>

      {error ? (
        <p role="alert" className="text-sm text-red-600">
          {error}
        </p>
      ) : null}

      {!loaded ? (
        <p className="text-sm text-slate-500">Loading…</p>
      ) : error && items.length === 0 ? null : items.length === 0 ? (
        <div className="rounded-xl border border-slate-200 bg-white px-5 py-7">
          <p className="text-sm font-medium text-slate-900">No API keys yet.</p>
          <p className="mt-1 max-w-xl text-sm text-slate-500">
            Create a key when a backend needs to call Pay. One mints it; Pay does not store the secret. Copy it
            immediately.
          </p>
        </div>
      ) : (
        <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
          <Table>
            <TableHeader>
              <TableRow className="border-slate-200 bg-slate-50 hover:bg-slate-50">
                <TableHead className="h-11 w-[38%] px-4 text-[11px] font-medium uppercase tracking-wider text-slate-500">
                  Name
                </TableHead>
                <TableHead className="h-11 px-4 text-[11px] font-medium uppercase tracking-wider text-slate-500">
                  Access
                </TableHead>
                <TableHead className="h-11 px-4 text-[11px] font-medium uppercase tracking-wider text-slate-500">
                  Status
                </TableHead>
                <TableHead className="h-11 w-[1%] px-4 text-right text-[11px] font-medium uppercase tracking-wider text-slate-500">
                  <span className="sr-only">Actions</span>
                </TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {items.map((key) => (
                <TableRow key={key.id} className="border-slate-100">
                  <TableCell className="max-w-[280px] px-4 py-3.5">
                    <p className="truncate font-semibold text-slate-900">{key.name}</p>
                    <p className="mt-0.5 truncate font-mono text-xs text-slate-500">{key.prefix}</p>
                  </TableCell>
                  <TableCell className="px-4 py-3.5">
                    {(key.scopes?.length ?? 0) === 0 ? (
                      <span className="text-xs text-slate-500">No privileges</span>
                    ) : (
                      <div className="flex flex-wrap gap-1">
                        {key.scopes.map((id) => (
                          <span
                            key={id}
                            title={id}
                            className="inline-flex items-center rounded-md bg-slate-100 px-2 py-0.5 text-xs font-medium text-slate-800"
                          >
                            {scopeLabel(id)}
                          </span>
                        ))}
                      </div>
                    )}
                  </TableCell>
                  <TableCell className="px-4 py-3.5">
                    {key.revoked_at ? (
                      <span className="inline-flex rounded-full bg-red-50 px-2.5 py-0.5 text-[11px] font-semibold text-red-700">
                        Revoked
                      </span>
                    ) : (
                      <span className="inline-flex rounded-full bg-emerald-50 px-2.5 py-0.5 text-[11px] font-semibold text-emerald-800">
                        On
                      </span>
                    )}
                  </TableCell>
                  <TableCell className="px-4 py-3.5 text-right whitespace-nowrap">
                    {write && !key.revoked_at ? (
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        className="h-8 px-2 text-red-700 hover:bg-red-50 hover:text-red-800"
                        disabled={busy || suspended}
                        onClick={() => void onRevoke(key)}
                      >
                        Revoke
                      </Button>
                    ) : null}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      <Dialog
        open={formOpen}
        onOpenChange={(open) => {
          if (!open) closeForm()
          else setFormOpen(true)
        }}
      >
        <DialogContent>
          <form onSubmit={(e) => void onCreate(e)}>
            <DialogHeader>
              <DialogTitle>New key</DialogTitle>
              <DialogDescription>
                One mints a shown-once <code>lzr_sk_</code>. Scopes are Workspace and Check permissions — enough to
                call Pay. Not a Stripe secret.
              </DialogDescription>
            </DialogHeader>
            {formError ? (
              <p role="alert" className="mt-4 text-sm text-red-600">
                {formError}
              </p>
            ) : null}
            <div className="mt-4 space-y-2">
              <Label htmlFor="api_key_name">Name</Label>
              <Input
                id="api_key_name"
                value={name}
                onChange={(e) => setName(e.target.value)}
                required
                maxLength={200}
                placeholder="Pay worker"
                disabled={!write || busy || suspended}
              />
            </div>
            <DialogFooter className="mt-6">
              <Button type="button" variant="outline" onClick={closeForm} disabled={busy}>
                Cancel
              </Button>
              <Button type="submit" disabled={!write || busy || suspended || !name.trim()}>
                {busy ? 'Creating…' : 'Create key'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <Dialog
        open={shownSecret != null}
        onOpenChange={(open) => {
          if (!open) closeSecret()
        }}
      >
        <DialogContent showCloseButton={false} onInteractOutside={(e) => e.preventDefault()}>
          <DialogHeader>
            <DialogTitle>Save this API key</DialogTitle>
            <DialogDescription>Copy now. One will not show this again.</DialogDescription>
          </DialogHeader>
          <p className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-950">
            Store this on your server. Do not commit it, and do not put it in <code>VITE_*</code> or a browser app.
          </p>
          {shownPrefix ? (
            <p className="text-sm text-slate-500">
              Prefix <code className="font-mono text-slate-800">{shownPrefix}</code>
            </p>
          ) : null}
          <div className="space-y-2">
            <Label htmlFor="api_key_secret">Secret</Label>
            <div className="flex gap-2">
              <Input
                id="api_key_secret"
                ref={secretInputRef}
                readOnly
                value={shownSecret ?? ''}
                spellCheck={false}
                autoComplete="off"
                className="font-mono text-xs"
                onFocus={(e) => e.currentTarget.select()}
              />
              <Button type="button" variant="outline" onClick={() => void copySecret()}>
                {copied ? 'Copied' : 'Copy'}
              </Button>
            </div>
          </div>
          <DialogFooter>
            <Button type="button" onClick={closeSecret}>
              Done
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </PageCanvas>
  )
}
