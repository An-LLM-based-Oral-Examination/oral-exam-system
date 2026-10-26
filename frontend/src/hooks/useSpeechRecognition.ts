import { useState, useCallback, useEffect, useRef } from 'react'
import { DEFAULT_TTS_VOICE, playNaturalSpeech } from '@/services/speech.service'

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

/**
 * Web Speech API Hook for Text-to-Speech (TTS) and Speech-to-Text (STT)
 * Used in MF-01 and MF-02
 */
export function useSpeechRecognition() {
  const [isListening, setIsListening] = useState(false)
  const [transcript, setTranscript] = useState('')
  const [error, setError] = useState<string | null>(null)
  const recognitionRef = useRef<SpeechRecognitionLike | null>(null)
  const transcriptRef = useRef('')

  const speak = useCallback((text: string) => {
    void playNaturalSpeech(text, DEFAULT_TTS_VOICE, 1).catch((speechError) => {
      console.warn('Không thể phát giọng đọc tự nhiên.', speechError)
    })
  }, [])

  const startListening = useCallback((lang = 'vi-VN') => {
    const speechWindow = window as SpeechRecognitionWindow
    const SpeechRecognition = speechWindow.SpeechRecognition ?? speechWindow.webkitSpeechRecognition

    if (!SpeechRecognition) {
      setError('Web Speech Recognition is not supported in this browser.')
      return
    }

    try {
      const recognition = new SpeechRecognition()
      recognition.lang = lang.trim() || 'vi-VN'
      recognition.continuous = true
      recognition.interimResults = true

      transcriptRef.current = ''
      setTranscript('')
      recognitionRef.current = recognition

      recognition.onstart = () => {
        setIsListening(true)
        setError(null)
      }

      recognition.onresult = (event) => {
        let currentTranscript = ''
        for (let i = 0; i < event.results.length; i++) {
          currentTranscript += event.results[i][0].transcript
        }
        transcriptRef.current = currentTranscript.trim()
        setTranscript(transcriptRef.current)
      }

      recognition.onerror = (event) => {
        setError(event.error)
        setIsListening(false)
      }

      recognition.onend = () => {
        setIsListening(false)
        if (recognitionRef.current === recognition) recognitionRef.current = null
      }

      recognition.start()
    } catch (err: unknown) {
      recognitionRef.current = null
      setError(err instanceof Error ? err.message : 'Error initializing speech recognition')
    }
  }, [])

  const stopListening = useCallback(() => {
    recognitionRef.current?.stop()
    recognitionRef.current = null
    setIsListening(false)
  }, [])

  const resetTranscript = useCallback(() => {
    transcriptRef.current = ''
    setTranscript('')
  }, [])

  const getCurrentTranscript = useCallback(() => transcriptRef.current, [])

  useEffect(() => {
    return () => {
      recognitionRef.current?.stop()
      recognitionRef.current = null
    }
  }, [])

  return {
    isListening,
    transcript,
    error,
    speak,
    startListening,
    stopListening,
    resetTranscript,
    getCurrentTranscript,
    setTranscript,
  }
}

export { useSpeechRecognition as useWebSpeech }
