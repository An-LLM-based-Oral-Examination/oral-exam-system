const SESSION_EPOCH_KEY = 'oes_auth_session_epoch'

function createEpoch(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID()
  }
  return `${Date.now()}-${Math.random().toString(36).slice(2)}`
}

export function getSessionEpoch(): string {
  const current = sessionStorage.getItem(SESSION_EPOCH_KEY)
  if (current) return current
  const created = createEpoch()
  sessionStorage.setItem(SESSION_EPOCH_KEY, created)
  return created
}

export function rotateSessionEpoch(): string {
  const next = createEpoch()
  sessionStorage.setItem(SESSION_EPOCH_KEY, next)
  return next
}

export function isSessionEpochCurrent(expected: string): boolean {
  return sessionStorage.getItem(SESSION_EPOCH_KEY) === expected
}
