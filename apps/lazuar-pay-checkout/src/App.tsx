import { useEffect, useState } from 'react'
import './App.css'

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

function App() {
  const token = tokenFromPath()
  const [pay, setPay] = useState<PayView | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [busy, setBusy] = useState(false)
  const [verifying, setVerifying] = useState(verifyingQuery())
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
      <main>
        <p className="kicker">Lazuar Pay</p>
        <h1>Checkout</h1>
        <p>This payment link is not valid. No sign-in.</p>
      </main>
    )
  }

  if (!pay) {
    return <p>Loading…</p>
  }

  if (pay.status === 'paid') {
    return (
      <main>
        <h1>Paid</h1>
        <p>
          Thank you. This page is not a membership login. The merchant will see
          an Official Receipt, not an e-invoice.
        </p>
      </main>
    )
  }

  if (pay.status === 'expired') {
    return (
      <main>
        <h1>Expired</h1>
        <p>This payment link is no longer open.</p>
      </main>
    )
  }

  if (verifying && pay.status !== 'paid') {
    return (
      <main>
        <p className="kicker">Lazuar Pay</p>
        <h1>Verifying…</h1>
        <p>The processor success URL is not paid. Waiting for the webhook.</p>
        {verifyTimedOut ? (
          <>
            <p>Not paid yet. The success URL is not paid.</p>
            <button
              type="button"
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
            </button>
          </>
        ) : null}
      </main>
    )
  }

  const emailBlocked = Boolean(pay.email_required && !usableEmail(email))
  const started = Boolean(pay.started)

  return (
    <main>
      <p className="kicker">Lazuar Pay</p>
      <h1>Checkout</h1>
      <p>
        {pay.amount} {pay.currency}. Buyers have no One account.
        {pay.provider === 'test'
          ? ' Test processor: Pay marks this paid. No card, no secret.'
          : ' Completing payment on the processor is not the same as a success URL.'}
      </p>
      {error && <p role="alert">{error}</p>}
      <p>
        <label>
          Name{' '}
          <input value={name} onChange={(e) => setName(e.target.value)} />
        </label>
      </p>
      <p>
        <label>
          Email{' '}
          <input value={email} onChange={(e) => setEmail(e.target.value)} />
        </label>
      </p>
      {started ? <p>You already started this payment.</p> : null}
      <button type="button" disabled={busy || emailBlocked} onClick={() => void startPay()}>
        {started ? 'Continue to processor' : 'Pay'}
      </button>
    </main>
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
