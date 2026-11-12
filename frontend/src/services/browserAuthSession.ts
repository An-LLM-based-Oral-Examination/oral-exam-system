const ACTIVE_IDENTITY_KEY = 'oes_active_identity'
const CHANNEL_NAME = 'oes-auth'

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

export const browserAuthSession = {
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
