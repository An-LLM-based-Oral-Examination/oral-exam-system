import { useState, useCallback } from 'react'

/**
 * Web Speech API Hook for Text-to-Speech (TTS) and Speech-to-Text (STT)
 * Used in MF-01 and MF-02
 */
export function useWebSpeech() {
  const [isListening, setIsListening] = useState(false)
  const [transcript, setTranscript] = useState('')
  const [error, setError] = useState<string | null>(null)

  const speak = useCallback((text: string) => {
    if (!('speechSynthesis' in window)) {
      console.warn('Web Speech Synthesis is not supported in this browser.')
      return
    }
    window.speechSynthesis.cancel()
    const utterance = new SpeechSynthesisUtterance(text)
    utterance.lang = 'vi-VN'
    utterance.rate = 1.0
    window.speechSynthesis.speak(utterance)
  }, [])

  const startListening = useCallback(() => {
    const SpeechRecognition = (window as any).SpeechRecognition || (window as any).webkitSpeechRecognition
    if (!SpeechRecognition) {
      setError('Web Speech Recognition is not supported in this browser.')
      return
    }

    try {
      const recognition = new SpeechRecognition()
      recognition.lang = 'vi-VN'
      recognition.continuous = true
      recognition.interimResults = true

      recognition.onstart = () => {
        setIsListening(true)
        setError(null)
      }

      recognition.onresult = (event: any) => {
        let currentTranscript = ''
        for (let i = event.resultIndex; i < event.results.length; i++) {
          currentTranscript += event.results[i][0].transcript
        }
        setTranscript(currentTranscript)
      }

      recognition.onerror = (event: any) => {
        setError(event.error)
        setIsListening(false)
      }

      recognition.onend = () => {
        setIsListening(false)
      }

      recognition.start()
    } catch (err: any) {
      setError(err?.message || 'Error initializing speech recognition')
    }
  }, [])

  const stopListening = useCallback(() => {
    setIsListening(false)
  }, [])

  const resetTranscript = useCallback(() => {
    setTranscript('')
  }, [])

  return {
    isListening,
    transcript,
    error,
    speak,
    startListening,
    stopListening,
    resetTranscript,
    setTranscript,
  }
}
