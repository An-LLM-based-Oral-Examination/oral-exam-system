import { act, renderHook } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useSpeechRecognition } from '../useSpeechRecognition'

interface MockSpeechInstance {
  lang: string
  continuous: boolean
  interimResults: boolean
  onstart: (() => void) | null
  onresult: ((event: unknown) => void) | null
  onerror: ((event: { error: string }) => void) | null
  onend: (() => void) | null
  start: ReturnType<typeof vi.fn>
  stop: ReturnType<typeof vi.fn>
}

describe('useSpeechRecognition double-start guard (M2 R2)', () => {
  const originalSpeech = (window as unknown as { SpeechRecognition?: unknown }).SpeechRecognition
  const originalWebkitSpeech = (window as unknown as { webkitSpeechRecognition?: unknown }).webkitSpeechRecognition

  let createdInstances: MockSpeechInstance[] = []

  beforeEach(() => {
    createdInstances = []
    class MockSpeechRecognition implements MockSpeechInstance {
      lang = ''
      continuous = false
      interimResults = false
      onstart: (() => void) | null = null
      onresult: ((event: unknown) => void) | null = null
      onerror: ((event: { error: string }) => void) | null = null
      onend: (() => void) | null = null
      start = vi.fn()
      stop = vi.fn()

      constructor() {
        createdInstances.push(this)
      }
    }
    ;(window as unknown as { SpeechRecognition?: unknown }).SpeechRecognition = MockSpeechRecognition
    ;(window as unknown as { webkitSpeechRecognition?: unknown }).webkitSpeechRecognition = undefined
  })

  afterEach(() => {
    ;(window as unknown as { SpeechRecognition?: unknown }).SpeechRecognition = originalSpeech
    ;(window as unknown as { webkitSpeechRecognition?: unknown }).webkitSpeechRecognition = originalWebkitSpeech
    vi.restoreAllMocks()
  })

  it('stops and detaches handlers of a pending SpeechRecognition instance before starting a new one', () => {
    const { result, unmount } = renderHook(() => useSpeechRecognition())

    act(() => {
      result.current.startListening('vi-VN')
      // Double-start trước khi onstart của instance đầu kịp chạy
      result.current.startListening('en-US')
    })

    expect(createdInstances).toHaveLength(2)
    const [firstInstance, secondInstance] = createdInstances

    expect(firstInstance.stop).toHaveBeenCalledTimes(1)
    expect(firstInstance.onstart).toBeNull()
    expect(firstInstance.onresult).toBeNull()
    expect(firstInstance.onerror).toBeNull()
    expect(firstInstance.onend).toBeNull()

    expect(secondInstance.start).toHaveBeenCalledTimes(1)
    expect(secondInstance.lang).toBe('en-US')

    // Late error/end từ instance cũ không được làm hỏng state của instance mới
    act(() => {
      secondInstance.onstart?.()
    })
    expect(result.current.isListening).toBe(true)
    expect(result.current.error).toBeNull()

    unmount()
    expect(secondInstance.stop).toHaveBeenCalledTimes(1)
  })

  it('detaches handlers and ignores queued onstart/onresult when stopListening is called while onstart is pending', () => {
    const { result } = renderHook(() => useSpeechRecognition())

    act(() => {
      result.current.startListening('vi-VN')
    })

    expect(createdInstances).toHaveLength(1)
    const [pendingInstance] = createdInstances
    const queuedOnStart = pendingInstance.onstart
    const queuedOnResult = pendingInstance.onresult
    const queuedOnError = pendingInstance.onerror
    const queuedOnEnd = pendingInstance.onend

    expect(result.current.isListening).toBe(false)

    act(() => {
      result.current.stopListening()
    })

    expect(pendingInstance.stop).toHaveBeenCalledTimes(1)
    expect(pendingInstance.onstart).toBeNull()
    expect(pendingInstance.onresult).toBeNull()
    expect(pendingInstance.onerror).toBeNull()
    expect(pendingInstance.onend).toBeNull()

    // Even if browser cached handler references before stopListening(), queued events must be ignored
    act(() => {
      queuedOnStart?.()
      queuedOnResult?.({
        resultIndex: 0,
        results: {
          length: 1,
          0: { 0: { transcript: 'Nội dung Q1 bị rò sang Q2' } },
        },
      })
      queuedOnError?.({ error: 'aborted' })
      queuedOnEnd?.()
    })

    expect(result.current.isListening).toBe(false)
    expect(result.current.transcript).toBe('')
    expect(result.current.getCurrentTranscript()).toBe('')
    expect(result.current.error).toBeNull()
  })

  it('detaches handlers and ignores queued onstart/onresult on unmount while onstart is pending', () => {
    const { result, unmount } = renderHook(() => useSpeechRecognition())

    act(() => {
      result.current.startListening('vi-VN')
    })

    expect(createdInstances).toHaveLength(1)
    const [pendingInstance] = createdInstances
    const queuedOnStart = pendingInstance.onstart
    const queuedOnResult = pendingInstance.onresult

    unmount()

    expect(pendingInstance.stop).toHaveBeenCalledTimes(1)
    expect(pendingInstance.onstart).toBeNull()
    expect(pendingInstance.onresult).toBeNull()
    expect(pendingInstance.onerror).toBeNull()
    expect(pendingInstance.onend).toBeNull()

    expect(() => {
      queuedOnStart?.()
      queuedOnResult?.({
        resultIndex: 0,
        results: {
          length: 1,
          0: { 0: { transcript: 'Late unmount transcript' } },
        },
      })
    }).not.toThrow()
  })
})
