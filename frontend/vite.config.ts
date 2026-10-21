import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'path'

// https://vite.dev/config/
const envDir = path.resolve(import.meta.dirname, '..')

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, envDir, '')
  const apiTarget = env.VITE_API_PROXY_TARGET || 'http://localhost:5000'

  return {
    envDir,
    plugins: [react(), tailwindcss()],
    resolve: {
      alias: {
        '@': path.resolve(import.meta.dirname, './src'),
      },
    },
    server: {
      port: 5173,
      host: true,
      strictPort: true,
      proxy: {
        '/api': {
          target: apiTarget,
          changeOrigin: true,
        },
        '/hubs': {
          target: apiTarget,
          changeOrigin: true,
          ws: true,
        },
        '/uploads': {
          target: apiTarget,
          changeOrigin: true,
        },
      },
    },
  }
})
