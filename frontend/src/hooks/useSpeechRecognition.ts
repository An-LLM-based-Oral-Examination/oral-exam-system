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
  const isSupported =
    typeof window !== 'undefined' &&
    Boolean(
      (window as SpeechRecognitionWindow).SpeechRecognition ??
        (window as SpeechRecognitionWindow).webkitSpeechRecognition,
    )

  const speak = useCallback((text: string) => {
    void playNaturalSpeech(text, DEFAULT_TTS_VOICE, 1).catch((speechError) => {
      console.warn('Không thể phát giọng đọc tự nhiên.', speechError)
    })
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

  const startListening = useCallback((lang = 'vi-VN') => {
    const speechWindow = window as SpeechRecognitionWindow
    const SpeechRecognition = speechWindow.SpeechRecognition ?? speechWindow.webkitSpeechRecognition

    if (!SpeechRecognition) {
      setError('Web Speech Recognition is not supported in this browser.')
      return
    }

    if (recognitionRef.current) {
      const previous = recognitionRef.current
      recognitionRef.current = null
      detachAndStop(previous)
    }

    let recognition: SpeechRecognitionLike | null = null
    try {
      recognition = new SpeechRecognition()
      const currentRecognition = recognition
      currentRecognition.lang = lang.trim() || 'vi-VN'
      currentRecognition.continuous = true
      currentRecognition.interimResults = true

      transcriptRef.current = ''
      setTranscript('')
      recognitionRef.current = currentRecognition

      currentRecognition.onstart = () => {
        if (recognitionRef.current !== currentRecognition) return
        setIsListening(true)
        setError(null)
      }

      currentRecognition.onresult = (event) => {
        if (recognitionRef.current !== currentRecognition) return
        let currentTranscript = ''
        for (let i = 0; i < event.results.length; i++) {
          currentTranscript += event.results[i][0].transcript
        }
        transcriptRef.current = currentTranscript.trim()
        setTranscript(transcriptRef.current)
      }

      currentRecognition.onerror = (event) => {
        if (recognitionRef.current !== currentRecognition) return
        setError(event.error)
        setIsListening(false)
      }

      currentRecognition.onend = () => {
        if (recognitionRef.current !== currentRecognition) return
        setIsListening(false)
        recognitionRef.current = null
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
      setError(err instanceof Error ? err.message : 'Error initializing speech recognition')
    }
  }, [detachAndStop])

  const stopListening = useCallback(() => {
    const current = recognitionRef.current
    recognitionRef.current = null
    detachAndStop(current)
    setIsListening(false)
  }, [detachAndStop])

  const resetTranscript = useCallback(() => {
    transcriptRef.current = ''
    setTranscript('')
  }, [])

  const getCurrentTranscript = useCallback(() => transcriptRef.current, [])

  useEffect(() => {
    return () => {
      const current = recognitionRef.current
      recognitionRef.current = null
      detachAndStop(current)
    }
  }, [detachAndStop])

  return {
    isSupported,
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
