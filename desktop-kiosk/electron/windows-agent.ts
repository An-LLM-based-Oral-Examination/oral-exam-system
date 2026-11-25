import { app } from 'electron/main'
import { execFile } from 'node:child_process'
import { existsSync } from 'node:fs'
import path from 'node:path'

export interface WindowsAgentProbe {
  deviceFingerprint: string
  displayCount: number
  vmDetected: boolean
  vmIndicators: string[]
  remoteControlDetected: boolean
  remoteControlProcesses: string[]
  disallowedProcesses: string[]
  probedAtUtc: string
}

function resolveWindowsAgentExecutable(electronDirectory: string): string | null {
  if (process.platform !== 'win32') return null

  const candidates = app.isPackaged
    ? [path.join(process.resourcesPath, 'native', 'Oes.WindowsAgent', 'Oes.WindowsAgent.exe')]
    : [
        path.resolve(electronDirectory, '..', 'native', 'dist', 'Oes.WindowsAgent', 'Oes.WindowsAgent.exe'),
        path.resolve(
          electronDirectory,
          '..',
          'native',
          'Oes.WindowsAgent',
          'bin',
          'Release',
          'net8.0',
          'Oes.WindowsAgent.exe',
        ),
        path.resolve(
          electronDirectory,
          '..',
          'native',
          'Oes.WindowsAgent',
          'bin',
          'Debug',
          'net8.0',
          'Oes.WindowsAgent.exe',
        ),
      ]

  for (const candidate of candidates) {
    if (existsSync(candidate)) return candidate
  }

  return null
}

export function isWindowsAgentAvailable(electronDirectory: string): boolean {
  return resolveWindowsAgentExecutable(electronDirectory) !== null
}

export function probeWindowsAgent(electronDirectory: string, timeoutMs = 2500): Promise<WindowsAgentProbe | null> {
  const executablePath = resolveWindowsAgentExecutable(electronDirectory)
  if (!executablePath) return Promise.resolve(null)

  return new Promise((resolve) => {
    execFile(
      executablePath,
      ['--probe'],
      { timeout: timeoutMs, windowsHide: true, maxBuffer: 256 * 1024 },
      (error, stdout) => {
        if (error || !stdout.trim()) {
          resolve(null)
          return
        }

        try {
          const parsed = JSON.parse(stdout.trim()) as WindowsAgentProbe
          if (typeof parsed?.deviceFingerprint === 'string' && typeof parsed?.displayCount === 'number') {
            resolve(parsed)
            return
          }
          resolve(null)
        } catch {
          resolve(null)
        }
      },
    )
  })
}
