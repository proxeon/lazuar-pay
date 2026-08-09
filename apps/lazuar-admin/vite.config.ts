import path from "path"
import tailwindcss from "@tailwindcss/vite"
import react from "@vitejs/plugin-react"
import { defineConfig } from "vite"

// https://vite.dev/config/
export default defineConfig({
  // Production: https://hub.lazuar.com/admin/  (local dev: leave unset → "/")
  base: process.env.VITE_BASE_PATH || "/",
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
  // Dual-pinned with package.json `vite --port=3005 --host=0.0.0.0`.
  // strictPort: fail loud if 3005 is busy — never silently steal another app's port.
  server: {
    host: true,
    port: 3005,
    strictPort: true,
  },
})
