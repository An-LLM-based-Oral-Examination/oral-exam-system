import { useState, useCallback, useEffect, useRef } from 'react'
import { DEFAULT_TTS_VOICE, playNaturalSpeech } from '@/services/speech.service'
import {
  classifyMicrophoneError,
  hasAvailableAudioInputDevice,
  type MicrophoneConnectionState,
  type MicrophoneIssueKind,
} from '@/utils/microphoneDiagnostics'

interface SpeechRecognitionAlternativeLike {
  transcript: string
}

interface SpeechRecognitionResultLike {
  readonly [index: number]: SpeechRecognitionAlternativeLike
}

interface SpeechRecognitionResultListLike {
  readonly length: number
  readonly [index: number]: SpeechRecognitionResultLike
}

interface SpeechRecognitionEventLike extends Event {
  readonly resultIndex: number
  readonly results: SpeechRecognitionResultListLike
}

interface SpeechRecognitionErrorEventLike extends Event {
  readonly error: string
}

interface SpeechRecognitionLike {
  lang: string
  continuous: boolean
  interimResults: boolean
  onstart: (() => void) | null
  onresult: ((event: SpeechRecognitionEventLike) => void) | null
  onerror: ((event: SpeechRecognitionErrorEventLike) => void) | null
  onend: (() => void) | null
  start: () => void
  stop: () => void
}

type SpeechRecognitionConstructor = new () => SpeechRecognitionLike

interface SpeechRecognitionWindow extends Window {
  SpeechRecognition?: SpeechRecognitionConstructor
  webkitSpeechRecognition?: SpeechRecognitionConstructor
}

export interface StartListeningOptions {
  preserveTranscript?: boolean
}

const RECONNECTED_BANNER_MS = 4_500

/**
 * Web Speech API Hook for Text-to-Speech (TTS) and Speech-to-Text (STT)
 * Hỗ trợ giữ nguyên câu trả lời đã nói và tự động nối tiếp khi cắm lại tai nghe.
 */
export function useSpeechRecognition() {
  const [isListening, setIsListening] = useState(false)
  const [transcript, setTranscript] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [micIssue, setMicIssue] = useState<MicrophoneIssueKind | null>(null)
  const [connectionState, setConnectionState] = useState<MicrophoneConnectionState>('idle')

  const recognitionRef = useRef<SpeechRecognitionLike | null>(null)
  const isSupported =
    typeof window !== 'undefined' &&
    Boolean(
      (window as SpeechRecognitionWindow).SpeechRecognition ??
        (window as SpeechRecognitionWindow).webkitSpeechRecognition,
    )
  const transcriptRef = useRef('')
  const committedPrefixRef = useRef('')
  const lastLangRef = useRef('vi-VN')
  const waitingForDeviceReconnectRef = useRef(false)
  const reconnectTimerRef = useRef<number | null>(null)
  const reconnectedBannerTimerRef = useRef<number | null>(null)
  const startListeningInternalRef = useRef<
    (lang: string, preserveTranscript: boolean, fromAutoReconnect: boolean) => void
  >(() => {})

  const speak = useCallback((text: string) => {
    void playNaturalSpeech(text, DEFAULT_TTS_VOICE, 1).catch((speechError) => {
      console.warn('Không thể phát giọng đọc tự nhiên.', speechError)
    })
  }, [])

  const clearTimers = useCallback(() => {
    if (reconnectTimerRef.current !== null) {
      window.clearTimeout(reconnectTimerRef.current)
      reconnectTimerRef.current = null
    }
    if (reconnectedBannerTimerRef.current !== null) {
      window.clearTimeout(reconnectedBannerTimerRef.current)
      reconnectedBannerTimerRef.current = null
    }
  }, [])

  const detachAndStop = useCallback((recognition: SpeechRecognitionLike | null) => {
    if (!recognition) return
    recognition.onstart = null
    recognition.onresult = null
    recognition.onerror = null
    recognition.onend = null
    try {
      recognition.stop()
    } catch {
      // Ignore stop errors if the instance was not yet active or already stopped.
    }
  }, [])

  useEffect(() => {
    startListeningInternalRef.current = (
      lang: string,
      preserveTranscript: boolean,
      fromAutoReconnect: boolean,
    ) => {
      const speechWindow = window as SpeechRecognitionWindow
      const SpeechRecognition =
        speechWindow.SpeechRecognition ?? speechWindow.webkitSpeechRecognition

      if (!SpeechRecognition) {
        setError('Trình duyệt hiện tại chưa hỗ trợ nhận diện giọng nói trực tiếp.')
        setMicIssue('unsupported')
        return
      }

      if (recognitionRef.current) {
        const previous = recognitionRef.current
        recognitionRef.current = null
        detachAndStop(previous)
      }

      let recognition: SpeechRecognitionLike | null = null
      try {
        const normalizedLang = lang.trim() || 'vi-VN'
        lastLangRef.current = normalizedLang
        recognition = new SpeechRecognition()
        const currentRecognition = recognition
        currentRecognition.lang = normalizedLang
        currentRecognition.continuous = true
        currentRecognition.interimResults = true

        if (preserveTranscript) {
          committedPrefixRef.current = transcriptRef.current.trim()
        } else {
          committedPrefixRef.current = ''
          transcriptRef.current = ''
          setTranscript('')
        }

        recognitionRef.current = currentRecognition

        currentRecognition.onstart = () => {
          if (recognitionRef.current !== currentRecognition) return
          waitingForDeviceReconnectRef.current = false
          setIsListening(true)
          setError(null)
          setMicIssue(null)
          if (fromAutoReconnect) {
            setConnectionState('reconnected')
            if (reconnectedBannerTimerRef.current !== null) {
              window.clearTimeout(reconnectedBannerTimerRef.current)
            }
            reconnectedBannerTimerRef.current = window.setTimeout(() => {
              reconnectedBannerTimerRef.current = null
              setConnectionState((prev) => (prev === 'reconnected' ? 'connected' : prev))
            }, RECONNECTED_BANNER_MS)
          } else {
            setConnectionState('connected')
          }
        }

        currentRecognition.onresult = (event) => {
          if (recognitionRef.current !== currentRecognition) return
          let currentSegment = ''
          for (let i = 0; i < event.results.length; i += 1) {
            currentSegment += event.results[i][0].transcript
          }
          const cleanSegment = currentSegment.trim()
          const prefix = committedPrefixRef.current
          const merged =
            prefix && cleanSegment ? `${prefix} ${cleanSegment}`.trim() : cleanSegment || prefix
          transcriptRef.current = merged
          setTranscript(merged)
        }

        currentRecognition.onerror = (event) => {
          if (recognitionRef.current !== currentRecognition) return
          const code = event.error
          if (code === 'audio-capture') {
            committedPrefixRef.current = transcriptRef.current.trim()
            waitingForDeviceReconnectRef.current = true
            setIsListening(false)
            setConnectionState('disconnected')
            setMicIssue(null)
            setError(null)
            return
          }

          if (code === 'not-allowed' || code === 'service-not-allowed') {
            waitingForDeviceReconnectRef.current = false
            setMicIssue('permission_denied')
            setError(code)
            setIsListening(false)
            setConnectionState('idle')
            return
          }

          setError(code)
          setIsListening(false)
        }

        currentRecognition.onend = () => {
          if (recognitionRef.current !== currentRecognition) return
          setIsListening(false)
          recognitionRef.current = null
          if (!waitingForDeviceReconnectRef.current) {
            setConnectionState((prev) => (prev === 'reconnected' ? prev : 'idle'))
          }
        }

        currentRecognition.start()
      } catch (err: unknown) {
        if (recognition) {
          recognition.onstart = null
          recognition.onresult = null
          recognition.onerror = null
          recognition.onend = null
        }
        recognitionRef.current = null
        const classified = classifyMicrophoneError(err)
        setMicIssue(classified)
        setError(err instanceof Error ? err.message : 'Không thể khởi tạo nhận diện giọng nói.')
      }
    }
  }, [detachAndStop])

  const startListening = useCallback((lang = 'vi-VN', options?: StartListeningOptions) => {
    waitingForDeviceReconnectRef.current = false
    startListeningInternalRef.current(lang, Boolean(options?.preserveTranscript), false)
  }, [])

  const reconnectListening = useCallback(async () => {
    setConnectionState('reconnecting')
    const available = await hasAvailableAudioInputDevice()
    if (!available) {
      setConnectionState('disconnected')
      return
    }
    startListeningInternalRef.current(lastLangRef.current, true, true)
  }, [])

  useEffect(() => {
    if (
      connectionState !== 'disconnected' ||
      typeof navigator === 'undefined' ||
      !navigator.mediaDevices?.addEventListener
    ) {
      return
    }

    const handleDeviceChange = () => {
      if (!waitingForDeviceReconnectRef.current) return
      if (reconnectTimerRef.current !== null) {
        window.clearTimeout(reconnectTimerRef.current)
      }
      reconnectTimerRef.current = window.setTimeout(() => {
        reconnectTimerRef.current = null
        void reconnectListening()
      }, 350)
    }

    navigator.mediaDevices.addEventListener('devicechange', handleDeviceChange)
    return () => {
      navigator.mediaDevices.removeEventListener('devicechange', handleDeviceChange)
    }
  }, [connectionState, reconnectListening])

  const stopListening = useCallback(() => {
    waitingForDeviceReconnectRef.current = false
    clearTimers()
    const current = recognitionRef.current
    recognitionRef.current = null
    detachAndStop(current)
    setIsListening(false)
    setConnectionState('idle')
  }, [clearTimers, detachAndStop])

  const resetTranscript = useCallback(() => {
    committedPrefixRef.current = ''
    transcriptRef.current = ''
    setTranscript('')
  }, [])

  const clearError = useCallback(() => {
    setError(null)
    setMicIssue(null)
    if (connectionState === 'disconnected') {
      waitingForDeviceReconnectRef.current = false
      setConnectionState('idle')
    }
  }, [connectionState])

  const getCurrentTranscript = useCallback(() => transcriptRef.current, [])

  useEffect(() => {
    return () => {
      waitingForDeviceReconnectRef.current = false
      clearTimers()
      const current = recognitionRef.current
      recognitionRef.current = null
      detachAndStop(current)
    }
  }, [clearTimers, detachAndStop])

  return {
    isSupported,
    isListening,
    transcript,
    error,
    micIssue,
    connectionState,
    speak,
    startListening,
    reconnectListening,
    stopListening,
    resetTranscript,
    clearError,
    getCurrentTranscript,
    setTranscript,
  }
}

export { useSpeechRecognition as useWebSpeech }
