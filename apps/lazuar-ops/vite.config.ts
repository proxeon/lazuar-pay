import path from "path"
import tailwindcss from "@tailwindcss/vite"
import react from "@vitejs/plugin-react"
import { defineConfig } from "vite"

// https://vite.dev/config/
export default defineConfig({
  // Ops serves at hub root (/). Override only if you mount it under a prefix.
  base: process.env.VITE_BASE_PATH || "/",
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
  // Dual-pinned with package.json `vite --port=3003 --host=0.0.0.0`.
  // strictPort: fail loud if 3003 is busy — never silently steal 3004/3005.
  server: {
    host: true,
    port: 3003,
    strictPort: true,
  },
})
