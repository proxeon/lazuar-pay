import { useEffect, useState } from 'react'
import { Link, useOutletContext } from 'react-router-dom'
import { problemDetail } from '../../lib/http'
import { payFetch, payJson } from '../../lib/payApi'
import { isRail, railLabel, type Processor, type Rail } from '../../lib/processors'
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
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../../ui/components/table'
import { cn } from '../../ui/lib/utils'
import { ArrowUpRight, Check, Copy } from 'lucide-react'

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

function formatMoney(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat('en-MY', { style: 'currency', currency }).format(amount)
  } catch {
    return `${amount} ${currency}`
  }
}

function formatWhen(iso?: string): string {
  if (!iso) return ''
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return ''
  return new Intl.DateTimeFormat('en-GB', {
    day: 'numeric',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
  }).format(d)
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
  max_payers?: number | null
  unlimited?: boolean
  paid_count?: number
  taken_count?: number
  remaining?: number | null
}

type Capacity = 'one' | 'limited' | 'unlimited'

function payersLabel(row: PayLink): string {
  const taken = row.taken_count ?? 0
  if (row.unlimited || row.max_payers == null) {
    return taken === 0 ? 'Unlimited' : `${taken} started · unlimited`
  }
  return `${taken} / ${row.max_payers}`
}

function statusLabel(row: PayLink): string {
  if (row.status === 'full' && row.max_payers === 1 && (row.paid_count ?? 0) >= 1) return 'paid'
  return row.status
}

export function CheckoutsPage() {
  const { orgId, token, write } = useOutletContext<OrgOutletContext>()
  const [productName, setProductName] = useState('Dogfood')
  const [amount, setAmount] = useState('10')
  const [capacity, setCapacity] = useState<Capacity>('one')
  const [maxPayers, setMaxPayers] = useState('2')
  const [provider, setProvider] = useState<Rail | ''>('test')
  const [configured, setConfigured] = useState<Processor[]>([testProcessor])
  const [links, setLinks] = useState<PayLink[]>([])
  const [open, setOpen] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [listError, setListError] = useState<string | null>(null)
  const [linksLoaded, setLinksLoaded] = useState(false)
  const [busy, setBusy] = useState(false)
  const [copied, setCopied] = useState<string | null>(null)

  async function loadLinks() {
    const rows = await payJson<PayLink[]>(token, `/v1/orgs/${orgId}/payment-links`, { orgHint: orgId })
    setLinks(rows)
    setListError(null)
    setLinksLoaded(true)
  }

  useEffect(() => {
    let stop = false
    setLinksLoaded(false)
    setListError(null)
    void loadLinks().catch((err: unknown) => {
      if (!stop) setListError(err instanceof Error ? err.message : 'Pay unreachable')
    })
    payJson<{ processors?: Processor[] }>(token, `/v1/orgs/${orgId}/gateways`, { orgHint: orgId })
      .then((body) => {
        if (stop) return
        const ready = withTest(body.processors ?? [])
        setConfigured(ready)
        setProvider((prev) => {
          if (prev && ready.some((p) => p.provider === prev)) return prev
          const firstReal = ready.find((p) => p.provider !== 'test')?.provider
          const first = firstReal ?? ready[0]?.provider
          return isRail(first) ? first : 'test'
        })
      })
      .catch((err: unknown) => {
        if (stop) return
        setListError(err instanceof Error ? err.message : 'Pay unreachable')
      })
    return () => {
      stop = true
    }
  }, [orgId, token])

  function closeCreate() {
    setOpen(false)
    setError(null)
  }

  async function createProductAndLink() {
    if (!write || !provider) return
    const limited = Number(maxPayers)
    if (capacity === 'limited' && (!Number.isInteger(limited) || limited < 2)) {
      setError('Limited links need at least 2 people')
      return
    }
    setBusy(true)
    setError(null)
    let productCreated = false
    try {
      const created = await payFetch(token, `/v1/orgs/${orgId}/products`, {
        method: 'POST',
        orgHint: orgId,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name: productName, amount: Number(amount), currency: 'MYR' }),
      })
      if (!created.ok) {
        setError(await problemDetail(created, `product ${created.status}`))
        return
      }
      productCreated = true
      const product = (await created.json()) as { id?: string }
      const checkout = await payFetch(token, '/v1/payment-links', {
        method: 'POST',
        orgHint: orgId,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          org_id: orgId,
          amount: Number(amount),
          currency: 'MYR',
          provider,
          product_id: product.id,
          max_payers: capacity === 'one' ? 1 : capacity === 'limited' ? limited : undefined,
          unlimited: capacity === 'unlimited',
        }),
      })
      if (!checkout.ok) {
        const detail = await problemDetail(checkout, `pay link ${checkout.status}`)
        setError(`A product was created. Pay link failed: ${detail}`)
        return
      }
      closeCreate()
      await loadLinks()
    } catch (err: unknown) {
      const detail = err instanceof Error ? err.message : 'Pay unreachable'
      const network = detail === 'Failed to fetch' ? 'Pay unreachable' : detail
      setError(productCreated ? `A product was created. Pay link failed: ${network}` : network)
    } finally {
      setBusy(false)
    }
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
      <div className="flex flex-wrap items-center justify-between gap-3">
        <PageHeader
          title="Pay links"
          subtitle="Hosted links for this workspace. Set how many people can pay. Buyer has no One account and no PSP picker."
        />
        {write ? (
          <Button type="button" onClick={() => setOpen(true)}>
            Create pay link
          </Button>
        ) : (
          <p className="text-sm text-slate-500">Member cannot create charges.</p>
        )}
      </div>

      {listError ? (
        <p role="alert" className="text-sm text-red-600">
          {listError}
        </p>
      ) : null}

      {listError && links.length === 0 ? null : (
      <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
        {!linksLoaded ? (
          <div className="px-6 py-14 text-center text-sm text-slate-500">Loading…</div>
        ) : links.length === 0 ? (
          <div className="px-6 py-14 text-center">
            <p className="text-sm font-medium text-slate-900">No pay links yet</p>
            <p className="mt-1 text-sm text-slate-500">Create a hosted link. The buyer pays without a One account.</p>
          </div>
        ) : (
          <Table>
            <TableHeader>
              <TableRow className="border-slate-200 hover:bg-transparent">
                <TableHead className="h-11 w-[38%] px-4 text-[11px] font-medium uppercase tracking-wider text-slate-500">
                  Label
                </TableHead>
                <TableHead className="h-11 px-4 text-right text-[11px] font-medium uppercase tracking-wider text-slate-500">
                  Amount
                </TableHead>
                <TableHead className="h-11 px-4 text-[11px] font-medium uppercase tracking-wider text-slate-500">
                  Processor
                </TableHead>
                <TableHead className="h-11 px-4 text-[11px] font-medium uppercase tracking-wider text-slate-500">
                  Payers
                </TableHead>
                <TableHead className="h-11 px-4 text-[11px] font-medium uppercase tracking-wider text-slate-500">
                  Status
                </TableHead>
                <TableHead className="h-11 px-4 text-right text-[11px] font-medium uppercase tracking-wider text-slate-500">
                  <span className="sr-only">Actions</span>
                </TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {links.map((row) => {
                const url = row.public_token ? buyerUrl(row.public_token) : null
                const rail = isRail(row.provider) ? railLabel[row.provider] : (row.provider ?? '—')
                const when = formatWhen(row.created_at)
                const status = statusLabel(row)
                return (
                  <TableRow key={row.id} className="border-slate-100">
                    <TableCell className="max-w-[280px] px-4 py-3">
                      <p className="truncate font-medium text-slate-900">{row.label || 'Untitled'}</p>
                      {when ? <p className="mt-0.5 text-xs text-slate-500">{when}</p> : null}
                    </TableCell>
                    <TableCell className="px-4 py-3 text-right font-medium tabular-nums text-slate-900">
                      {formatMoney(row.amount, row.currency)}
                    </TableCell>
                    <TableCell className="px-4 py-3 text-sm text-slate-600">{rail}</TableCell>
                    <TableCell className="px-4 py-3 text-sm tabular-nums text-slate-600">
                      {payersLabel(row)}
                    </TableCell>
                    <TableCell className="px-4 py-3">
                      <span
                        className={cn(
                          'inline-flex rounded-full px-2 py-0.5 text-[11px] font-medium capitalize',
                          status === 'paid'
                            ? 'bg-emerald-50 text-emerald-800'
                            : status === 'full' || status === 'expired'
                              ? 'bg-slate-100 text-slate-600'
                              : 'bg-amber-50 text-amber-800',
                        )}
                      >
                        {status}
                      </span>
                    </TableCell>
                    <TableCell className="px-4 py-3">
                      {url ? (
                        <div className="flex justify-end gap-1">
                          <Button
                            type="button"
                            variant="ghost"
                            size="sm"
                            className="h-8 px-2 text-slate-600"
                            onClick={() => void copyLink(url, row.id)}
                          >
                            {copied === row.id ? <Check className="size-3.5" /> : <Copy className="size-3.5" />}
                            {copied === row.id ? 'Copied' : 'Copy'}
                          </Button>
                          <Button variant="ghost" size="sm" className="h-8 px-2 text-slate-600" asChild>
                            <a href={url} target="_blank" rel="noreferrer">
                              Open
                              <ArrowUpRight className="size-3.5" />
                            </a>
                          </Button>
                        </div>
                      ) : null}
                    </TableCell>
                  </TableRow>
                )
              })}
            </TableBody>
          </Table>
        )}
      </div>
      )}

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
            <div className="space-y-2">
              <Label htmlFor="capacity">Who can pay</Label>
              <Select value={capacity} onValueChange={(v) => setCapacity(v as Capacity)}>
                <SelectTrigger id="capacity" className="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="one">1 person only</SelectItem>
                  <SelectItem value="limited">Limited number</SelectItem>
                  <SelectItem value="unlimited">Unlimited</SelectItem>
                </SelectContent>
              </Select>
              {capacity === 'limited' ? (
                <div className="space-y-2">
                  <Label htmlFor="max_payers">Number of people</Label>
                  <Input
                    id="max_payers"
                    inputMode="numeric"
                    value={maxPayers}
                    onChange={(e) => setMaxPayers(e.target.value)}
                  />
                </div>
              ) : (
                <p className="text-xs text-slate-500">
                  {capacity === 'one'
                    ? 'The link closes after one person starts Pay. Unpaid starts free after 30 minutes.'
                    : 'Anyone with the URL can pay. It does not close on its own.'}
                </p>
              )}
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
