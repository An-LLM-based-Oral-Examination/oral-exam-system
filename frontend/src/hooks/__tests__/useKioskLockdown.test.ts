import { act, renderHook } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useKioskLockdown } from '../useKioskLockdown'

function setFullscreenElement(value: Element | null) {
  Object.defineProperty(document, 'fullscreenElement', {
    configurable: true,
    value,
  })
}

describe('useKioskLockdown — FULLSCREEN_EXIT first-exit detection', () => {
  const originalRequestFullscreen = document.documentElement.requestFullscreen

  beforeEach(() => {
    setFullscreenElement(null)
    document.documentElement.requestFullscreen = vi.fn().mockResolvedValue(undefined)
  })

  afterEach(() => {
    setFullscreenElement(null)
    document.documentElement.requestFullscreen = originalRequestFullscreen
    vi.restoreAllMocks()
  })

  it('triggers FULLSCREEN_EXIT on the very first fullscreenchange when already fullscreen on mount', () => {
    const onViolation = vi.fn()
    setFullscreenElement(document.documentElement)

    renderHook(() =>
      useKioskLockdown({
        enabled: true,
        onViolation,
      }),
    )

    expect(document.documentElement.requestFullscreen).not.toHaveBeenCalled()

    act(() => {
      setFullscreenElement(null)
      document.dispatchEvent(new Event('fullscreenchange'))
    })

    expect(onViolation).toHaveBeenCalledTimes(1)
    expect(onViolation).toHaveBeenCalledWith(
      expect.objectContaining({
        eventType: 'FULLSCREEN_EXIT',
        severity: 'high',
        clientEventId: expect.any(String),
      }),
    )
  })

  it('triggers FULLSCREEN_EXIT on first exit when fullscreen was entered before enabled became true (OfficialExamPage flow)', () => {
    const onViolation = vi.fn()

    const { rerender } = renderHook(
      ({ enabled }: { enabled: boolean }) =>
        useKioskLockdown({
          enabled,
          onViolation,
        }),
      { initialProps: { enabled: false } },
    )

    // Simulate bridge.enterFullscreen() completing before audioRecorder.startRecording() enables lockdown.
    setFullscreenElement(document.documentElement)
    rerender({ enabled: true })

    act(() => {
      setFullscreenElement(null)
      document.dispatchEvent(new Event('fullscreenchange'))
    })

    expect(onViolation).toHaveBeenCalledTimes(1)
    expect(onViolation).toHaveBeenCalledWith(
      expect.objectContaining({
        eventType: 'FULLSCREEN_EXIT',
        severity: 'high',
      }),
    )
  })

  it('does not emit false-positive FULLSCREEN_EXIT if fullscreen was never active', () => {
    const onViolation = vi.fn()
    setFullscreenElement(null)

    renderHook(() =>
      useKioskLockdown({
        enabled: true,
        onViolation,
      }),
    )

    act(() => {
      document.dispatchEvent(new Event('fullscreenchange'))
    })

    expect(onViolation).not.toHaveBeenCalled()
  })
})
