import { act, renderHook, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useAntiCheatHub } from '../useAntiCheatHub'
import { ensureFreshAccessToken } from '@/services/api.client'
import { officialExamService } from '@/services/officialExam.service'

const hubMock = vi.hoisted(() => {
  type Handler = (...args: unknown[]) => void
  const state = {
    hubUrl: '',
    accessTokenFactory: undefined as undefined | (() => string | Promise<string>),
    connectionState: 'Disconnected' as string,
    handlers: new Map<string, Handler>(),
    reconnectingCb: undefined as undefined | (() => void),
    reconnectedCb: undefined as undefined | (() => void),
    closeCb: undefined as undefined | (() => void),
    start: vi.fn(),
    stop: vi.fn(),
    invoke: vi.fn(),
  }
  return state
})

vi.mock('@microsoft/signalr', () => {
  class MockHubConnectionBuilder {
    withUrl(url: string, options?: { accessTokenFactory?: () => string | Promise<string> }) {
      hubMock.hubUrl = url
      hubMock.accessTokenFactory = options?.accessTokenFactory
      return this
    }
    withAutomaticReconnect() {
      return this
    }
    configureLogging() {
      return this
    }
    build() {
      return {
        get state() {
          return hubMock.connectionState
        },
        on: vi.fn((event: string, cb: (...args: unknown[]) => void) => {
          hubMock.handlers.set(event, cb)
        }),
        off: vi.fn((event: string) => {
          hubMock.handlers.delete(event)
        }),
        onreconnecting: vi.fn((cb: () => void) => {
          hubMock.reconnectingCb = cb
        }),
        onreconnected: vi.fn((cb: () => void) => {
          hubMock.reconnectedCb = cb
        }),
        onclose: vi.fn((cb: () => void) => {
          hubMock.closeCb = cb
        }),
        start: hubMock.start,
        stop: hubMock.stop,
        invoke: hubMock.invoke,
      }
    }
  }

  return {
    HubConnectionBuilder: MockHubConnectionBuilder,
    HubConnectionState: {
      Disconnected: 'Disconnected',
      Connecting: 'Connecting',
      Connected: 'Connected',
      Disconnecting: 'Disconnecting',
      Reconnecting: 'Reconnecting',
    },
    LogLevel: { Warning: 3 },
  }
})

vi.mock('@/services/api.client', () => ({
  ensureFreshAccessToken: vi.fn().mockResolvedValue('fresh-anti-cheat-jwt'),
  default: { get: vi.fn(), post: vi.fn(), put: vi.fn() },
}))

describe('useAntiCheatHub', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    hubMock.hubUrl = ''
    hubMock.accessTokenFactory = undefined
    hubMock.connectionState = 'Disconnected'
    hubMock.handlers.clear()
    hubMock.reconnectingCb = undefined
    hubMock.reconnectedCb = undefined
    hubMock.closeCb = undefined
    hubMock.start.mockImplementation(async () => {
      hubMock.connectionState = 'Connected'
    })
    hubMock.stop.mockImplementation(async () => {
      hubMock.connectionState = 'Disconnected'
    })
    hubMock.invoke.mockResolvedValue(undefined)
    vi.spyOn(officialExamService, 'getAntiCheatLogs').mockResolvedValue([])
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.restoreAllMocks()
  })

  it('joins shift, normalizes alerts missing clientEventId, deduplicates, reconnects, and leaves on unmount', async () => {
    const onAlert = vi.fn()
    const { result, unmount } = renderHook(() => useAntiCheatHub('shift-1', { onAlert }))

    await waitFor(() => expect(result.current.status).toBe('connected'))
    expect(hubMock.hubUrl).toBe('/hubs/anti-cheat')
    expect(await hubMock.accessTokenFactory?.()).toBe('fresh-anti-cheat-jwt')
    expect(ensureFreshAccessToken).toHaveBeenCalledWith(90)
    expect(hubMock.invoke).toHaveBeenCalledWith('JoinShift', 'shift-1')

    const receiveHandler = hubMock.handlers.get('ReceiveAntiCheatAlert')
    expect(receiveHandler).toBeDefined()

    act(() => {
      receiveHandler?.({
        id: 'log-1',
        ticketId: 'ticket-1',
        shiftId: 'shift-1',
        studentId: 'student-1',
        studentCode: 'SE170001',
        studentName: 'Nguyen An',
        eventType: 'WINDOW_BLUR',
        severity: 'HIGH',
        details: 'Rời khỏi cửa sổ thi',
        violationCount: 1,
        occurredAt: '2026-11-20T08:05:00.000Z',
      })
      receiveHandler?.({
        id: 'log-1',
        ticketId: 'ticket-1',
        shiftId: 'shift-1',
        studentId: 'student-1',
        studentCode: 'SE170001',
        studentName: 'Nguyen An',
        eventType: 'WINDOW_BLUR',
        severity: 'HIGH',
        details: 'Rời khỏi cửa sổ thi',
        violationCount: 1,
        occurredAt: '2026-11-20T08:05:00.000Z',
      })
    })

    expect(result.current.alerts).toHaveLength(1)
    expect(result.current.alerts[0].clientEventId).toBe('log-1')
    expect(result.current.latestAlert?.id).toBe('log-1')
    expect(onAlert).toHaveBeenCalledTimes(2)

    await act(async () => {
      hubMock.reconnectingCb?.()
    })
    expect(result.current.status).toBe('reconnecting')

    await act(async () => {
      hubMock.connectionState = 'Connected'
      hubMock.reconnectedCb?.()
    })
    await waitFor(() => expect(result.current.status).toBe('connected'))
    expect(result.current.reconnectVersion).toBe(1)
    expect(hubMock.invoke).toHaveBeenCalledTimes(2)

    unmount()
    await waitFor(() => expect(hubMock.invoke).toHaveBeenCalledWith('LeaveShift', 'shift-1'))
    expect(hubMock.stop).toHaveBeenCalled()
  })

  it('runs fallback REST polling while reconnecting/offline and stops retrying on permanent FORBIDDEN error', async () => {
    vi.useFakeTimers()
    const polledLog = {
      id: 'log-poll-1',
      ticketId: 'ticket-2',
      shiftId: 'shift-poll',
      studentId: 'student-2',
      clientEventId: 'evt-poll-1',
      studentCode: 'SE170002',
      studentName: 'Tran Binh',
      eventType: 'MULTI_DISPLAY',
      severity: 'CRITICAL',
      details: 'Phát hiện màn hình phụ',
      violationCount: 2,
      occurredAt: '2026-11-20T08:20:00.000Z',
    }
    const getLogsSpy = vi
      .spyOn(officialExamService, 'getAntiCheatLogs')
      .mockResolvedValue([polledLog])
    const onFallbackSync = vi.fn()

    const { result, unmount } = renderHook(() =>
      useAntiCheatHub('shift-poll', { fallbackPollIntervalMs: 5_000, onFallbackSync }),
    )

    await act(async () => {
      await vi.advanceTimersByTimeAsync(0)
    })
    expect(result.current.status).toBe('connected')

    await act(async () => {
      hubMock.reconnectingCb?.()
    })
    expect(result.current.status).toBe('reconnecting')

    await act(async () => {
      await vi.advanceTimersByTimeAsync(5_000)
    })
    expect(getLogsSpy).toHaveBeenCalledWith('shift-poll')
    expect(onFallbackSync).toHaveBeenCalledWith([polledLog])
    expect(result.current.alerts).toHaveLength(1)
    unmount()

    hubMock.invoke.mockRejectedValueOnce(new Error('FORBIDDEN: Không có quyền theo dõi ca thi'))
    getLogsSpy.mockClear()

    const forbiddenHook = renderHook(() =>
      useAntiCheatHub('shift-denied', { fallbackPollIntervalMs: 5_000 }),
    )
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0)
    })
    expect(forbiddenHook.result.current.status).toBe('offline')
    expect(forbiddenHook.result.current.lastError).toMatch(/Không có quyền/)

    await act(async () => {
      await vi.advanceTimersByTimeAsync(15_000)
    })
    expect(getLogsSpy).not.toHaveBeenCalled()
    forbiddenHook.unmount()
  })

  it('closes the SignalR socket immediately when reconnect JoinShift hits permanent FORBIDDEN error', async () => {
    vi.useFakeTimers()
    const getLogsSpy = vi.spyOn(officialExamService, 'getAntiCheatLogs').mockResolvedValue([])
    const { result, unmount } = renderHook(() =>
      useAntiCheatHub('shift-reconnect-denied', { fallbackPollIntervalMs: 5_000 }),
    )

    await act(async () => {
      await vi.advanceTimersByTimeAsync(0)
    })
    expect(result.current.status).toBe('connected')

    hubMock.stop.mockClear()
    hubMock.invoke.mockRejectedValueOnce(new Error('FORBIDDEN: Quyền theo dõi ca đã bị thu hồi'))

    await act(async () => {
      hubMock.connectionState = 'Connected'
      hubMock.reconnectedCb?.()
      await Promise.resolve()
      await Promise.resolve()
    })

    expect(result.current.status).toBe('offline')
    expect(hubMock.stop).toHaveBeenCalledTimes(1)

    await act(async () => {
      await vi.advanceTimersByTimeAsync(15_000)
    })
    expect(getLogsSpy).not.toHaveBeenCalled()
    unmount()
  })
})
