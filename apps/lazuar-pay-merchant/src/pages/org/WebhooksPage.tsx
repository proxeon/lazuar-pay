import { useEffect, useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import { problemDetail } from '../../lib/http'
import { payFetch, payJson } from '../../lib/payApi'
import type { OrgOutletContext } from '../../layout/OrgLayout'
import { PageCanvas, PageHeader } from '../../layout/PageHeader'
import { Button } from '../../ui/components/button'
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '../../ui/components/card'
import { Input } from '../../ui/components/input'
import { Label } from '../../ui/components/label'

type OrgWebhookView = {
  org_id: string
  url?: string
  webhook_configured: boolean
  secret_prefix?: string
  webhook_secret?: string
}

export function WebhooksPage() {
  const { orgId, token, write } = useOutletContext<OrgOutletContext>()
  const [url, setUrl] = useState('')
  const [configured, setConfigured] = useState(false)
  const [prefix, setPrefix] = useState<string | null>(null)
  const [shownSecret, setShownSecret] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [loaded, setLoaded] = useState(false)

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
      if (!stop) setError(err instanceof Error ? err.message : 'Pay unreachable')
    })
    return () => {
      stop = true
    }
  }, [orgId, token])

  async function save() {
    if (!write) return
    setBusy(true)
    setError(null)
    setShownSecret(null)
    try {
      const response = await payFetch(token, `/v1/orgs/${orgId}/webhooks`, {
        method: 'PUT',
        orgHint: orgId,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ url }),
      })
      if (!response.ok) {
        setError(await problemDetail(response, `webhooks ${response.status}`))
        return
      }
      const body = (await response.json()) as OrgWebhookView
      setConfigured(true)
      setPrefix(body.secret_prefix ?? null)
      setShownSecret(body.webhook_secret ?? null)
      setUrl(body.url ?? url)
    } catch {
      setError('Pay unreachable')
    } finally {
      setBusy(false)
    }
  }

  async function rotate() {
    if (!write) return
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

  return (
    <PageCanvas>
      <PageHeader subtitle="Pay will POST here; you verify. Not Stripe vault, not One inbound, not a One API key." />

      {error ? (
        <p role="alert" className="text-sm text-red-600">
          {error}
        </p>
      ) : null}

      <Card className="max-w-xl border-slate-200 shadow-none">
        <CardHeader>
          <CardTitle className="text-sm">App webhook</CardTitle>
          <CardDescription>
            After a paid fulfill Pay POSTs <code>payment.completed</code> (and failed / expired / refund events when
            they are written) with One-dialect HMAC. You verify <code>X-Lazuar-Signature</code>. Processor vault secrets
            live on the Processor page — Stripe signs; Pay verifies those.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="webhook_url">HTTPS URL Pay will POST to</Label>
            <Input
              id="webhook_url"
              value={url}
              onChange={(e) => setUrl(e.target.value)}
              autoComplete="off"
              placeholder="https://app.example/pay/webhooks"
              disabled={!write}
            />
          </div>
          {loaded && configured ? (
            <p className="text-xs text-slate-500">
              Configured{prefix ? ` · secret ends with …${prefix}` : ''}. GET never echoes the secret.
            </p>
          ) : loaded ? (
            <p className="text-xs text-slate-500">No endpoint yet. Saving mints a shown-once <code>whsec_</code>.</p>
          ) : null}
          {shownSecret ? (
            <div className="space-y-1 rounded-md border border-amber-200 bg-amber-50 p-3">
              <p className="text-xs font-medium text-amber-900">Copy now. Pay will not show this again.</p>
              <code className="block break-all text-xs text-amber-950">{shownSecret}</code>
            </div>
          ) : null}
        </CardContent>
        <CardFooter className="gap-2">
          <Button type="button" onClick={() => void save()} disabled={!write || busy || !url.trim()}>
            Save URL
          </Button>
          <Button type="button" variant="outline" onClick={() => void rotate()} disabled={!write || busy || !configured}>
            Rotate secret
          </Button>
        </CardFooter>
      </Card>
    </PageCanvas>
  )
}
