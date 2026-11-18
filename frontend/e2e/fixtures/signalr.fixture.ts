import type { Page, WebSocketRoute } from '@playwright/test'
import { isLiveApiMode } from './env'

const RECORD_SEPARATOR = '\x1e'

export type HubName = 'practice' | 'in-class-shift'

export interface SignalRInvocationRecord {
  hub: HubName
  target: string
  arguments: unknown[]
}

export interface SignalRFixture {
  emitPracticeEvent: (target: string, ...args: unknown[]) => Promise<void>
  emitInClassEvent: (target: string, ...args: unknown[]) => Promise<void>
  getInvocations: (hub?: HubName) => SignalRInvocationRecord[]
}

interface SignalRWireFrame {
  type?: number
  protocol?: string
  version?: number
  invocationId?: string
  target?: string
  arguments?: unknown[]
}

export async function createSignalRFixture(page: Page): Promise<SignalRFixture> {
  const practiceSockets = new Set<WebSocketRoute>()
  const inClassSockets = new Set<WebSocketRoute>()
  const invocations: SignalRInvocationRecord[] = []
  let connectionSeq = 0

  if (!isLiveApiMode()) {
    await page.route('**/hubs/*/negotiate**', async (route) => {
      if (route.request().method().toUpperCase() !== 'POST') {
        return route.fallback()
      }
      connectionSeq += 1
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          negotiateVersion: 1,
          connectionId: `e2e-conn-${connectionSeq}`,
          connectionToken: `e2e-token-${connectionSeq}`,
          availableTransports: [
            {
              transport: 'WebSockets',
              transferFormats: ['Text', 'Binary'],
            },
          ],
        }),
      })
    })

    await page.routeWebSocket(/\/hubs\/(practice|in-class-shift)/, (ws) => {
      const hub: HubName = ws.url().includes('/hubs/in-class-shift') ? 'in-class-shift' : 'practice'
      const socketSet = hub === 'practice' ? practiceSockets : inClassSockets
      socketSet.add(ws)

      ws.onMessage((rawMessage) => {
        const text = typeof rawMessage === 'string' ? rawMessage : rawMessage.toString('utf8')
        const frames = text.split(RECORD_SEPARATOR).filter((part) => part.trim().length > 0)
        for (const frame of frames) {
          let parsed: SignalRWireFrame
          try {
            parsed = JSON.parse(frame) as SignalRWireFrame
          } catch {
            continue
          }

          if (typeof parsed.protocol === 'string') {
            ws.send(`{}${RECORD_SEPARATOR}`)
            continue
          }

          if (parsed.type === 6) {
            ws.send(JSON.stringify({ type: 6 }) + RECORD_SEPARATOR)
            continue
          }

          if (parsed.type === 1 && typeof parsed.target === 'string') {
            invocations.push({
              hub,
              target: parsed.target,
              arguments: Array.isArray(parsed.arguments) ? parsed.arguments : [],
            })
            if (typeof parsed.invocationId === 'string') {
              ws.send(
                JSON.stringify({
                  type: 3,
                  invocationId: parsed.invocationId,
                  result: null,
                }) + RECORD_SEPARATOR,
              )
            }
          }
        }
      })

      ws.onClose(() => {
        socketSet.delete(ws)
      })
    })
  }

  const sendToSockets = async (
    hub: HubName,
    socketSet: Set<WebSocketRoute>,
    target: string,
    args: unknown[],
  ): Promise<void> => {
    if (isLiveApiMode()) {
      throw new Error(`SignalR mock emit(${target}) cannot be called when E2E_LIVE_API=1.`)
    }

    const deadline = Date.now() + 3000
    while (socketSet.size === 0 && Date.now() < deadline) {
      await new Promise((resolve) => setTimeout(resolve, 50))
    }

    if (socketSet.size === 0) {
      throw new Error(`No active SignalR WebSocket connection found for hub "${hub}".`)
    }

    const payload = JSON.stringify({ type: 1, target, arguments: args }) + RECORD_SEPARATOR
    for (const socket of socketSet) {
      socket.send(payload)
    }
  }

  return {
    emitPracticeEvent: (target, ...args) => sendToSockets('practice', practiceSockets, target, args),
    emitInClassEvent: (target, ...args) =>
      sendToSockets('in-class-shift', inClassSockets, target, args),
    getInvocations: (hub) =>
      hub ? invocations.filter((item) => item.hub === hub) : [...invocations],
  }
}
