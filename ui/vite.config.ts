import { defineConfig } from 'vite'
import { fileURLToPath, URL } from 'node:url'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    proxy: {
      // Forward backend routes to the ASP.NET app during `pnpm dev`.
      '/health': 'http://localhost:8080',
      '/api': 'http://localhost:8080',
      // Also expose the OpenAPI doc through the dev server. `pnpm gen:api`
      // hits :8080 directly, but this keeps the spec reachable at :5173 too.
      '/openapi': 'http://localhost:8080',
    },
  },
})
