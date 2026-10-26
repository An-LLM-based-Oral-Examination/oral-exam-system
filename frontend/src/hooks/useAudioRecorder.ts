import { useCallback, useEffect, useRef, useState } from 'react'

const AUDIO_MIME_TYPES = ['audio/webm;codecs=opus', 'audio/webm', 'audio/ogg;codecs=opus'] as const

function stopMediaStream(stream: MediaStream | null) {
  stream?.getTracks().forEach((track) => track.stop())
}

function getSupportedMimeType() {
  if (typeof MediaRecorder === 'undefined') return ''
  return AUDIO_MIME_TYPES.find((mimeType) => MediaRecorder.isTypeSupported(mimeType)) ?? ''
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
        audioBitsPerSecond: 24_000,
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
        sampleRate: 16000,
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

      const bindAndStartRecorder = () => {
        const localChunks: BlobPart[] = []
        const recorder = createMediaRecorder(stream, mimeType)
        recorderRef.current = recorder
        chunksRef.current = localChunks

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
            if (audio.size > 0) {
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
        segmentTimerRef.current = window.setInterval(() => {
          const prevRecorder = recorderRef.current
          const activeStream = streamRef.current
          if (
            !activeStream ||
            !prevRecorder ||
            prevRecorder.state !== 'recording' ||
            pendingStopRef.current
          ) {
            return
          }
          try {
            // Khởi tạo và chạy recorder kế tiếp trên cùng MediaStream trước khi đóng phân đoạn cũ (Zero-Gap < 2ms)
            bindAndStartRecorder()
            prevRecorder.requestData()
            prevRecorder.stop()
          } catch {
            // Giữ nguyên recorder hiện tại nếu trình duyệt không cho xoay vòng
          }
        }, segmentDurationMs)
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
