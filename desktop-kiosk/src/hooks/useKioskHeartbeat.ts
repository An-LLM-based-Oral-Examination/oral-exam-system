import { useEffect, useRef } from 'react'
import { classifyByodConflict, describeByodConflict } from '../services/api'
import type { ApiClient } from '../services/api'
import type { ByodConflictCode, KioskHeartbeatResponse } from '../types/mf04.types'

interface UseKioskHeartbeatOptions {
  apiClient: ApiClient
  ticketId: string | null
  enabled: boolean
  intervalMs?: number
  getAudioLevelDb: () => number | null
  onServerSync: (response: KioskHeartbeatResponse) => void
  onTerminate: (response: KioskHeartbeatResponse) => void
  onByodConflict: (code: ByodConflictCode, message: string) => void
}

export function useKioskHeartbeat({
  apiClient,
  ticketId,
  enabled,
  intervalMs = 20_000,
  getAudioLevelDb,
  onServerSync,
  onTerminate,
  onByodConflict,
}: UseKioskHeartbeatOptions): void {
  const callbacksRef = useRef({ getAudioLevelDb, onServerSync, onTerminate, onByodConflict })
  callbacksRef.current = { getAudioLevelDb, onServerSync, onTerminate, onByodConflict }

  useEffect(() => {
    if (!enabled || !ticketId) return

    let cancelled = false
    let inFlight = false
    const controller = new AbortController()

    const pulse = async () => {
      if (cancelled || inFlight || !apiClient.getKioskSessionToken()) return
      inFlight = true
      try {
        const snapshot = await window.kioskApi?.getSecuritySnapshot().catch(() => null)
        if (cancelled) return

        const audioLevelDb = callbacksRef.current.getAudioLevelDb()
        const response = await apiClient.sendHeartbeat(
          ticketId,
          {
            audioLevelDb,
            isFullscreen: snapshot?.isFullscreen ?? null,
            displayCount: snapshot?.displayCount ?? null,
          },
          controller.signal,
        )
        if (cancelled) return

        callbacksRef.current.onServerSync(response)
        if (response.isLocked || response.shouldTerminateExam || response.deviceUnlockRequired) {
          callbacksRef.current.onTerminate(response)
        }
      } catch (error) {
        if (cancelled) return
        const conflict = classifyByodConflict(error)
        if (conflict) {
          callbacksRef.current.onByodConflict(conflict, describeByodConflict(conflict))
        }
      } finally {
        inFlight = false
      }
    }

    void pulse()
    const timerId = window.setInterval(() => {
      void pulse()
    }, intervalMs)

    return () => {
      cancelled = true
      controller.abort()
      window.clearInterval(timerId)
    }
  }, [apiClient, enabled, intervalMs, ticketId])
}
