const ACTIVE_IDENTITY_KEY = 'oes_active_identity'
const CHANNEL_NAME = 'oes-auth'
const REFRESH_LOCK_KEY = 'oes_auth_refresh_lock'
const REFRESH_LOCK_NAME = 'oes-auth-refresh'
const REFRESH_LOCK_TTL_MS = 20_000

type BrowserAuthEventType = 'LOGIN' | 'LOGOUT'

export interface BrowserAuthEvent {
  eventId: string
  type: BrowserAuthEventType
  userId: string
  sourceTabId: string
  occurredAt: number
}

interface ActiveIdentity extends BrowserAuthEvent {
  type: 'LOGIN'
}

function createId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID()
  }
  return `${Date.now()}-${Math.random().toString(36).slice(2)}`
}

const TAB_ID = createId()

function parseEvent(value: unknown, expectedType?: BrowserAuthEventType): BrowserAuthEvent | null {
  if (!value) return null
  try {
    const parsed = (
      typeof value === 'string' ? JSON.parse(value) : value
    ) as Partial<BrowserAuthEvent>
    if (
      (parsed.type !== 'LOGIN' && parsed.type !== 'LOGOUT') ||
      (expectedType && parsed.type !== expectedType) ||
      typeof parsed.eventId !== 'string' ||
      typeof parsed.userId !== 'string' ||
      typeof parsed.sourceTabId !== 'string' ||
      typeof parsed.occurredAt !== 'number'
    ) {
      return null
    }
    return parsed as BrowserAuthEvent
  } catch {
    return null
  }
}

function readActiveIdentity(): ActiveIdentity | null {
  try {
    return parseEvent(localStorage.getItem(ACTIVE_IDENTITY_KEY), 'LOGIN') as ActiveIdentity | null
  } catch {
    return null
  }
}

function createEvent(type: BrowserAuthEventType, userId: string): BrowserAuthEvent {
  return {
    eventId: createId(),
    type,
    userId,
    sourceTabId: TAB_ID,
    occurredAt: Date.now(),
  }
}

function broadcast(event: BrowserAuthEvent): void {
  if (typeof BroadcastChannel === 'undefined') return
  try {
    const channel = new BroadcastChannel(CHANNEL_NAME)
    channel.postMessage(event)
    channel.close()
  } catch {
    // localStorage events remain the fallback for browsers that block BroadcastChannel.
  }
}

interface RefreshLockLease {
  owner: string
  expiresAt: number
}

function readRefreshLease(): RefreshLockLease | null {
  try {
    const parsed = JSON.parse(
      localStorage.getItem(REFRESH_LOCK_KEY) ?? 'null',
    ) as Partial<RefreshLockLease> | null
    return parsed && typeof parsed.owner === 'string' && typeof parsed.expiresAt === 'number'
      ? (parsed as RefreshLockLease)
      : null
  } catch {
    return null
  }
}

async function waitForRefreshLease(): Promise<void> {
  await new Promise<void>((resolve) => {
    const timeout = window.setTimeout(done, 75)
    function done() {
      window.clearTimeout(timeout)
      window.removeEventListener('storage', handleStorage)
      resolve()
    }
    function handleStorage(event: StorageEvent) {
      if (event.key === REFRESH_LOCK_KEY) done()
    }
    window.addEventListener('storage', handleStorage)
  })
}

async function withStorageRefreshLock<T>(action: () => Promise<T>): Promise<T> {
  const owner = createId()
  const deadline = Date.now() + REFRESH_LOCK_TTL_MS * 2

  while (Date.now() < deadline) {
    let acquired = false
    try {
      const current = readRefreshLease()
      if (!current || current.expiresAt <= Date.now()) {
        const lease: RefreshLockLease = { owner, expiresAt: Date.now() + REFRESH_LOCK_TTL_MS }
        localStorage.setItem(REFRESH_LOCK_KEY, JSON.stringify(lease))
        acquired = readRefreshLease()?.owner === owner
      }
    } catch {
      return action()
    }

    if (acquired) {
      try {
        return await action()
      } finally {
        try {
          if (readRefreshLease()?.owner === owner) localStorage.removeItem(REFRESH_LOCK_KEY)
        } catch {
          // The lease expires automatically if storage becomes unavailable.
        }
      }
    }
    await waitForRefreshLease()
  }

  throw new Error('Không thể đồng bộ phiên làm mới đăng nhập giữa các tab.')
}

export const browserAuthSession = {
  async withRefreshLock<T>(action: () => Promise<T>): Promise<T> {
    if (typeof navigator !== 'undefined' && navigator.locks) {
      return navigator.locks.request(REFRESH_LOCK_NAME, action)
    }
    return withStorageRefreshLock(action)
  },

  isIdentityAllowed(userId: string): boolean {
    const active = readActiveIdentity()
    return !active || active.userId === userId
  },

  claimIdentity(userId: string): boolean {
    const active = readActiveIdentity()
    if (active && active.userId !== userId) return false
    if (!active) this.activateIdentity(userId)
    return true
  },

  activateIdentity(userId: string): void {
    const event = createEvent('LOGIN', userId)
    try {
      localStorage.setItem(ACTIVE_IDENTITY_KEY, JSON.stringify(event))
    } catch {
      // BroadcastChannel still coordinates currently open tabs when storage is unavailable.
    }
    broadcast(event)
  },

  deactivateIdentity(userId: string): void {
    const event = createEvent('LOGOUT', userId)
    try {
      const active = readActiveIdentity()
      if (active?.userId === userId) localStorage.removeItem(ACTIVE_IDENTITY_KEY)
    } catch {
      // Broadcast the logout even when storage is unavailable.
    }
    broadcast(event)
  },

  subscribe(listener: (event: BrowserAuthEvent) => void): () => void {
    let channel: BroadcastChannel | null = null

    if (typeof BroadcastChannel !== 'undefined') {
      try {
        channel = new BroadcastChannel(CHANNEL_NAME)
        channel.onmessage = (message: MessageEvent<unknown>) => {
          const event = parseEvent(message.data)
          if (event && event.sourceTabId !== TAB_ID) listener(event)
        }
      } catch {
        channel = null
      }
    }

    const handleStorage = (storageEvent: StorageEvent) => {
      if (channel || storageEvent.key !== ACTIVE_IDENTITY_KEY) return
      const event = storageEvent.newValue
        ? parseEvent(storageEvent.newValue, 'LOGIN')
        : parseEvent(storageEvent.oldValue, 'LOGIN')
      if (!event || event.sourceTabId === TAB_ID) return
      listener(storageEvent.newValue ? event : { ...event, type: 'LOGOUT' })
    }

    window.addEventListener('storage', handleStorage)
    return () => {
      window.removeEventListener('storage', handleStorage)
      channel?.close()
    }
  },
}
