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

export function useAudioRecorder() {
  const [isRecording, setIsRecording] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const recorderRef = useRef<MediaRecorder | null>(null)
  const streamRef = useRef<MediaStream | null>(null)
  const chunksRef = useRef<BlobPart[]>([])
  const pendingStopRef = useRef<PendingStop | null>(null)

  const releaseRecorder = useCallback(() => {
    stopMediaStream(streamRef.current)
    streamRef.current = null
    recorderRef.current = null
    setIsRecording(false)
  }, [])

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
        echoCancellation: true,
        noiseSuppression: true,
        autoGainControl: true,
      },
    })

    try {
      const mimeType = getSupportedMimeType()
      const recorder = mimeType
        ? new MediaRecorder(stream, { mimeType })
        : new MediaRecorder(stream)

      streamRef.current = stream
      recorderRef.current = recorder
      chunksRef.current = []

      recorder.ondataavailable = (event) => {
        if (event.data.size > 0) chunksRef.current.push(event.data)
      }

      recorder.onerror = () => {
        const recordingError = new Error('Không thể tiếp tục ghi âm. Vui lòng kiểm tra microphone.')
        setError(recordingError.message)
        pendingStopRef.current?.reject(recordingError)
        pendingStopRef.current = null
        releaseRecorder()
      }

      recorder.onstop = () => {
        const audio = new Blob(chunksRef.current, {
          type: recorder.mimeType || mimeType || 'audio/webm',
        })
        chunksRef.current = []
        const pendingStop = pendingStopRef.current
        pendingStopRef.current = null
        releaseRecorder()

        if (!pendingStop) return
        if (audio.size === 0) {
          pendingStop.reject(new Error('Bản ghi âm đang trống. Vui lòng ghi âm lại câu trả lời.'))
          return
        }
        pendingStop.resolve(audio)
      }

      recorder.start(250)
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
  }, [releaseRecorder])

  const stopRecording = useCallback(async () => {
    const recorder = recorderRef.current
    if (!recorder || recorder.state === 'inactive') {
      throw new Error('Chưa có bản ghi âm đang hoạt động.')
    }

    return new Promise<Blob>((resolve, reject) => {
      pendingStopRef.current = { resolve, reject }
      recorder.requestData()
      recorder.stop()
    })
  }, [])

  const cancelRecording = useCallback(() => {
    const recorder = recorderRef.current
    pendingStopRef.current?.reject(new Error('Đã hủy bản ghi âm.'))
    pendingStopRef.current = null
    chunksRef.current = []

    if (recorder && recorder.state !== 'inactive') {
      recorder.ondataavailable = null
      recorder.onstop = null
      recorder.onerror = null
      recorder.stop()
    }
    releaseRecorder()
  }, [releaseRecorder])

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
