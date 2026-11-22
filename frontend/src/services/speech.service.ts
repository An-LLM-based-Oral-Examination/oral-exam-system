import apiClient from './api.client'

export const DEFAULT_TTS_VOICE = 'vi-VN-HoaiMyNeural'
export const DEFAULT_ENGLISH_TTS_VOICE = 'en-US-JennyNeural'
const MAX_AUDIO_SIZE_BYTES = 25 * 1024 * 1024

const VIETNAMESE_CHAR_REGEX =
  /[ăâđêôơưáàảãạấầẩẫậắằẳẵặéèẻẽẹếềểễệíìỉĩịóòỏõọốồổỗộớờởỡợúùủũụứừửữựýỳỷỹỵ]/i

export function containsVietnameseCharacters(text?: string | null): boolean {
  return Boolean(text && VIETNAMESE_CHAR_REGEX.test(text))
}

export function isEnglishCourse(courseCode?: string | null, courseName?: string | null): boolean {
  const code = courseCode?.trim().toUpperCase() ?? ''
  const name = courseName?.trim().toLowerCase() ?? ''
  return /^ENT\d{3}$/.test(code) || /\benglish\b/.test(name)
}

const TTS_REQUIREMENT_MARKER_REGEX =
  /(?<![\p{L}\p{N}_])\(\s*Y\d+(?:\s*[,;/&–\u2014-]\s*Y\d+)*\s*\)\s*:?/giu

export function sanitizeTextForTts(text?: string | null): string {
  if (!text) return ''
  return text
    .replace(TTS_REQUIREMENT_MARKER_REGEX, ' ')
    .replace(/\s+/g, ' ')
    .trim()
}

export function getTtsVoiceForCourse(
  courseCode?: string | null,
  courseName?: string | null,
  text?: string | null,
): string {
  const normalizedText = sanitizeTextForTts(text)
  if (normalizedText) {
    if (containsVietnameseCharacters(normalizedText)) {
      return DEFAULT_TTS_VOICE
    }
    if (/[a-zA-Z]{2,}/.test(normalizedText)) {
      return DEFAULT_ENGLISH_TTS_VOICE
    }
  }
  return isEnglishCourse(courseCode, courseName) ? DEFAULT_ENGLISH_TTS_VOICE : DEFAULT_TTS_VOICE
}

export function getSpeechRecognitionLangForCourse(
  courseCode?: string | null,
  courseName?: string | null,
  promptText?: string | null,
): string {
  // Hỗ trợ nhận diện song ngữ Việt - Anh (Code-Switching) cho tất cả các môn học (kể cả ENT và SE)
  void courseCode
  void courseName
  void promptText
  return 'vi-VN'
}

export interface SpeechTranscriptionResponse {
  transcript: string
  rawText?: string | null
  provider?: string | null
  usedFallback?: boolean
  confidenceScore?: number | null
  timestampsJson?: string | null
}

interface ActivePlayback {
  audio: HTMLAudioElement
  objectUrl: string
  finish: (error?: Error) => void
}

let activePlayback: ActivePlayback | null = null
let activeAbortController: AbortController | null = null
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

function isRecord(value: unknown): value is Record<string, unknown> {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) return false
  if (typeof Blob !== 'undefined' && value instanceof Blob) return false
  return true
}

async function readBlobTextSafe(blob: Blob): Promise<string> {
  if (typeof blob.text === 'function') {
    return blob.text()
  }
  if (typeof FileReader !== 'undefined') {
    return await new Promise<string>((resolve, reject) => {
      const reader = new FileReader()
      reader.onload = () => resolve(typeof reader.result === 'string' ? reader.result : '')
      reader.onerror = () => reject(reader.error ?? new Error('Không đọc được dữ liệu Blob.'))
      reader.readAsText(blob)
    })
  }
  return ''
}

async function parseTtsProblemBlob(blob: Blob, fallbackStatus = 500) {
  const mime = (blob.type || '').toLowerCase()
  const looksReadable = !mime || mime.includes('json') || mime.startsWith('text/')
  if (!looksReadable || blob.size === 0 || blob.size > 64 * 1024) return null
  try {
    const raw = (await readBlobTextSafe(blob)).trim()
    if (!raw) return null
    const parsed: unknown = JSON.parse(raw)
    if (!isRecord(parsed)) return null
    const detail = readString(parsed, 'detail', 'Detail', 'error', 'Error', 'message', 'Message')
    const title = readString(parsed, 'title', 'Title')
    const errorCode = readString(parsed, 'errorCode', 'ErrorCode', 'code', 'Code')
    const status = readNumber(parsed, 'status', 'Status') ?? fallbackStatus
    if (!detail && !title && !errorCode) return null
    return {
      ...parsed,
      status,
      title: title ?? 'Không thể tổng hợp giọng đọc AI',
      detail: detail ?? title ?? 'Dịch vụ đọc giọng nói trả về lỗi.',
      ...(errorCode ? { errorCode } : {}),
    }
  } catch {
    return null
  }
}

function readString(record: Record<string, unknown>, ...keys: string[]) {
  for (const key of keys) {
    const value = record[key]
    if (typeof value === 'string' && value.trim()) return value.trim()
  }
  return undefined
}

function readBoolean(record: Record<string, unknown>, ...keys: string[]) {
  for (const key of keys) {
    const value = record[key]
    if (typeof value === 'boolean') return value
  }
  return undefined
}

function readNumber(record: Record<string, unknown>, ...keys: string[]) {
  for (const key of keys) {
    const value = record[key]
    if (typeof value === 'number' && Number.isFinite(value)) return value
    if (typeof value === 'string' && value.trim() && Number.isFinite(Number(value))) return Number(value)
  }
  return undefined
}

function audioExtension(mimeType: string) {
  if (mimeType.includes('ogg')) return 'ogg'
  if (mimeType.includes('mp4')) return 'm4a'
  if (mimeType.includes('wav')) return 'wav'
  return 'webm'
}

function getSpeechErrorMessage(error: unknown, fallback: string) {
  if (!isRecord(error)) return fallback
  const message = error.error ?? error.detail ?? error.message ?? error.Message
  return typeof message === 'string' && message.trim() ? message.trim() : fallback
}

function stopActivePlayback(error?: Error) {
  if (activeAbortController) {
    const controller = activeAbortController
    activeAbortController = null
    controller.abort()
  }

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
  const normalizedText = sanitizeTextForTts(text)
  if (!normalizedText) return

  playbackGeneration += 1
  const requestGeneration = playbackGeneration
  stopActivePlayback(new SpeechPlaybackCancelledError())

  const abortController = new AbortController()
  activeAbortController = abortController

  let responseData: Blob
  try {
    const response = await apiClient.post<Blob>(
      '/speech/tts',
      { text: normalizedText, voiceName, speakingRate },
      { responseType: 'blob', timeout: 30000, signal: abortController.signal },
    )
    responseData = response.data
  } catch (error: unknown) {
    if (
      requestGeneration !== playbackGeneration ||
      abortController.signal.aborted ||
      isSpeechPlaybackCancellation(error) ||
      (isRecord(error) && (error.code === 'ERR_CANCELED' || error.name === 'CanceledError'))
    ) {
      throw new SpeechPlaybackCancelledError()
    }
    if (
      isRecord(error) &&
      isRecord(error.response) &&
      typeof Blob !== 'undefined' &&
      error.response.data instanceof Blob
    ) {
      const responseStatus = typeof error.response.status === 'number' ? error.response.status : 500
      const parsedProblem = await parseTtsProblemBlob(error.response.data, responseStatus)
      if (parsedProblem) {
        throw { ...parsedProblem, status: responseStatus }
      }
    }
    throw error
  } finally {
    if (activeAbortController === abortController) {
      activeAbortController = null
    }
  }

  if (requestGeneration !== playbackGeneration) throw new SpeechPlaybackCancelledError()
  if (!(responseData instanceof Blob) || responseData.size === 0) {
    throw new Error('TTS service returned an empty audio response.')
  }
  const responseMime = (responseData.type || '').toLowerCase()
  if (responseMime.includes('json')) {
    const parsedProblem = await parseTtsProblemBlob(responseData, 500)
    throw (
      parsedProblem ?? {
        status: 500,
        title: 'Không thể tổng hợp giọng đọc AI',
        detail: 'Dịch vụ đọc giọng nói trả về lỗi.',
      }
    )
  }
  if (
    (!responseMime || responseMime === 'text/plain' || responseMime.startsWith('text/plain;')) &&
    responseData.size <= 64 * 1024
  ) {
    const parsedProblem = await parseTtsProblemBlob(responseData, 500)
    if (parsedProblem) {
      throw parsedProblem
    }
  }
  if (requestGeneration !== playbackGeneration) throw new SpeechPlaybackCancelledError()

  const objectUrl = URL.createObjectURL(responseData)
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

/**
 * Bóc băng qua API mới `POST /api/v1/speech/stt`
 * (Whisper → Gemini fallback + chuẩn hóa thuật ngữ SE).
 */
export async function transcribeSpeech(
  audio: Blob,
  options?: {
    vocabularyHint?: string | null
    clientTranscript?: string | null
    fileName?: string
  },
): Promise<SpeechTranscriptionResponse> {
  if (audio.size === 0) {
    throw {
      status: 400,
      title: 'Bản ghi âm không hợp lệ',
      detail: 'Bản ghi âm đang trống.',
    }
  }
  if (audio.size > MAX_AUDIO_SIZE_BYTES) {
    throw {
      status: 400,
      title: 'Bản ghi âm quá lớn',
      detail: 'Dung lượng bản ghi âm vượt quá giới hạn 25MB.',
    }
  }

  const formData = new FormData()
  const extension = audioExtension(audio.type || 'audio/webm')
  formData.append(
    'file',
    audio,
    options?.fileName?.trim() || `speech-${Date.now()}.${extension}`,
  )
  if (options?.vocabularyHint?.trim()) {
    formData.append('vocabularyHint', options.vocabularyHint.trim())
  }
  if (options?.clientTranscript?.trim()) {
    formData.append('clientTranscript', options.clientTranscript.trim())
  }

  try {
    const response = await apiClient.post<unknown>('/speech/stt', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
      timeout: 120_000,
    })

    const payload = isRecord(response.data) ? response.data : null
    if (!payload) {
      throw {
        status: 502,
        title: 'Phản hồi API không hợp lệ',
        detail: 'Dịch vụ bóc băng không trả về JSON hợp lệ.',
      }
    }

    const transcript =
      readString(payload, 'transcript', 'Transcript', 'text', 'Text') ?? ''
    if (!transcript) {
      throw {
        status: 422,
        title: 'Không nhận diện được giọng nói',
        detail: 'Không tìm thấy nội dung giọng nói trong bản ghi. Vui lòng ghi âm lại.',
      }
    }

    const rawTimestamps =
      payload.timestampsJson ?? payload.TimestampsJson ?? payload.timestamps_json
    const timestampsJson =
      typeof rawTimestamps === 'string' && rawTimestamps.trim()
        ? rawTimestamps.trim()
        : Array.isArray(rawTimestamps) || isRecord(rawTimestamps)
          ? JSON.stringify(rawTimestamps)
          : null

    return {
      transcript,
      rawText: readString(payload, 'rawText', 'RawText') ?? null,
      provider: readString(payload, 'provider', 'Provider') ?? null,
      usedFallback: readBoolean(payload, 'usedFallback', 'UsedFallback'),
      confidenceScore: readNumber(payload, 'confidenceScore', 'ConfidenceScore') ?? null,
      timestampsJson,
    }
  } catch (error) {
    const candidate = error as Partial<{ status: number; title: string; detail: string }>
    throw {
      status: candidate.status ?? 500,
      title: candidate.title ?? 'Không thể bóc băng âm thanh',
      detail: getSpeechErrorMessage(error, 'Không thể bóc băng bản ghi âm.'),
    }
  }
}
