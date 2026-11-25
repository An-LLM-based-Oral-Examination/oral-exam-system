interface ImportMetaEnv {
  readonly DEV: boolean
  readonly PROD: boolean
  readonly VITE_API_BASE_URL?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}

declare global {
  interface WindowsAgentProbe {
    deviceFingerprint: string
    displayCount: number
    vmDetected: boolean
    vmIndicators: string[]
    remoteControlDetected: boolean
    remoteControlProcesses: string[]
    disallowedProcesses: string[]
    probedAtUtc: string
  }

  interface KioskSecuritySnapshot {
    deviceId: string
    displayCount: number
    isFullscreen: boolean
    windowsAgent: WindowsAgentProbe | null
  }

  interface KioskSecurityEvent {
    eventType: 'WINDOW_BLUR' | 'TAB_SWITCH' | 'FULLSCREEN_EXIT' | 'MULTI_MONITOR'
    details: string
    timestampUtc: string
  }

  interface KioskRuntimeInfo {
    appName: string
    appVersion: string
    isPackaged: boolean
    isKioskMode: boolean
    altTabTestMode: boolean
    platform: string
    apiBaseUrl: string
    deviceId: string
    kioskVersion: string
    displayCount: number
    isFullscreen: boolean
    windowsAgentAvailable: boolean
  }

  interface KioskApi {
    getRuntimeInfo: () => Promise<KioskRuntimeInfo | null>
    getSecuritySnapshot: () => Promise<KioskSecuritySnapshot | null>
    onSecurityEvent: (listener: (event: KioskSecurityEvent) => void) => () => void
    requestExit: () => Promise<boolean>
    authorizeExitAfterSubmit: () => Promise<boolean>
  }

  interface Window {
    kioskApi?: KioskApi
  }
}

export {}
