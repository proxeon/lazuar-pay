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

export function WorkspacePage() {
  const { orgId = '' } = useParams<{ orgId: string }>()
  const auth = useAuth()
  const token = pickApiBearerToken(auth.user)
  const [tenant, setTenant] = useState<WhoamiTenant | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [sk, setSk] = useState('')
  const [productName, setProductName] = useState('Dogfood')
  const [amount, setAmount] = useState('10')
  const [payUrl, setPayUrl] = useState<string | null>(null)
  const [products, setProducts] = useState<Product[]>([])
  const [payments, setPayments] = useState<Payment[]>([])
  const [receipts, setReceipts] = useState<Receipt[]>([])

  const write = canWriteMoney(tenant?.role)

  async function refresh(access: string) {
    const [plist, pay, rec] = await Promise.all([
      payFetch(access, `/v1/orgs/${orgId}/products`, { orgHint: orgId }),
      payFetch(access, `/v1/orgs/${orgId}/payments`, { orgHint: orgId }),
      payFetch(access, `/v1/orgs/${orgId}/receipts`, { orgHint: orgId }),
    ])
    if (plist.ok) setProducts((await plist.json()) as Product[])
    if (pay.ok) setPayments((await pay.json()) as Payment[])
    if (rec.ok) setReceipts((await rec.json()) as Receipt[])
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
    const response = await payFetch(token, `/v1/orgs/${orgId}/gateway`, {
      method: 'PUT',
      orgHint: orgId,
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ provider: 'stripe', secret: sk }),
    })
    if (!response.ok) setError(`keys ${response.status}`)
    else setError(null)
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

      {write ? (
        <>
          <h2>Stripe keys</h2>
          <p>Paste test <code>sk_test_</code>. VIEWER-class / member cannot.</p>
          <p>
            <input
              value={sk}
              onChange={(e) => setSk(e.target.value)}
              autoComplete="off"
              placeholder="sk_test_…"
            />
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
