import { useEffect, useState } from 'react'
import { Link, useOutletContext } from 'react-router-dom'
import { problemDetail } from '../../lib/http'
import { payFetch } from '../../lib/payApi'
import { isRail, railLabel, type Processor, type Rail } from '../../lib/processors'
import type { OrgOutletContext } from '../../layout/OrgLayout'
import { PageCanvas, PageHeader } from '../../layout/PageHeader'
import { Button } from '../../ui/components/button'
import { Card, CardContent } from '../../ui/components/card'
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
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../../ui/components/table'
import { cn } from '../../ui/lib/utils'

const testProcessor: Processor = { provider: 'test', configured: true }

function withTest(list: Processor[]): Processor[] {
  const ready = list.filter((p) => p.configured && isRail(p.provider))
  if (!ready.some((p) => p.provider === 'test')) {
    ready.unshift(testProcessor)
  }
  return ready
}

function checkoutOrigin(): string {
  return ((import.meta.env.VITE_CHECKOUT_ORIGIN as string | undefined) ?? 'http://localhost:5179').replace(
    /\/$/,
    '',
  )
}

function buyerUrl(token: string): string {
  return `${checkoutOrigin()}/c/${token}`
}

type PayLink = {
  id: string
  provider?: string | null
  amount: number
  currency: string
  status: string
  public_token?: string | null
  created_at?: string
  label?: string | null
}

export function CheckoutsPage() {
  const { orgId, token, write } = useOutletContext<OrgOutletContext>()
  const [productName, setProductName] = useState('Dogfood')
  const [amount, setAmount] = useState('10')
  const [provider, setProvider] = useState<Rail | ''>('test')
  const [configured, setConfigured] = useState<Processor[]>([testProcessor])
  const [links, setLinks] = useState<PayLink[]>([])
  const [open, setOpen] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [copied, setCopied] = useState<string | null>(null)

  async function loadLinks() {
    const r = await payFetch(token, `/v1/orgs/${orgId}/checkouts`, { orgHint: orgId })
    if (!r.ok) return
    setLinks((await r.json()) as PayLink[])
  }

  useEffect(() => {
    void loadLinks()
    payFetch(token, `/v1/orgs/${orgId}/gateways`, { orgHint: orgId })
      .then(async (r) => {
        if (!r.ok) {
          setConfigured(withTest([]))
          return
        }
        const body = (await r.json()) as { processors?: Processor[] }
        const ready = withTest(body.processors ?? [])
        setConfigured(ready)
        setProvider((prev) => {
          if (prev && ready.some((p) => p.provider === prev)) return prev
          const firstReal = ready.find((p) => p.provider !== 'test')?.provider
          const first = firstReal ?? ready[0]?.provider
          return isRail(first) ? first : 'test'
        })
      })
      .catch(() => {
        setConfigured(withTest([]))
        setProvider((prev) => prev || 'test')
      })
  }, [orgId, token])

  function closeCreate() {
    setOpen(false)
    setError(null)
  }

  async function createProductAndLink() {
    if (!write || !provider) return
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
    const product = (await created.json()) as { id?: string }
    const checkout = await payFetch(token, '/v1/checkouts', {
      method: 'POST',
      orgHint: orgId,
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        org_id: orgId,
        amount: Number(amount),
        currency: 'MYR',
        provider,
        product_id: product.id,
      }),
    })
    setBusy(false)
    if (!checkout.ok) {
      setError(await problemDetail(checkout, `checkout ${checkout.status}`))
      return
    }
    closeCreate()
    await loadLinks()
  }

  async function copyLink(url: string, id: string) {
    try {
      await navigator.clipboard.writeText(url)
      setCopied(id)
      window.setTimeout(() => setCopied((cur) => (cur === id ? null : cur)), 1500)
    } catch {
      setError('Could not copy')
    }
  }

  return (
    <PageCanvas>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <PageHeader
          title="Pay links"
          subtitle="Hosted links for this workspace. Buyer has no One account and no PSP picker."
        />
        {write ? (
          <Button type="button" onClick={() => setOpen(true)}>
            Create pay link
          </Button>
        ) : (
          <p className="text-sm text-slate-500">Member cannot create charges.</p>
        )}
      </div>

      <Card className="shadow-none">
        <CardContent className="pt-6">
          {links.length === 0 ? (
            <p className="text-sm text-slate-500">No pay links yet.</p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Label</TableHead>
                  <TableHead>Amount</TableHead>
                  <TableHead>Processor</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Created</TableHead>
                  <TableHead className="text-right">Link</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {links.map((row) => {
                  const url = row.public_token ? buyerUrl(row.public_token) : null
                  const rail = isRail(row.provider) ? railLabel[row.provider] : (row.provider ?? '—')
                  return (
                    <TableRow key={row.id}>
                      <TableCell className="font-medium">{row.label || 'Pay link'}</TableCell>
                      <TableCell>
                        {row.amount} {row.currency}
                      </TableCell>
                      <TableCell>
                        <code className="text-xs">{rail}</code>
                      </TableCell>
                      <TableCell>
                        <span
                          className={cn(
                            'rounded-full px-2 py-0.5 text-[11px] font-medium',
                            row.status === 'paid'
                              ? 'bg-emerald-50 text-emerald-800'
                              : row.status === 'expired'
                                ? 'bg-slate-100 text-slate-600'
                                : 'bg-amber-50 text-amber-800',
                          )}
                        >
                          {row.status}
                        </span>
                      </TableCell>
                      <TableCell className="whitespace-nowrap text-xs text-slate-500">
                        {row.created_at ? new Date(row.created_at).toLocaleString() : '—'}
                      </TableCell>
                      <TableCell className="text-right">
                        {url ? (
                          <div className="flex justify-end gap-2">
                            <Button type="button" variant="outline" size="sm" onClick={() => void copyLink(url, row.id)}>
                              {copied === row.id ? 'Copied' : 'Copy'}
                            </Button>
                            <Button type="button" variant="outline" size="sm" asChild>
                              <a href={url} target="_blank" rel="noreferrer">
                                Open
                              </a>
                            </Button>
                          </div>
                        ) : (
                          <span className="text-xs text-slate-400">—</span>
                        )}
                      </TableCell>
                    </TableRow>
                  )
                })}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Dialog
        open={open}
        onOpenChange={(next) => {
          if (!next) closeCreate()
          else setOpen(true)
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Create pay link</DialogTitle>
            <DialogDescription>
              MYR. Success URL defaults to checkout ?status=verifying (not paid).
            </DialogDescription>
          </DialogHeader>
          {error ? (
            <p role="alert" className="text-sm text-red-600">
              {error}
            </p>
          ) : null}
          <div className="space-y-4">
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
            <div className="space-y-2">
              <Label htmlFor="provider">Processor</Label>
              <Select value={provider || 'test'} onValueChange={(v) => setProvider(v as Rail)}>
                <SelectTrigger id="provider" className="w-full">
                  <SelectValue placeholder="Select a rail" />
                </SelectTrigger>
                <SelectContent>
                  {configured.map((p) => (
                    <SelectItem key={p.provider} value={p.provider!}>
                      {isRail(p.provider) ? railLabel[p.provider] : p.provider}
                      {p.last4 ? ` · …${p.last4}` : p.provider === 'test' ? ' · no keys' : ''}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {configured.every((p) => p.provider === 'test') ? (
                <p className="text-xs text-slate-500">
                  Test needs no secrets.{' '}
                  <Link className="text-sky-700 underline-offset-2 hover:underline" to={`/o/${orgId}/gateway`}>
                    Paste keys
                  </Link>{' '}
                  for a live rail.
                </p>
              ) : null}
            </div>
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={closeCreate} disabled={busy}>
              Cancel
            </Button>
            <Button type="button" onClick={() => void createProductAndLink()} disabled={busy || !provider}>
              Create pay link
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </PageCanvas>
  )
}
