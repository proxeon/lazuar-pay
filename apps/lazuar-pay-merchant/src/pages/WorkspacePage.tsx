import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { pickApiBearerToken } from '../auth/bearerToken'
import { getWhoami, payApi, payFetch, type WhoamiTenant } from '../lib/payApi'
import { canWriteMoney } from '../lib/roles'
import { setOrgHint } from '../lib/sessionKeys'

type Product = { id: string; name: string; prices?: { amount: number; currency: string; interval: string }[] }
type Payment = { id: string; amount: number; currency: string; status: string; checkout_id: string }
type Receipt = { id: string; number: string; title: string; checkout_id: string }
type Gateway = {
  provider?: string
  last4?: string
  configured?: boolean
  capability?: string
  public_merchant_id?: string
  environment?: string
  webhook_configured?: boolean
}

const rails = ['stripe', 'chip', 'billplz', 'xendit', 'razorpay'] as const

const copy: Record<(typeof rails)[number], string> = {
  stripe: 'Hosted Checkout on Stripe. Cards on Stripe’s page. Official Receipt, not an e-invoice.',
  chip: 'Hosted CHIP page (FPX/wallets if enabled on the brand). Auto-debit later, not this program. Paste PEM from the CHIP dashboard — Pay does not register webhooks.',
  billplz: 'Reminder + hosted bill. We do not auto-debit. Callback must be public https (localhost will fail).',
  xendit: 'Hosted invoice. Wallets on Xendit’s page if you enabled them there. We do not auto-debit.',
  razorpay: 'Hosted payment link. Not e-mandate. We do not auto-debit.',
}

export function WorkspacePage() {
  const { orgId = '' } = useParams<{ orgId: string }>()
  const auth = useAuth()
  const token = pickApiBearerToken(auth.user)
  const [tenant, setTenant] = useState<WhoamiTenant | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [provider, setProvider] = useState<(typeof rails)[number]>('stripe')
  const [secret, setSecret] = useState('')
  const [webhookSecret, setWebhookSecret] = useState('')
  const [publicMerchantId, setPublicMerchantId] = useState('')
  const [environment, setEnvironment] = useState('test')
  const [keyId, setKeyId] = useState('')
  const [keySecret, setKeySecret] = useState('')
  const [gateway, setGateway] = useState<Gateway | null>(null)
  const [productName, setProductName] = useState('Dogfood')
  const [amount, setAmount] = useState('10')
  const [payUrl, setPayUrl] = useState<string | null>(null)
  const [products, setProducts] = useState<Product[]>([])
  const [payments, setPayments] = useState<Payment[]>([])
  const [receipts, setReceipts] = useState<Receipt[]>([])

  const write = canWriteMoney(tenant?.role)

  async function refresh(access: string) {
    const [plist, pay, rec, gw] = await Promise.all([
      payFetch(access, `/v1/orgs/${orgId}/products`, { orgHint: orgId }),
      payFetch(access, `/v1/orgs/${orgId}/payments`, { orgHint: orgId }),
      payFetch(access, `/v1/orgs/${orgId}/receipts`, { orgHint: orgId }),
      payFetch(access, `/v1/orgs/${orgId}/gateway`, { orgHint: orgId }),
    ])
    if (plist.ok) setProducts((await plist.json()) as Product[])
    if (pay.ok) setPayments((await pay.json()) as Payment[])
    if (rec.ok) setReceipts((await rec.json()) as Receipt[])
    if (gw.ok) {
      const body = (await gw.json()) as Gateway
      setGateway(body)
      if (body.provider && rails.includes(body.provider as (typeof rails)[number])) {
        setProvider(body.provider as (typeof rails)[number])
      }
    }
  }

  useEffect(() => {
    setOrgHint(orgId)
    if (!token) return
    getWhoami(token, orgId)
      .then(async (who) => {
        const match = who.tenants.find((t) => t.id === orgId) ?? null
        setTenant(match)
        if (!match) setError('Not a member of this org')
        else await refresh(token)
      })
      .catch((err: unknown) =>
        setError(err instanceof Error ? err.message : 'whoami failed'),
      )
  }, [orgId, token])

  async function pasteKey() {
    if (!token || !write) return
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
    if (!response.ok) setError(`keys ${response.status}`)
    else {
      setError(null)
      setSecret('')
      setWebhookSecret('')
      setKeySecret('')
      await refresh(token)
    }
  }

  async function createProductAndLink() {
    if (!token || !write) return
    const created = await payFetch(token, `/v1/orgs/${orgId}/products`, {
      method: 'POST',
      orgHint: orgId,
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name: productName, amount: Number(amount), currency: 'MYR' }),
    })
    if (!created.ok) {
      setError(`product ${created.status}`)
      return
    }
    const checkout = await payFetch(token, '/v1/checkouts', {
      method: 'POST',
      orgHint: orgId,
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        org_id: orgId,
        amount: Number(amount),
        currency: 'MYR',
      }),
    })
    if (!checkout.ok) {
      setError(`checkout ${checkout.status}`)
      return
    }
    const body = (await checkout.json()) as { public_token?: string }
    if (body.public_token) setPayUrl(`http://localhost:5179/c/${body.public_token}`)
    await refresh(token)
  }

  return (
    <main>
      <p className="kicker">Lazuar Pay</p>
      <h1>{tenant?.name ?? orgId}</h1>
      <p>
        Role <code>{tenant?.role ?? '…'}</code>. Path org id is authorization SoT.
      </p>
      {error && <p role="alert">{error}</p>}
      <p>
        Active rail:{' '}
        <code>{gateway?.configured ? gateway.provider : 'none'}</code>
        {gateway?.last4 ? ` · last4 ${gateway.last4}` : ''}
        {gateway?.capability ? ` · ${gateway.capability}` : ''}
      </p>
      <p>Pay does not file SST or MyInvois. Receipts are Official Receipts.</p>

      {write ? (
        <>
          <h2>Processor keys</h2>
          <p>
            <label>
              Provider{' '}
              <select
                value={provider}
                onChange={(e) => setProvider(e.target.value as (typeof rails)[number])}
              >
                {rails.map((r) => (
                  <option key={r} value={r}>
                    {r}
                  </option>
                ))}
              </select>
            </label>
          </p>
          <p>{copy[provider]}</p>
          {provider === 'razorpay' ? (
            <p>
              <input
                value={keyId}
                onChange={(e) => setKeyId(e.target.value)}
                autoComplete="off"
                placeholder="key_id"
              />
              <input
                value={keySecret}
                onChange={(e) => setKeySecret(e.target.value)}
                autoComplete="off"
                placeholder="key_secret"
              />
            </p>
          ) : (
            <p>
              <input
                value={secret}
                onChange={(e) => setSecret(e.target.value)}
                autoComplete="off"
                placeholder={provider === 'stripe' ? 'sk_test_…' : 'API secret'}
              />
            </p>
          )}
          <p>
            <input
              value={webhookSecret}
              onChange={(e) => setWebhookSecret(e.target.value)}
              autoComplete="off"
              placeholder={
                provider === 'stripe'
                  ? 'whsec_… (endpoint signing secret)'
                  : provider === 'chip'
                    ? 'PEM from CHIP dashboard'
                    : provider === 'billplz'
                      ? 'X-Signature secret'
                      : provider === 'xendit'
                        ? 'x-callback-token'
                        : 'webhook secret'
              }
            />
          </p>
          {(provider === 'chip' || provider === 'billplz') && (
            <p>
              <input
                value={publicMerchantId}
                onChange={(e) => setPublicMerchantId(e.target.value)}
                autoComplete="off"
                placeholder={provider === 'chip' ? 'Brand ID' : 'Collection ID'}
              />
            </p>
          )}
          {provider === 'billplz' && (
            <p>
              <select value={environment} onChange={(e) => setEnvironment(e.target.value)}>
                <option value="test">test (sandbox)</option>
                <option value="live">live</option>
              </select>
            </p>
          )}
          <p>
            Webhook URL:{' '}
            <code>
              {payApi}/v1/webhooks/{provider}/{orgId}
            </code>
          </p>
          <p>
            <button type="button" onClick={() => void pasteKey()}>
              Save key
            </button>
          </p>
          <h2>Product + pay link</h2>
          <p>
            <input value={productName} onChange={(e) => setProductName(e.target.value)} />
            <input value={amount} onChange={(e) => setAmount(e.target.value)} />
            MYR
            <button type="button" onClick={() => void createProductAndLink()}>
              Create pay link
            </button>
          </p>
          {payUrl && (
            <p>
              Buyer (no One account): <a href={payUrl}>{payUrl}</a>
            </p>
          )}
        </>
      ) : (
        <p>Member can see payments. Cannot paste keys or create charges.</p>
      )}

      <h2>Products</h2>
      <ul>
        {products.map((p) => (
          <li key={p.id}>{p.name}</li>
        ))}
      </ul>
      <h2>Payments</h2>
      <ul>
        {payments.map((p) => (
          <li key={p.id}>
            {p.amount} {p.currency} {p.status}
          </li>
        ))}
      </ul>
      <h2>Receipts</h2>
      <ul>
        {receipts.map((r) => (
          <li key={r.id}>
            {r.number} — {r.title}
          </li>
        ))}
      </ul>
      <p>
        Pay API <code>{payApi}</code>
      </p>
      <p>
        <Link to="/">All workspaces</Link>
      </p>
    </main>
  )
}
