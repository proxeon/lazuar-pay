import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useOutletContext } from 'react-router-dom'
import { problemDetail } from '../../lib/http'
import { payFetch, payJson } from '../../lib/payApi'
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

type OrgWebhookView = {
  org_id: string
  url?: string
  webhook_configured: boolean
  secret_prefix?: string
  webhook_secret?: string
}

const PAY_EVENTS: { id: string; label: string }[] = [
  { id: 'payment.completed', label: 'Paid' },
  { id: 'payment.failed', label: 'Failed' },
  { id: 'checkout.expired', label: 'Expired' },
  { id: 'refund.created', label: 'Refund' },
]

function truncateUrl(url: string, max = 48): string {
  if (url.length <= max) return url
  const keep = Math.floor((max - 1) / 2)
  return `${url.slice(0, keep)}…${url.slice(-keep)}`
}

function endpointName(url: string): string {
  try {
    return new URL(url).host
  } catch {
    return truncateUrl(url)
  }
}

export function WebhooksPage() {
  const { orgId, token, write } = useOutletContext<OrgOutletContext>()
  const [url, setUrl] = useState('')
  const [configured, setConfigured] = useState(false)
  const [prefix, setPrefix] = useState<string | null>(null)
  const [shownSecret, setShownSecret] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [formError, setFormError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [loaded, setLoaded] = useState(false)
  const [formOpen, setFormOpen] = useState(false)
  const [draftUrl, setDraftUrl] = useState('')
  const [copied, setCopied] = useState(false)
  const secretInputRef = useRef<HTMLInputElement>(null)

  async function refresh() {
    const body = await payJson<OrgWebhookView>(token, `/v1/orgs/${orgId}/webhooks`, { orgHint: orgId })
    setConfigured(body.webhook_configured)
    setUrl(body.url ?? '')
    setPrefix(body.secret_prefix ?? null)
    setLoaded(true)
  }

  useEffect(() => {
    let stop = false
    setLoaded(false)
    setError(null)
    void refresh().catch((err: unknown) => {
      if (stop) return
      setError(err instanceof Error ? err.message : 'Pay unreachable')
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
    setDraftUrl(configured ? url : '')
    setFormOpen(true)
  }

  function closeForm() {
    if (busy) return
    setFormOpen(false)
    setFormError(null)
  }

  function closeSecret() {
    setShownSecret(null)
    setCopied(false)
  }

  async function save(e: FormEvent) {
    e.preventDefault()
    if (!write) return
    const next = draftUrl.trim()
    if (!next) return
    setBusy(true)
    setFormError(null)
    setShownSecret(null)
    try {
      const response = await payFetch(token, `/v1/orgs/${orgId}/webhooks`, {
        method: 'PUT',
        orgHint: orgId,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ url: next }),
      })
      if (!response.ok) {
        setFormError(await problemDetail(response, `webhooks ${response.status}`))
        return
      }
      const body = (await response.json()) as OrgWebhookView
      setConfigured(true)
      setPrefix(body.secret_prefix ?? null)
      setUrl(body.url ?? next)
      setFormOpen(false)
      setShownSecret(body.webhook_secret ?? null)
    } catch {
      setFormError('Pay unreachable')
    } finally {
      setBusy(false)
    }
  }

  async function rotate() {
    if (!write) return
    if (!window.confirm('Rotate the signing secret? The current secret stops working.')) return
    setBusy(true)
    setError(null)
    setShownSecret(null)
    try {
      const response = await payFetch(token, `/v1/orgs/${orgId}/webhooks/rotate`, {
        method: 'POST',
        orgHint: orgId,
      })
      if (!response.ok) {
        setError(await problemDetail(response, `rotate ${response.status}`))
        return
      }
      const body = (await response.json()) as OrgWebhookView
      setPrefix(body.secret_prefix ?? null)
      setShownSecret(body.webhook_secret ?? null)
    } catch {
      setError('Pay unreachable')
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

  const editing = configured && formOpen

  return (
    <PageCanvas>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <PageHeader subtitle="Pay will POST here; you verify. Not Stripe vault, not One inbound, not a One API key." />
        {loaded && write && !configured ? (
          <Button type="button" onClick={openForm}>
            Add endpoint
          </Button>
        ) : null}
        {!write ? <p className="text-sm text-slate-500">Member cannot change the endpoint.</p> : null}
      </div>

      {error ? (
        <p role="alert" className="text-sm text-red-600">
          {error}
        </p>
      ) : null}

      {!loaded ? (
        <p className="text-sm text-slate-500">Loading…</p>
      ) : error && !configured ? null : !configured ? (
        <div className="rounded-xl border border-slate-200 bg-white px-5 py-7">
          <p className="text-sm font-medium text-slate-900">No endpoints yet.</p>
          <p className="mt-1 max-w-xl text-sm text-slate-500">
            Add an HTTPS URL. Pay POSTs a signed event after a paid fulfill (and failed / expired / refund when they
            are written). The signing secret is shown once.
          </p>
        </div>
      ) : (
        <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
          <Table>
            <TableHeader>
              <TableRow className="border-slate-200 bg-slate-50 hover:bg-slate-50">
                <TableHead className="h-11 w-[42%] px-4 text-[11px] font-medium uppercase tracking-wider text-slate-500">
                  Endpoint
                </TableHead>
                <TableHead className="h-11 px-4 text-[11px] font-medium uppercase tracking-wider text-slate-500">
                  Events
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
              <TableRow className="border-slate-100">
                <TableCell className="max-w-[320px] px-4 py-3.5">
                  <p className="truncate font-semibold text-slate-900">{endpointName(url)}</p>
                  <p className="mt-0.5 truncate font-mono text-xs text-slate-500" title={url}>
                    {url}
                  </p>
                </TableCell>
                <TableCell className="px-4 py-3.5">
                  <div className="flex flex-wrap gap-1">
                    {PAY_EVENTS.map((event) => (
                      <span
                        key={event.id}
                        title={event.id}
                        className="inline-flex items-center rounded-md bg-slate-100 px-2 py-0.5 text-xs font-medium text-slate-800"
                      >
                        {event.label}
                      </span>
                    ))}
                  </div>
                </TableCell>
                <TableCell className="px-4 py-3.5">
                  <span className="inline-flex rounded-full bg-emerald-50 px-2.5 py-0.5 text-[11px] font-semibold text-emerald-800">
                    On
                  </span>
                </TableCell>
                <TableCell className="px-4 py-3.5 text-right whitespace-nowrap">
                  {write ? (
                    <div className="flex justify-end gap-0.5">
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        className="h-8 px-2 text-slate-700"
                        disabled={busy}
                        onClick={openForm}
                      >
                        Edit
                      </Button>
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        className="h-8 px-2 text-slate-700"
                        disabled={busy}
                        onClick={() => void rotate()}
                      >
                        Rotate secret
                      </Button>
                    </div>
                  ) : null}
                </TableCell>
              </TableRow>
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
          <form onSubmit={(e) => void save(e)}>
            <DialogHeader>
              <DialogTitle>{editing ? 'Edit endpoint' : 'Add endpoint'}</DialogTitle>
              <DialogDescription>
                HTTPS URL Pay will POST to. Saving mints a new shown-once secret. You verify{' '}
                <code>X-Lazuar-Signature</code>.
              </DialogDescription>
            </DialogHeader>
            {formError ? (
              <p role="alert" className="mt-4 text-sm text-red-600">
                {formError}
              </p>
            ) : null}
            <div className="mt-4 space-y-2">
              <Label htmlFor="webhook_url">Endpoint URL</Label>
              <Input
                id="webhook_url"
                type="url"
                value={draftUrl}
                onChange={(e) => setDraftUrl(e.target.value)}
                autoComplete="off"
                required
                maxLength={2048}
                placeholder="https://app.example/pay/webhooks"
                disabled={!write || busy}
              />
              <p className="text-xs text-slate-500">
                Use HTTPS. Processor vault secrets live on Processor — Stripe signs; Pay verifies those.
              </p>
            </div>
            <DialogFooter className="mt-6">
              <Button type="button" variant="outline" onClick={closeForm} disabled={busy}>
                Cancel
              </Button>
              <Button type="submit" disabled={!write || busy || !draftUrl.trim()}>
                {busy ? 'Saving…' : editing ? 'Save URL' : 'Create endpoint'}
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
        <DialogContent
          showCloseButton={false}
          onInteractOutside={(e) => e.preventDefault()}
        >
          <DialogHeader>
            <DialogTitle>Save this signing secret</DialogTitle>
            <DialogDescription>Copy now. Pay will not show this again.</DialogDescription>
          </DialogHeader>
          <p className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-950">
            Store this on your server. Do not commit it, and do not put it in <code>VITE_*</code> or a browser app.
          </p>
          {prefix ? (
            <p className="text-sm text-slate-500">
              Prefix <code className="font-mono text-slate-800">…{prefix}</code>
            </p>
          ) : null}
          <div className="space-y-2">
            <Label htmlFor="webhook_secret">Secret</Label>
            <div className="flex gap-2">
              <Input
                id="webhook_secret"
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
