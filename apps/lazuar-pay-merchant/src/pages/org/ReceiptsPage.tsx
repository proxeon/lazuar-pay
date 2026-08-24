import { useEffect, useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import { payFetch } from '../../lib/payApi'
import type { OrgOutletContext } from '../../layout/OrgLayout'
import { PageCanvas, PageHeader } from '../../layout/PageHeader'
import { Card, CardContent } from '../../ui/components/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../../ui/components/table'

type Receipt = { id: string; number: string; title: string; checkout_id: string }

export function ReceiptsPage() {
  const { orgId, token } = useOutletContext<OrgOutletContext>()
  const [receipts, setReceipts] = useState<Receipt[]>([])

  useEffect(() => {
    payFetch(token, `/v1/orgs/${orgId}/receipts`, { orgHint: orgId })
      .then(async (r) => {
        if (r.ok) setReceipts((await r.json()) as Receipt[])
      })
      .catch(() => undefined)
  }, [orgId, token])

  return (
    <PageCanvas>
      <PageHeader title="Receipts" subtitle="Official Receipt RCPT-…. Never a Tax Invoice." />
      <Card>
        <CardContent className="pt-6">
          {receipts.length === 0 ? (
            <p className="text-sm text-slate-500">No receipts yet.</p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Title</TableHead>
                  <TableHead>Checkout</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {receipts.map((r) => (
                  <TableRow key={r.id}>
                    <TableCell>
                      <code>{r.number}</code>
                    </TableCell>
                    <TableCell>{r.title}</TableCell>
                    <TableCell>
                      <code className="text-xs">{r.checkout_id}</code>
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
