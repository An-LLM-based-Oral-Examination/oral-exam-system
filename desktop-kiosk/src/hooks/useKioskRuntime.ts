import { useEffect, useState } from 'react'

const defaultRuntimeInfo: KioskRuntimeInfo = {
  appName: 'Oral Exam Desktop Kiosk',
  appVersion: 'development',
  isPackaged: false,
  isKioskMode: false,
  altTabTestMode: false,
  platform: 'renderer',
  apiBaseUrl: 'http://127.0.0.1:5000/api/v1',
  deviceId: '',
  kioskVersion: 'desktop-kiosk-development',
  displayCount: 1,
  isFullscreen: false,
  windowsAgentAvailable: false,
}

export function useKioskRuntime(): KioskRuntimeInfo {
  const [runtimeInfo, setRuntimeInfo] = useState<KioskRuntimeInfo>(defaultRuntimeInfo)

  useEffect(() => {
    let mounted = true
    window.kioskApi
      ?.getRuntimeInfo()
      .then((info) => {
        if (mounted && info) setRuntimeInfo(info)
      })
      .catch(() => undefined)

    return () => {
      mounted = false
    }
  }, [])

  return runtimeInfo
}
