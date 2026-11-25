import { app, BrowserWindow, ipcMain, Menu, session } from 'electron/main'
import path from 'node:path'
import {
  getApiBaseUrl,
  getDevRendererUrl,
  isAltTabTestMode,
  isTrustedRendererUrl,
  isWindowLockdownEnabled,
} from './config'
import { getOrCreatePersistentDeviceId } from './device'
import {
  configureWindowSecurity,
  enforceKioskWindow,
  getDisplayCount,
  watchDisplayChanges,
  type KioskSecurityEvent,
} from './security'
import { isWindowsAgentAvailable, probeWindowsAgent } from './windows-agent'

const electronDirectory = __dirname
let mainWindow: BrowserWindow | null = null
let exitAuthorized = false
let stopDisplayWatcher: (() => void) | null = null

function isTrustedSender(sender: Electron.WebContents): boolean {
  return Boolean(mainWindow && !mainWindow.isDestroyed() && sender === mainWindow.webContents)
}

function sendSecurityEventToRenderer(event: KioskSecurityEvent): void {
  if (!mainWindow || mainWindow.isDestroyed()) return
  mainWindow.webContents.send('kiosk:security-event', event)
}

function configureIpc(): void {
  ipcMain.handle('runtime-info', (event) => {
    if (!isTrustedSender(event.sender)) return null

    const appVersion = app.getVersion()
    return {
      appName: 'Oral Exam Desktop Kiosk',
      appVersion,
      isPackaged: app.isPackaged,
      kioskMode: isWindowLockdownEnabled(),
      altTabTestMode: isAltTabTestMode(),
      platform: process.platform,
      apiBaseUrl: getApiBaseUrl(),
      deviceId: getOrCreatePersistentDeviceId(),
      kioskVersion: `desktop-kiosk-${appVersion}`,
      displayCount: getDisplayCount(),
      isFullscreen: mainWindow ? mainWindow.isFullScreen() : false,
      windowsAgentAvailable: isWindowsAgentAvailable(electronDirectory),
    }
  })

  ipcMain.handle('security-snapshot', async (event) => {
    if (!isTrustedSender(event.sender)) return null

    const windowsAgent = await probeWindowsAgent(electronDirectory)
    const displayCount = Math.max(getDisplayCount(), windowsAgent?.displayCount ?? 1)
    return {
      deviceId: getOrCreatePersistentDeviceId(),
      displayCount,
      isFullscreen: mainWindow ? mainWindow.isFullScreen() : false,
      windowsAgent,
    }
  })

  ipcMain.handle('request-exit', (event) => {
    if (!isTrustedSender(event.sender) || (isWindowLockdownEnabled() && !exitAuthorized)) return false
    app.quit()
    return true
  })

  ipcMain.handle('authorize-exit-after-submit', (event) => {
    if (!isTrustedSender(event.sender)) return false
    exitAuthorized = true
    return true
  })
}

async function createMainWindow(): Promise<void> {
  const kioskMode = isWindowLockdownEnabled()
  exitAuthorized = false
  const preloadPath = path.join(electronDirectory, 'preload.cjs')

  mainWindow = new BrowserWindow({
    width: 1440,
    height: 900,
    minWidth: 1024,
    minHeight: 720,
    show: false,
    fullscreen: kioskMode,
    kiosk: kioskMode,
    frame: !kioskMode,
    closable: !kioskMode,
    minimizable: !kioskMode,
    maximizable: !kioskMode,
    skipTaskbar: kioskMode,
    alwaysOnTop: kioskMode,
    autoHideMenuBar: true,
    backgroundColor: '#08111f',
    webPreferences: {
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
      preload: preloadPath,
    },
  })

  configureWindowSecurity(mainWindow, electronDirectory, sendSecurityEventToRenderer)
  stopDisplayWatcher?.()
  stopDisplayWatcher = watchDisplayChanges(sendSecurityEventToRenderer)

  mainWindow.once('ready-to-show', () => {
    mainWindow?.show()
    if (mainWindow) enforceKioskWindow(mainWindow)
  })
  mainWindow.on('close', (event) => {
    if (!isWindowLockdownEnabled() || exitAuthorized) return
    event.preventDefault()
    if (mainWindow) enforceKioskWindow(mainWindow)
  })
  mainWindow.on('minimize', () => {
    if (mainWindow) enforceKioskWindow(mainWindow)
  })
  mainWindow.on('blur', () => {
    if (mainWindow) setTimeout(() => mainWindow && enforceKioskWindow(mainWindow), 0)
  })
  mainWindow.on('closed', () => {
    stopDisplayWatcher?.()
    stopDisplayWatcher = null
    mainWindow = null
  })

  if (!app.isPackaged && !process.argv.includes('--serve-built')) {
    await mainWindow.loadURL(getDevRendererUrl())
    return
  }

  await mainWindow.loadFile(path.join(electronDirectory, '..', 'dist', 'index.html'))
}

function configureApiCorsBridge(): void {
  const apiOrigin = new URL(getApiBaseUrl()).origin
  const filter = { urls: [`${apiOrigin}/*`] }

  session.defaultSession.webRequest.onBeforeSendHeaders(filter, (details, callback) => {
    const requestHeaders = { ...details.requestHeaders }
    delete requestHeaders.Origin
    delete requestHeaders.origin
    callback({ requestHeaders })
  })

  session.defaultSession.webRequest.onHeadersReceived(filter, (details, callback) => {
    const responseHeaders: Record<string, string | string[]> = { ...(details.responseHeaders ?? {}) }
    const hasAcao = Object.keys(responseHeaders).some((k) => k.toLowerCase() === 'access-control-allow-origin')
    if (!hasAcao) {
      responseHeaders['Access-Control-Allow-Origin'] = ['*']
      responseHeaders['Access-Control-Allow-Headers'] = [
        'Authorization, Content-Type, Accept, X-Device-Id, X-Kiosk-Version, X-Kiosk-Session-Token',
      ]
      responseHeaders['Access-Control-Allow-Methods'] = ['GET, POST, PUT, PATCH, DELETE, OPTIONS']
    }
    callback({ responseHeaders })
  })
}

app.on('certificate-error', (event, _webContents, url, _error, _certificate, callback) => {
  if (!app.isPackaged && url.startsWith('https://host.docker.internal:9000/')) {
    event.preventDefault()
    callback(true)
    return
  }
  callback(false)
})

app.whenReady().then(async () => {
  Menu.setApplicationMenu(null)
  configureIpc()
  configureApiCorsBridge()
  session.defaultSession.setPermissionRequestHandler((webContents, permission, callback) => {
    const allowed = permission === 'media' && isTrustedSender(webContents)
    callback(allowed)
  })
  await createMainWindow()

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) void createMainWindow()
  })
})

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit()
})

app.on('web-contents-created', (_event, contents) => {
  contents.on('will-navigate', (event, navigationUrl) => {
    if (!isTrustedRendererUrl(navigationUrl, electronDirectory)) event.preventDefault()
  })
  contents.setWindowOpenHandler(() => ({ action: 'deny' }))
})
