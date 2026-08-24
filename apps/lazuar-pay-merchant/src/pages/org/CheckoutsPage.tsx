import { useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import { problemDetail } from '../../lib/http'
import { payFetch } from '../../lib/payApi'
import type { OrgOutletContext } from '../../layout/OrgLayout'
import { PageCanvas, PageHeader } from '../../layout/PageHeader'
import { Button } from '../../ui/components/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../../ui/components/card'
import { Input } from '../../ui/components/input'
import { Label } from '../../ui/components/label'

export function CheckoutsPage() {
  const { orgId, token, write } = useOutletContext<OrgOutletContext>()
  const [productName, setProductName] = useState('Dogfood')
  const [amount, setAmount] = useState('10')
  const [payUrl, setPayUrl] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function createProductAndLink() {
    if (!write) return
    setBusy(true)
    setError(null)
    const created = await payFetch(token, `/v1/orgs/${orgId}/products`, {
      method: 'POST',
      orgHint: orgId,
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name: productName, amount: Number(amount), currency: 'MYR' }),
    })
    if (!created.ok) {
      setBusy(false)
      setError(await problemDetail(created, `product ${created.status}`))
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
    setBusy(false)
    if (!checkout.ok) {
      setError(await problemDetail(checkout, `checkout ${checkout.status}`))
      return
    }
    const body = (await checkout.json()) as { public_token?: string }
    const checkoutOrigin = (import.meta.env.VITE_CHECKOUT_ORIGIN as string | undefined) ?? 'http://localhost:5179'
    if (body.public_token) setPayUrl(`${checkoutOrigin.replace(/\/$/, '')}/c/${body.public_token}`)
  }

  return (
    <PageCanvas>
      <PageHeader
        title="Pay links"
        subtitle="Amount is typed here. Catalog row is a label, not this charge. Buyer has no One account."
      />
      {error ? (
        <p role="alert" className="text-sm text-red-600">
          {error}
        </p>
      ) : null}
      <Card>
        <CardHeader>
          <CardTitle>Mint a hosted link</CardTitle>
          <CardDescription>MYR. Success URL defaults to checkout ?status=verifying (not paid).</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {write ? (
            <>
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="space-y-2">
                  <Label htmlFor="product_name">Label</Label>
                  <Input
                    id="product_name"
                    value={productName}
                    onChange={(e) => setProductName(e.target.value)}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="amount">Amount (MYR)</Label>
                  <Input id="amount" value={amount} onChange={(e) => setAmount(e.target.value)} />
                </div>
              </div>
              <Button type="button" onClick={() => void createProductAndLink()} disabled={busy}>
                Create pay link
              </Button>
            </>
          ) : (
            <p className="text-sm text-slate-500">Member cannot create charges.</p>
          )}
          {payUrl ? (
            <p className="text-sm">
              Buyer:{' '}
              <a className="break-all text-sky-700 underline-offset-2 hover:underline" href={payUrl}>
                {payUrl}
              </a>
            </p>
          ) : null}
        </CardContent>
      </Card>
    </PageCanvas>
  )
}
