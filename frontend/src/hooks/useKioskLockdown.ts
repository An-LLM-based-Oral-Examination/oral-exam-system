import { useCallback, useEffect, useRef, useState } from 'react'

/**
 * MF-04 Kiosk Mode Lockdown Hook (3 strikes violation detection)
 */
export function useKioskLockdown(maxViolations = 3, onLockdown?: () => void, enabled = true) {
  const [violations, setViolations] = useState(0)
  const [isLocked, setIsLocked] = useState(false)
  const enteredFullscreenRef = useRef(false)

  const recordViolation = useCallback(() => {
    setViolations((prev) => {
      const next = prev + 1
      if (next >= maxViolations) {
        setIsLocked(true)
        if (onLockdown) onLockdown()
      }
      return next
    })
  }, [maxViolations, onLockdown])

  useEffect(() => {
    if (!enabled || isLocked) return

    void document.documentElement.requestFullscreen?.().catch(() => undefined)

    // Prevent context menu (right click)
    const handleContextMenu = (e: MouseEvent) => {
      e.preventDefault()
      recordViolation()
    }

    // Detect tab switch / window blur
    const handleBlur = () => {
      recordViolation()
    }

    const handleVisibilityChange = () => {
      if (document.visibilityState === 'hidden') recordViolation()
    }

    const handleFullscreenChange = () => {
      if (document.fullscreenElement) {
        enteredFullscreenRef.current = true
      } else if (enteredFullscreenRef.current) {
        recordViolation()
      }
    }

    // Block F12, Ctrl+C, Ctrl+V, Alt+Tab, etc.
    const handleKeyDown = (e: KeyboardEvent) => {
      if (
        e.key === 'F12' ||
        e.key === 'F11' ||
        e.key === 'Escape' ||
        (e.altKey && e.key === 'Tab') ||
        (e.ctrlKey && e.shiftKey && (e.key === 'I' || e.key === 'C')) ||
        (e.ctrlKey && (e.key === 'c' || e.key === 'v' || e.key === 'u'))
      ) {
        e.preventDefault()
        recordViolation()
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
  }, [enabled, isLocked, recordViolation])

  return {
    violations,
    isLocked,
    remainingStrikes: Math.max(0, maxViolations - violations),
    resetViolations: () => {
      setViolations(0)
      setIsLocked(false)
    },
  }
}

export { useKioskLockdown as useKiosk }
