import { useState, useCallback, useEffect, useMemo, useRef } from 'react'
import { speechService } from '@/services/speech.service'

export interface SpeechRecognitionAlternativeLike {
  transcript: string
}

export interface SpeechRecognitionResultLike {
  readonly isFinal?: boolean
  readonly length?: number
  readonly [index: number]: SpeechRecognitionAlternativeLike
}

export interface SpeechRecognitionResultListLike {
  readonly length: number
  readonly [index: number]: SpeechRecognitionResultLike
}

export interface SpeechRecognitionEventLike extends Event {
  readonly resultIndex: number
  readonly results: SpeechRecognitionResultListLike
}

export interface SpeechRecognitionErrorEventLike extends Event {
  readonly error: string
}

export interface SpeechRecognitionLike {
  lang: string
  continuous: boolean
  interimResults: boolean
  onstart: (() => void) | null
  onresult: ((event: SpeechRecognitionEventLike) => void) | null
  onerror: ((event: SpeechRecognitionErrorEventLike) => void) | null
  onend: (() => void) | null
  start: () => void
  stop: () => void
  abort?: () => void
}

export type SpeechRecognitionConstructor = new () => SpeechRecognitionLike

export interface SpeechRecognitionWindow extends Window {
  SpeechRecognition?: SpeechRecognitionConstructor
  webkitSpeechRecognition?: SpeechRecognitionConstructor
}

export interface UseSpeechRecognitionOptions {
  vocabularyHint?: string
}

const NOISE_HALLUCINATION_REGEX =
  /^(?:thank\s+you(?:\s+very\s+much|\s+so\s+much|\s+for\s+watching)?|thanks(?:\s+for\s+watching)?|subtitles\s+by.*|amara\.org.*|please\s+subscribe.*|like\s+and\s+subscribe.*|hãy\s+đăng\s+ký\s+kênh.*|cảm\s+ơn\s+các\s+bạn\s+đã\s+theo\s+dõi.*|hẹn\s+gặp\s+lại\s+các\s+bạn.*|phụ\s+đề\s+bởi.*|you|bye(?:\s+bye)?)$/i

const SE_TERM_REPLACEMENTS: ReadonlyArray<readonly [RegExp, string]> = [
  [/\bcờ\s*lin\s*a\s*ki\s*téc\s*chơ\b/gi, 'Clean Architecture'],
  [/\bcờ\s*lin\s*a\s*ki\s*téc\b/gi, 'Clean Architecture'],
  [/\blinh\s*a\s*ki\s*téc(?:\s*trở)?\b/gi, 'Clean Architecture'],
  [/\blinh\s+akitek(?:\s+trở)?\b/gi, 'Clean Architecture'],
  [/\bđốt\s*nét\s*tám\b/gi, '.NET 8'],
  [/\bđốt\s*nét\b/gi, '.NET'],
  [/\bdot\s+net\b/gi, '.NET'],
  [/\bsi\s*sáp\b/gi, 'C#'],
  [/\bc\s+sharp\b/gi, 'C#'],
  [/\bsi\s*quy\s*a\s*ét\b/gi, 'CQRS'],
  [/\bsi\s*quy\s*ai\s*ét\b/gi, 'CQRS'],
  [/\bsi\s*quy\s*rờ\s*ét\b/gi, 'CQRS'],
  [/\bcqis\b/gi, 'CQRS'],
  [/\bmê\s*đi\s*a\s*tờ\b/gi, 'MediatR'],
  [/\bmê\s*đi\s*ây\s*tơ\b/gi, 'MediatR'],
  [/\bmê\s*đi\s*a\s*trơ\b/gi, 'MediatR'],
  [/\bmedia\s+trơ\b/gi, 'MediatR'],
  [/\bbao\s*đựt\s*chan\s*nồ\b/gi, 'BoundedChannel'],
  [/\bbao\s*địt\s*chan\s*nồ\b/gi, 'BoundedChannel'],
  [/\bđì\s*pen\s*đần\s*xi\s*in\s*vơ\s*sần\b/gi, 'Dependency Inversion'],
  [/\bđì\s*pen\s*đần\s*xi\s*in\s*giéc\s*sần\b/gi, 'Dependency Injection'],
  [/\bbách\s*grao\s*sơ\s*vít\b/gi, 'BackgroundService'],
  [/\bai\s*hốt\s*tựt\s*sơ\s*vít\b/gi, 'IHostedService'],
  [/\bphờ\s*lu\s*ần\s*va\s*li\s*đây\s*sần\b/gi, 'FluentValidation'],
  [/\bflu\s*ần\s*va\s*li\s*đây\s*sần\b/gi, 'FluentValidation'],
  [/\boăn\s*guây\s*lốc\b/gi, 'OneWayLock'],
  [/\bxích\s*nồ\s*a\b/gi, 'SignalR'],
  [/\bi\s*ép\s*co\b/gi, 'EF Core'],
  [/\ben\s*ti\s*ti\s*phờ\s*rêm\s*guốc(?:\s*co)?\b/gi, 'Entity Framework Core'],
  [/\bđi\s*bi\s*con\s*tếch\b/gi, 'DbContext'],
  [/\bác\s*nô\s*trắc\s*kinh\b/gi, 'AsNoTracking'],
  [/\brê\s*pô\s*xi\s*tô\s*ri(?:\s*pát\s*tần)?\b/gi, 'Repository Pattern'],
  [/\biu\s*nít\s*óp\s*guốc\b/gi, 'Unit of Work'],
  [/\bpô\s*li\s*rì\s*trai\b/gi, 'Polly Retry'],
  [/\bsơ\s*kít\s*bờ\s*rếch\s*cơ\b/gi, 'Circuit Breaker'],
  [/\bđét\s*lét\s*tơ\s*kiu\b/gi, 'Dead-Letter Queue'],
  [/\bmai\s*crô\s*sơ\s*vít\b/gi, 'Microservices'],
  [/\bsa\s*ga\s*pát\s*tần\b/gi, 'Saga Pattern'],
  [/\bcáp\s*thê\s*ô\s*rầm\b/gi, 'CAP Theorem'],
  [/\bpốt\s*gờ\s*rét\b/gi, 'PostgreSQL'],
  [/\bri\s*ách\b/gi, 'React'],
  [/\btai\s*xờ\s*cờ\s*ríp\b/gi, 'TypeScript'],
  [/\bkiến\s+chúc\b/gi, 'kiến trúc'],
]

export function normalizeSeTermsClient(text: string): string {
  if (!text) return ''
  const rawSentences = text
    .trim()
    .split(/(?<=[.!?。])\s+/)
    .map((s) => s.trim())
    .filter(Boolean)

  const filteredSentences: string[] = []
  let lastCore = ''
  for (const sentence of rawSentences) {
    const core = sentence.replace(/^[\s.,!?]+|[\s.,!?]+$/g, '').trim()
    if (!core || NOISE_HALLUCINATION_REGEX.test(core)) continue
    if (lastCore && core.toLowerCase() === lastCore.toLowerCase()) continue
    lastCore = core
    filteredSentences.push(sentence)
  }

  if (filteredSentences.length === 0) return ''

  let normalized = filteredSentences.join(' ')
  for (const [pattern, replacement] of SE_TERM_REPLACEMENTS) {
    normalized = normalized.replace(pattern, replacement)
  }
  return normalized.replace(/\s+/g, ' ').trim()
}

/**
 * Hook kết hợp Web Speech API (hiển thị tức thì) và Backend STT (/api/v1/speech/stt)
 * Dùng cho MF-01, MF-02 và kiểm tra thiết bị âm thanh.
 */
export function useSpeechRecognition(options?: UseSpeechRecognitionOptions) {
  const [isListening, setIsListening] = useState(false)
  const [isTranscribingBackend, setIsTranscribingBackend] = useState(false)
  const [transcript, setTranscriptState] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [sttProvider, setSttProvider] = useState<string | null>(null)
  const [confidenceScore, setConfidenceScore] = useState<number | null>(null)

  const recognitionRef = useRef<SpeechRecognitionLike | null>(null)
  const mediaRecorderRef = useRef<MediaRecorder | null>(null)
  const mediaStreamRef = useRef<MediaStream | null>(null)
  const audioChunksRef = useRef<Blob[]>([])
  const shouldKeepListeningRef = useRef(false)
  const accumulatedFinalRef = useRef('')
  const latestInterimRef = useRef('')
  const vocabularyHintRef = useRef(options?.vocabularyHint)

  useEffect(() => {
    vocabularyHintRef.current = options?.vocabularyHint
  }, [options?.vocabularyHint])

  const cleanupMediaStream = useCallback(() => {
    if (mediaStreamRef.current) {
      mediaStreamRef.current.getTracks().forEach((track) => track.stop())
      mediaStreamRef.current = null
    }
  }, [])

  const setTranscript = useCallback((value: string | ((prev: string) => string)) => {
    setTranscriptState((prev) => {
      const nextValue = typeof value === 'function' ? value(prev) : value
      const normalized = normalizeSeTermsClient(nextValue)
      accumulatedFinalRef.current = normalized
      latestInterimRef.current = ''
      return normalized
    })
  }, [])

  const speak = useCallback((text: string) => {
    void speechService.speakText(text)
  }, [])

  const stopSpeaking = useCallback(() => {
    speechService.stopSpeaking()
  }, [])

  const startBackendMediaRecorder = useCallback(async (): Promise<boolean> => {
    if (
      typeof window === 'undefined' ||
      typeof MediaRecorder === 'undefined' ||
      !navigator.mediaDevices?.getUserMedia
    ) {
      return false
    }

    try {
      cleanupMediaStream()
      const stream = await navigator.mediaDevices.getUserMedia({
        audio: {
          echoCancellation: true,
          noiseSuppression: true,
        },
      })
      if (!shouldKeepListeningRef.current) {
        stream.getTracks().forEach((track) => track.stop())
        return false
      }

      mediaStreamRef.current = stream
      audioChunksRef.current = []

      const mimeType = MediaRecorder.isTypeSupported?.('audio/webm;codecs=opus')
        ? 'audio/webm;codecs=opus'
        : MediaRecorder.isTypeSupported?.('audio/webm')
          ? 'audio/webm'
          : ''

      const recorder = mimeType ? new MediaRecorder(stream, { mimeType }) : new MediaRecorder(stream)
      mediaRecorderRef.current = recorder

      recorder.ondataavailable = (event) => {
        if (event.data && event.data.size > 0) {
          audioChunksRef.current.push(event.data)
        }
      }

      recorder.onstop = () => {
        const recordedChunks = [...audioChunksRef.current]
        audioChunksRef.current = []
        cleanupMediaStream()

        if (recordedChunks.length === 0) return
        const blob = new Blob(recordedChunks, { type: recorder.mimeType || 'audio/webm' })
        if (blob.size === 0) return

        const currentClientTranscript = normalizeSeTermsClient(
          `${accumulatedFinalRef.current} ${latestInterimRef.current}`.trim(),
        )

        setIsTranscribingBackend(true)
        void speechService
          .transcribeAudio(blob, {
            vocabularyHint: vocabularyHintRef.current,
            clientTranscript: currentClientTranscript || undefined,
          })
          .then((res) => {
            if (res.text.trim()) {
              const normalized = normalizeSeTermsClient(res.text)
              accumulatedFinalRef.current = normalized
              latestInterimRef.current = ''
              setTranscriptState(normalized)
              setSttProvider(res.provider)
              setConfidenceScore(res.confidenceScore ?? null)
              setError(null)
            }
          })
          .catch(() => {
            if (!currentClientTranscript) {
              setError('Không nhận diện được giọng nói rõ ràng. Vui lòng nói gần micro hơn.')
            }
          })
          .finally(() => {
            setIsTranscribingBackend(false)
          })
      }

      recorder.start(250)
      return true
    } catch {
      cleanupMediaStream()
      return false
    }
  }, [cleanupMediaStream])

  const startListening = useCallback(() => {
    const speechWindow = window as SpeechRecognitionWindow
    const SpeechRecognition = speechWindow.SpeechRecognition ?? speechWindow.webkitSpeechRecognition

    if (recognitionRef.current) {
      shouldKeepListeningRef.current = false
      try {
        recognitionRef.current.stop()
      } catch {
        // Bỏ qua lỗi dừng phiên nhận diện cũ.
      }
      recognitionRef.current = null
    }

    if (mediaRecorderRef.current && mediaRecorderRef.current.state !== 'inactive') {
      try {
        mediaRecorderRef.current.stop()
      } catch {
        // Bỏ qua lỗi dừng bộ ghi âm cũ.
      }
      mediaRecorderRef.current = null
    }

    shouldKeepListeningRef.current = true
    setError(null)
    setSttProvider(null)
    setConfidenceScore(null)

    let startedWebSpeech = false
    if (SpeechRecognition) {
      try {
        const recognition = new SpeechRecognition()
        recognition.lang = 'vi-VN'
        recognition.continuous = true
        recognition.interimResults = true
        recognitionRef.current = recognition

        recognition.onstart = () => {
          setIsListening(true)
          setError(null)
        }

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
          setTranscriptState(combined)
          setSttProvider((prev) => prev ?? 'WebSpeechRealtime')
        }

        recognition.onerror = (event) => {
          if (event.error === 'no-speech' || event.error === 'aborted') {
            return
          }
          // Nếu MediaRecorder vẫn đang ghi âm để gửi về Backend STT thì không ngắt phiên nghe
          if (mediaRecorderRef.current && mediaRecorderRef.current.state === 'recording') {
            return
          }
          shouldKeepListeningRef.current = false
          setError(event.error)
          setIsListening(false)
        }

        recognition.onend = () => {
          if (latestInterimRef.current) {
            accumulatedFinalRef.current =
              `${accumulatedFinalRef.current} ${latestInterimRef.current}`.trim()
            latestInterimRef.current = ''
            setTranscriptState(normalizeSeTermsClient(accumulatedFinalRef.current))
          }

          if (shouldKeepListeningRef.current && recognitionRef.current === recognition) {
            try {
              recognition.start()
              return
            } catch {
              // Nếu trình duyệt từ chối bật lại tự động thì kiểm tra MediaRecorder.
            }
          }
          if (!mediaRecorderRef.current || mediaRecorderRef.current.state !== 'recording') {
            setIsListening(false)
          }
        }

        recognition.start()
        startedWebSpeech = true
        setIsListening(true)
      } catch (err: unknown) {
        if (!navigator.mediaDevices?.getUserMedia) {
          shouldKeepListeningRef.current = false
          setError(err instanceof Error ? err.message : 'Error initializing speech recognition')
          return
        }
      }
    }

    void startBackendMediaRecorder().then((recorderStarted) => {
      if (recorderStarted) {
        setIsListening(true)
        setError(null)
      } else if (!startedWebSpeech) {
        shouldKeepListeningRef.current = false
        setIsListening(false)
        setError('Web Speech Recognition is not supported in this browser.')
      }
    })
  }, [startBackendMediaRecorder])

  const stopListening = useCallback(() => {
    shouldKeepListeningRef.current = false
    try {
      recognitionRef.current?.stop()
    } catch {
      // Bỏ qua lỗi khi dừng SpeechRecognition.
    }
    recognitionRef.current = null

    if (mediaRecorderRef.current && mediaRecorderRef.current.state !== 'inactive') {
      try {
        mediaRecorderRef.current.stop()
      } catch {
        cleanupMediaStream()
      }
      mediaRecorderRef.current = null
    } else {
      cleanupMediaStream()
    }

    setIsListening(false)
  }, [cleanupMediaStream])

  const resetTranscript = useCallback(() => {
    accumulatedFinalRef.current = ''
    latestInterimRef.current = ''
    setTranscriptState('')
    setSttProvider(null)
    setConfidenceScore(null)
    setError(null)
  }, [])

  useEffect(() => {
    return () => {
      shouldKeepListeningRef.current = false
      try {
        recognitionRef.current?.stop()
      } catch {
        // Bỏ qua lỗi khi unmount.
      }
      recognitionRef.current = null
      if (mediaRecorderRef.current && mediaRecorderRef.current.state !== 'inactive') {
        try {
          mediaRecorderRef.current.stop()
        } catch {
          // Bỏ qua lỗi khi unmount.
        }
      }
      mediaRecorderRef.current = null
      cleanupMediaStream()
    }
  }, [cleanupMediaStream])

  return useMemo(
    () => ({
      isListening,
      isTranscribingBackend,
      transcript,
      error,
      sttProvider,
      confidenceScore,
      speak,
      stopSpeaking,
      startListening,
      stopListening,
      resetTranscript,
      setTranscript,
    }),
    [
      isListening,
      isTranscribingBackend,
      transcript,
      error,
      sttProvider,
      confidenceScore,
      speak,
      stopSpeaking,
      startListening,
      stopListening,
      resetTranscript,
      setTranscript,
    ],
  )
}

export { useSpeechRecognition as useWebSpeech }
