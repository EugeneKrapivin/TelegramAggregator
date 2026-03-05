import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    proxy: {
      '/api': {
        target: process.env['services__api__https__0'] ?? process.env['services__api__http__0'] ?? 'https://localhost:7169',
        changeOrigin: true,
        secure: false,
      },
    },
  },
})
