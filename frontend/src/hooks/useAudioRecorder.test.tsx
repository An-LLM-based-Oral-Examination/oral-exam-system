import { act, fireEvent, render, renderHook, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { MicrophoneRecoveryBanner } from '@/components/audio/MicrophoneRecoveryBanner'
import { useAudioRecorder } from '@/hooks/useAudioRecorder'
import { useSpeechRecognition } from '@/hooks/useSpeechRecognition'

interface FakeTrack {
  label: string
  readyState: 'live' | 'ended'
  onended: (() => void) | null
  stop: () => void
}

function createFakeStream(label: string): { stream: MediaStream; track: FakeTrack } {
  const track: FakeTrack = {
    label,
    readyState: 'live',
    onended: null,
    stop() {
      this.readyState = 'ended'
    },
  }
  const stream = {
    getTracks: () => [track as unknown as MediaStreamTrack],
    getAudioTracks: () => [track as unknown as MediaStreamTrack],
  } as unknown as MediaStream
  return { stream, track }
}

describe('HOÀNG-08 Microphone permission guide & mid-exam headset auto-reconnect', () => {
  const originalMediaDevices = navigator.mediaDevices
  const originalPermissions = navigator.permissions
  const originalMediaRecorder = globalThis.MediaRecorder
  const originalAudioContext = window.AudioContext

  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
  })

  afterEach(() => {
    vi.useRealTimers()
    Object.defineProperty(navigator, 'mediaDevices', {
      configurable: true,
      value: originalMediaDevices,
    })
    Object.defineProperty(navigator, 'permissions', {
      configurable: true,
      value: originalPermissions,
    })
    globalThis.MediaRecorder = originalMediaRecorder
    window.AudioContext = originalAudioContext
  })

  it('renders browser-specific permission steps, allows switching browser tabs, and auto-retries when permission becomes granted', async () => {
    const onRetry = vi.fn()
    const listeners = new Set<() => void>()
    const fakeStatus = {
      state: 'denied' as PermissionState,
      addEventListener: (_type: string, cb: () => void) => listeners.add(cb),
      removeEventListener: (_type: string, cb: () => void) => listeners.delete(cb),
    }

    Object.defineProperty(navigator, 'permissions', {
      configurable: true,
      value: {
        query: vi.fn().mockResolvedValue(fakeStatus),
      },
    })

    render(
      <MicrophoneRecoveryBanner issue="permission_denied" preserveWorkNotice onRetry={onRetry} />,
    )

    expect(screen.getByText('Quyền truy cập Microphone đang bị chặn')).toBeInTheDocument()
    expect(
      screen.getByText(/Bài đang làm và câu trả lời đã lưu được bảo toàn 100%/i),
    ).toBeInTheDocument()

    // Chuyển tab sang Firefox để xem hướng dẫn riêng của Firefox
    fireEvent.click(screen.getByRole('tab', { name: 'Firefox' }))
    expect(screen.getByText(/Cách cấp lại quyền trên Mozilla Firefox/i)).toBeInTheDocument()

    // Giả lập sinh viên vừa bật Allow trên thanh địa chỉ trình duyệt -> tự động gọi onRetry không cần F5
    await waitFor(() => expect(navigator.permissions.query).toHaveBeenCalled())
    act(() => {
      fakeStatus.state = 'granted'
      listeners.forEach((cb) => cb())
    })
    expect(onRetry).toHaveBeenCalledTimes(1)
  })

  it('keeps recording session alive when headset unplugs mid-exam and hot-swaps new headset on devicechange without losing audio', async () => {
    const deviceChangeListeners = new Set<() => void>()
    const firstHeadset = createFakeStream('USB Headset #1')
    const repluggedHeadset = createFakeStream('USB Headset #2 (Reconnected)')

    const getUserMediaMock = vi
      .fn<() => Promise<MediaStream>>()
      .mockResolvedValueOnce(firstHeadset.stream)
      .mockResolvedValueOnce(repluggedHeadset.stream)

    Object.defineProperty(navigator, 'mediaDevices', {
      configurable: true,
      value: {
        getUserMedia: getUserMediaMock,
        enumerateDevices: vi.fn().mockResolvedValue([{ kind: 'audioinput', deviceId: 'usb-2' }]),
        addEventListener: (_event: string, cb: () => void) => deviceChangeListeners.add(cb),
        removeEventListener: (_event: string, cb: () => void) => deviceChangeListeners.delete(cb),
      },
    })

    const destinationStream = createFakeStream('AudioContext Destination').stream
    class FakeAudioContext {
      state = 'running'
      resume = vi.fn().mockResolvedValue(undefined)
      close = vi.fn().mockResolvedValue(undefined)
      createMediaStreamDestination = () => ({ stream: destinationStream })
      createAnalyser = () => ({
        fftSize: 1024,
        smoothingTimeConstant: 0.2,
        getFloatTimeDomainData: (buf: Float32Array) => buf.fill(0.05),
      })
      createMediaStreamSource = () => ({
        connect: vi.fn(),
        disconnect: vi.fn(),
      })
    }
    window.AudioContext = FakeAudioContext as unknown as typeof AudioContext

    class FakeMediaRecorder {
      static isTypeSupported = () => true
      state: 'inactive' | 'recording' | 'paused' = 'inactive'
      mimeType = 'audio/webm'
      ondataavailable: ((event: { data: Blob }) => void) | null = null
      onerror: (() => void) | null = null
      onstop: (() => void) | null = null

      start() {
        this.state = 'recording'
        this.ondataavailable?.({ data: new Blob(['part-before-unplug-'], { type: 'audio/webm' }) })
      }
      pause() {
        this.state = 'paused'
      }
      resume() {
        this.state = 'recording'
        this.ondataavailable?.({ data: new Blob(['part-after-replug'], { type: 'audio/webm' }) })
      }
      requestData() {}
      stop() {
        this.state = 'inactive'
        this.onstop?.()
      }
    }
    globalThis.MediaRecorder = FakeMediaRecorder as unknown as typeof MediaRecorder

    const { result } = renderHook(() => useAudioRecorder())

    await act(async () => {
      await result.current.startRecording()
    })
    expect(result.current.isRecording).toBe(true)
    expect(result.current.connectionState).toBe('connected')
    expect(result.current.activeDeviceLabel).toBe('USB Headset #1')

    // Giả lập tuột giắc tai nghe giữa lúc đang thi
    act(() => {
      firstHeadset.track.readyState = 'ended'
      firstHeadset.track.onended?.()
    })

    // Phiên ghi âm VẪN giữ sống (isRecording = true), chỉ báo trạng thái ngắt tai nghe tạm thời
    expect(result.current.isRecording).toBe(true)
    expect(result.current.connectionState).toBe('disconnected')

    // Giả lập cắm lại tai nghe -> sự kiện devicechange kích hoạt tự kết nối lại
    await act(async () => {
      deviceChangeListeners.forEach((cb) => cb())
      await vi.advanceTimersByTimeAsync(400)
    })

    await waitFor(() => {
      expect(result.current.connectionState).toBe('reconnected')
    })
    expect(result.current.isRecording).toBe(true)
    expect(result.current.activeDeviceLabel).toBe('USB Headset #2 (Reconnected)')

    let finalBlob: Blob | null = null
    await act(async () => {
      finalBlob = await result.current.stopRecording()
    })

    expect(finalBlob).not.toBeNull()
    expect(finalBlob!.size).toBeGreaterThan(20)
  })

  it('preserves spoken transcript across Web Speech audio-capture disconnect and appends new speech on devicechange', async () => {
    const deviceChangeListeners = new Set<() => void>()
    Object.defineProperty(navigator, 'mediaDevices', {
      configurable: true,
      value: {
        enumerateDevices: vi.fn().mockResolvedValue([{ kind: 'audioinput', deviceId: 'mic-1' }]),
        addEventListener: (_event: string, cb: () => void) => deviceChangeListeners.add(cb),
        removeEventListener: (_event: string, cb: () => void) => deviceChangeListeners.delete(cb),
      },
    })

    const instances: Array<{
      onstart: (() => void) | null
      onresult: ((e: unknown) => void) | null
      onerror: ((e: { error: string }) => void) | null
      onend: (() => void) | null
    }> = []

    class FakeSpeechRecognition {
      lang = 'vi-VN'
      continuous = true
      interimResults = true
      onstart: (() => void) | null = null
      onresult: ((e: unknown) => void) | null = null
      onerror: ((e: { error: string }) => void) | null = null
      onend: (() => void) | null = null
      constructor() {
        instances.push(this)
      }
      start() {
        this.onstart?.()
      }
      stop() {
        this.onend?.()
      }
    }

    ;(window as unknown as { SpeechRecognition: unknown }).SpeechRecognition = FakeSpeechRecognition

    const { result } = renderHook(() => useSpeechRecognition())

    act(() => {
      result.current.startListening('vi-VN')
    })
    act(() => {
      instances[0].onresult?.({
        resultIndex: 0,
        results: [[{ transcript: 'Clean Architecture tách tầng Domain' }]],
      })
    })
    expect(result.current.transcript).toBe('Clean Architecture tách tầng Domain')

    // Rút tai nghe giữa chừng -> bắn lỗi audio-capture
    act(() => {
      instances[0].onerror?.({ error: 'audio-capture' })
      instances[0].onend?.()
    })
    expect(result.current.connectionState).toBe('disconnected')
    expect(result.current.transcript).toBe('Clean Architecture tách tầng Domain')

    // Cắm lại tai nghe -> tự nối tiếp câu trả lời, không xóa câu cũ
    await act(async () => {
      deviceChangeListeners.forEach((cb) => cb())
      await vi.advanceTimersByTimeAsync(400)
    })
    expect(result.current.connectionState).toBe('reconnected')

    act(() => {
      instances[1].onresult?.({
        resultIndex: 0,
        results: [[{ transcript: 'khỏi tầng Infrastructure' }]],
      })
    })
    expect(result.current.transcript).toBe(
      'Clean Architecture tách tầng Domain khỏi tầng Infrastructure',
    )
  })
})
