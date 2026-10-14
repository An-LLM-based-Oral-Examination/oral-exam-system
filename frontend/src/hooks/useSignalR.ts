import { useCallback, useEffect, useState } from 'react'
import { HubConnectionState } from '@microsoft/signalr'
import type { PracticeAnswerDetail } from '@/types/practice.types'
import { createPracticeHubConnection } from '@/services/signalr.service'
import { normalizePracticeAnswerDetail } from '@/services/practice.service'

export type PracticeHubStatus = 'idle' | 'connecting' | 'connected' | 'reconnecting' | 'offline'

export interface PracticeGradingError {
  answerId: string
  message: string
}

const INITIAL_RETRY_DELAYS = [0, 2_000, 5_000]

function wait(delay: number) {
  return new Promise<void>((resolve) => window.setTimeout(resolve, delay))
}

export function useSignalR(
  sessionId: string | null,
  hubUrl: string = import.meta.env.VITE_SIGNALR_HUB_URL || '/hubs/practice',
) {
  const [status, setStatus] = useState<PracticeHubStatus>(sessionId ? 'connecting' : 'idle')
  const [gradingResults, setGradingResults] = useState<PracticeAnswerDetail[]>([])
  const [gradingErrors, setGradingErrors] = useState<PracticeGradingError[]>([])
  const [reconnectVersion, setReconnectVersion] = useState(0)

  const clearGradingResult = useCallback((answerId: string) => {
    setGradingResults((current) => current.filter((result) => result.answerId !== answerId))
    setGradingErrors((current) => current.filter((error) => error.answerId !== answerId))
  }, [])

  useEffect(() => {
    if (!sessionId) {
      const resetTimer = window.setTimeout(() => {
        setStatus('idle')
        setGradingResults([])
        setGradingErrors([])
      }, 0)
      return () => window.clearTimeout(resetTimer)
    }

    let disposed = false
    let isStarting = false
    let isJoiningGroup = false
    let restartTimer: number | undefined
    let rejoinTimer: number | undefined
    const connection = createPracticeHubConnection(
      hubUrl,
      () => sessionStorage.getItem('access_token') ?? '',
    )

    const resetTimer = window.setTimeout(() => {
      setStatus('connecting')
      setGradingResults([])
      setGradingErrors([])
    }, 0)

    connection.on('ReceiveGradingResult', (payload: unknown) => {
      let result: PracticeAnswerDetail
      try {
        result = normalizePracticeAnswerDetail(payload)
      } catch {
        setGradingErrors((current) => [
          ...current,
          {
            answerId: 'unknown',
            message: 'Backend trả về kết quả chấm không đúng định dạng.',
          },
        ])
        return
      }
      setGradingResults((current) => {
        const withoutDuplicate = current.filter((item) => item.answerId !== result.answerId)
        return [...withoutDuplicate, result]
      })
      setGradingErrors((current) => current.filter((item) => item.answerId !== result.answerId))
    })

    connection.on('ReceiveGradingError', (answerId: string, message: string) => {
      setGradingErrors((current) => [
        ...current.filter((item) => item.answerId !== answerId),
        { answerId, message },
      ])
    })

    connection.onreconnecting(() => {
      if (!disposed) setStatus('reconnecting')
    })

    const scheduleGroupRejoin = () => {
      if (disposed || rejoinTimer !== undefined) return
      rejoinTimer = window.setTimeout(() => {
        rejoinTimer = undefined
        void joinSessionGroup(true)
      }, 5_000)
    }

    const joinSessionGroup = async (isReconnect: boolean) => {
      if (disposed || isJoiningGroup || connection.state !== HubConnectionState.Connected) {
        return
      }

      isJoiningGroup = true
      try {
        await connection.invoke('JoinSession', sessionId)
        if (!disposed) {
          setStatus('connected')
          if (isReconnect) setReconnectVersion((current) => current + 1)
        }
      } catch {
        if (!disposed) {
          setStatus('offline')
          scheduleGroupRejoin()
        }
      } finally {
        isJoiningGroup = false
      }
    }

    connection.onreconnected(() => {
      if (!disposed) void joinSessionGroup(true)
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
            return
          }
          await connection.invoke('JoinSession', sessionId)
          setStatus('connected')
          isStarting = false
          return
        } catch {
          if (connection.state !== HubConnectionState.Disconnected) {
            await connection.stop()
          }
        }
      }

      isStarting = false
      if (!disposed) {
        setStatus('offline')
        scheduleConnectionRestart()
      }
    }

    void startConnection()

    return () => {
      disposed = true
      window.clearTimeout(resetTimer)
      if (restartTimer !== undefined) window.clearTimeout(restartTimer)
      if (rejoinTimer !== undefined) window.clearTimeout(rejoinTimer)
      connection.off('ReceiveGradingResult')
      connection.off('ReceiveGradingError')

      const stopConnection = async () => {
        if (connection.state === HubConnectionState.Connected) {
          try {
            await connection.invoke('LeaveSession', sessionId)
          } catch {
            // Socket có thể đóng giữa lúc kiểm tra trạng thái và gọi LeaveSession.
          }
        }
        if (connection.state !== HubConnectionState.Disconnected) await connection.stop()
      }

      void stopConnection()
    }
  }, [hubUrl, sessionId])

  return {
    status,
    isConnected: status === 'connected',
    gradingResults,
    gradingErrors,
    reconnectVersion,
    clearGradingResult,
  }
}
