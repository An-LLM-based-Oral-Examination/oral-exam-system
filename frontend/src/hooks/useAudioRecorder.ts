import { useCallback, useEffect, useRef, useState } from 'react'
import {
  classifyMicrophoneError,
  getMicrophoneIssueMessage,
  hasAvailableAudioInputDevice,
  type MicrophoneConnectionState,
  type MicrophoneIssueKind,
} from '@/utils/microphoneDiagnostics'

const AUDIO_MIME_TYPES = ['audio/webm;codecs=opus', 'audio/webm', 'audio/ogg;codecs=opus'] as const
const OPUS_VOICE_BITRATE_BPS = 64_000
const VAD_POLL_INTERVAL_MS = 100
const VAD_SPEECH_RMS_THRESHOLD = 0.011
const VAD_SILENCE_RMS_THRESHOLD = 0.009
const VAD_MIN_SILENCE_DURATION_MS = 280
const VAD_SOFT_CUT_RATIO = 0.75
const VAD_HARD_CUT_RATIO = 1.2
const RECONNECTED_BANNER_MS = 4_500

const AUDIO_CONSTRAINTS: MediaTrackConstraints = {
  channelCount: 1,
  sampleRate: 48000,
  echoCancellation: true,
  noiseSuppression: true,
  autoGainControl: true,
}

function getPrimaryAudioTrack(stream: MediaStream | null): MediaStreamTrack | undefined {
  if (!stream) return undefined
  if (typeof stream.getAudioTracks === 'function') {
    return stream.getAudioTracks()[0]
  }
  if (typeof stream.getTracks === 'function') {
    return stream.getTracks()[0]
  }
  return undefined
}

function stopMediaStream(stream: MediaStream | null) {
  if (!stream || typeof stream.getTracks !== 'function') return
  stream.getTracks().forEach((track) => {
    track.onended = null
    track.stop()
  })
}

function detachRecorderHandlers(recorder: MediaRecorder) {
  recorder.ondataavailable = null
  recorder.onstop = null
  recorder.onerror = null
}

function getSupportedMimeType() {
  if (typeof MediaRecorder === 'undefined') return ''
  return AUDIO_MIME_TYPES.find((mimeType) => MediaRecorder.isTypeSupported(mimeType)) ?? ''
}

interface AudioBridgeGraph {
  context: AudioContext
  destination: MediaStreamAudioDestinationNode | null
  analyser: AnalyserNode | null
  buffer: Float32Array<ArrayBuffer> | null
}

function createAudioBridgeGraph(): AudioBridgeGraph | null {
  if (typeof window === 'undefined') return null
  const AudioContextCtor =
    window.AudioContext ??
    (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext
  if (!AudioContextCtor) return null

  try {
    const context = new AudioContextCtor()
    if (context.state === 'suspended') {
      void context.resume().catch(() => {})
    }
    const destination =
      typeof context.createMediaStreamDestination === 'function'
        ? context.createMediaStreamDestination()
        : null
    const analyser = typeof context.createAnalyser === 'function' ? context.createAnalyser() : null
    let buffer: Float32Array<ArrayBuffer> | null = null
    if (analyser) {
      analyser.fftSize = 1024
      analyser.smoothingTimeConstant = 0.2
      buffer = new Float32Array(new ArrayBuffer(analyser.fftSize * Float32Array.BYTES_PER_ELEMENT))
    }
    return { context, destination, analyser, buffer }
  } catch {
    return null
  }
}

function computeRmsEnergy(analyser: AnalyserNode, buffer: Float32Array<ArrayBuffer>): number {
  analyser.getFloatTimeDomainData(buffer)
  let sumSquares = 0
  for (let i = 0; i < buffer.length; i += 1) {
    const sample = buffer[i]
    sumSquares += sample * sample
  }
  return Math.sqrt(sumSquares / buffer.length)
}

interface PendingStop {
  resolve: (audio: Blob) => void
  reject: (reason: Error) => void
}

export interface UseAudioRecorderOptions {
  /** Thời lượng mỗi phân đoạn (ms) để bóc băng cuốn chiếu ngầm khi nói dài (ví dụ: 20_000ms = 20 giây) */
  segmentDurationMs?: number
  /** Callback nhận từng phân đoạn WebM hoàn chỉnh ngay trong lúc sinh viên đang nói */
  onSegment?: (segment: Blob, segmentIndex: number) => void
}

function createMediaRecorder(stream: MediaStream, mimeType: string): MediaRecorder {
  if (mimeType) {
    try {
      return new MediaRecorder(stream, {
        mimeType,
        audioBitsPerSecond: OPUS_VOICE_BITRATE_BPS,
      })
    } catch {
      return new MediaRecorder(stream, { mimeType })
    }
  }
  return new MediaRecorder(stream)
}

export function useAudioRecorder(options?: UseAudioRecorderOptions) {
  const [isRecording, setIsRecording] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [micIssue, setMicIssue] = useState<MicrophoneIssueKind | null>(null)
  const [connectionState, setConnectionState] = useState<MicrophoneConnectionState>('idle')
  const [activeDeviceLabel, setActiveDeviceLabel] = useState('')

  const recorderRef = useRef<MediaRecorder | null>(null)
  const activeRecordersRef = useRef(new Set<MediaRecorder>())
  const recordingSessionIdRef = useRef(0)
  const hardwareStreamRef = useRef<MediaStream | null>(null)
  const recordingStreamRef = useRef<MediaStream | null>(null)
  const audioGraphRef = useRef<AudioBridgeGraph | null>(null)
  const sourceNodeRef = useRef<MediaStreamAudioSourceNode | null>(null)
  const recorderVoicedMapRef = useRef(new WeakMap<MediaRecorder, boolean>())
  const chunksRef = useRef<BlobPart[]>([])
  const preservedChunksRef = useRef<BlobPart[]>([])
  const pendingStopRef = useRef<PendingStop | null>(null)
  const segmentTimerRef = useRef<number | null>(null)
  const reconnectDebounceRef = useRef<number | null>(null)
  const reconnectedBannerTimerRef = useRef<number | null>(null)
  const reconnectingInFlightRef = useRef(false)
  const connectionStateRef = useRef<MicrophoneConnectionState>('idle')
  const isSessionActiveRef = useRef(false)
  const mimeTypeRef = useRef('')
  const segmentIndexRef = useRef<number>(0)
  const lastEmittedSegmentRef = useRef<Blob | null>(null)
  const onSegmentRef = useRef(options?.onSegment)
  const bindRecorderOnStreamRef = useRef<((stream: MediaStream) => MediaRecorder) | null>(null)
  const segmentDurationMs = options?.segmentDurationMs ?? 0

  useEffect(() => {
    onSegmentRef.current = options?.onSegment
  }, [options?.onSegment])

  const updateConnectionState = useCallback((next: MicrophoneConnectionState) => {
    connectionStateRef.current = next
    setConnectionState(next)
  }, [])

  const clearTimers = useCallback(() => {
    if (segmentTimerRef.current !== null) {
      window.clearInterval(segmentTimerRef.current)
      segmentTimerRef.current = null
    }
    if (reconnectDebounceRef.current !== null) {
      window.clearTimeout(reconnectDebounceRef.current)
      reconnectDebounceRef.current = null
    }
    if (reconnectedBannerTimerRef.current !== null) {
      window.clearTimeout(reconnectedBannerTimerRef.current)
      reconnectedBannerTimerRef.current = null
    }
  }, [])

  const releaseRecorder = useCallback(() => {
    clearTimers()
    recordingSessionIdRef.current += 1
    for (const trackedRecorder of activeRecordersRef.current) {
      detachRecorderHandlers(trackedRecorder)
      if (trackedRecorder.state !== 'inactive') {
        try {
          trackedRecorder.stop()
        } catch {
          // Ignore stop errors when tearing down tracked recorders.
        }
      }
    }
    activeRecordersRef.current.clear()
    isSessionActiveRef.current = false
    reconnectingInFlightRef.current = false
    sourceNodeRef.current?.disconnect()
    sourceNodeRef.current = null

    if (audioGraphRef.current) {
      const graph = audioGraphRef.current
      audioGraphRef.current = null
      stopMediaStream(graph.destination?.stream ?? null)
      void graph.context.close().catch(() => {})
    }

    stopMediaStream(hardwareStreamRef.current)
    hardwareStreamRef.current = null
    recordingStreamRef.current = null
    recorderRef.current = null
    bindRecorderOnStreamRef.current = null
    preservedChunksRef.current = []
    setIsRecording(false)
    updateConnectionState('idle')
  }, [clearTimers, updateConnectionState])

  const attachHardwareStreamRef = useRef<(stream: MediaStream, isReconnect: boolean) => void>(
    () => {},
  )

  const reconnectMicrophone = useCallback(async (): Promise<boolean> => {
    if (!navigator.mediaDevices?.getUserMedia) {
      setMicIssue('unsupported')
      setError(getMicrophoneIssueMessage('unsupported'))
      return false
    }

    if (reconnectingInFlightRef.current) return false
    reconnectingInFlightRef.current = true

    if (isSessionActiveRef.current) {
      updateConnectionState('reconnecting')
    }

    try {
      const nextStream = await navigator.mediaDevices.getUserMedia({
        audio: AUDIO_CONSTRAINTS,
      })

      // Trường hợp chưa vào phiên ghi âm (chỉ kiểm tra lại quyền micro sau khi mở khóa)
      if (!isSessionActiveRef.current) {
        const track = getPrimaryAudioTrack(nextStream)
        setActiveDeviceLabel(track?.label || 'Microphone mặc định')
        stopMediaStream(nextStream)
        setMicIssue(null)
        setError(null)
        updateConnectionState('idle')
        return true
      }

      attachHardwareStreamRef.current(nextStream, true)
      return true
    } catch (reconnectError) {
      const issue = classifyMicrophoneError(reconnectError)
      if (isSessionActiveRef.current && issue === 'device_missing') {
        updateConnectionState('disconnected')
      } else {
        setMicIssue(issue)
        setError(getMicrophoneIssueMessage(issue))
        if (isSessionActiveRef.current) {
          updateConnectionState('disconnected')
        }
      }
      return false
    } finally {
      reconnectingInFlightRef.current = false
    }
  }, [updateConnectionState])

  useEffect(() => {
    attachHardwareStreamRef.current = (nextHardwareStream: MediaStream, isReconnect: boolean) => {
      const prevHardwareStream = hardwareStreamRef.current
      sourceNodeRef.current?.disconnect()
      sourceNodeRef.current = null
      stopMediaStream(prevHardwareStream)

      hardwareStreamRef.current = nextHardwareStream
      const primaryTrack = getPrimaryAudioTrack(nextHardwareStream)
      setActiveDeviceLabel(primaryTrack?.label || 'Microphone mặc định')

      const graph = audioGraphRef.current
      if (graph) {
        if (graph.context.state === 'suspended') {
          void graph.context.resume().catch(() => {})
        }
        try {
          const source = graph.context.createMediaStreamSource(nextHardwareStream)
          if (graph.destination) source.connect(graph.destination)
          if (graph.analyser) source.connect(graph.analyser)
          sourceNodeRef.current = source
        } catch {
          // Bỏ qua nếu môi trường test mock không hiện thực đầy đủ AudioNode
        }
      }

      if (primaryTrack) {
        primaryTrack.onended = () => {
          if (!isSessionActiveRef.current || pendingStopRef.current) return

          sourceNodeRef.current?.disconnect()
          sourceNodeRef.current = null

          const activeRecorder = recorderRef.current
          if (
            activeRecorder &&
            activeRecorder.state === 'recording' &&
            typeof activeRecorder.pause === 'function'
          ) {
            try {
              activeRecorder.requestData()
              activeRecorder.pause()
            } catch {
              // Một số trình duyệt không hỗ trợ pause; destination stream vẫn giữ sống MediaRecorder
            }
          } else if (!graph?.destination && chunksRef.current.length > 0) {
            preservedChunksRef.current = [...preservedChunksRef.current, ...chunksRef.current]
            chunksRef.current = []
          }

          updateConnectionState('disconnected')
        }
      }

      if (isReconnect) {
        const activeRecorder = recorderRef.current
        if (graph?.destination) {
          if (
            activeRecorder &&
            activeRecorder.state === 'paused' &&
            typeof activeRecorder.resume === 'function'
          ) {
            try {
              activeRecorder.resume()
            } catch {
              // Giữ recorder tiếp tục nếu đã ở trạng thái recording
            }
          }
        } else if (
          (!activeRecorder || activeRecorder.state === 'inactive') &&
          bindRecorderOnStreamRef.current
        ) {
          recordingStreamRef.current = nextHardwareStream
          bindRecorderOnStreamRef.current(nextHardwareStream)
        }

        setMicIssue(null)
        setError(null)
        updateConnectionState('reconnected')
        if (reconnectedBannerTimerRef.current !== null) {
          window.clearTimeout(reconnectedBannerTimerRef.current)
        }
        reconnectedBannerTimerRef.current = window.setTimeout(() => {
          reconnectedBannerTimerRef.current = null
          if (isSessionActiveRef.current && connectionStateRef.current === 'reconnected') {
            updateConnectionState('connected')
          }
        }, RECONNECTED_BANNER_MS)
      } else {
        setMicIssue(null)
        setError(null)
        updateConnectionState('connected')
      }
    }
  }, [updateConnectionState])

  // Lắng nghe sự kiện cắm lại tai nghe (devicechange) trong suốt phiên đang thu âm
  useEffect(() => {
    if (
      !isRecording ||
      typeof navigator === 'undefined' ||
      !navigator.mediaDevices?.addEventListener
    ) {
      return
    }

    const handleDeviceChange = () => {
      if (!isSessionActiveRef.current || pendingStopRef.current) return

      const currentTrack = getPrimaryAudioTrack(hardwareStreamRef.current)
      const trackDead = !currentTrack || currentTrack.readyState === 'ended'
      if (connectionStateRef.current !== 'disconnected' && !trackDead) {
        return
      }

      if (reconnectDebounceRef.current !== null) {
        window.clearTimeout(reconnectDebounceRef.current)
      }
      reconnectDebounceRef.current = window.setTimeout(() => {
        reconnectDebounceRef.current = null
        void (async () => {
          const available = await hasAvailableAudioInputDevice()
          if (available && isSessionActiveRef.current && !pendingStopRef.current) {
            await reconnectMicrophone()
          }
        })()
      }, 300)
    }

    navigator.mediaDevices.addEventListener('devicechange', handleDeviceChange)
    return () => {
      navigator.mediaDevices.removeEventListener('devicechange', handleDeviceChange)
    }
  }, [isRecording, reconnectMicrophone])

  const startRecording = useCallback(async () => {
    if (!navigator.mediaDevices?.getUserMedia || typeof MediaRecorder === 'undefined') {
      const message = getMicrophoneIssueMessage('unsupported')
      setMicIssue('unsupported')
      setError(message)
      throw new Error(message)
    }

    if (
      isSessionActiveRef.current &&
      recorderRef.current &&
      recorderRef.current.state !== 'inactive'
    ) {
      return
    }

    setError(null)
    setMicIssue(null)
    const sessionId = ++recordingSessionIdRef.current

    let stream: MediaStream
    try {
      stream = await navigator.mediaDevices.getUserMedia({
        audio: AUDIO_CONSTRAINTS,
      })
    } catch (getUserMediaError) {
      const issue = classifyMicrophoneError(getUserMediaError)
      const message = getMicrophoneIssueMessage(issue)
      setMicIssue(issue)
      setError(message)
      throw new Error(message, { cause: getUserMediaError })
    }

    if (sessionId !== recordingSessionIdRef.current) {
      stopMediaStream(stream)
      return
    }

    try {
      const mimeType = getSupportedMimeType()
      mimeTypeRef.current = mimeType
      segmentIndexRef.current = 0
      lastEmittedSegmentRef.current = null
      preservedChunksRef.current = []
      isSessionActiveRef.current = true

      const graph = createAudioBridgeGraph()
      audioGraphRef.current = graph

      attachHardwareStreamRef.current(stream, false)

      const recordingStream = graph?.destination?.stream ?? stream
      recordingStreamRef.current = recordingStream
      const hasVadAnalyser = Boolean(
        segmentDurationMs >= 5_000 && onSegmentRef.current && graph?.analyser && graph.buffer,
      )

      const bindAndStartRecorder = (targetStream: MediaStream) => {
        const localChunks: BlobPart[] = []
        const recorder = createMediaRecorder(targetStream, mimeType)
        activeRecordersRef.current.add(recorder)
        recorderRef.current = recorder
        chunksRef.current = localChunks
        // Nếu trình duyệt hỗ trợ Web Audio Analyser, chỉ gửi phân đoạn cuốn chiếu khi phát hiện năng lượng giọng nói thật (RMS Silence Gate)
        recorderVoicedMapRef.current.set(recorder, !hasVadAnalyser)

        recorder.ondataavailable = (event) => {
          if (sessionId !== recordingSessionIdRef.current) return
          if (event.data.size > 0) localChunks.push(event.data)
        }

        recorder.onerror = () => {
          if (sessionId !== recordingSessionIdRef.current) return
          // Nếu lỗi xảy ra đúng lúc tai nghe vừa rút ở chế độ fallback không có AudioContext destination, giữ lại các chunk đã thu để chờ cắm lại
          const currentTrack = getPrimaryAudioTrack(hardwareStreamRef.current)
          if (
            isSessionActiveRef.current &&
            !pendingStopRef.current &&
            (!currentTrack || currentTrack.readyState === 'ended')
          ) {
            if (localChunks.length > 0) {
              preservedChunksRef.current = [...preservedChunksRef.current, ...localChunks]
            }
            updateConnectionState('disconnected')
            return
          }

          const recordingError = new Error(
            'Không thể tiếp tục ghi âm. Vui lòng kiểm tra microphone.',
          )
          setError(recordingError.message)
          setMicIssue('device_busy')
          pendingStopRef.current?.reject(recordingError)
          pendingStopRef.current = null
          releaseRecorder()
        }

        recorder.onstop = () => {
          activeRecordersRef.current.delete(recorder)
          detachRecorderHandlers(recorder)
          if (sessionId !== recordingSessionIdRef.current) return
          // Nếu dừng tạm ở chế độ fallback do rút tai nghe (chưa bấm dừng nộp), giữ nguyên chunk chờ cắm lại tai nghe
          if (
            isSessionActiveRef.current &&
            !pendingStopRef.current &&
            recorderRef.current === recorder &&
            connectionStateRef.current === 'disconnected'
          ) {
            if (localChunks.length > 0) {
              preservedChunksRef.current = [...preservedChunksRef.current, ...localChunks]
            }
            return
          }

          const combinedChunks =
            preservedChunksRef.current.length > 0
              ? [...preservedChunksRef.current, ...localChunks]
              : localChunks

          const audio = new Blob(combinedChunks, {
            type: recorder.mimeType || mimeType || 'audio/webm',
          })

          // Nếu đây là phân đoạn 20s tự xoay vòng trong khi recorder mới đã chạy tiếp
          if (recorderRef.current !== recorder) {
            const hadVoiceEnergy = recorderVoicedMapRef.current.get(recorder) ?? true
            if (audio.size > 0 && hadVoiceEnergy) {
              const currentIdx = segmentIndexRef.current++
              lastEmittedSegmentRef.current = audio
              onSegmentRef.current?.(audio, currentIdx)
            }
            return
          }

          const pendingStop = pendingStopRef.current
          pendingStopRef.current = null
          const fallbackSegment = lastEmittedSegmentRef.current
          releaseRecorder()

          if (!pendingStop) return
          if (audio.size === 0 && !fallbackSegment) {
            pendingStop.reject(new Error('Bản ghi âm đang trống. Vui lòng ghi âm lại câu trả lời.'))
            return
          }
          pendingStop.resolve(audio)
        }

        recorder.start(250)
        return recorder
      }

      bindRecorderOnStreamRef.current = bindAndStartRecorder
      bindAndStartRecorder(recordingStream)

      if (segmentTimerRef.current !== null) {
        window.clearInterval(segmentTimerRef.current)
        segmentTimerRef.current = null
      }

      if (segmentDurationMs >= 5_000 && onSegmentRef.current) {
        const rotateRecorderSegment = () => {
          const prevRecorder = recorderRef.current
          const activeStream = recordingStreamRef.current
          if (
            !activeStream ||
            !prevRecorder ||
            prevRecorder.state !== 'recording' ||
            connectionStateRef.current === 'disconnected' ||
            connectionStateRef.current === 'reconnecting' ||
            pendingStopRef.current
          ) {
            return false
          }
          try {
            // Khởi tạo và chạy recorder kế tiếp trên cùng MediaStream trước khi đóng phân đoạn cũ (Zero-Gap < 2ms)
            bindAndStartRecorder(activeStream)
            prevRecorder.requestData()
            prevRecorder.stop()
            return true
          } catch {
            return false
          }
        }

        if (hasVadAnalyser && graph?.analyser && graph.buffer) {
          const analyser = graph.analyser
          const buffer = graph.buffer
          let segmentStartedAt = Date.now()
          let consecutiveSilenceMs = 0
          const softCutMs = Math.max(4_000, Math.round(segmentDurationMs * VAD_SOFT_CUT_RATIO))
          const hardCutMs = Math.max(
            segmentDurationMs,
            Math.round(segmentDurationMs * VAD_HARD_CUT_RATIO),
          )

          segmentTimerRef.current = window.setInterval(() => {
            const activeRecorder = recorderRef.current
            if (
              !activeRecorder ||
              activeRecorder.state !== 'recording' ||
              connectionStateRef.current === 'disconnected' ||
              pendingStopRef.current
            ) {
              return
            }

            const rms = computeRmsEnergy(analyser, buffer)
            if (rms >= VAD_SPEECH_RMS_THRESHOLD) {
              recorderVoicedMapRef.current.set(activeRecorder, true)
              consecutiveSilenceMs = 0
            } else if (rms < VAD_SILENCE_RMS_THRESHOLD) {
              consecutiveSilenceMs += VAD_POLL_INTERVAL_MS
            }

            const elapsedMs = Date.now() - segmentStartedAt
            const shouldCutOnBreathPause =
              elapsedMs >= softCutMs && consecutiveSilenceMs >= VAD_MIN_SILENCE_DURATION_MS
            const shouldForceCutAtCeiling = elapsedMs >= hardCutMs

            if (shouldCutOnBreathPause || shouldForceCutAtCeiling) {
              if (rotateRecorderSegment()) {
                segmentStartedAt = Date.now()
                consecutiveSilenceMs = 0
              }
            }
          }, VAD_POLL_INTERVAL_MS)
        } else {
          segmentTimerRef.current = window.setInterval(() => {
            rotateRecorderSegment()
          }, segmentDurationMs)
        }
      }

      setIsRecording(true)
    } catch (recordingError) {
      stopMediaStream(stream)
      isSessionActiveRef.current = false
      const issue = classifyMicrophoneError(recordingError)
      const message =
        recordingError instanceof Error && recordingError.message
          ? recordingError.message
          : getMicrophoneIssueMessage(issue)
      setMicIssue(issue)
      setError(message)
      throw recordingError
    }
  }, [releaseRecorder, segmentDurationMs, updateConnectionState])

  const stopRecording = useCallback(async () => {
    if (segmentTimerRef.current !== null) {
      window.clearInterval(segmentTimerRef.current)
      segmentTimerRef.current = null
    }

    const recorder = recorderRef.current
    if (!recorder || recorder.state === 'inactive') {
      if (isSessionActiveRef.current && preservedChunksRef.current.length > 0) {
        const salvaged = new Blob(preservedChunksRef.current, {
          type: mimeTypeRef.current || 'audio/webm',
        })
        releaseRecorder()
        return salvaged
      }
      releaseRecorder()
      throw new Error('Chưa có bản ghi âm đang hoạt động.')
    }

    return new Promise<Blob>((resolve, reject) => {
      pendingStopRef.current = { resolve, reject }
      try {
        recorder.requestData()
      } catch {
        // Bỏ qua nếu recorder đang ở trạng thái không cho gọi requestData
      }
      recorder.stop()
    })
  }, [releaseRecorder])

  const cancelRecording = useCallback(() => {
    const recorder = recorderRef.current
    pendingStopRef.current?.reject(new Error('Đã hủy bản ghi âm.'))
    pendingStopRef.current = null
    chunksRef.current = []
    preservedChunksRef.current = []
    lastEmittedSegmentRef.current = null

    if (recorder && recorder.state !== 'inactive') {
      recorder.ondataavailable = null
      recorder.onstop = null
      recorder.onerror = null
      try {
        recorder.stop()
      } catch {
        // Ignore
      }
    }
    releaseRecorder()
  }, [releaseRecorder])

  const clearError = useCallback(() => {
    setError(null)
    setMicIssue(null)
  }, [])

  useEffect(() => cancelRecording, [cancelRecording])

  return {
    isRecording,
    error,
    micIssue,
    connectionState,
    activeDeviceLabel,
    startRecording,
    stopRecording,
    cancelRecording,
    reconnectMicrophone,
    clearError,
  }
}
