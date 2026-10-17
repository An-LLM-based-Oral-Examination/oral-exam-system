import apiClient from './api.client'

export type NeuralVoiceId = 'vi-VN-HoaiMyNeural' | 'vi-VN-NamMinhNeural'

export interface SpeechPreferences {
  voiceName: NeuralVoiceId
  speakingRate: number
}

export interface SpeakOptions {
  voiceName?: NeuralVoiceId
  speakingRate?: number
  onStart?: () => void
  onEnd?: () => void
  onError?: (err: unknown) => void
}

export interface VoiceProfileDto {
  voiceId: NeuralVoiceId
  displayName: string
  gender: string
  locale: string
  isDefault: boolean
}

export interface SpeechTranscriptionDto {
  text: string
  rawText?: string | null
  provider: string
  usedFallback: boolean
  confidenceScore?: number | null
}

export interface TranscribeAudioOptions {
  vocabularyHint?: string
  clientTranscript?: string
}

export const TTS_VOICE_OPTIONS: ReadonlyArray<{
  voiceId: NeuralVoiceId
  label: string
  shortLabel: string
}> = [
  {
    voiceId: 'vi-VN-HoaiMyNeural',
    label: 'Cô Hoài My (Giọng Nữ)',
    shortLabel: 'Giọng Nữ (Hoài My)',
  },
  {
    voiceId: 'vi-VN-NamMinhNeural',
    label: 'Thầy Nam Minh (Giọng Nam)',
    shortLabel: 'Giọng Nam (Nam Minh)',
  },
]

export const TTS_RATE_OPTIONS: ReadonlyArray<{
  value: number
  label: string
}> = [
  { value: 0.85, label: '0.85x (Chậm rõ)' },
  { value: 1.0, label: '1.0x (Chuẩn)' },
  { value: 1.15, label: '1.15x (Nhanh)' },
]

const STORAGE_KEY = 'oral_exam_tts_prefs_v1'
const DEFAULT_PREFERENCES: SpeechPreferences = {
  voiceName: 'vi-VN-HoaiMyNeural',
  speakingRate: 1.0,
}
const MAX_CACHE_ENTRIES = 60

interface CachedAudioEntry {
  blobUrl: string
  provider: string
  isSingleVoiceFallback: boolean
}

type PitchConfigurableAudio = HTMLAudioElement & {
  preservesPitch?: boolean
  mozPreservesPitch?: boolean
  webkitPreservesPitch?: boolean
}

const audioBlobCache = new Map<string, CachedAudioEntry>()
let activeAudio: HTMLAudioElement | null = null
let activeSessionToken = 0

function isValidVoiceId(value: unknown): value is NeuralVoiceId {
  return value === 'vi-VN-HoaiMyNeural' || value === 'vi-VN-NamMinhNeural'
}

function isValidSpeakingRate(value: unknown): value is number {
  return typeof value === 'number' && Number.isFinite(value) && value >= 0.5 && value <= 2.0
}

function getPreferences(): SpeechPreferences {
  if (typeof window === 'undefined') return { ...DEFAULT_PREFERENCES }
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY)
    if (!raw) return { ...DEFAULT_PREFERENCES }
    const parsed = JSON.parse(raw) as Partial<SpeechPreferences>
    return {
      voiceName: isValidVoiceId(parsed.voiceName)
        ? parsed.voiceName
        : DEFAULT_PREFERENCES.voiceName,
      speakingRate: isValidSpeakingRate(parsed.speakingRate)
        ? parsed.speakingRate
        : DEFAULT_PREFERENCES.speakingRate,
    }
  } catch {
    return { ...DEFAULT_PREFERENCES }
  }
}

function savePreferences(partial: Partial<SpeechPreferences>): SpeechPreferences {
  const current = getPreferences()
  const next: SpeechPreferences = {
    voiceName: isValidVoiceId(partial.voiceName) ? partial.voiceName : current.voiceName,
    speakingRate: isValidSpeakingRate(partial.speakingRate)
      ? partial.speakingRate
      : current.speakingRate,
  }
  if (typeof window !== 'undefined') {
    try {
      window.localStorage.setItem(STORAGE_KEY, JSON.stringify(next))
    } catch {
      // Bỏ qua lỗi ghi localStorage khi trình duyệt ở chế độ riêng tư hạn chế bộ nhớ.
    }
  }
  return next
}

function cacheBlobEntry(key: string, entry: CachedAudioEntry): void {
  if (audioBlobCache.has(key)) {
    const existing = audioBlobCache.get(key)
    if (existing && existing.blobUrl !== entry.blobUrl) {
      URL.revokeObjectURL(existing.blobUrl)
    }
    audioBlobCache.delete(key)
  } else if (audioBlobCache.size >= MAX_CACHE_ENTRIES) {
    const oldestKey = audioBlobCache.keys().next().value
    if (oldestKey) {
      const oldestEntry = audioBlobCache.get(oldestKey)
      if (oldestEntry) URL.revokeObjectURL(oldestEntry.blobUrl)
      audioBlobCache.delete(oldestKey)
    }
  }
  audioBlobCache.set(key, entry)
}

function stopSpeaking(): void {
  activeSessionToken += 1

  if (activeAudio) {
    try {
      activeAudio.onplay = null
      activeAudio.onended = null
      activeAudio.onerror = null
      activeAudio.pause()
      activeAudio.currentTime = 0
    } catch {
      // Bỏ qua lỗi khi dừng HTMLAudioElement.
    }
    activeAudio = null
  }

  if (typeof window !== 'undefined' && 'speechSynthesis' in window) {
    try {
      window.speechSynthesis.cancel()
    } catch {
      // Bỏ qua lỗi khi hủy speechSynthesis.
    }
  }
}

function speakWithBrowserFallback(
  text: string,
  voiceName: NeuralVoiceId,
  speakingRate: number,
  sessionToken: number,
  options?: SpeakOptions,
): 'browser-fallback' {
  if (
    typeof window === 'undefined' ||
    !('speechSynthesis' in window) ||
    !('SpeechSynthesisUtterance' in window)
  ) {
    options?.onError?.(new Error('Trình duyệt không hỗ trợ phát giọng nói.'))
    return 'browser-fallback'
  }

  const startUtterance = () => {
    if (sessionToken !== activeSessionToken) return
    const utterance = new SpeechSynthesisUtterance(text)
    utterance.lang = 'vi-VN'
    utterance.rate = Math.max(0.95, Math.min(1.65, Number((speakingRate * 1.22).toFixed(2))))
    const voices = window.speechSynthesis.getVoices()
    const vietnameseVoices = voices.filter((v) => v.lang.toLowerCase().startsWith('vi'))

    if (voiceName === 'vi-VN-NamMinhNeural') {
      const maleKeywords = ['microsoft an', ' an ', 'namminh', 'nam', 'male', 'minh']
      const preferredMaleVoice =
        vietnameseVoices.find((v) =>
          maleKeywords.some((keyword) => v.name.toLowerCase().includes(keyword)),
        ) ?? vietnameseVoices[0]
      if (preferredMaleVoice) utterance.voice = preferredMaleVoice
      utterance.pitch = 1.0
    } else {
      const femaleKeywords = ['hoai', 'my', 'linh', 'female', 'nu']
      const preferredFemaleVoice =
        vietnameseVoices.find((v) =>
          femaleKeywords.some((keyword) => v.name.toLowerCase().includes(keyword)),
        ) ?? vietnameseVoices[0]
      if (preferredFemaleVoice) utterance.voice = preferredFemaleVoice
      utterance.pitch = 1.04
    }

    utterance.onstart = () => {
      if (sessionToken === activeSessionToken) options?.onStart?.()
    }
    utterance.onend = () => {
      if (sessionToken === activeSessionToken) options?.onEnd?.()
    }
    utterance.onerror = (event) => {
      if (sessionToken === activeSessionToken) options?.onError?.(event)
    }

    window.speechSynthesis.cancel()
    window.speechSynthesis.speak(utterance)
  }

  if (window.speechSynthesis.getVoices().length > 0) {
    startUtterance()
  } else {
    let hasStarted = false
    const runOnce = () => {
      if (hasStarted) return
      hasStarted = true
      startUtterance()
    }
    const handleVoicesChanged = () => {
      window.speechSynthesis.removeEventListener('voiceschanged', handleVoicesChanged)
      runOnce()
    }
    window.speechSynthesis.addEventListener('voiceschanged', handleVoicesChanged)
    runOnce()
  }

  return 'browser-fallback'
}

async function speakText(
  text: string,
  options?: SpeakOptions,
): Promise<'backend-neural' | 'browser-fallback'> {
  const normalizedText = text.trim()
  if (!normalizedText) {
    stopSpeaking()
    return 'browser-fallback'
  }

  stopSpeaking()
  const sessionToken = activeSessionToken
  const prefs = getPreferences()
  const voiceName = options?.voiceName ?? prefs.voiceName
  const speakingRate = options?.speakingRate ?? prefs.speakingRate
  const cacheKey = `${voiceName}|${speakingRate}|${normalizedText}`

  let entry = audioBlobCache.get(cacheKey)

  try {
    if (!entry) {
      const response = await apiClient.post<Blob>(
        '/speech/tts',
        {
          text: normalizedText,
          voiceName,
          speakingRate,
        },
        {
          responseType: 'blob',
          _skipAuthRefresh: true,
        },
      )

      if (sessionToken !== activeSessionToken) {
        return 'backend-neural'
      }

      const blob = response.data
      if (!(blob instanceof Blob) || blob.size === 0) {
        throw new Error('Dữ liệu âm thanh từ máy chủ trống.')
      }

      const providerHeader = String(response.headers?.['x-tts-provider'] ?? '')
      const isNativeMaleWav =
        providerHeader === 'WindowsOneCoreMaleTts' || blob.type.toLowerCase().includes('wav')
      const isSingleVoiceFallback =
        !isNativeMaleWav &&
        (response.headers?.['x-tts-fallback'] === 'true' ||
          providerHeader === 'GoogleNeuralTranslateTts' ||
          (providerHeader !== 'EdgeNeuralTts' && providerHeader !== 'GeminiNativeAudioTts'))

      entry = {
        blobUrl: URL.createObjectURL(blob),
        provider: isNativeMaleWav ? 'WindowsOneCoreMaleTts' : providerHeader,
        isSingleVoiceFallback,
      }
      cacheBlobEntry(cacheKey, entry)
    }

    if (sessionToken !== activeSessionToken) {
      return 'backend-neural'
    }

    if (voiceName === 'vi-VN-NamMinhNeural' && entry.isSingleVoiceFallback) {
      const browserVoices =
        typeof window !== 'undefined' && 'speechSynthesis' in window
          ? window.speechSynthesis.getVoices()
          : []
      const hasBrowserMaleVi = browserVoices.some(
        (v) =>
          v.lang.toLowerCase().startsWith('vi') &&
          (v.name.toLowerCase().includes('an') ||
            v.name.toLowerCase().includes('nam') ||
            v.name.toLowerCase().includes('male')),
      )
      if (hasBrowserMaleVi) {
        return speakWithBrowserFallback(normalizedText, voiceName, speakingRate, sessionToken, options)
      }
    }

    const audio = new Audio(entry.blobUrl)
    const pitchAudio = audio as PitchConfigurableAudio
    pitchAudio.preservesPitch = true
    pitchAudio.mozPreservesPitch = true
    pitchAudio.webkitPreservesPitch = true

    if (entry.provider === 'WindowsOneCoreMaleTts') {
      audio.playbackRate = Math.max(0.95, Math.min(1.45, Number((speakingRate * 1.08).toFixed(2))))
    } else {
      audio.playbackRate = Math.max(0.85, Math.min(1.65, Number((speakingRate * 1.15).toFixed(2))))
    }
    activeAudio = audio

    let startedNotified = false
    const notifyStarted = () => {
      if (!startedNotified && sessionToken === activeSessionToken) {
        startedNotified = true
        options?.onStart?.()
      }
    }

    audio.onplay = () => {
      notifyStarted()
    }
    audio.onended = () => {
      if (activeAudio === audio) {
        activeAudio = null
      }
      if (sessionToken === activeSessionToken) {
        options?.onEnd?.()
      }
    }
    audio.onerror = (event) => {
      if (activeAudio === audio) {
        activeAudio = null
      }
      if (sessionToken === activeSessionToken) {
        options?.onError?.(event)
      }
    }

    await audio.play()
    notifyStarted()
    return 'backend-neural'
  } catch {
    if (activeAudio) {
      activeAudio = null
    }
    if (sessionToken !== activeSessionToken) {
      return 'backend-neural'
    }
    return speakWithBrowserFallback(normalizedText, voiceName, speakingRate, sessionToken, options)
  }
}

async function transcribeAudio(
  audioBlob: Blob,
  options?: TranscribeAudioOptions,
): Promise<SpeechTranscriptionDto> {
  const formData = new FormData()
  const ext = audioBlob.type.includes('ogg')
    ? 'ogg'
    : audioBlob.type.includes('mp4')
      ? 'm4a'
      : audioBlob.type.includes('wav')
        ? 'wav'
        : 'webm'
  formData.append('file', audioBlob, `speech-check-${Date.now()}.${ext}`)
  if (options?.vocabularyHint?.trim()) {
    formData.append('vocabularyHint', options.vocabularyHint.trim())
  }
  if (options?.clientTranscript?.trim()) {
    formData.append('clientTranscript', options.clientTranscript.trim())
  }

  const response = await apiClient.post<unknown>('/speech/stt', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
    timeout: 45_000,
    _skipAuthRefresh: true,
  })

  const raw =
    typeof response.data === 'object' && response.data !== null && 'value' in response.data
      ? (response.data as { value: Record<string, unknown> }).value
      : (response.data as Record<string, unknown>)

  const text = String(raw?.text ?? raw?.Text ?? options?.clientTranscript ?? '').trim()
  const rawText =
    typeof (raw?.rawText ?? raw?.RawText) === 'string'
      ? String(raw?.rawText ?? raw?.RawText)
      : null
  const provider = String(raw?.provider ?? raw?.Provider ?? 'BackendStt')
  const usedFallback = Boolean(raw?.usedFallback ?? raw?.UsedFallback ?? false)
  const confidenceScore =
    typeof (raw?.confidenceScore ?? raw?.ConfidenceScore) === 'number'
      ? Number(raw?.confidenceScore ?? raw?.ConfidenceScore)
      : null

  return {
    text,
    rawText,
    provider,
    usedFallback,
    confidenceScore,
  }
}

async function getVoices(): Promise<VoiceProfileDto[]> {
  try {
    const response = await apiClient.get<unknown>('/speech/voices', {
      _skipAuthRefresh: true,
    })
    const payload =
      typeof response.data === 'object' && response.data !== null && 'value' in response.data
        ? (response.data as { value: unknown }).value
        : response.data
    if (Array.isArray(payload) && payload.length > 0) {
      return payload as VoiceProfileDto[]
    }
  } catch {
    // Dự phòng danh sách giọng đọc mặc định khi ngoại tuyến.
  }
  return [
    {
      voiceId: 'vi-VN-HoaiMyNeural',
      displayName: 'Cô Hoài My (Giọng Nữ - Mặc định)',
      gender: 'Female',
      locale: 'vi-VN',
      isDefault: true,
    },
    {
      voiceId: 'vi-VN-NamMinhNeural',
      displayName: 'Thầy Nam Minh (Giọng Nam)',
      gender: 'Male',
      locale: 'vi-VN',
      isDefault: false,
    },
  ]
}

export function getVoiceLabel(voiceId: NeuralVoiceId): string {
  return (
    TTS_VOICE_OPTIONS.find((option) => option.voiceId === voiceId)?.label ??
    'Cô Hoài My (Giọng Nữ)'
  )
}

export function getVoiceShortLabel(voiceId: NeuralVoiceId): string {
  return (
    TTS_VOICE_OPTIONS.find((option) => option.voiceId === voiceId)?.shortLabel ??
    'Giọng Nữ (Hoài My)'
  )
}

export const speechService = {
  getPreferences,
  savePreferences,
  speakText,
  stopSpeaking,
  transcribeAudio,
  getVoices,
  getVoiceLabel,
  getVoiceShortLabel,
  TTS_VOICE_OPTIONS,
  TTS_RATE_OPTIONS,
}
