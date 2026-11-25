import { app } from 'electron/main'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const DEFAULT_DEV_API_BASE_URL = 'http://127.0.0.1:5000/api/v1'
const DEFAULT_PROD_API_BASE_URL = 'https://oralexam.io.vn/api/v1'
const DEV_RENDERER_ORIGIN = 'http://127.0.0.1:5174'

export function isKioskMode(): boolean {
  if (app.isPackaged) return true
  return process.env.KIOSK_MODE !== '0'
}

export function isAltTabTestMode(): boolean {
  const explicitTestSwitch =
    process.env.KIOSK_DISABLE_ALT_TAB === '1' || process.argv.includes('--disable-alt-tab')
  const testBuild = app.isPackaged && app.getVersion().includes('-test')
  return explicitTestSwitch || testBuild
}

export function isWindowLockdownEnabled(): boolean {
  return isKioskMode() && !isAltTabTestMode()
}

export function isAltTabLockEnabled(): boolean {
  return !isAltTabTestMode()
}

export function getApiBaseUrl(): string {
  const configuredUrl = process.env.KIOSK_API_BASE_URL?.trim()
  if (!configuredUrl) {
    if (!app.isPackaged && !process.argv.includes('--serve-built')) {
      return `${DEV_RENDERER_ORIGIN}/api/v1`
    }
    return app.isPackaged ? DEFAULT_PROD_API_BASE_URL : DEFAULT_DEV_API_BASE_URL
  }

  const fallback = app.isPackaged ? DEFAULT_PROD_API_BASE_URL : DEFAULT_DEV_API_BASE_URL
  try {
    const parsedUrl = new URL(configuredUrl)
    if (parsedUrl.protocol !== 'http:' && parsedUrl.protocol !== 'https:') {
      return fallback
    }
    return `${parsedUrl.origin}${parsedUrl.pathname.replace(/\/$/, '')}`
  } catch {
    return fallback
  }
}

export function getDevRendererUrl(): string {
  return DEV_RENDERER_ORIGIN
}

export function isTrustedRendererUrl(candidate: string, electronDirectory: string): boolean {
  try {
    const parsedUrl = new URL(candidate)
    if (parsedUrl.protocol === 'file:') {
      const expectedPath = path.resolve(electronDirectory, '..', 'dist', 'index.html')
      return path.resolve(fileURLToPath(parsedUrl)) === expectedPath
    }
    return !app.isPackaged && parsedUrl.origin === DEV_RENDERER_ORIGIN
  } catch {
    return false
  }
}
