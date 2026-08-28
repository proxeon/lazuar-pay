import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

function requirePayApiUrl(mode: string, envDir: string) {
  if (mode !== 'production') return
  const env = loadEnv(mode, envDir, '')
  if (!env.VITE_PAY_API_URL?.trim()) {
    throw new Error(
      'VITE_PAY_API_URL is required for production checkout builds. It is the public Pay origin (never a secret).',
    )
  }
}

// Dual-pinned with package.json `vite --port=5179`.
// strictPort: fail loud if 5179 is busy — never silently steal merchant :5178.
export default defineConfig(({ mode }) => {
  requirePayApiUrl(mode, process.cwd())
  return {
    plugins: [react(), tailwindcss()],
    server: {
      host: true,
      port: 5179,
      strictPort: true,
    },
    preview: {
      host: true,
      port: 4179,
      strictPort: true,
    },
  }
})
