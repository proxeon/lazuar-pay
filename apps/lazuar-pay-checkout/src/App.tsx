import { useEffect, useState } from 'react'
import './App.css'

const payApi = import.meta.env.VITE_PAY_API_URL ?? 'http://localhost:8081'

type PayView = {
  token: string
  amount: number
  currency: string
  status: string
}

function tokenFromPath(): string | null {
  const m = window.location.pathname.match(/^\/c\/([^/]+)/)
  return m ? decodeURIComponent(m[1]) : null
}

function App() {
  const token = tokenFromPath()
  const [pay, setPay] = useState<PayView | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    if (!token) {
      setError('missing')
      return
    }
    fetch(`${payApi}/v1/pay/${token}`)
      .then((r) => {
        if (r.status === 404) throw new Error('missing')
        if (!r.ok) throw new Error(`status ${r.status}`)
        return r.json()
      })
      .then((body: PayView) => setPay(body))
      .catch((err: unknown) =>
        setError(err instanceof Error ? err.message : 'error'),
      )
  }, [token])

  async function startPay() {
    if (!token) return
    setBusy(true)
    try {
      const response = await fetch(`${payApi}/v1/pay/${token}/start`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name, email }),
      })
      if (response.status === 503) {
        setError('rail not configured')
        return
      }
      if (!response.ok) {
        setError(`start ${response.status}`)
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
          an Official Receipt.
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

  return (
    <main>
      <p className="kicker">Lazuar Pay</p>
      <h1>Checkout</h1>
      <p>
        {pay.amount} {pay.currency}. Buyers have no One account. Completing
        payment on the processor is not the same as a success URL.
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
      <button type="button" disabled={busy} onClick={() => void startPay()}>
        Pay
      </button>
    </main>
  )
}

export default App
