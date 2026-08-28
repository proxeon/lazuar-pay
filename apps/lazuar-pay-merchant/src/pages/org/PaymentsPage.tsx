import { useEffect, useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import { payJson } from '../../lib/payApi'
import { isRail, railLabel } from '../../lib/processors'
import type { OrgOutletContext } from '../../layout/OrgLayout'
import { PageCanvas, PageHeader } from '../../layout/PageHeader'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../../ui/components/table'
import { cn } from '../../ui/lib/utils'

type Payment = {
  id: string
  amount: number
  currency: string
  status: string
  checkout_id: string
  provider?: string | null
  payer_name?: string | null
  created_at?: string
  label?: string | null
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

export function PaymentsPage() {
  const { orgId, token } = useOutletContext<OrgOutletContext>()
  const [payments, setPayments] = useState<Payment[]>([])
  const [listError, setListError] = useState<string | null>(null)
  const [loaded, setLoaded] = useState(false)

  useEffect(() => {
    let stop = false
    setLoaded(false)
    setListError(null)
    payJson<Payment[]>(token, `/v1/orgs/${orgId}/payments`, { orgHint: orgId })
      .then((rows) => {
        if (stop) return
        setPayments(rows)
        setLoaded(true)
      })
      .catch((err: unknown) => {
        if (stop) return
        setListError(err instanceof Error ? err.message : 'Pay unreachable')
      })
    return () => {
      stop = true
    }
  }, [orgId, token])

  return (
    <PageCanvas>
      <PageHeader subtitle="Charges booked on a verified webhook. Amount charged = amount booked." />

      {listError ? (
        <p role="alert" className="text-sm text-red-600">
          {listError}
        </p>
      ) : null}

      {listError && payments.length === 0 ? null : (
      <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
        {!loaded ? (
          <div className="px-6 py-14 text-center text-sm text-slate-500">Loading…</div>
        ) : payments.length === 0 ? (
          <div className="px-6 py-14 text-center">
            <p className="text-sm font-medium text-slate-900">No payments yet</p>
            <p className="mt-1 text-sm text-slate-500">
              Charges appear after a buyer pays on a hosted link.
            </p>
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
                  Payer
                </TableHead>
                <TableHead className="h-11 px-4 text-[11px] font-medium uppercase tracking-wider text-slate-500">
                  Status
                </TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {payments.map((row) => {
                const rail = isRail(row.provider) ? railLabel[row.provider] : (row.provider ?? '—')
                const when = formatWhen(row.created_at)
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
                    <TableCell className="max-w-[180px] px-4 py-3 text-sm text-slate-600">
                      <span className="truncate block">{row.payer_name?.trim() || '—'}</span>
                    </TableCell>
                    <TableCell className="px-4 py-3">
                      <span
                        className={cn(
                          'inline-flex rounded-full px-2 py-0.5 text-[11px] font-medium capitalize',
                          row.status === 'paid'
                            ? 'bg-emerald-50 text-emerald-800'
                            : row.status === 'expired' || row.status === 'failed'
                              ? 'bg-slate-100 text-slate-600'
                              : 'bg-amber-50 text-amber-800',
                        )}
                      >
                        {row.status}
                      </span>
                    </TableCell>
                  </TableRow>
                )
              })}
            </TableBody>
          </Table>
        )}
      </div>
      )}
    </PageCanvas>
  )
}
