import type { KioskRequestHeaders } from '@/types/kiosk.types'

const SESSION_DEVICE_ID_KEY = 'oes_web_kiosk_device_id'
const SESSION_TOKEN_PREFIX = 'oes_web_kiosk_session_token_'
const KIOSK_WEB_VERSION = 'web-prototype-2.0.0'

/**
 * Shared contract between the React 19 Kiosk UI and the host runtime:
 * - Web SPA Prototype/Fallback uses WebKioskBridge (RAM + sessionStorage only).
 * - Sprint 4 Desktop Kiosk (OralExamination.DesktopKiosk) injects a native implementation
 *   backed by OS Keychain/DPAPI and encrypted LocalJournal via window.__OES_DESKTOP_KIOSK_BRIDGE__.
 */
export interface IKioskNativeBridge {
  readonly runtime: 'web-prototype' | 'desktop-kiosk'
  getDeviceId(): string
  getSessionToken(ticketId: string): string | null
  setSessionToken(ticketId: string, token: string): void
  clearSessionToken(ticketId: string): void
  getKioskVersion(): string
  getKioskHeaders(ticketId?: string | null): KioskRequestHeaders
  enterFullscreen(): Promise<void>
  exitFullscreen(): Promise<void>
  saveDraft<T>(scopeKey: string, payload: T): void
  loadDraft<T>(scopeKey: string): T | null
  clearDraft(scopeKey: string): void
}

declare global {
  interface Window {
    __OES_DESKTOP_KIOSK_BRIDGE__?: IKioskNativeBridge
  }
}

function generateWebKioskDeviceId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return `WEB-KIOSK-${crypto.randomUUID()}`
  }
  const randomPart = `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 12)}`
  return `WEB-KIOSK-${randomPart.padEnd(20, '0')}`
}

function isValidDeviceId(value: string | null | undefined): value is string {
  return typeof value === 'string' && /^[A-Za-z0-9._:-]{16,128}$/.test(value.trim())
}

export class WebKioskBridge implements IKioskNativeBridge {
  readonly runtime = 'web-prototype' as const
  private cachedDeviceId: string | null = null
  private readonly memorySessionTokens = new Map<string, string>()

  getDeviceId(): string {
    if (isValidDeviceId(this.cachedDeviceId)) {
      return this.cachedDeviceId
    }
    try {
      const existing = sessionStorage.getItem(SESSION_DEVICE_ID_KEY)
      if (isValidDeviceId(existing)) {
        this.cachedDeviceId = existing.trim()
        return this.cachedDeviceId
      }
    } catch {
      // sessionStorage có thể không khả dụng trong môi trường sandboxed.
    }

    const generated = generateWebKioskDeviceId()
    this.cachedDeviceId = generated
    try {
      sessionStorage.setItem(SESSION_DEVICE_ID_KEY, generated)
    } catch {
      // Giữ trong RAM khi không ghi được sessionStorage.
    }
    return generated
  }

  getSessionToken(ticketId: string): string | null {
    const normalizedId = ticketId.trim()
    if (!normalizedId) return null
    const fromMemory = this.memorySessionTokens.get(normalizedId)
    if (fromMemory) return fromMemory
    try {
      const stored = sessionStorage.getItem(`${SESSION_TOKEN_PREFIX}${normalizedId}`)
      if (stored && stored.trim()) {
        this.memorySessionTokens.set(normalizedId, stored.trim())
        return stored.trim()
      }
    } catch {
      // Bỏ qua lỗi đọc sessionStorage.
    }
    return null
  }

  setSessionToken(ticketId: string, token: string): void {
    const normalizedId = ticketId.trim()
    const normalizedToken = token.trim()
    if (!normalizedId || !normalizedToken) return
    this.memorySessionTokens.set(normalizedId, normalizedToken)
    try {
      sessionStorage.setItem(`${SESSION_TOKEN_PREFIX}${normalizedId}`, normalizedToken)
    } catch {
      // Giữ trong RAM nếu sessionStorage đầy.
    }
  }

  clearSessionToken(ticketId: string): void {
    const normalizedId = ticketId.trim()
    if (!normalizedId) return
    this.memorySessionTokens.delete(normalizedId)
    try {
      sessionStorage.removeItem(`${SESSION_TOKEN_PREFIX}${normalizedId}`)
    } catch {
      // Bỏ qua lỗi xóa sessionStorage.
    }
  }

  getKioskVersion(): string {
    return KIOSK_WEB_VERSION
  }

  getKioskHeaders(ticketId?: string | null): KioskRequestHeaders {
    const headers: KioskRequestHeaders = {
      deviceId: this.getDeviceId(),
      kioskVersion: this.getKioskVersion(),
    }
    if (ticketId) {
      const token = this.getSessionToken(ticketId)
      if (token) {
        headers.kioskSessionToken = token
      }
    }
    return headers
  }

  async enterFullscreen(): Promise<void> {
    if (typeof document === 'undefined') return
    await document.documentElement.requestFullscreen?.().catch(() => undefined)
  }

  async exitFullscreen(): Promise<void> {
    if (typeof document === 'undefined' || !document.fullscreenElement) return
    await document.exitFullscreen?.().catch(() => undefined)
  }

  saveDraft<T>(scopeKey: string, payload: T): void {
    try {
      sessionStorage.setItem(scopeKey, JSON.stringify(payload))
    } catch {
      // Bỏ qua lỗi giới hạn dung lượng sessionStorage.
    }
  }

  loadDraft<T>(scopeKey: string): T | null {
    try {
      const raw = sessionStorage.getItem(scopeKey)
      if (!raw) return null
      return JSON.parse(raw) as T
    } catch {
      return null
    }
  }

  clearDraft(scopeKey: string): void {
    try {
      sessionStorage.removeItem(scopeKey)
    } catch {
      // Bỏ qua lỗi xóa sessionStorage.
    }
  }
}

export const webKioskBridge = new WebKioskBridge()

export function getActiveKioskBridge(): IKioskNativeBridge {
  if (typeof window !== 'undefined' && window.__OES_DESKTOP_KIOSK_BRIDGE__) {
    return window.__OES_DESKTOP_KIOSK_BRIDGE__
  }
  return webKioskBridge
}
