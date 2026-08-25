import { useEffect, useState, type ReactNode } from 'react'
import { Check, CircleAlert, LoaderCircle } from 'lucide-react'
import { Button } from './ui/components/button'
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from './ui/components/card'
import { Input } from './ui/components/input'
import { Label } from './ui/components/label'

const payApi = import.meta.env.VITE_PAY_API_URL ?? 'http://localhost:8081'

type PayView = {
  token: string
  amount: number
  currency: string
  status: string
  email_required?: boolean
  started?: boolean
  provider?: string | null
  redirect_url?: string | null
}

function tokenFromPath(): string | null {
  const m = window.location.pathname.match(/^\/c\/([^/]+)/)
  return m ? decodeURIComponent(m[1]) : null
}

function verifyingQuery(): boolean {
  return new URLSearchParams(window.location.search).get('status') === 'verifying'
}

function formatMoney(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat('en-MY', { style: 'currency', currency }).format(amount)
  } catch {
    return `${amount} ${currency}`
  }
}

function Shell({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-dvh flex-col items-center justify-center px-4 py-10">
      <p className="mb-4 text-[11px] font-medium uppercase tracking-[0.18em] text-slate-500">Lazuar Pay</p>
      <div className="w-full max-w-md">{children}</div>
    </div>
  )
}

function App() {
  const token = tokenFromPath()
  const [pay, setPay] = useState<PayView | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [busy, setBusy] = useState(false)
  const verifying = verifyingQuery()
  const [verifyTimedOut, setVerifyTimedOut] = useState(false)

  useEffect(() => {
    if (!token) {
      setError('missing')
      return
    }
    let stop = false
    async function load() {
      const r = await fetch(`${payApi}/v1/pay/${token}`)
      if (r.status === 404) throw new Error('missing')
      if (!r.ok) throw new Error(`status ${r.status}`)
      return (await r.json()) as PayView
    }
    void load()
      .then((body) => {
        if (!stop) setPay(body)
      })
      .catch((err: unknown) => {
        if (!stop) setError(err instanceof Error ? err.message : 'error')
      })
    return () => {
      stop = true
    }
  }, [token])

  useEffect(() => {
    if (!token || !verifying || pay?.status === 'paid' || pay?.status === 'expired') return
    let n = 0
    const id = window.setInterval(() => {
      n += 1
      void fetch(`${payApi}/v1/pay/${token}`)
        .then((r) => (r.ok ? r.json() : null))
        .then((body: PayView | null) => {
          if (body) setPay(body)
        })
      if (n >= 15) {
        window.clearInterval(id)
        setVerifyTimedOut(true)
      }
    }, 2000)
    return () => window.clearInterval(id)
  }, [token, verifying, pay?.status])

  async function startPay() {
    if (!token) return
    if (pay?.email_required && !usableEmail(email)) {
      setError('email is required')
      return
    }
    if (pay?.started && pay.redirect_url) {
      window.location.assign(pay.redirect_url)
      return
    }
    setBusy(true)
    try {
      const response = await fetch(`${payApi}/v1/pay/${token}/start`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name, email }),
      })
      const detail = await readDetail(response)
      if (response.status === 503) {
        setError(detail ?? 'rail not configured')
        return
      }
      if (response.status === 400) {
        setError(detail ?? `start ${response.status}`)
        return
      }
      if (!response.ok) {
        setError(detail ?? `start ${response.status}`)
        return
      }
      const body = (await response.json()) as { redirect_url?: string }
      if (body.redirect_url) {
        window.location.assign(body.redirect_url)
      }
    } finally {
      setBusy(false)
    }
  }

  if (error === 'missing' || !token) {
    return (
      <Shell>
        <Card>
          <CardHeader className="text-center">
            <div className="mx-auto mb-2 flex size-12 items-center justify-center rounded-full bg-slate-100 text-slate-600">
              <CircleAlert className="size-6" />
            </div>
            <CardTitle className="text-xl">Link not found</CardTitle>
            <CardDescription>This payment link is not valid. No sign-in.</CardDescription>
          </CardHeader>
        </Card>
      </Shell>
    )
  }

  if (!pay) {
    return (
      <Shell>
        <Card>
          <CardContent className="py-10 text-center text-sm text-slate-500">Loading…</CardContent>
        </Card>
      </Shell>
    )
  }

  if (pay.status === 'paid') {
    return (
      <Shell>
        <Card>
          <CardHeader className="text-center">
            <div className="mx-auto mb-2 flex size-12 items-center justify-center rounded-full bg-emerald-50 text-emerald-700">
              <Check className="size-6" />
            </div>
            <CardTitle className="text-xl">Payment received</CardTitle>
            <p className="pt-2 text-2xl font-semibold tracking-tight text-slate-900">
              {formatMoney(pay.amount, pay.currency)}
            </p>
            <CardDescription className="pt-1">
              Thank you. The merchant will see an Official Receipt, not an e-invoice. This page is not a membership
              login.
            </CardDescription>
          </CardHeader>
        </Card>
      </Shell>
    )
  }

  if (pay.status === 'expired') {
    return (
      <Shell>
        <Card>
          <CardHeader className="text-center">
            <div className="mx-auto mb-2 flex size-12 items-center justify-center rounded-full bg-slate-100 text-slate-600">
              <CircleAlert className="size-6" />
            </div>
            <CardTitle className="text-xl">Link expired</CardTitle>
            <CardDescription>This payment link is no longer open.</CardDescription>
          </CardHeader>
        </Card>
      </Shell>
    )
  }

  if (verifying && pay.status !== 'paid') {
    return (
      <Shell>
        <Card>
          <CardHeader className="text-center">
            <div className="mx-auto mb-2 flex size-12 items-center justify-center rounded-full bg-slate-100 text-slate-600">
              <LoaderCircle className="size-6 animate-spin" />
            </div>
            <CardTitle className="text-xl">Confirming payment</CardTitle>
            <CardDescription>
              The processor success URL is not paid. Waiting for the webhook.
            </CardDescription>
          </CardHeader>
          {verifyTimedOut ? (
            <CardFooter className="flex-col gap-3">
              <p className="text-center text-sm text-slate-500">Not paid yet. The success URL is not paid.</p>
              <Button
                type="button"
                variant="outline"
                className="w-full"
                onClick={() => {
                  setVerifyTimedOut(false)
                  void fetch(`${payApi}/v1/pay/${token}`)
                    .then((r) => (r.ok ? r.json() : null))
                    .then((body: PayView | null) => {
                      if (body) setPay(body)
                    })
                }}
              >
                Refresh status
              </Button>
            </CardFooter>
          ) : null}
        </Card>
      </Shell>
    )
  }

  const emailBlocked = Boolean(pay.email_required && !usableEmail(email))
  const started = Boolean(pay.started)

  return (
    <Shell>
      <Card>
        <CardHeader>
          <CardDescription>Amount due</CardDescription>
          <CardTitle className="text-3xl tracking-tight">{formatMoney(pay.amount, pay.currency)}</CardTitle>
          <p className="text-sm text-slate-500">
            Buyers have no One account.
            {pay.provider === 'test'
              ? ' Test processor: Pay marks this paid. No card, no secret.'
              : ' Completing payment on the processor is not the same as a success URL.'}
          </p>
        </CardHeader>
        <CardContent className="space-y-4">
          {error && error !== 'missing' ? (
            <p role="alert" className="text-sm text-red-600">
              {error}
            </p>
          ) : null}
          <div className="space-y-2">
            <Label htmlFor="payer_name">Name</Label>
            <Input
              id="payer_name"
              value={name}
              autoComplete="name"
              onChange={(e) => setName(e.target.value)}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="payer_email">Email</Label>
            <Input
              id="payer_email"
              type="email"
              value={email}
              autoComplete="email"
              onChange={(e) => setEmail(e.target.value)}
            />
          </div>
          {started ? <p className="text-sm text-slate-500">You already started this payment.</p> : null}
        </CardContent>
        <CardFooter>
          <Button
            type="button"
            className="w-full"
            size="lg"
            disabled={busy || emailBlocked}
            onClick={() => void startPay()}
          >
            {started ? 'Continue to processor' : 'Pay'}
          </Button>
        </CardFooter>
      </Card>
    </Shell>
  )
}

function usableEmail(value: string): boolean {
  const trimmed = value.trim()
  return trimmed.length > 0 && trimmed.toLowerCase() !== 'customer@example.com'
}

async function readDetail(response: Response): Promise<string | null> {
  try {
    const clone = response.clone()
    const body = (await clone.json()) as { detail?: string }
    return body.detail?.trim() || null
  } catch {
    return null
  }
}

export default App
