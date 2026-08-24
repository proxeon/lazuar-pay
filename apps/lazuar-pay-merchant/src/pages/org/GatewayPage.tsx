import { useEffect, useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import { problemDetail } from '../../lib/http'
import { payApi, payFetch } from '../../lib/payApi'
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

const rails = ['stripe', 'chip', 'billplz', 'xendit', 'razorpay'] as const

const copy: Record<(typeof rails)[number], string> = {
  stripe: 'Hosted Checkout on Stripe. Cards on Stripe’s page. Official Receipt, not an e-invoice.',
  chip: 'Hosted CHIP page (FPX/wallets if enabled on the brand). Auto-debit later, not this program. Paste PEM from the CHIP dashboard — Pay does not register webhooks.',
  billplz: 'Reminder + hosted bill. We do not auto-debit. Callback must be public https (localhost will fail).',
  xendit: 'Hosted invoice. Wallets on Xendit’s page if you enabled them there. We do not auto-debit.',
  razorpay: 'Hosted payment link. Not e-mandate. We do not auto-debit.',
}

type Gateway = {
  provider?: string
  last4?: string
  configured?: boolean
  capability?: string
  public_merchant_id?: string
  environment?: string
  webhook_configured?: boolean
}

export function GatewayPage() {
  const { orgId, token, write } = useOutletContext<OrgOutletContext>()
  const [error, setError] = useState<string | null>(null)
  const [provider, setProvider] = useState<(typeof rails)[number]>('stripe')
  const [secret, setSecret] = useState('')
  const [webhookSecret, setWebhookSecret] = useState('')
  const [publicMerchantId, setPublicMerchantId] = useState('')
  const [environment, setEnvironment] = useState('test')
  const [keyId, setKeyId] = useState('')
  const [keySecret, setKeySecret] = useState('')
  const [gateway, setGateway] = useState<Gateway | null>(null)
  const [saving, setSaving] = useState(false)

  async function refresh() {
    const gw = await payFetch(token, `/v1/orgs/${orgId}/gateway`, { orgHint: orgId })
    if (!gw.ok) return
    const body = (await gw.json()) as Gateway
    setGateway(body)
    if (body.provider && rails.includes(body.provider as (typeof rails)[number])) {
      setProvider(body.provider as (typeof rails)[number])
    }
    if (body.environment === 'test' || body.environment === 'live') {
      setEnvironment(body.environment)
    }
    if (body.public_merchant_id) setPublicMerchantId(body.public_merchant_id)
  }

  useEffect(() => {
    void refresh()
  }, [orgId, token])

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
        subtitle="BYOK wrap. GET never echoes the secret. One active rail per org."
      />
      {error ? (
        <p role="alert" className="text-sm text-red-600">
          {error}
        </p>
      ) : null}
      <Card>
        <CardHeader>
          <CardTitle>Keys</CardTitle>
          <CardDescription>{copy[provider]}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="provider">Provider</Label>
            <Select
              value={provider}
              onValueChange={(v) => setProvider(v as (typeof rails)[number])}
              disabled={!write}
            >
              <SelectTrigger id="provider" className="w-full max-w-xs">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {rails.map((r) => (
                  <SelectItem key={r} value={r}>
                    {r}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

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
            {gateway?.webhook_configured ? (
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
