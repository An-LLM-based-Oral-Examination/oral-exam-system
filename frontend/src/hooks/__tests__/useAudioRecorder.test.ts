import { act, renderHook } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useAudioRecorder } from '../useAudioRecorder'

interface MockMediaRecorderInstance {
  state: 'inactive' | 'recording' | 'paused'
  mimeType: string
  ondataavailable: ((event: { data: Blob }) => void) | null
  onstop: (() => void) | null
  onerror: (() => void) | null
  start: ReturnType<typeof vi.fn>
  stop: ReturnType<typeof vi.fn>
  requestData: ReturnType<typeof vi.fn>
}

describe('useAudioRecorder (M2 R4 regression guards)', () => {
  const originalMediaRecorder = globalThis.MediaRecorder
  const originalMediaDevices = navigator.mediaDevices
  const originalAudioContext = (window as unknown as { AudioContext?: unknown }).AudioContext
  const originalWebkitAudioContext = (window as unknown as { webkitAudioContext?: unknown })
    .webkitAudioContext

  let instances: MockMediaRecorderInstance[] = []
  let mockTracks: Array<{ stop: ReturnType<typeof vi.fn> }> = []
  let autoFireStop = true

  beforeEach(() => {
    vi.useFakeTimers()
    instances = []
    mockTracks = [{ stop: vi.fn() }]
    autoFireStop = true

    class MockMediaRecorder implements MockMediaRecorderInstance {
      static isTypeSupported = vi.fn(() => true)

      state: 'inactive' | 'recording' | 'paused' = 'inactive'
      mimeType: string
      ondataavailable: ((event: { data: Blob }) => void) | null = null
      onstop: (() => void) | null = null
      onerror: (() => void) | null = null
      start = vi.fn(() => {
        this.state = 'recording'
      })
      requestData = vi.fn()
      stop = vi.fn(() => {
        this.state = 'inactive'
        if (autoFireStop) {
          this.onstop?.()
        }
      })

      constructor(_stream: MediaStream, options?: { mimeType?: string }) {
        this.mimeType = options?.mimeType ?? 'audio/webm'
        instances.push(this)
      }
    }

    globalThis.MediaRecorder = MockMediaRecorder as unknown as typeof MediaRecorder
    Object.defineProperty(navigator, 'mediaDevices', {
      configurable: true,
      value: {
        getUserMedia: vi.fn(async () => ({
          getTracks: () => mockTracks,
        })),
      },
    })
    ;(window as unknown as { AudioContext?: unknown }).AudioContext = undefined
    ;(window as unknown as { webkitAudioContext?: unknown }).webkitAudioContext = undefined
  })

  afterEach(() => {
    vi.useRealTimers()
    globalThis.MediaRecorder = originalMediaRecorder
    Object.defineProperty(navigator, 'mediaDevices', {
      configurable: true,
      value: originalMediaDevices,
    })
    ;(window as unknown as { AudioContext?: unknown }).AudioContext = originalAudioContext
    ;(window as unknown as { webkitAudioContext?: unknown }).webkitAudioContext =
      originalWebkitAudioContext
    vi.restoreAllMocks()
  })

  it('emits 20s rotated segment normally and resolves final blob on stopRecording', async () => {
    const segments: Array<{ text: string; index: number }> = []
    const onSegment = vi.fn(async (blob: Blob, index: number) => {
      segments.push({ text: await blob.text(), index })
    })

    const { result } = renderHook(() =>
      useAudioRecorder({ segmentDurationMs: 20_000, onSegment }),
    )

    await act(async () => {
      await result.current.startRecording()
    })
    expect(result.current.isRecording).toBe(true)
    expect(instances).toHaveLength(1)

    const recorder1 = instances[0]
    recorder1.ondataavailable?.({ data: new Blob(['Seg0'], { type: 'audio/webm' }) })

    act(() => {
      vi.advanceTimersByTime(20_000)
    })

    expect(instances).toHaveLength(2)
    expect(onSegment).toHaveBeenCalledTimes(1)
    expect(onSegment).toHaveBeenCalledWith(expect.any(Blob), 0)

    const recorder2 = instances[1]
    recorder2.ondataavailable?.({ data: new Blob(['Tail1'], { type: 'audio/webm' }) })

    let finalBlob!: Blob
    await act(async () => {
      finalBlob = await result.current.stopRecording()
    })

    expect(await finalBlob.text()).toBe('Tail1')
    expect(result.current.isRecording).toBe(false)
    expect(mockTracks[0].stop).toHaveBeenCalledTimes(1)
  })

  it('prevents delayed prevRecorder.onstop after 20s rotation and cancelRecording from leaking into a subsequent session', async () => {
    autoFireStop = false
    const onSegment = vi.fn()
    const { result } = renderHook(() =>
      useAudioRecorder({ segmentDurationMs: 20_000, onSegment }),
    )

    // Session 1 (Q1)
    await act(async () => {
      await result.current.startRecording()
    })
    expect(instances).toHaveLength(1)
    const recorder1 = instances[0]
    recorder1.ondataavailable?.({ data: new Blob(['LeakedQ1'], { type: 'audio/webm' }) })

    // Trigger 20s segment rotation: recorder1 -> recorder2, but delay recorder1.onstop
    act(() => {
      vi.advanceTimersByTime(20_000)
    })
    expect(instances).toHaveLength(2)
    const recorder2 = instances[1]
    expect(recorder1.stop).toHaveBeenCalledTimes(1)
    const delayedPrevOnStop = recorder1.onstop
    expect(delayedPrevOnStop).not.toBeNull()

    // Cancel Q1 while recorder1.onstop has still not fired
    act(() => {
      result.current.cancelRecording()
    })

    expect(recorder1.onstop).toBeNull()
    expect(recorder1.ondataavailable).toBeNull()
    expect(recorder1.onerror).toBeNull()
    expect(recorder2.onstop).toBeNull()
    expect(result.current.isRecording).toBe(false)

    // Session 2 (Q2)
    await act(async () => {
      await result.current.startRecording()
    })
    expect(instances).toHaveLength(3)
    const recorder3 = instances[2]
    recorder3.ondataavailable?.({ data: new Blob(['CleanQ2'], { type: 'audio/webm' }) })

    // Simulate browser delivering delayed recorder1.onstop into Q2's session
    act(() => {
      delayedPrevOnStop?.()
    })
    expect(onSegment).not.toHaveBeenCalled()

    // Stop Q2 cleanly
    let q2Blob!: Blob
    await act(async () => {
      const stopPromise = result.current.stopRecording()
      recorder3.onstop?.()
      q2Blob = await stopPromise
    })

    expect(await q2Blob.text()).toBe('CleanQ2')
    expect(onSegment).not.toHaveBeenCalled()
  })

  it('prevents delayed prevRecorder.onstop after recorder.onerror from leaking into a new recording session', async () => {
    autoFireStop = false
    const onSegment = vi.fn()
    const { result } = renderHook(() =>
      useAudioRecorder({ segmentDurationMs: 20_000, onSegment }),
    )

    await act(async () => {
      await result.current.startRecording()
    })
    const recorder1 = instances[0]
    recorder1.ondataavailable?.({ data: new Blob(['LeakedOnErrorQ1'], { type: 'audio/webm' }) })

    act(() => {
      vi.advanceTimersByTime(20_000)
    })
    const recorder2 = instances[1]
    const delayedPrevOnStop = recorder1.onstop

    // Q1 fails via recorder2.onerror
    act(() => {
      recorder2.onerror?.()
    })
    expect(recorder1.onstop).toBeNull()
    expect(result.current.isRecording).toBe(false)

    // Q2 starts fresh
    await act(async () => {
      await result.current.startRecording()
    })
    const recorder3 = instances[2]
    recorder3.ondataavailable?.({ data: new Blob(['CleanQ2'], { type: 'audio/webm' }) })

    act(() => {
      delayedPrevOnStop?.()
    })
    expect(onSegment).not.toHaveBeenCalled()

    let q2Blob!: Blob
    await act(async () => {
      const stopPromise = result.current.stopRecording()
      recorder3.onstop?.()
      q2Blob = await stopPromise
    })

    expect(await q2Blob.text()).toBe('CleanQ2')
    expect(onSegment).not.toHaveBeenCalled()
  })
})
