import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// Dual-pinned with package.json `vite --port=5179`.
// strictPort: fail loud if 5179 is busy — never silently steal merchant :5178.
export default defineConfig({
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
})
