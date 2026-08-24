import { useEffect, useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import { problemDetail } from '../../lib/http'
import { payApi, payFetch } from '../../lib/payApi'
import { railCopy, railLabel, rails, type Processor, type Rail } from '../../lib/processors'
import type { OrgOutletContext } from '../../layout/OrgLayout'
import { PageCanvas, PageHeader } from '../../layout/PageHeader'
import { Button } from '../../ui/components/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../../ui/components/card'
import { Input } from '../../ui/components/input'
import { Label } from '../../ui/components/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '../../ui/components/select'
import { Textarea } from '../../ui/components/textarea'
import { cn } from '../../ui/lib/utils'

export function GatewayPage() {
  const { orgId, token, write } = useOutletContext<OrgOutletContext>()
  const [error, setError] = useState<string | null>(null)
  const [provider, setProvider] = useState<Rail>('stripe')
  const [secret, setSecret] = useState('')
  const [webhookSecret, setWebhookSecret] = useState('')
  const [publicMerchantId, setPublicMerchantId] = useState('')
  const [environment, setEnvironment] = useState('test')
  const [keyId, setKeyId] = useState('')
  const [keySecret, setKeySecret] = useState('')
  const [processors, setProcessors] = useState<Processor[]>([])
  const [saving, setSaving] = useState(false)

  const selected = processors.find((p) => p.provider === provider)

  async function refresh() {
    const gw = await payFetch(token, `/v1/orgs/${orgId}/gateways`, { orgHint: orgId })
    if (!gw.ok) return
    const body = (await gw.json()) as { processors?: Processor[] }
    const list = body.processors ?? []
    setProcessors(list)
    const row = list.find((p) => p.provider === provider)
    if (row?.environment === 'test' || row?.environment === 'live') {
      setEnvironment(row.environment)
    }
    setPublicMerchantId(row?.public_merchant_id ?? '')
  }

  useEffect(() => {
    void refresh()
  }, [orgId, token])

  function selectRail(next: Rail) {
    setProvider(next)
    setSecret('')
    setWebhookSecret('')
    setKeyId('')
    setKeySecret('')
    const row = processors.find((p) => p.provider === next)
    if (row?.environment === 'test' || row?.environment === 'live') {
      setEnvironment(row.environment)
    } else {
      setEnvironment('test')
    }
    setPublicMerchantId(row?.public_merchant_id ?? '')
  }

  async function pasteKey() {
    if (!write) return
    setSaving(true)
    const payload: Record<string, string> = {
      provider,
      webhook_secret: webhookSecret,
    }
    if (provider === 'razorpay') {
      payload.secret = `${keyId}:${keySecret}`
    } else {
      payload.secret = secret
    }
    if (provider === 'chip' || provider === 'billplz') {
      payload.public_merchant_id = publicMerchantId
    }
    if (provider === 'billplz') {
      payload.environment = environment
    }
    const response = await payFetch(token, `/v1/orgs/${orgId}/gateway`, {
      method: 'PUT',
      orgHint: orgId,
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    })
    setSaving(false)
    if (!response.ok) setError(await problemDetail(response, `keys ${response.status}`))
    else {
      setError(null)
      setSecret('')
      setWebhookSecret('')
      setKeySecret('')
      await refresh()
    }
  }

  return (
    <PageCanvas>
      <PageHeader
        title="Processor"
        subtitle="Vault keys per rail. Saving a secret does not pick the rail for pay links."
      />
      {error ? (
        <p role="alert" className="text-sm text-red-600">
          {error}
        </p>
      ) : null}

      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
        {rails.map((r) => {
          const row = processors.find((p) => p.provider === r)
          const on = Boolean(row?.configured)
          const selectedCard = provider === r
          return (
            <button
              type="button"
              key={r}
              onClick={() => selectRail(r)}
              className={cn(
                'aspect-square rounded-xl border bg-white p-4 text-left shadow-sm transition',
                selectedCard
                  ? 'border-slate-900 ring-2 ring-slate-900'
                  : 'border-slate-200 hover:border-slate-400',
              )}
            >
              <p className="text-sm font-semibold tracking-tight">{railLabel[r]}</p>
              <p className="mt-2 text-xs text-slate-500">{on ? 'On file' : 'Empty'}</p>
              {on && row?.last4 ? (
                <p className="mt-1 font-mono text-xs text-slate-600">…{row.last4}</p>
              ) : null}
              {on ? (
                <p className="mt-1 text-xs text-slate-500">
                  {row?.webhook_configured ? 'Webhook on file' : 'No webhook'}
                </p>
              ) : null}
            </button>
          )
        })}
      </div>

      <Card>
        <CardHeader>
          <CardTitle>{railLabel[provider]}</CardTitle>
          <CardDescription>{railCopy[provider]}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {provider === 'razorpay' ? (
            <div className="grid gap-3 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="key_id">key_id</Label>
                <Input
                  id="key_id"
                  value={keyId}
                  onChange={(e) => setKeyId(e.target.value)}
                  autoComplete="off"
                  placeholder="key_id"
                  disabled={!write}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="key_secret">key_secret</Label>
                <Input
                  id="key_secret"
                  value={keySecret}
                  onChange={(e) => setKeySecret(e.target.value)}
                  autoComplete="off"
                  placeholder="key_secret"
                  disabled={!write}
                />
              </div>
            </div>
          ) : (
            <div className="space-y-2">
              <Label htmlFor="secret">API secret</Label>
              <Input
                id="secret"
                value={secret}
                onChange={(e) => setSecret(e.target.value)}
                autoComplete="off"
                placeholder={provider === 'stripe' ? 'sk_test_…' : 'API secret'}
                disabled={!write}
              />
            </div>
          )}

          <div className="space-y-2">
            <Label htmlFor="webhook_secret">Webhook secret</Label>
            {provider === 'chip' ? (
              <Textarea
                id="webhook_secret"
                value={webhookSecret}
                onChange={(e) => setWebhookSecret(e.target.value)}
                autoComplete="off"
                rows={6}
                placeholder="PEM from CHIP dashboard"
                disabled={!write}
              />
            ) : (
              <Input
                id="webhook_secret"
                value={webhookSecret}
                onChange={(e) => setWebhookSecret(e.target.value)}
                autoComplete="off"
                placeholder={
                  provider === 'stripe'
                    ? 'whsec_… (endpoint signing secret)'
                    : provider === 'billplz'
                      ? 'X-Signature secret'
                      : provider === 'xendit'
                        ? 'x-callback-token'
                        : 'webhook secret'
                }
                disabled={!write}
              />
            )}
            {selected?.webhook_configured ? (
              <p className="text-xs text-slate-500">Webhook secret on file. Saving again requires a fresh value.</p>
            ) : null}
          </div>

          {(provider === 'chip' || provider === 'billplz') && (
            <div className="space-y-2">
              <Label htmlFor="public_merchant_id">
                {provider === 'chip' ? 'Brand ID' : 'Collection ID'}
              </Label>
              <Input
                id="public_merchant_id"
                value={publicMerchantId}
                onChange={(e) => setPublicMerchantId(e.target.value)}
                autoComplete="off"
                placeholder={provider === 'chip' ? 'Brand ID' : 'Collection ID'}
                disabled={!write}
              />
            </div>
          )}

          {provider === 'billplz' && (
            <div className="space-y-2">
              <Label>Environment</Label>
              <Select value={environment} onValueChange={setEnvironment} disabled={!write}>
                <SelectTrigger className="w-full max-w-xs">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="test">test (sandbox)</SelectItem>
                  <SelectItem value="live">live</SelectItem>
                </SelectContent>
              </Select>
            </div>
          )}

          <p className="text-xs leading-relaxed text-slate-500">
            Webhook URL:{' '}
            <code>
              {payApi}/v1/webhooks/{provider}/{orgId}
            </code>
          </p>
          {provider === 'billplz' ? (
            <p className="text-xs text-slate-500">
              Dashboard callback is registered at start from Pay:PublicBaseUrl (public https). This path is the
              shape; localhost will fail.
            </p>
          ) : null}

          {write ? (
            <Button type="button" onClick={() => void pasteKey()} disabled={saving}>
              Save key
            </Button>
          ) : (
            <p className="text-sm text-slate-500">Member can see metadata. Cannot paste keys.</p>
          )}
        </CardContent>
      </Card>
    </PageCanvas>
  )
}
