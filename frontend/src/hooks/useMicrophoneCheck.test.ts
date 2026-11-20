import { act, cleanup, renderHook } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useMicrophoneCheck } from './useMicrophoneCheck'

describe('useMicrophoneCheck', () => {
  const stop = vi.fn()
  const close = vi.fn().mockResolvedValue(undefined)
  const disconnect = vi.fn()
  let sample = 128

  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout', 'performance'] })
    sample = 128
    const track = { label: 'Test microphone', readyState: 'live', muted: false, stop }
    vi.stubGlobal('navigator', {
      mediaDevices: {
        getUserMedia: vi.fn().mockResolvedValue({
          getAudioTracks: () => [track],
          getTracks: () => [track],
        }),
      },
    })
    class TestAudioContext {
      state = 'running'
      close = close
      createAnalyser() {
        return {
          fftSize: 2048,
          smoothingTimeConstant: 0,
          getByteTimeDomainData: (samples: Uint8Array) => samples.fill(sample),
        }
      }
      createMediaStreamSource() { return { connect: vi.fn(), disconnect } }
    }
    vi.stubGlobal('AudioContext', TestAudioContext)
  })

  afterEach(() => {
    cleanup()
    vi.useRealTimers()
    vi.unstubAllGlobals()
    vi.clearAllMocks()
  })

  it('requires audible input and releases the microphone after success', async () => {
    sample = 140
    const { result } = renderHook(() => useMicrophoneCheck())
    await act(async () => { void result.current.handleDeviceCheck() })
    expect(result.current.deviceState).toBe('testing')
    await act(async () => { await vi.advanceTimersByTimeAsync(2000) })
    expect(result.current.deviceState).toBe('ready')
    expect(result.current.deviceNames).toBe('Test microphone')
    expect(stop).toHaveBeenCalled()
    expect(close).toHaveBeenCalled()
  })

  it('reports silence instead of marking the microphone ready', async () => {
    const { result } = renderHook(() => useMicrophoneCheck())
    await act(async () => { void result.current.handleDeviceCheck() })
    await act(async () => { await vi.advanceTimersByTimeAsync(10100) })
    expect(result.current.deviceState).toBe('error')
    expect(result.current.deviceIssue).toBe('silent')
    expect(stop).toHaveBeenCalled()
  })

  it('stops audio capture when leaving the page during a test', async () => {
    const { result, unmount } = renderHook(() => useMicrophoneCheck())
    await act(async () => { void result.current.handleDeviceCheck() })
    unmount()
    expect(stop).toHaveBeenCalled()
    expect(disconnect).toHaveBeenCalled()
    expect(close).toHaveBeenCalled()
    await act(async () => { await vi.advanceTimersByTimeAsync(100) })
  })
})
