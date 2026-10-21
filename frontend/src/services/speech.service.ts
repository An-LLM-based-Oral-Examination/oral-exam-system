import apiClient from './api.client'

export const DEFAULT_TTS_VOICE = 'vi-VN-HoaiMyNeural'

interface ActivePlayback {
  audio: HTMLAudioElement
  objectUrl: string
  finish: (error?: Error) => void
}

let activePlayback: ActivePlayback | null = null
let playbackGeneration = 0

export class SpeechPlaybackCancelledError extends Error {
  constructor() {
    super('Speech playback was cancelled.')
    this.name = 'AbortError'
  }
}

export function isSpeechPlaybackCancellation(error: unknown): boolean {
  return error instanceof SpeechPlaybackCancelledError || (error instanceof Error && error.name === 'AbortError')
}

function stopActivePlayback(error?: Error) {
  const playback = activePlayback
  if (!playback) return

  activePlayback = null
  playback.audio.pause()
  playback.audio.removeAttribute('src')
  playback.audio.load()
  playback.finish(error)
}

export function stopSpeechPlayback() {
  playbackGeneration += 1
  stopActivePlayback(new SpeechPlaybackCancelledError())

  if (typeof window !== 'undefined') window.speechSynthesis?.cancel()
}

export async function playNaturalSpeech(
  text: string,
  voiceName = DEFAULT_TTS_VOICE,
  speakingRate = 1,
): Promise<void> {
  const normalizedText = text.trim()
  if (!normalizedText) return

  playbackGeneration += 1
  const requestGeneration = playbackGeneration
  stopActivePlayback(new SpeechPlaybackCancelledError())

  const response = await apiClient.post<Blob>(
    '/speech/tts',
    { text: normalizedText, voiceName, speakingRate },
    { responseType: 'blob', timeout: 30000 },
  )

  if (requestGeneration !== playbackGeneration) throw new SpeechPlaybackCancelledError()
  if (!(response.data instanceof Blob) || response.data.size === 0) {
    throw new Error('TTS service returned an empty audio response.')
  }

  const objectUrl = URL.createObjectURL(response.data)
  const audio = new Audio(objectUrl)
  audio.preload = 'auto'

  await new Promise<void>((resolve, reject) => {
    let settled = false

    const cleanup = () => {
      audio.removeEventListener('ended', handleEnded)
      audio.removeEventListener('error', handleError)
      if (activePlayback?.audio === audio) activePlayback = null
      URL.revokeObjectURL(objectUrl)
    }

    const finish = (error?: Error) => {
      if (settled) return
      settled = true
      cleanup()
      if (error) reject(error)
      else resolve()
    }

    const handleEnded = () => finish()
    const handleError = () => finish(new Error('Unable to play the generated speech audio.'))

    activePlayback = { audio, objectUrl, finish }
    audio.addEventListener('ended', handleEnded)
    audio.addEventListener('error', handleError)
    void audio.play().catch((error: unknown) => {
      finish(error instanceof Error ? error : new Error('Unable to start speech playback.'))
    })
  })
}
