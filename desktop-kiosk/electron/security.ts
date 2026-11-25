import { screen, type BrowserWindow } from 'electron/main'
import { isAltTabLockEnabled, isTrustedRendererUrl, isWindowLockdownEnabled } from './config'

export type KioskSecurityEventType =
  | 'WINDOW_BLUR'
  | 'TAB_SWITCH'
  | 'FULLSCREEN_EXIT'
  | 'MULTI_MONITOR'

export interface KioskSecurityEvent {
  eventType: KioskSecurityEventType
  details: string
  timestampUtc: string
}

export function getDisplayCount(): number {
  try {
    const displays = screen.getAllDisplays()
    return displays.length > 0 ? displays.length : 1
  } catch {
    return 1
  }
}

export function enforceKioskWindow(window: BrowserWindow): void {
  if (!isWindowLockdownEnabled() || window.isDestroyed()) return

  if (window.isMinimized()) window.restore()
  if (!window.isFullScreen()) window.setFullScreen(true)
  if (!window.isKiosk()) window.setKiosk(true)
  window.setAlwaysOnTop(true, 'screen-saver')
  window.focus()
}

export function configureWindowSecurity(
  window: BrowserWindow,
  electronDirectory: string,
  emitSecurityEvent: (event: KioskSecurityEvent) => void,
): void {
  window.webContents.setWindowOpenHandler(() => ({ action: 'deny' }))
  window.webContents.on('will-navigate', (event, navigationUrl) => {
    if (!isTrustedRendererUrl(navigationUrl, electronDirectory)) event.preventDefault()
  })
  window.webContents.on('will-attach-webview', (event) => {
    event.preventDefault()
  })
  window.webContents.on('before-input-event', (event, input) => {
    const normalizedKey = input.key.toLowerCase()
    const normalizedCode = input.code.toLowerCase()
    const isTabKey = normalizedKey === 'tab' || normalizedCode === 'tab'
    const isDeveloperShortcut =
      normalizedKey === 'f12' ||
      (input.control && input.shift && ['i', 'j', 'c'].includes(normalizedKey))
    const isTaskSwitchShortcut =
      (isAltTabLockEnabled() && input.alt && isTabKey) ||
      (input.meta && isTabKey) ||
      (input.control && isTabKey)
    const isNavigationShortcut =
      normalizedKey === 'f5' ||
      normalizedKey === 'f11' ||
      (input.control && ['r', 'l', 'n', 't', 'w', 'q', 'escape'].includes(normalizedKey)) ||
      (input.alt && ['left', 'right', 'f4', 'escape', 'space'].includes(normalizedKey)) ||
      input.meta

    if (isDeveloperShortcut || isTaskSwitchShortcut || isNavigationShortcut) {
      event.preventDefault()
      emitSecurityEvent({
        eventType: 'TAB_SWITCH',
        details: `Electron chặn tổ hợp phím: ${input.key}`,
        timestampUtc: new Date().toISOString(),
      })
    }
  })

  window.on('leave-full-screen', () => {
    emitSecurityEvent({
      eventType: 'FULLSCREEN_EXIT',
      details: 'Cửa sổ thoát khỏi chế độ toàn màn hình.',
      timestampUtc: new Date().toISOString(),
    })
    enforceKioskWindow(window)
  })
}

export function watchDisplayChanges(
  emitSecurityEvent: (event: KioskSecurityEvent) => void,
): () => void {
  const onDisplayChanged = () => {
    const count = getDisplayCount()
    if (count > 1) {
      emitSecurityEvent({
        eventType: 'MULTI_MONITOR',
        details: `Phát hiện ${count} màn hình đang kết nối.`,
        timestampUtc: new Date().toISOString(),
      })
    }
  }

  screen.on('display-added', onDisplayChanged)
  screen.on('display-removed', onDisplayChanged)

  return () => {
    screen.removeListener('display-added', onDisplayChanged)
    screen.removeListener('display-removed', onDisplayChanged)
  }
}
