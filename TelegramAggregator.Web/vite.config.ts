import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    proxy: {
      '/api': {
        target: process.env['services__api__http__0'] ?? 'http://localhost:5068',
        changeOrigin: true,
      },
    },
  },
})
