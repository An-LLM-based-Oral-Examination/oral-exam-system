import { useEffect, useState, useCallback } from 'react'

/**
 * MF-04 Kiosk Mode Lockdown Hook (3 strikes violation detection)
 */
export function useKioskLockdown(maxViolations = 3, onLockdown?: () => void) {
  const [violations, setViolations] = useState(0)
  const [isLocked, setIsLocked] = useState(false)

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
    if (isLocked) return

    // Prevent context menu (right click)
    const handleContextMenu = (e: MouseEvent) => {
      e.preventDefault()
      recordViolation()
    }

    // Detect tab switch / window blur
    const handleBlur = () => {
      recordViolation()
    }

    // Block F12, Ctrl+C, Ctrl+V, Alt+Tab, etc.
    const handleKeyDown = (e: KeyboardEvent) => {
      if (
        e.key === 'F12' ||
        (e.ctrlKey && e.shiftKey && (e.key === 'I' || e.key === 'C')) ||
        (e.ctrlKey && (e.key === 'c' || e.key === 'v' || e.key === 'u'))
      ) {
        e.preventDefault()
        recordViolation()
      }
    }

    window.addEventListener('blur', handleBlur)
    document.addEventListener('contextmenu', handleContextMenu)
    document.addEventListener('keydown', handleKeyDown)

    return () => {
      window.removeEventListener('blur', handleBlur)
      document.removeEventListener('contextmenu', handleContextMenu)
      document.removeEventListener('keydown', handleKeyDown)
    }
  }, [isLocked, recordViolation])

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
