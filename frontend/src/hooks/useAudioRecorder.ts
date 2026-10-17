import { useCallback, useEffect, useRef, useState } from 'react'
import {
  normalizeSeTermsClient,
  type SpeechRecognitionLike,
  type SpeechRecognitionWindow,
} from '@/hooks/useSpeechRecognition'

const AUDIO_MIME_TYPES = ['audio/webm;codecs=opus', 'audio/webm', 'audio/ogg;codecs=opus'] as const

export type AudioBlobWithTranscript = Blob & {
  clientTranscript?: string
}

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

interface FilteredAudioPipeline {
  recordingStream: MediaStream
  cleanup: () => void
}

/**
 * Tạo chuỗi lọc tiếng ồn DSP thời gian thực bằng Web Audio API:
 * 1. High-Pass Filter (95 Hz): Triệt tiêu tiếng quạt gió, máy lạnh, tiếng ù nền tần số thấp.
 * 2. Low-Pass Filter (7500 Hz): Cắt tiếng xì/nhiễu tần số cao của microphone.
 * 3. Vocal Presence Boost (Peaking 2400 Hz, +3.5 dB): Làm rõ phụ âm tiếng Việt và thuật ngữ tiếng Anh SE.
 * 4. Dynamics Compressor + Makeup Gain (1.3x): Cân bằng giọng nói nhỏ/xa mic của sinh viên.
 */
function createNoiseFilteredStream(rawStream: MediaStream): FilteredAudioPipeline {
  if (typeof window === 'undefined') {
    return { recordingStream: rawStream, cleanup: () => {} }
  }

  const AudioContextClass =
    window.AudioContext ??
    (window as Window & { webkitAudioContext?: typeof AudioContext }).webkitAudioContext

  if (!AudioContextClass) {
    return { recordingStream: rawStream, cleanup: () => {} }
  }

  try {
    const audioContext = new AudioContextClass()
    const source = audioContext.createMediaStreamSource(rawStream)

    const highPass = audioContext.createBiquadFilter()
    highPass.type = 'highpass'
    highPass.frequency.value = 95
    highPass.Q.value = 0.707

    const lowPass = audioContext.createBiquadFilter()
    lowPass.type = 'lowpass'
    lowPass.frequency.value = 7500
    lowPass.Q.value = 0.707

    const presenceBoost = audioContext.createBiquadFilter()
    presenceBoost.type = 'peaking'
    presenceBoost.frequency.value = 2400
    presenceBoost.Q.value = 1.0
    presenceBoost.gain.value = 3.5

    const compressor = audioContext.createDynamicsCompressor()
    compressor.threshold.value = -26
    compressor.knee.value = 15
    compressor.ratio.value = 3.5
    compressor.attack.value = 0.003
    compressor.release.value = 0.2

    const makeupGain = audioContext.createGain()
    makeupGain.gain.value = 1.3

    const destination = audioContext.createMediaStreamDestination()

    source.connect(highPass)
    highPass.connect(lowPass)
    lowPass.connect(presenceBoost)
    presenceBoost.connect(compressor)
    compressor.connect(makeupGain)
    makeupGain.connect(destination)

    if (audioContext.state === 'suspended') {
      void audioContext.resume().catch(() => {})
    }

    const cleanup = () => {
      try {
        source.disconnect()
        highPass.disconnect()
        lowPass.disconnect()
        presenceBoost.disconnect()
        compressor.disconnect()
        makeupGain.disconnect()
      } catch {
        // Bỏ qua lỗi disconnect node.
      }
      if (audioContext.state !== 'closed') {
        void audioContext.close().catch(() => {})
      }
    }

    const processedStream =
      destination.stream.getAudioTracks().length > 0 ? destination.stream : rawStream

    return {
      recordingStream: processedStream,
      cleanup,
    }
  } catch {
    return { recordingStream: rawStream, cleanup: () => {} }
  }
}

export function useAudioRecorder() {
  const [isRecording, setIsRecording] = useState(false)
  const [liveTranscript, setLiveTranscript] = useState('')
  const [error, setError] = useState<string | null>(null)
  const recorderRef = useRef<MediaRecorder | null>(null)
  const streamRef = useRef<MediaStream | null>(null)
  const filterCleanupRef = useRef<(() => void) | null>(null)
  const chunksRef = useRef<BlobPart[]>([])
  const pendingStopRef = useRef<PendingStop | null>(null)
  const recognitionRef = useRef<SpeechRecognitionLike | null>(null)
  const recognitionEndResolverRef = useRef<(() => void) | null>(null)
  const shouldKeepRecognitionRef = useRef(false)
  const accumulatedFinalRef = useRef('')
  const latestInterimRef = useRef('')
  const liveTranscriptRef = useRef('')

  const flushInterimTranscript = useCallback(() => {
    if (latestInterimRef.current) {
      accumulatedFinalRef.current =
        `${accumulatedFinalRef.current} ${latestInterimRef.current}`.trim()
      latestInterimRef.current = ''
    }
    const normalized = normalizeSeTermsClient(accumulatedFinalRef.current)
    liveTranscriptRef.current = normalized
    setLiveTranscript(normalized)
  }, [])

  const stopParallelRecognition = useCallback(async (waitForFinalFlush = false) => {
    shouldKeepRecognitionRef.current = false
    const recognition = recognitionRef.current
    if (!recognition) {
      flushInterimTranscript()
      return
    }

    recognitionRef.current = null

    if (waitForFinalFlush) {
      await new Promise<void>((resolve) => {
        const timeoutId = window.setTimeout(() => {
          recognitionEndResolverRef.current = null
          resolve()
        }, 220)

        recognitionEndResolverRef.current = () => {
          window.clearTimeout(timeoutId)
          recognitionEndResolverRef.current = null
          resolve()
        }

        try {
          recognition.stop()
        } catch {
          window.clearTimeout(timeoutId)
          recognitionEndResolverRef.current = null
          resolve()
        }
      })
    } else {
      try {
        recognition.stop()
      } catch {
        // Bỏ qua lỗi khi dừng SpeechRecognition song song.
      }
    }

    flushInterimTranscript()
  }, [flushInterimTranscript])

  const startParallelRecognition = useCallback(() => {
    if (typeof window === 'undefined') return
    const speechWindow = window as SpeechRecognitionWindow
    const SpeechRecognition = speechWindow.SpeechRecognition ?? speechWindow.webkitSpeechRecognition
    if (!SpeechRecognition) return

    try {
      const recognition = new SpeechRecognition()
      recognition.lang = 'vi-VN'
      recognition.continuous = true
      recognition.interimResults = true
      recognitionRef.current = recognition
      shouldKeepRecognitionRef.current = true

      recognition.onresult = (event) => {
        let interimSegment = ''
        for (let i = event.resultIndex; i < event.results.length; i++) {
          const result = event.results[i]
          const segment = result?.[0]?.transcript ?? ''
          if (!segment) continue
          if (result.isFinal !== false) {
            accumulatedFinalRef.current = `${accumulatedFinalRef.current} ${segment}`.trim()
          } else {
            interimSegment += segment
          }
        }
        latestInterimRef.current = interimSegment.trim()
        const combined = normalizeSeTermsClient(
          `${accumulatedFinalRef.current} ${latestInterimRef.current}`.trim(),
        )
        liveTranscriptRef.current = combined
        setLiveTranscript(combined)
      }

      recognition.onerror = () => {
        // Không làm gián đoạn MediaRecorder nếu SpeechRecognition của trình duyệt gặp lỗi.
      }

      recognition.onend = () => {
        flushInterimTranscript()

        if (recognitionEndResolverRef.current) {
          recognitionEndResolverRef.current()
          return
        }

        if (shouldKeepRecognitionRef.current && recognitionRef.current === recognition) {
          try {
            recognition.start()
          } catch {
            // Bỏ qua nếu trình duyệt không cho phép khởi động lại tức thì.
          }
        }
      }

      recognition.start()
    } catch {
      // Nếu trình duyệt chặn hoặc không hỗ trợ SpeechRecognition, MediaRecorder vẫn chạy 100%.
    }
  }, [flushInterimTranscript])

  const releaseRecorder = useCallback(() => {
    void stopParallelRecognition(false)
    filterCleanupRef.current?.()
    filterCleanupRef.current = null
    stopMediaStream(streamRef.current)
    streamRef.current = null
    recorderRef.current = null
    setIsRecording(false)
  }, [stopParallelRecognition])

  const startRecording = useCallback(async () => {
    if (!navigator.mediaDevices?.getUserMedia || typeof MediaRecorder === 'undefined') {
      const message = 'Trình duyệt này chưa hỗ trợ ghi âm bằng microphone.'
      setError(message)
      throw new Error(message)
    }

    if (recorderRef.current?.state === 'recording') return

    setError(null)
    accumulatedFinalRef.current = ''
    latestInterimRef.current = ''
    liveTranscriptRef.current = ''
    setLiveTranscript('')

    const rawStream = await navigator.mediaDevices.getUserMedia({
      audio: {
        echoCancellation: { ideal: true },
        noiseSuppression: { ideal: true },
        autoGainControl: { ideal: true },
        channelCount: { ideal: 1 },
        sampleRate: { ideal: 16000 },
      },
    })

    const { recordingStream, cleanup } = createNoiseFilteredStream(rawStream)
    filterCleanupRef.current = cleanup

    try {
      const mimeType = getSupportedMimeType()
      const recorder = mimeType
        ? new MediaRecorder(recordingStream, { mimeType, audioBitsPerSecond: 64000 })
        : new MediaRecorder(recordingStream)

      streamRef.current = rawStream
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
        flushInterimTranscript()
        const audio: AudioBlobWithTranscript = new Blob(chunksRef.current, {
          type: recorder.mimeType || mimeType || 'audio/webm',
        })
        audio.clientTranscript = liveTranscriptRef.current.trim()
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
      startParallelRecognition()
    } catch (recordingError) {
      void stopParallelRecognition(false)
      cleanup()
      filterCleanupRef.current = null
      stopMediaStream(rawStream)
      const message =
        recordingError instanceof Error
          ? recordingError.message
          : 'Không thể khởi tạo thiết bị ghi âm.'
      setError(message)
      throw recordingError
    }
  }, [flushInterimTranscript, releaseRecorder, startParallelRecognition, stopParallelRecognition])

  const stopRecording = useCallback(async () => {
    const recorder = recorderRef.current
    if (!recorder || recorder.state === 'inactive') {
      throw new Error('Chưa có bản ghi âm đang hoạt động.')
    }

    await stopParallelRecognition(true)

    return new Promise<Blob>((resolve, reject) => {
      pendingStopRef.current = { resolve, reject }
      recorder.requestData()
      recorder.stop()
    })
  }, [stopParallelRecognition])

  const cancelRecording = useCallback(() => {
    void stopParallelRecognition(false)
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
  }, [releaseRecorder, stopParallelRecognition])

  useEffect(() => cancelRecording, [cancelRecording])

  return {
    isRecording,
    liveTranscript,
    error,
    startRecording,
    stopRecording,
    cancelRecording,
    clearError: () => setError(null),
  }
}
