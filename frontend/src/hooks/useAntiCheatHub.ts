import { useCallback, useEffect, useRef, useState } from 'react'
import { HubConnectionState } from '@microsoft/signalr'
import { ensureFreshAccessToken } from '@/services/api.client'
import { normalizeAntiCheatLog, officialExamService } from '@/services/officialExam.service'
import { createAntiCheatHubConnection } from '@/services/signalr.service'
import type { ExamAntiCheatLog } from '@/types/kiosk.types'

export type AntiCheatHubStatus = 'idle' | 'connecting' | 'connected' | 'reconnecting' | 'offline'

export interface UseAntiCheatHubOptions {
  hubUrl?: string
  enabled?: boolean
  fallbackPollIntervalMs?: number
  onAlert?: (alert: ExamAntiCheatLog) => void
  onFallbackSync?: (logs: ExamAntiCheatLog[]) => void
}

const INITIAL_RETRY_DELAYS = [0, 2_000, 5_000]
const DEFAULT_FALLBACK_POLL_MS = 15_000
const MAX_STORED_ALERTS = 200

function wait(delay: number) {
  return new Promise<void>((resolve) => window.setTimeout(resolve, delay))
}

function isPermanentHubError(error: unknown): boolean {
  const message = error instanceof Error ? error.message : String(error ?? '')
  return /NOT_FOUND|FORBIDDEN|BAD_REQUEST/i.test(message)
}

function alertDedupeKey(item: ExamAntiCheatLog): string {
  return item.clientEventId || item.id
}

function mergeAlerts(
  current: ExamAntiCheatLog[],
  incoming: ExamAntiCheatLog[],
): ExamAntiCheatLog[] {
  const seen = new Map<string, ExamAntiCheatLog>()
  for (const item of [...incoming, ...current]) {
    const key = alertDedupeKey(item)
    if (!seen.has(key)) {
      seen.set(key, item)
    }
  }
  return Array.from(seen.values())
    .sort((left, right) => Date.parse(right.occurredAt) - Date.parse(left.occurredAt))
    .slice(0, MAX_STORED_ALERTS)
}

export function useAntiCheatHub(
  shiftId: string | null,
  options: UseAntiCheatHubOptions = {},
) {
  const {
    hubUrl = import.meta.env.VITE_ANTI_CHEAT_HUB_URL || '/hubs/anti-cheat',
    enabled = true,
    fallbackPollIntervalMs = DEFAULT_FALLBACK_POLL_MS,
    onAlert,
    onFallbackSync,
  } = options

  const activeShiftId = enabled ? shiftId : null
  const [status, setStatus] = useState<AntiCheatHubStatus>(activeShiftId ? 'connecting' : 'idle')
  const [alerts, setAlerts] = useState<ExamAntiCheatLog[]>([])
  const [latestAlert, setLatestAlert] = useState<ExamAntiCheatLog | null>(null)
  const [reconnectVersion, setReconnectVersion] = useState(0)
  const [lastError, setLastError] = useState<string | null>(null)
  const [isPermanentFailure, setIsPermanentFailure] = useState(false)

  const onAlertRef = useRef(onAlert)
  const onFallbackSyncRef = useRef(onFallbackSync)

  useEffect(() => {
    onAlertRef.current = onAlert
    onFallbackSyncRef.current = onFallbackSync
  }, [onAlert, onFallbackSync])

  const clearAlerts = useCallback(() => {
    setAlerts([])
    setLatestAlert(null)
    setLastError(null)
  }, [])

  useEffect(() => {
    if (!activeShiftId) {
      const resetTimer = window.setTimeout(() => {
        setStatus('idle')
        setAlerts([])
        setLatestAlert(null)
        setLastError(null)
        setIsPermanentFailure(false)
      }, 0)
      return () => window.clearTimeout(resetTimer)
    }

    let disposed = false
    let isStarting = false
    let isJoiningGroup = false
    let restartTimer: number | undefined
    let rejoinTimer: number | undefined

    const connection = createAntiCheatHubConnection(hubUrl, () => ensureFreshAccessToken(90))

    const syncCatchUpLogs = async () => {
      try {
        const logs = await officialExamService.getAntiCheatLogs(activeShiftId)
        if (disposed) return
        if (logs.length > 0) {
          setAlerts((current) => mergeAlerts(current, logs))
        }
        onFallbackSyncRef.current?.(logs)
      } catch {
        // Giữ trạng thái hiện tại; chu kỳ polling hoặc lần làm mới kế tiếp sẽ thử lại.
      }
    }

    connection.on('ReceiveAntiCheatAlert', (payload: unknown) => {
      let alert: ExamAntiCheatLog
      try {
        alert = normalizeAntiCheatLog(payload)
      } catch {
        if (!disposed) {
          setLastError('Máy chủ gửi cảnh báo chống gian lận không đúng định dạng.')
        }
        return
      }
      if (disposed) return
      setLastError(null)
      setLatestAlert(alert)
      setAlerts((current) => mergeAlerts(current, [alert]))
      onAlertRef.current?.(alert)
    })

    connection.onreconnecting(() => {
      if (!disposed) setStatus('reconnecting')
    })

    const scheduleGroupRejoin = () => {
      if (disposed || rejoinTimer !== undefined) return
      rejoinTimer = window.setTimeout(() => {
        rejoinTimer = undefined
        void joinShiftGroup(true)
      }, 5_000)
    }

    const joinShiftGroup = async (isReconnect: boolean) => {
      if (disposed || isJoiningGroup || connection.state !== HubConnectionState.Connected) {
        return
      }
      isJoiningGroup = true
      try {
        await connection.invoke('JoinShift', activeShiftId)
        if (!disposed) {
          setStatus('connected')
          setLastError(null)
          if (isReconnect) {
            setReconnectVersion((current) => current + 1)
            void syncCatchUpLogs()
          }
        }
      } catch (joinError) {
        if (!disposed) {
          setStatus('offline')
          if (isPermanentHubError(joinError)) {
            disposed = true
            setIsPermanentFailure(true)
            setLastError('Không có quyền hoặc ca thi không hợp lệ để nhận cảnh báo chống gian lận.')
            void connection.stop().catch(() => undefined)
          } else {
            scheduleGroupRejoin()
          }
        }
      } finally {
        isJoiningGroup = false
      }
    }

    connection.onreconnected(() => {
      if (!disposed) void joinShiftGroup(true)
    })

    connection.onclose(() => {
      if (!disposed) {
        setStatus('offline')
        scheduleConnectionRestart()
      }
    })

    const scheduleConnectionRestart = () => {
      if (disposed || restartTimer !== undefined) return
      restartTimer = window.setTimeout(() => {
        restartTimer = undefined
        void startConnection()
      }, 10_000)
    }

    const startConnection = async () => {
      if (disposed || isStarting || connection.state !== HubConnectionState.Disconnected) return
      isStarting = true

      for (const delay of INITIAL_RETRY_DELAYS) {
        if (delay > 0) await wait(delay)
        if (disposed) {
          isStarting = false
          return
        }

        try {
          await connection.start()
          if (disposed) {
            await connection.stop()
            isStarting = false
            return
          }
          await connection.invoke('JoinShift', activeShiftId)
          if (!disposed) {
            setStatus('connected')
            setLastError(null)
          }
          isStarting = false
          return
        } catch (startError) {
          if (connection.state !== HubConnectionState.Disconnected) {
            await connection.stop()
          }
          if (isPermanentHubError(startError)) {
            isStarting = false
            if (!disposed) {
              disposed = true
              setStatus('offline')
              setIsPermanentFailure(true)
              setLastError('Không có quyền hoặc ca thi không hợp lệ để nhận cảnh báo chống gian lận.')
            }
            return
          }
        }
      }

      isStarting = false
      if (!disposed) {
        setStatus('offline')
        scheduleConnectionRestart()
      }
    }

    const startTimer = window.setTimeout(() => {
      if (disposed) return
      setStatus('connecting')
      setAlerts([])
      setLatestAlert(null)
      setLastError(null)
      setIsPermanentFailure(false)
      void startConnection()
    }, 0)

    return () => {
      disposed = true
      window.clearTimeout(startTimer)
      if (restartTimer !== undefined) window.clearTimeout(restartTimer)
      if (rejoinTimer !== undefined) window.clearTimeout(rejoinTimer)
      connection.off('ReceiveAntiCheatAlert')

      const stopConnection = async () => {
        if (connection.state === HubConnectionState.Connected) {
          try {
            await connection.invoke('LeaveShift', activeShiftId)
          } catch {
            // Kết nối có thể đã ngắt giữa lúc kiểm tra trạng thái và gọi LeaveShift.
          }
        }
        if (connection.state !== HubConnectionState.Disconnected) {
          await connection.stop()
        }
      }

      void stopConnection()
    }
  }, [activeShiftId, hubUrl])

  useEffect(() => {
    if (
      !activeShiftId ||
      isPermanentFailure ||
      fallbackPollIntervalMs <= 0 ||
      (status !== 'offline' && status !== 'reconnecting')
    ) {
      return
    }

    let cancelled = false
    const poll = async () => {
      try {
        const logs = await officialExamService.getAntiCheatLogs(activeShiftId)
        if (cancelled) return
        if (logs.length > 0) {
          setAlerts((current) => mergeAlerts(current, logs))
        }
        onFallbackSyncRef.current?.(logs)
      } catch {
        // Bỏ qua lỗi mạng tạm thời trong chu kỳ fallback polling.
      }
    }

    const timer = window.setInterval(() => {
      void poll()
    }, fallbackPollIntervalMs)

    return () => {
      cancelled = true
      window.clearInterval(timer)
    }
  }, [activeShiftId, fallbackPollIntervalMs, isPermanentFailure, status])

  return {
    status,
    isConnected: status === 'connected',
    alerts,
    latestAlert,
    reconnectVersion,
    lastError,
    clearAlerts,
  }
}
