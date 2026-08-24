import { useEffect, useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import { payFetch } from '../../lib/payApi'
import type { OrgOutletContext } from '../../layout/OrgLayout'
import { PageCanvas, PageHeader } from '../../layout/PageHeader'
import { Card, CardContent } from '../../ui/components/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../../ui/components/table'

type Payment = { id: string; amount: number; currency: string; status: string; checkout_id: string }

export function PaymentsPage() {
  const { orgId, token } = useOutletContext<OrgOutletContext>()
  const [payments, setPayments] = useState<Payment[]>([])

  useEffect(() => {
    payFetch(token, `/v1/orgs/${orgId}/payments`, { orgHint: orgId })
      .then(async (r) => {
        if (r.ok) setPayments((await r.json()) as Payment[])
      })
      .catch(() => undefined)
  }, [orgId, token])

  return (
    <PageCanvas>
      <PageHeader title="Payments" subtitle="Charges booked on a verified webhook. Amount charged = amount booked." />
      <Card>
        <CardContent className="pt-6">
          {payments.length === 0 ? (
            <p className="text-sm text-slate-500">No payments yet.</p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Amount</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Checkout</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {payments.map((p) => (
                  <TableRow key={p.id}>
                    <TableCell>
                      {p.amount} {p.currency}
                    </TableCell>
                    <TableCell>{p.status}</TableCell>
                    <TableCell>
                      <code className="text-xs">{p.checkout_id}</code>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </PageCanvas>
  )
}
