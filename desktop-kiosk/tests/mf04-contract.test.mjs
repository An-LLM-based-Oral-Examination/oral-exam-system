import assert from 'node:assert/strict'
import { existsSync } from 'node:fs'
import { readFile } from 'node:fs/promises'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const root = join(dirname(fileURLToPath(import.meta.url)), '..')
const read = (relativePath) => readFile(join(root, relativePath), 'utf8')

test('Desktop Kiosk matches modular 3-tier repository structure', () => {
  for (const requiredPath of [
    'AGENTS.md',
    'README.md',
    'docs/ARCHITECTURE.md',
    'docs/WALKTHROUGH.md',
    'electron/main.ts',
    'electron/preload.ts',
    'electron/config.ts',
    'electron/device.ts',
    'electron/security.ts',
    'electron/windows-agent.ts',
    'native/Oes.WindowsAgent/Oes.WindowsAgent.csproj',
    'native/Oes.WindowsAgent/Program.cs',
    'scripts/verify-kiosk.mjs',
    'scripts/build-windows-agent.ps1',
    'scripts/run-tests.mjs',
    'src/main.tsx',
    'src/App.tsx',
    'src/services/api.ts',
    'src/services/offline-draft.ts',
    'src/types/mf04.types.ts',
    'src/utils/transcript-diff.ts',
    'src/hooks/useKioskHeartbeat.ts',
    'src/hooks/useAntiCheatMonitor.ts',
    'tsconfig.json',
    'tsconfig.app.json',
    'tsconfig.node.json',
    'vite.config.ts',
    'vite.electron.config.ts',
  ]) {
    assert.ok(existsSync(join(root, requiredPath)), `Missing expected path: ${requiredPath}`)
  }
})

test('MF-04 API client keeps all required BYOD, Heartbeat, Per-Turn, and Follow-Up endpoints without web storage', async () => {
  const source = await read('src/services/api.ts')
  for (const endpoint of [
    '/auth/password-login',
    '/auth/refresh-token',
    '/official-exams/tickets/current',
    '/heartbeat',
    '/audio-upload-authorizations',
    '/answers',
    '/next-followup',
    '/speech/stt',
    '/official-exams/anti-cheat/log',
    '/submission',
  ]) {
    assert.match(source, new RegExp(endpoint.replaceAll('/', '\\/')))
  }
  assert.match(source, /X-Device-Id/)
  assert.match(source, /X-Kiosk-Session-Token/)
  assert.match(source, /X-Kiosk-Version/)
  assert.doesNotMatch(source, /sessionStorage|localStorage/)
})

test('MF-04 renderer enforces per-turn SHA-256 audio verification, follow-up flow, and zero grade UI', async () => {
  const appSource = await read('src/App.tsx')
  const cryptoSource = await read('src/utils/crypto.ts')
  const submittedSource = await read('src/screens/SubmittedScreen.tsx')
  const blockedSource = await read('src/screens/BlockedScreen.tsx')

  assert.match(appSource, /createQuestionAudioUploadAuthorization/)
  assert.match(appSource, /submitQuestionAnswer/)
  assert.match(appSource, /requestNextFollowUp/)
  assert.match(appSource, /useKioskHeartbeat/)
  assert.match(appSource, /useAntiCheatMonitor/)
  assert.match(cryptoSource, /SHA-256/)
  assert.match(appSource, /submittedStatuses\.has\(response\.status\)/)
  assert.match(appSource, /clearTicketDrafts\(ticket\.id\)/)
  assert.match(submittedSource, /không hiển thị điểm/i)
  assert.match(blockedSource, /Không thể tiếp tục hoặc tự ý nộp bài/)
})

test('MF-04 recovery syncs ticket state from server and clears local evidence after confirmation', async () => {
  const source = await read('src/App.tsx')
  assert.match(source, /apiClient\.getTicketState\(currentTicket\.id\)/)
  assert.match(source, /loadTicketTurnDrafts\(loadedTicket\.id\)/)
  assert.match(
    source,
    /submittedStatuses = new Set\(\['SUBMITTED', 'AI_GRADED', 'AUDITED', 'PUBLISHED', 'LOCKED'\]\)/,
  )
  assert.match(source, /clearTicketDrafts\(ticket\.id\)/)
})

test('MF-04 anti-cheat blocks the session after exceeding three losses of focus', async () => {
  const source = await read('src/App.tsx')
  assert.match(source, /blurCountRef\.current \+= 1/)
  assert.match(source, /nextCount > 3/)
  assert.match(source, /setScreen\('blocked'\)/)
  assert.match(source, /appendAntiCheatLog\(eventType/)
})

test('local per-turn journal uses encrypted IndexedDB records', async () => {
  const source = await read('src/services/offline-draft.ts')
  assert.match(source, /indexedDB\.open/)
  assert.match(source, /AES-GCM/)
  assert.match(source, /crypto\.subtle\.encrypt/)
  assert.match(source, /crypto\.subtle\.decrypt/)
})

test('CSP permits only the local API and HTTPS presigned storage', async () => {
  const source = await read('index.html')
  assert.match(source, /http:\/\/127\.0\.0\.1:5000/)
  assert.match(source, /http:\/\/localhost:5000/)
  assert.match(source, /connect-src[^;]*https:/)
  assert.match(source, /src="\/src\/main\.tsx"/)
})

test('Electron shell keeps renderer isolation, sandboxed preload, and navigation restrictions', async () => {
  const mainSource = await read('electron/main.ts')
  const securitySource = await read('electron/security.ts')
  const preloadSource = await read('electron/preload.ts')

  assert.match(mainSource, /contextIsolation: true/)
  assert.match(mainSource, /nodeIntegration: false/)
  assert.match(mainSource, /sandbox: true/)
  assert.match(securitySource, /setWindowOpenHandler/)
  assert.match(securitySource, /before-input-event/)
  assert.doesNotMatch(preloadSource, /from\s+['"]\.\.?\/|require\(['"]\.\.?\//)
})
