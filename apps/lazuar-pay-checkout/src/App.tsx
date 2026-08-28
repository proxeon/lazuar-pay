import { useEffect, useRef, useState, type ReactNode } from 'react'
import { Check, CircleAlert, LoaderCircle } from 'lucide-react'
import { Button } from './ui/components/button'
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from './ui/components/card'
import { Input } from './ui/components/input'
import { Label } from './ui/components/label'
import { payApi, payPath, slotKey, tokenFromPath, usableEmail, verifyingQuery } from './pay'
import { SolanaQr } from './SolanaQr'

type PayView = {
  token: string
  amount: number
  currency: string
  status: string
  email_required?: boolean
  started?: boolean
  mine?: boolean
  provider?: string | null
  redirect_url?: string | null
  solana_pay_url?: string | null
  payer_name?: string | null
  payer_email?: string | null
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

function Heading({ children, live, className }: { children: ReactNode; live?: boolean; className?: string }) {
  const ref = useRef<HTMLHeadingElement>(null)
  useEffect(() => {
    ref.current?.focus()
  }, [children])
  return (
    <CardTitle
      ref={ref}
      tabIndex={-1}
      className={className ?? 'text-xl outline-none'}
      aria-live={live ? 'polite' : undefined}
    >
      {children}
    </CardTitle>
  )
}

function App() {
  const token = tokenFromPath()
  const [pay, setPay] = useState<PayView | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [busy, setBusy] = useState(false)
  const [verifying, setVerifying] = useState(() => verifyingQuery())
  const [verifyTimedOut, setVerifyTimedOut] = useState(false)
  const [pollNonce, setPollNonce] = useState(0)
  const [reload, setReload] = useState(0)
  const payStatus = pay?.status

  useEffect(() => {
    if (!token) {
      setError('missing')
      return
    }
    const path = payPath(token)
    let stop = false
    setError(null)
    async function load() {
      let r: Response
      try {
        r = await fetch(path)
      } catch {
        throw new Error("Can't reach Pay")
      }
      if (r.status === 404) throw new Error('missing')
      if (!r.ok) {
        const detail = await readDetail(r)
        throw new Error(detail ?? "Can't reach Pay")
      }
      return (await r.json()) as PayView
    }
    void load()
      .then((body) => {
        if (stop) return
        setPay(body)
        setError(null)
        setName((prev) => prev || body.payer_name?.trim() || '')
        if (body.payer_email && usableEmail(body.payer_email)) {
          setEmail((prev) => prev || body.payer_email!.trim())
        }
      })
      .catch((err: unknown) => {
        if (!stop) setError(err instanceof Error ? err.message : "Can't reach Pay")
      })
    return () => {
      stop = true
    }
  }, [token, reload])

  useEffect(() => {
    if (
      !token ||
      !verifying ||
      !payStatus ||
      error === 'missing' ||
      payStatus === 'paid' ||
      payStatus === 'expired' ||
      payStatus === 'full' ||
      payStatus === 'already_paid'
    ) {
      return
    }
    let n = 0
    let stopped = false
    const id = window.setInterval(() => {
      n += 1
      void fetch(payPath(token))
        .then(async (r) => {
          if (r.status === 404) {
            setError('missing')
            window.clearInterval(id)
            return null
          }
          if (!r.ok) return null
          return (await r.json()) as PayView
        })
        .then((body: PayView | null) => {
          if (stopped || !body) return
          setPay(body)
        })
      if (n >= 15) {
        window.clearInterval(id)
        setVerifyTimedOut(true)
      }
    }, 2000)
    return () => {
      stopped = true
      window.clearInterval(id)
    }
  }, [token, verifying, payStatus, error, pollNonce])

  useEffect(() => {
    if (pay?.provider === 'solana' && pay.solana_pay_url && pay.status === 'open') {
      setVerifying(true)
    }
  }, [pay?.provider, pay?.solana_pay_url, pay?.status])

  async function startPay() {
    if (!token) return
    if (pay?.email_required && !usableEmail(email)) {
      setError('email is required')
      return
    }
    if (pay?.provider !== 'solana' && pay?.started && pay.redirect_url) {
      window.location.assign(pay.redirect_url)
      return
    }
    setBusy(true)
    try {
      const response = await fetch(`${payApi}/v1/pay/${token}/start`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name, email, slot_key: slotKey(token) }),
      })
      const detail = await readDetail(response)
      if (response.status === 409) {
        const again = await fetch(payPath(token))
        if (again.ok) setPay((await again.json()) as PayView)
        else setError(detail ?? 'This pay link is full')
        return
      }
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
      const body = (await response.json()) as { redirect_url?: string; solana_pay_url?: string }
      if (body.solana_pay_url) {
        setPay((prev) =>
          prev
            ? { ...prev, started: true, solana_pay_url: body.solana_pay_url, redirect_url: null }
            : prev,
        )
        setVerifying(true)
        return
      }
      if (body.redirect_url) {
        window.location.assign(body.redirect_url)
      } else {
        setError('Processor did not return a pay URL')
      }
    } catch {
      setError("Can't reach Pay")
    } finally {
      setBusy(false)
    }
  }

  function returnToPay() {
    window.history.replaceState(null, '', window.location.pathname)
    setVerifying(false)
    setVerifyTimedOut(false)
  }

  if (error === 'missing' || !token) {
    return (
      <Shell>
        <Card>
          <CardHeader className="text-center">
            <div className="mx-auto mb-2 flex size-12 items-center justify-center rounded-full bg-slate-100 text-slate-600">
              <CircleAlert className="size-6" />
            </div>
            <Heading>Link not found</Heading>
            <CardDescription>This payment link is not valid. No sign-in.</CardDescription>
          </CardHeader>
        </Card>
      </Shell>
    )
  }

  if (!pay) {
    if (error && error !== 'missing') {
      return (
        <Shell>
          <Card>
            <CardHeader className="text-center">
              <div className="mx-auto mb-2 flex size-12 items-center justify-center rounded-full bg-slate-100 text-slate-600">
                <CircleAlert className="size-6" />
              </div>
              <CardTitle className="text-xl">Can&apos;t reach Pay</CardTitle>
              <CardDescription>
                {error === "Can't reach Pay" ? 'The pay host did not respond. No sign-in.' : error}
              </CardDescription>
            </CardHeader>
            <CardFooter>
              <Button
                type="button"
                variant="outline"
                className="w-full"
                onClick={() => {
                  setError(null)
                  setReload((n) => n + 1)
                }}
              >
                Retry
              </Button>
            </CardFooter>
          </Card>
        </Shell>
      )
    }
    return (
      <Shell>
        <Card>
          <CardContent aria-live="polite" className="py-10 text-center text-sm text-slate-500">
            Loading…
          </CardContent>
        </Card>
      </Shell>
    )
  }

  if (pay.status === 'already_paid' || (pay.status === 'paid' && pay.mine === false && !pay.started)) {
    return (
      <Shell>
        <Card>
          <CardHeader className="text-center">
            <div className="mx-auto mb-2 flex size-12 items-center justify-center rounded-full bg-slate-100 text-slate-600">
              <CircleAlert className="size-6" />
            </div>
            <Heading>This link is already paid</Heading>
            <p className="pt-2 text-2xl font-semibold tracking-tight text-slate-900">
              {formatMoney(pay.amount, pay.currency)}
            </p>
            <CardDescription className="pt-1">
              Someone else already paid this link. This page is not a receipt and not a membership login.
            </CardDescription>
          </CardHeader>
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
            <Heading>Payment received</Heading>
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
            <Heading>Link expired</Heading>
            <CardDescription>This payment link is no longer open.</CardDescription>
          </CardHeader>
        </Card>
      </Shell>
    )
  }

  if (pay.status === 'full') {
    return (
      <Shell>
        <Card>
          <CardHeader className="text-center">
            <div className="mx-auto mb-2 flex size-12 items-center justify-center rounded-full bg-slate-100 text-slate-600">
              <CircleAlert className="size-6" />
            </div>
            <Heading>Link is full</Heading>
            <CardDescription>This pay link has no remaining seats.</CardDescription>
          </CardHeader>
        </Card>
      </Shell>
    )
  }

  if (verifying && pay.status !== 'paid' && pay.provider !== 'solana') {
    return (
      <Shell>
        <Card aria-live="polite">
          <CardHeader className="text-center">
            <div className="mx-auto mb-2 flex size-12 items-center justify-center rounded-full bg-slate-100 text-slate-600">
              <LoaderCircle className="size-6 animate-spin" aria-hidden="true" />
            </div>
            <Heading live>Confirming payment</Heading>
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
                  setPollNonce((n) => n + 1)
                }}
              >
                Refresh status
              </Button>
              {pay.status === 'open' ? (
                <Button type="button" variant="ghost" className="w-full" onClick={returnToPay}>
                  Return to pay
                </Button>
              ) : null}
            </CardFooter>
          ) : null}
        </Card>
      </Shell>
    )
  }

  const emailBlocked = Boolean(pay.email_required && !usableEmail(email))
  const started = Boolean(pay.started)
  const placeholderEmail = email.trim().toLowerCase() === 'customer@example.com'

  return (
    <Shell>
      <Card>
        <CardHeader>
          <CardDescription>Amount due</CardDescription>
          <Heading className="text-3xl tracking-tight outline-none">{formatMoney(pay.amount, pay.currency)}</Heading>
          <p className="text-sm text-slate-500">
            Buyers have no One account.
            {pay.provider === 'test'
              ? ' Test processor: Pay marks this paid. No card, no secret.'
              : pay.provider === 'solana'
                ? ' Solana Pay QR. USDC. Wallet confirmation is not paid until Pay sees the transfer.'
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
            <Label htmlFor="payer_email">{pay.email_required ? 'Email *' : 'Email'}</Label>
            <Input
              id="payer_email"
              type="email"
              value={email}
              autoComplete="email"
              required={Boolean(pay.email_required)}
              aria-required={pay.email_required ? true : undefined}
              onChange={(e) => setEmail(e.target.value)}
            />
            {pay.email_required ? (
              <p className="text-xs text-slate-500">
                This processor needs an email (not customer@example.com).
              </p>
            ) : null}
            {pay.email_required && placeholderEmail ? (
              <p role="alert" className="text-sm text-red-600">
                Use your real email.
              </p>
            ) : null}
          </div>
          {pay.provider === 'solana' && pay.solana_pay_url ? (
            <div className="space-y-3">
              <SolanaQr url={pay.solana_pay_url} />
              <a className="block text-center text-sm text-sky-700 underline-offset-2 hover:underline" href={pay.solana_pay_url}>
                Open in wallet
              </a>
              <p className="text-center text-xs text-slate-500">Waiting for USDC on Solana. Not a card.</p>
            </div>
          ) : null}
          {started && pay.provider !== 'solana' ? (
            <p className="text-sm text-slate-500">You already started this payment.</p>
          ) : null}
        </CardContent>
        {pay.provider === 'solana' && pay.solana_pay_url ? null : (
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
        )}
      </Card>
    </Shell>
  )
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
