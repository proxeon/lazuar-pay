import { useEffect, useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import { problemDetail } from '../../lib/http'
import { payApi, payFetch } from '../../lib/payApi'
import { railCopy, railLabel, rails, type Processor, type Rail } from '../../lib/processors'
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
  const [editing, setEditing] = useState<Rail | null>(null)
  const [secret, setSecret] = useState('')
  const [webhookSecret, setWebhookSecret] = useState('')
  const [publicMerchantId, setPublicMerchantId] = useState('')
  const [environment, setEnvironment] = useState('test')
  const [keyId, setKeyId] = useState('')
  const [keySecret, setKeySecret] = useState('')
  const [processors, setProcessors] = useState<Processor[]>([])
  const [saving, setSaving] = useState(false)

  const selected = editing ? processors.find((p) => p.provider === editing) : undefined

  async function refresh() {
    const gw = await payFetch(token, `/v1/orgs/${orgId}/gateways`, { orgHint: orgId })
    if (!gw.ok) return
    const body = (await gw.json()) as { processors?: Processor[] }
    setProcessors(body.processors ?? [])
  }

  useEffect(() => {
    void refresh()
  }, [orgId, token])

  function closeEdit() {
    setEditing(null)
    setError(null)
    setSecret('')
    setWebhookSecret('')
    setKeyId('')
    setKeySecret('')
    setPublicMerchantId('')
    setEnvironment('test')
  }

  function openEdit(next: Rail) {
    setError(null)
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
    setEditing(next)
  }

  async function pasteKey() {
    if (!write || !editing) return
    setSaving(true)
    const payload: Record<string, string> = {
      provider: editing,
      webhook_secret: webhookSecret,
    }
    if (editing === 'razorpay') {
      payload.secret = `${keyId}:${keySecret}`
    } else {
      payload.secret = secret
    }
    if (editing === 'chip' || editing === 'billplz') {
      payload.public_merchant_id = publicMerchantId
    }
    if (editing === 'billplz') {
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
      closeEdit()
    }
  }

  return (
    <PageCanvas>
      <PageHeader
        title="Processor"
        subtitle="Vault keys per rail. Saving a secret does not pick the rail for pay links."
      />

      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
        {rails
          .filter((r) => r !== 'test' || processors.some((p) => p.provider === 'test'))
          .map((r) => {
          const row = processors.find((p) => p.provider === r)
          const on = Boolean(row?.configured)
          const isTest = r === 'test'
          return (
            <div
              key={r}
              className={cn(
                'flex aspect-square flex-col rounded-xl border bg-white p-4 text-left shadow-sm',
                isTest ? 'border-dashed border-slate-400' : 'border-slate-200',
              )}
            >
              <p className="text-sm font-semibold tracking-tight">{railLabel[r]}</p>
              <p className="mt-2 text-xs text-slate-500">{on ? 'On file' : 'Empty'}</p>
              {on && row?.last4 ? (
                <p className="mt-1 font-mono text-xs text-slate-600">…{row.last4}</p>
              ) : null}
              {isTest ? (
                <p className="mt-1 text-xs text-slate-500">No keys. Pay links mark paid.</p>
              ) : on ? (
                <p className="mt-1 text-xs text-slate-500">
                  {row?.webhook_configured ? 'Webhook on file' : 'No webhook'}
                </p>
              ) : null}
              {write && !isTest ? (
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  className="mt-auto w-full"
                  onClick={() => openEdit(r)}
                >
                  Edit
                </Button>
              ) : null}
            </div>
          )
        })}
      </div>

      {!write ? (
        <p className="text-sm text-slate-500">Member can see metadata. Cannot paste keys.</p>
      ) : null}

      <Dialog
        open={editing !== null}
        onOpenChange={(open) => {
          if (!open) closeEdit()
        }}
      >
        <DialogContent>
          {editing ? (
            <>
              <DialogHeader>
                <DialogTitle>{railLabel[editing]}</DialogTitle>
                <DialogDescription>{railCopy[editing]}</DialogDescription>
              </DialogHeader>
              {error ? (
                <p role="alert" className="text-sm text-red-600">
                  {error}
                </p>
              ) : null}
              <div className="space-y-4">
                {editing === 'razorpay' ? (
                  <div className="grid gap-3 sm:grid-cols-2">
                    <div className="space-y-2">
                      <Label htmlFor="key_id">key_id</Label>
                      <Input
                        id="key_id"
                        value={keyId}
                        onChange={(e) => setKeyId(e.target.value)}
                        autoComplete="off"
                        placeholder="key_id"
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
                      placeholder={editing === 'stripe' ? 'sk_test_…' : 'API secret'}
                    />
                  </div>
                )}

                <div className="space-y-2">
                  <Label htmlFor="webhook_secret">Webhook secret</Label>
                  {editing === 'chip' ? (
                    <Textarea
                      id="webhook_secret"
                      value={webhookSecret}
                      onChange={(e) => setWebhookSecret(e.target.value)}
                      autoComplete="off"
                      rows={6}
                      placeholder="PEM from CHIP dashboard"
                    />
                  ) : (
                    <Input
                      id="webhook_secret"
                      value={webhookSecret}
                      onChange={(e) => setWebhookSecret(e.target.value)}
                      autoComplete="off"
                      placeholder={
                        editing === 'stripe'
                          ? 'whsec_… (endpoint signing secret)'
                          : editing === 'billplz'
                            ? 'X-Signature secret'
                            : editing === 'xendit'
                              ? 'x-callback-token'
                              : 'webhook secret'
                      }
                    />
                  )}
                  {selected?.webhook_configured ? (
                    <p className="text-xs text-slate-500">
                      Webhook secret on file. Saving again requires a fresh value.
                    </p>
                  ) : null}
                </div>

                {(editing === 'chip' || editing === 'billplz') && (
                  <div className="space-y-2">
                    <Label htmlFor="public_merchant_id">
                      {editing === 'chip' ? 'Brand ID' : 'Collection ID'}
                    </Label>
                    <Input
                      id="public_merchant_id"
                      value={publicMerchantId}
                      onChange={(e) => setPublicMerchantId(e.target.value)}
                      autoComplete="off"
                      placeholder={editing === 'chip' ? 'Brand ID' : 'Collection ID'}
                    />
                  </div>
                )}

                {editing === 'billplz' && (
                  <div className="space-y-2">
                    <Label>Environment</Label>
                    <Select value={environment} onValueChange={setEnvironment}>
                      <SelectTrigger className="w-full">
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
                    {payApi}/v1/webhooks/{editing}/{orgId}
                  </code>
                </p>
                {editing === 'billplz' ? (
                  <p className="text-xs text-slate-500">
                    Dashboard callback is registered at start from Pay:PublicBaseUrl (public https). This path
                    is the shape; localhost will fail.
                  </p>
                ) : null}
              </div>
              <DialogFooter>
                <Button type="button" variant="outline" onClick={closeEdit} disabled={saving}>
                  Cancel
                </Button>
                <Button type="button" onClick={() => void pasteKey()} disabled={saving}>
                  Save key
                </Button>
              </DialogFooter>
            </>
          ) : null}
        </DialogContent>
      </Dialog>
    </PageCanvas>
  )
}
