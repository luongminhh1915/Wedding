import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    tailwindcss()
  ],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5008',
        changeOrigin: true,
        secure: false
      },
      '/hubs': {
        target: 'http://localhost:5008',
        ws: true,
        changeOrigin: true
      }
    }
  }
})
