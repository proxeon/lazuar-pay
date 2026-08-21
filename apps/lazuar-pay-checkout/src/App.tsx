import { useEffect, useState } from 'react'
import './App.css'

const payApi = import.meta.env.VITE_PAY_API_URL ?? 'http://localhost:8081'

function App() {
  const [health, setHealth] = useState('checking')

  useEffect(() => {
    const ac = new AbortController()
    fetch(`${payApi}/health`, { signal: ac.signal })
      .then((r) => (r.ok ? r.json() : Promise.reject(r.status)))
      .then((body: { status?: string }) => setHealth(body.status ?? 'ok'))
      .catch(() => {
        if (!ac.signal.aborted) {
          setHealth('unreachable')
        }
      })
    return () => ac.abort()
  }, [])

  return (
    <main>
      <p className="kicker">Lazuar Pay</p>
      <h1>Checkout</h1>
      <p>
        Hosted pay page. Buyers have no One account and no Pay password form. This
        origin is not <code>lazuar-portal</code> (<code>:3004</code>).
      </p>
      <dl>
        <dt>This origin</dt>
        <dd>http://localhost:5179</dd>
        <dt>Pay API</dt>
        <dd>{payApi}</dd>
        <dt>Pay /health</dt>
        <dd>{health}</dd>
      </dl>
    </main>
  )
}

export default App
