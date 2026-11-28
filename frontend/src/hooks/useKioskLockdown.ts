import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react'

export type KioskViolationEventType =
  | 'WINDOW_BLUR'
  | 'TAB_SWITCH'
  | 'FULLSCREEN_EXIT'
  | 'DISALLOWED_PROCESS'

export type KioskViolationSeverity = 'low' | 'medium' | 'high' | 'critical'

export interface KioskViolationEvent {
  clientEventId: string
  eventType: KioskViolationEventType
  severity: KioskViolationSeverity
  details: string
}

export interface ServerKioskViolationState {
  violationCount: number
  maxViolations?: number
  lockThreshold?: number
  remainingStrikes?: number
  isLocked: boolean
}

export interface UseKioskLockdownOptions {
  initialViolations?: number
  initialLocked?: boolean
  maxViolations?: number
  lockThreshold?: number
  enabled?: boolean
  onViolation?: (event: KioskViolationEvent) => void | Promise<void>
}

const DEFAULT_MAX_VIOLATIONS = 3
const DEFAULT_LOCK_THRESHOLD = 4
const VIOLATION_DEBOUNCE_MS = 800

function createClientEventId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID()
  }
  return `kiosk-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`
}

/**
 * MF-04 Server-Authoritative Kiosk Mode Lockdown Hook.
 * Records debounced violation events with idempotent clientEventId and delegates
 * violation counting & lockdown state strictly to backend responses via syncFromServer.
 */
export function useKioskLockdown(options: UseKioskLockdownOptions = {}) {
  const {
    initialViolations = 0,
    initialLocked = false,
    maxViolations: initialMaxViolations = DEFAULT_MAX_VIOLATIONS,
    lockThreshold: initialLockThreshold = DEFAULT_LOCK_THRESHOLD,
    enabled = true,
    onViolation,
  } = options

  const [violations, setViolations] = useState(initialViolations)
  const [maxViolations, setMaxViolations] = useState(initialMaxViolations)
  const [lockThreshold, setLockThreshold] = useState(initialLockThreshold)
  const [remainingStrikes, setRemainingStrikes] = useState(
    Math.max(0, initialLockThreshold - initialViolations),
  )
  const [isLocked, setIsLocked] = useState(initialLocked)
  const [lastViolationEvent, setLastViolationEvent] = useState<KioskViolationEvent | null>(null)

  const enteredFullscreenRef = useRef(
    typeof document !== 'undefined' && Boolean(document.fullscreenElement),
  )
  const lastViolationAtRef = useRef(0)
  const onViolationRef = useRef(onViolation)

  useLayoutEffect(() => {
    onViolationRef.current = onViolation
  }, [onViolation])

  const syncFromServer = useCallback((serverState: ServerKioskViolationState) => {
    const nextMax = serverState.maxViolations ?? DEFAULT_MAX_VIOLATIONS
    const nextThreshold =
      serverState.lockThreshold ?? Math.max(nextMax + 1, DEFAULT_LOCK_THRESHOLD)
    const nextViolations = Math.max(0, serverState.violationCount)
    const nextRemaining =
      typeof serverState.remainingStrikes === 'number'
        ? Math.max(0, serverState.remainingStrikes)
        : Math.max(0, nextThreshold - nextViolations)

    setViolations(nextViolations)
    setMaxViolations(nextMax)
    setLockThreshold(nextThreshold)
    setRemainingStrikes(nextRemaining)
    setIsLocked(Boolean(serverState.isLocked))
  }, [])

  const emitViolation = useCallback(
    (
      eventType: KioskViolationEventType,
      severity: KioskViolationSeverity,
      details: string,
    ) => {
      const now = Date.now()
      if (now - lastViolationAtRef.current < VIOLATION_DEBOUNCE_MS) {
        return
      }
      lastViolationAtRef.current = now

      const event: KioskViolationEvent = {
        clientEventId: createClientEventId().slice(0, 128),
        eventType,
        severity,
        details,
      }
      setLastViolationEvent(event)
      void onViolationRef.current?.(event)
    },
    [],
  )

  useEffect(() => {
    if (!enabled || isLocked || document.fullscreenElement) return
    void document.documentElement.requestFullscreen?.().catch(() => undefined)
  }, [enabled, isLocked])

  useLayoutEffect(() => {
    if (!enabled || isLocked) return

    enteredFullscreenRef.current = Boolean(document.fullscreenElement)

    const handleContextMenu = (e: MouseEvent) => {
      e.preventDefault()
      emitViolation(
        'DISALLOWED_PROCESS',
        'medium',
        'Thí sinh mở menu chuột phải bị chặn trong chế độ Kiosk.',
      )
    }

    const handleBlur = () => {
      emitViolation('WINDOW_BLUR', 'high', 'Cửa sổ thi mất tiêu điểm (window blur).')
    }

    const handleVisibilityChange = () => {
      if (document.visibilityState === 'hidden') {
        emitViolation('TAB_SWITCH', 'high', 'Thí sinh chuyển tab hoặc ẩn cửa sổ trình duyệt.')
      }
    }

    const handleFullscreenChange = () => {
      if (document.fullscreenElement) {
        enteredFullscreenRef.current = true
      } else if (enteredFullscreenRef.current) {
        emitViolation(
          'FULLSCREEN_EXIT',
          'high',
          'Thí sinh thoát khỏi chế độ toàn màn hình Kiosk.',
        )
      }
    }

    const handleKeyDown = (e: KeyboardEvent) => {
      const key = e.key
      if (
        key === 'F12' ||
        key === 'F11' ||
        key === 'Escape' ||
        (e.altKey && key === 'Tab') ||
        (e.ctrlKey && e.shiftKey && (key === 'I' || key === 'i' || key === 'C' || key === 'c')) ||
        (e.ctrlKey && (key === 'c' || key === 'C' || key === 'v' || key === 'V' || key === 'u' || key === 'U'))
      ) {
        e.preventDefault()
        emitViolation(
          'DISALLOWED_PROCESS',
          'medium',
          `Tổ hợp phím bị chặn trong chế độ Kiosk (${key}).`,
        )
      }
    }

    window.addEventListener('blur', handleBlur)
    document.addEventListener('visibilitychange', handleVisibilityChange)
    document.addEventListener('fullscreenchange', handleFullscreenChange)
    document.addEventListener('contextmenu', handleContextMenu)
    document.addEventListener('keydown', handleKeyDown)

    return () => {
      window.removeEventListener('blur', handleBlur)
      document.removeEventListener('visibilitychange', handleVisibilityChange)
      document.removeEventListener('fullscreenchange', handleFullscreenChange)
      document.removeEventListener('contextmenu', handleContextMenu)
      document.removeEventListener('keydown', handleKeyDown)
      enteredFullscreenRef.current = false
    }
  }, [enabled, isLocked, emitViolation])

  const resetViolations = useCallback(() => {
    lastViolationAtRef.current = 0
    setViolations(0)
    setMaxViolations(DEFAULT_MAX_VIOLATIONS)
    setLockThreshold(DEFAULT_LOCK_THRESHOLD)
    setRemainingStrikes(DEFAULT_LOCK_THRESHOLD)
    setIsLocked(false)
    setLastViolationEvent(null)
  }, [])

  return {
    violations,
    maxViolations,
    lockThreshold,
    remainingStrikes,
    isLocked,
    lastViolationEvent,
    syncFromServer,
    resetViolations,
  }
}

export { useKioskLockdown as useKiosk }
