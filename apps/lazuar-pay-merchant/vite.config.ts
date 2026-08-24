import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// Dual-pinned with package.json `vite --port=5178`.
// strictPort: fail loud if 5178 is busy — never silently steal login :5175 or checkout :5179.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    host: true,
    port: 5178,
    strictPort: true,
  },
  preview: {
    host: true,
    port: 4178,
    strictPort: true,
  },
})
