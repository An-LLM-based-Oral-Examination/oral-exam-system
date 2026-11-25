import { app } from 'electron/main'
import { randomUUID } from 'node:crypto'
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import path from 'node:path'

const DEVICE_ID_REGEX = /^[A-Za-z0-9._:-]{16,128}$/
const DEVICE_ID_FILENAME = 'kiosk-device-id.txt'

let cachedDeviceId: string | null = null

function generateDeviceId(): string {
  const platformTag = process.platform.replace(/[^a-z0-9]/gi, '').toLowerCase() || 'desk'
  return `oes-${platformTag}-${randomUUID()}`
}

export function getOrCreatePersistentDeviceId(): string {
  if (cachedDeviceId) return cachedDeviceId

  const userDataDir = app.getPath('userData')
  const filePath = path.join(userDataDir, DEVICE_ID_FILENAME)

  try {
    const existing = readFileSync(filePath, 'utf8').trim()
    if (DEVICE_ID_REGEX.test(existing)) {
      cachedDeviceId = existing
      return existing
    }
  } catch {
    // First launch or missing file; generate below.
  }

  const created = generateDeviceId()
  try {
    mkdirSync(userDataDir, { recursive: true })
    writeFileSync(filePath, created, { encoding: 'utf8' })
  } catch {
    // Keep in-memory device id for this session if userData is read-only.
  }

  cachedDeviceId = created
  return created
}
