import { useCallback, useEffect, useRef, useState } from 'react'

const AUDIO_MIME_TYPES = ['audio/webm;codecs=opus', 'audio/webm', 'audio/ogg;codecs=opus'] as const
const OPUS_VOICE_BITRATE_BPS = 64_000
const VAD_POLL_INTERVAL_MS = 100
const VAD_SPEECH_RMS_THRESHOLD = 0.011
const VAD_SILENCE_RMS_THRESHOLD = 0.009
const VAD_MIN_SILENCE_DURATION_MS = 280
const VAD_SOFT_CUT_RATIO = 0.75
const VAD_HARD_CUT_RATIO = 1.2

function stopMediaStream(stream: MediaStream | null) {
  stream?.getTracks().forEach((track) => track.stop())
}

function getSupportedMimeType() {
  if (typeof MediaRecorder === 'undefined') return ''
  return AUDIO_MIME_TYPES.find((mimeType) => MediaRecorder.isTypeSupported(mimeType)) ?? ''
}

function createAudioAnalyser(stream: MediaStream): {
  context: AudioContext
  analyser: AnalyserNode
  buffer: Float32Array<ArrayBuffer>
} | null {
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
    const source = context.createMediaStreamSource(stream)
    const analyser = context.createAnalyser()
    analyser.fftSize = 1024
    analyser.smoothingTimeConstant = 0.2
    source.connect(analyser)
    const buffer = new Float32Array(new ArrayBuffer(analyser.fftSize * Float32Array.BYTES_PER_ELEMENT))
    return { context, analyser, buffer }
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
  const recorderRef = useRef<MediaRecorder | null>(null)
  const streamRef = useRef<MediaStream | null>(null)
  const audioContextRef = useRef<AudioContext | null>(null)
  const recorderVoicedMapRef = useRef(new WeakMap<MediaRecorder, boolean>())
  const chunksRef = useRef<BlobPart[]>([])
  const pendingStopRef = useRef<PendingStop | null>(null)
  const segmentTimerRef = useRef<number | null>(null)
  const segmentIndexRef = useRef<number>(0)
  const lastEmittedSegmentRef = useRef<Blob | null>(null)
  const onSegmentRef = useRef(options?.onSegment)
  const segmentDurationMs = options?.segmentDurationMs ?? 0

  useEffect(() => {
    onSegmentRef.current = options?.onSegment
  }, [options?.onSegment])

  const clearSegmentTimer = useCallback(() => {
    if (segmentTimerRef.current !== null) {
      window.clearInterval(segmentTimerRef.current)
      segmentTimerRef.current = null
    }
  }, [])

  const releaseRecorder = useCallback(() => {
    clearSegmentTimer()
    if (audioContextRef.current) {
      const ctx = audioContextRef.current
      audioContextRef.current = null
      void ctx.close().catch(() => {})
    }
    stopMediaStream(streamRef.current)
    streamRef.current = null
    recorderRef.current = null
    setIsRecording(false)
  }, [clearSegmentTimer])

  const startRecording = useCallback(async () => {
    if (!navigator.mediaDevices?.getUserMedia || typeof MediaRecorder === 'undefined') {
      const message = 'Trình duyệt này chưa hỗ trợ ghi âm bằng microphone.'
      setError(message)
      throw new Error(message)
    }

    if (recorderRef.current?.state === 'recording') return

    setError(null)
    const stream = await navigator.mediaDevices.getUserMedia({
      audio: {
        channelCount: 1,
        sampleRate: 48000,
        echoCancellation: true,
        noiseSuppression: true,
        autoGainControl: true,
      },
    })

    try {
      const mimeType = getSupportedMimeType()
      streamRef.current = stream
      segmentIndexRef.current = 0
      lastEmittedSegmentRef.current = null
      const audioGraph =
        segmentDurationMs >= 5_000 && onSegmentRef.current ? createAudioAnalyser(stream) : null
      audioContextRef.current = audioGraph?.context ?? null

      const bindAndStartRecorder = () => {
        const localChunks: BlobPart[] = []
        const recorder = createMediaRecorder(stream, mimeType)
        recorderRef.current = recorder
        chunksRef.current = localChunks
        // Nếu trình duyệt hỗ trợ Web Audio Analyser, chỉ gửi phân đoạn cuốn chiếu khi phát hiện năng lượng giọng nói thật (RMS Silence Gate)
        recorderVoicedMapRef.current.set(recorder, !audioGraph)

        recorder.ondataavailable = (event) => {
          if (event.data.size > 0) localChunks.push(event.data)
        }

        recorder.onerror = () => {
          const recordingError = new Error('Không thể tiếp tục ghi âm. Vui lòng kiểm tra microphone.')
          setError(recordingError.message)
          pendingStopRef.current?.reject(recordingError)
          pendingStopRef.current = null
          releaseRecorder()
        }

        recorder.onstop = () => {
          const audio = new Blob(localChunks, {
            type: recorder.mimeType || mimeType || 'audio/webm',
          })

          // Nếu đây là phân đoạn 20s tự xoay vòng trong khi recorder mới đã chạy tiếp
          if (recorderRef.current !== recorder && !pendingStopRef.current) {
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

      bindAndStartRecorder()
      clearSegmentTimer()
      if (segmentDurationMs >= 5_000 && onSegmentRef.current) {
        const rotateRecorderSegment = () => {
          const prevRecorder = recorderRef.current
          const activeStream = streamRef.current
          if (
            !activeStream ||
            !prevRecorder ||
            prevRecorder.state !== 'recording' ||
            pendingStopRef.current
          ) {
            return false
          }
          try {
            // Khởi tạo và chạy recorder kế tiếp trên cùng MediaStream trước khi đóng phân đoạn cũ (Zero-Gap < 2ms)
            bindAndStartRecorder()
            prevRecorder.requestData()
            prevRecorder.stop()
            return true
          } catch {
            // Giữ nguyên recorder hiện tại nếu trình duyệt không cho xoay vòng
            return false
          }
        }

        if (audioGraph) {
          let segmentStartedAt = Date.now()
          let consecutiveSilenceMs = 0
          const softCutMs = Math.max(4_000, Math.round(segmentDurationMs * VAD_SOFT_CUT_RATIO))
          const hardCutMs = Math.max(segmentDurationMs, Math.round(segmentDurationMs * VAD_HARD_CUT_RATIO))

          segmentTimerRef.current = window.setInterval(() => {
            const activeRecorder = recorderRef.current
            if (!activeRecorder || activeRecorder.state !== 'recording' || pendingStopRef.current) {
              return
            }

            const rms = computeRmsEnergy(audioGraph.analyser, audioGraph.buffer)
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
      const message =
        recordingError instanceof Error
          ? recordingError.message
          : 'Không thể khởi tạo thiết bị ghi âm.'
      setError(message)
      throw recordingError
    }
  }, [clearSegmentTimer, releaseRecorder, segmentDurationMs])

  const stopRecording = useCallback(async () => {
    clearSegmentTimer()
    const recorder = recorderRef.current
    if (!recorder || recorder.state === 'inactive') {
      throw new Error('Chưa có bản ghi âm đang hoạt động.')
    }

    return new Promise<Blob>((resolve, reject) => {
      pendingStopRef.current = { resolve, reject }
      recorder.requestData()
      recorder.stop()
    })
  }, [clearSegmentTimer])

  const cancelRecording = useCallback(() => {
    clearSegmentTimer()
    const recorder = recorderRef.current
    pendingStopRef.current?.reject(new Error('Đã hủy bản ghi âm.'))
    pendingStopRef.current = null
    chunksRef.current = []
    lastEmittedSegmentRef.current = null

    if (recorder && recorder.state !== 'inactive') {
      recorder.ondataavailable = null
      recorder.onstop = null
      recorder.onerror = null
      recorder.stop()
    }
    releaseRecorder()
  }, [clearSegmentTimer, releaseRecorder])

  useEffect(() => cancelRecording, [cancelRecording])

  return {
    isRecording,
    error,
    startRecording,
    stopRecording,
    cancelRecording,
    clearError: () => setError(null),
  }
}
