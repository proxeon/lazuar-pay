import { encode } from 'uqr'

export function SolanaQr({ url }: { url: string }) {
  const qr = encode(url, { ecc: 'M', border: 2 })
  return (
    <svg
      role="img"
      aria-label="Solana Pay QR"
      viewBox={`0 0 ${qr.size} ${qr.size}`}
      className="mx-auto size-56 bg-white"
    >
      {qr.data.flatMap((row, y) =>
        row.flatMap((on, x) =>
          on ? <rect key={`${x}-${y}`} x={x} y={y} width={1} height={1} fill="currentColor" /> : [],
        ),
      )}
    </svg>
  )
}
