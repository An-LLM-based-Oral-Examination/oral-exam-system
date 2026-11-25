import { contextBridge, ipcRenderer } from 'electron'

// IMPORTANT: Keep preload.ts strictly self-contained (no relative runtime imports)
// so dist-electron/preload.cjs works reliably under Electron sandbox: true.

type MainRuntimeInfo = {
  appName: string
  appVersion: string
  isPackaged: boolean
  kioskMode: boolean
  altTabTestMode?: boolean
  isKioskMode?: boolean
  platform: string
  apiBaseUrl?: string
  deviceId?: string
  kioskVersion?: string
  displayCount?: number
  isFullscreen?: boolean
  windowsAgentAvailable?: boolean
}

type SecuritySnapshot = {
  deviceId: string
  displayCount: number
  isFullscreen: boolean
  windowsAgent: {
    deviceFingerprint: string
    displayCount: number
    vmDetected: boolean
    vmIndicators: string[]
    remoteControlDetected: boolean
    remoteControlProcesses: string[]
    disallowedProcesses: string[]
    probedAtUtc: string
  } | null
}

type SecurityEventPayload = {
  eventType: 'WINDOW_BLUR' | 'TAB_SWITCH' | 'FULLSCREEN_EXIT' | 'MULTI_MONITOR'
  details: string
  timestampUtc: string
}

const configuredApiBaseUrl =
  process.env.ORAL_EXAM_API_BASE_URL ?? process.env.VITE_API_BASE_URL ?? 'http://127.0.0.1:5000/api/v1'

contextBridge.exposeInMainWorld('kioskApi', {
  getRuntimeInfo: async () => {
    const info = (await ipcRenderer.invoke('runtime-info')) as MainRuntimeInfo | null
    if (!info) return null
    return {
      appName: info.appName,
      appVersion: info.appVersion,
      isPackaged: info.isPackaged,
      isKioskMode: info.isKioskMode ?? info.kioskMode,
      altTabTestMode: info.altTabTestMode ?? false,
      platform: info.platform,
      apiBaseUrl: info.apiBaseUrl ?? configuredApiBaseUrl,
      deviceId: info.deviceId ?? '',
      kioskVersion: info.kioskVersion ?? `desktop-kiosk-${info.appVersion}`,
      displayCount: info.displayCount ?? 1,
      isFullscreen: info.isFullscreen ?? false,
      windowsAgentAvailable: info.windowsAgentAvailable ?? false,
    }
  },
  getSecuritySnapshot: (): Promise<SecuritySnapshot | null> =>
    ipcRenderer.invoke('security-snapshot') as Promise<SecuritySnapshot | null>,
  onSecurityEvent: (listener: (event: SecurityEventPayload) => void): (() => void) => {
    const wrapped = (_event: Electron.IpcRendererEvent, payload: SecurityEventPayload) => {
      listener(payload)
    }
    ipcRenderer.on('kiosk:security-event', wrapped)
    return () => {
      ipcRenderer.removeListener('kiosk:security-event', wrapped)
    }
  },
  requestExit: (): Promise<boolean> => ipcRenderer.invoke('request-exit'),
  authorizeExitAfterSubmit: (): Promise<boolean> => ipcRenderer.invoke('authorize-exit-after-submit'),
})
