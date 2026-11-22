import { afterEach, describe, expect, it, vi } from 'vitest'
import { AxiosError, AxiosHeaders } from 'axios'
import apiClient from './api.client'
import {
  DEFAULT_ENGLISH_TTS_VOICE,
  DEFAULT_TTS_VOICE,
  getTtsVoiceForCourse,
  isSpeechPlaybackCancellation,
  playNaturalSpeech,
  sanitizeTextForTts,
  stopSpeechPlayback,
} from './speech.service'

function stubAudioPlayback() {
  const createdUrls: string[] = []
  const revokedUrls: string[] = []
  vi.spyOn(URL, 'createObjectURL').mockImplementation(() => {
    const url = `blob:mock-${createdUrls.length + 1}`
    createdUrls.push(url)
    return url
  })
  vi.spyOn(URL, 'revokeObjectURL').mockImplementation((url: string) => {
    revokedUrls.push(url)
  })

  class MockAudio {
    preload = ''
    src = ''
    private listeners = new Map<string, Set<() => void>>()
    constructor(src: string) {
      this.src = src
    }
    addEventListener(type: string, cb: () => void) {
      const set = this.listeners.get(type) ?? new Set()
      set.add(cb)
      this.listeners.set(type, set)
    }
    removeEventListener(type: string, cb: () => void) {
      this.listeners.get(type)?.delete(cb)
    }
    pause() {}
    removeAttribute() {
      this.src = ''
    }
    load() {}
    play() {
      queueMicrotask(() => {
        this.listeners.get('ended')?.forEach((cb) => cb())
      })
      return Promise.resolve()
    }
  }

  vi.stubGlobal('Audio', MockAudio)
  return { createdUrls, revokedUrls }
}

afterEach(() => {
  stopSpeechPlayback()
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
})

describe('sanitizeTextForTts', () => {
  it('strips leading, inline, grouped, and colon-suffixed (Y1)/(Y2)/(Y3) markers cleanly', () => {
    expect(
      sanitizeTextForTts('(Y1) Nêu hai mục đích khác nhau của kiểm thử phần mềm. (Y2) Giải thích một mục đích.'),
    ).toBe('Nêu hai mục đích khác nhau của kiểm thử phần mềm. Giải thích một mục đích.')

    expect(
      sanitizeTextForTts('Mất 20 giây. (Y1) Ngoài ra? (Y2) Cần điều kiện gì? (Y3) Dựa vào đâu?'),
    ).toBe('Mất 20 giây. Ngoài ra? Cần điều kiện gì? Dựa vào đâu?')

    expect(sanitizeTextForTts('(Y1, Y2): Phân tích sự khác biệt và (Y3): Kết luận.')).toBe(
      'Phân tích sự khác biệt và Kết luận.',
    )

    expect(sanitizeTextForTts('(Y1 — Y2): Đánh giá hiệu năng và bảo mật.')).toBe(
      'Đánh giá hiệu năng và bảo mật.',
    )
  })

  it('preserves technical parentheses, numbered lists, .NET, C#, and identifier calls', () => {
    expect(
      sanitizeTextForTts('Độ phức tạp (O(1)), bước (1), nhóm (A), biến (Y), chuẩn (YAML), func(Y1) trong .NET và C#.'),
    ).toBe('Độ phức tạp (O(1)), bước (1), nhóm (A), biến (Y), chuẩn (YAML), func(Y1) trong .NET và C#.')
  })

  it('returns empty string when input contains only requirement markers', () => {
    expect(sanitizeTextForTts('(Y1)')).toBe('')
    expect(sanitizeTextForTts('  (Y1, Y2):  ')).toBe('')
    expect(sanitizeTextForTts(null)).toBe('')
  })
})

describe('getTtsVoiceForCourse', () => {
  it('selects Vietnamese or English voice after stripping (Y1) markers', () => {
    expect(getTtsVoiceForCourse('SWT301', 'Software Testing', '(Y1) Giải thích Black-box testing.')).toBe(
      DEFAULT_TTS_VOICE,
    )
    expect(getTtsVoiceForCourse('SWT301', 'Software Testing', '(Y1) Explain boundary value analysis.')).toBe(
      DEFAULT_ENGLISH_TTS_VOICE,
    )
    expect(getTtsVoiceForCourse('ENT101', 'Academic English', '(Y1)')).toBe(
      DEFAULT_ENGLISH_TTS_VOICE,
    )
  })
})

describe('playNaturalSpeech', () => {
  it('early-returns without calling /speech/tts when sanitized text is empty', async () => {
    const postSpy = vi.spyOn(apiClient, 'post')
    await expect(playNaturalSpeech('(Y1): ')).resolves.toBeUndefined()
    expect(postSpy).not.toHaveBeenCalled()
  })

  it('sends sanitized text (without (Y1)/(Y2)) with blob responseType and AbortSignal', async () => {
    const { createdUrls, revokedUrls } = stubAudioPlayback()
    const audioBlob = new Blob([new Uint8Array([1, 2, 3])], { type: 'audio/mpeg' })
    const postSpy = vi.spyOn(apiClient, 'post').mockResolvedValue({ data: audioBlob })

    await playNaturalSpeech(
      '(Y1) Nêu định nghĩa Regression Testing. (Y2) Cho ví dụ.',
      DEFAULT_TTS_VOICE,
      0.95,
    )

    expect(postSpy).toHaveBeenCalledTimes(1)
    expect(postSpy).toHaveBeenCalledWith(
      '/speech/tts',
      {
        text: 'Nêu định nghĩa Regression Testing. Cho ví dụ.',
        voiceName: DEFAULT_TTS_VOICE,
        speakingRate: 0.95,
      },
      expect.objectContaining({
        responseType: 'blob',
        timeout: 30000,
        signal: expect.any(AbortSignal),
      }),
    )
    expect(createdUrls).toHaveLength(1)
    expect(revokedUrls).toEqual(createdUrls)
  })

  it('surfaces real RFC 7807 detail from normalized ProblemDetails or raw AxiosError Blob', async () => {
    vi.spyOn(apiClient, 'post').mockRejectedValueOnce({
      status: 400,
      title: 'Dữ liệu không hợp lệ',
      detail: 'Văn bản đọc không hợp lệ từ interceptor.',
    })
    await expect(playNaturalSpeech('Xin chào')).rejects.toMatchObject({
      status: 400,
      detail: 'Văn bản đọc không hợp lệ từ interceptor.',
    })

    const rawBlob = new Blob(
      [JSON.stringify({ title: 'Lỗi TTS', detail: 'Dịch vụ TTS tạm quá tải.', errorCode: 'TTS_BUSY' })],
      { type: 'application/problem+json' },
    )
    const axiosErr = new AxiosError('Request failed with status code 503', 'ERR_BAD_RESPONSE', undefined, null, {
      data: rawBlob,
      status: 503,
      statusText: 'Service Unavailable',
      headers: new AxiosHeaders(),
      config: { headers: new AxiosHeaders() },
    })
    vi.spyOn(apiClient, 'post').mockRejectedValueOnce(axiosErr)
    await expect(playNaturalSpeech('Xin chào')).rejects.toMatchObject({
      status: 503,
      detail: 'Dịch vụ TTS tạm quá tải.',
      errorCode: 'TTS_BUSY',
    })
  })

  it('rejects with structured ProblemDetails when 200 OK returns an application/problem+json Blob', async () => {
    const jsonBlob = new Blob(
      [JSON.stringify({ status: 422, title: 'TTS Error', detail: 'Không thể tổng hợp âm thanh.' })],
      { type: 'application/problem+json' },
    )
    vi.spyOn(apiClient, 'post').mockResolvedValueOnce({ data: jsonBlob })

    await expect(playNaturalSpeech('Kiểm thử phần mềm')).rejects.toMatchObject({
      status: 422,
      title: 'TTS Error',
      detail: 'Không thể tổng hợp âm thanh.',
    })
  })

  it('rejects with structured ProblemDetails when 200 OK returns a small untyped or text/plain JSON Blob while leaving audio/* Blobs untouched', async () => {
    const untypedBlob = new Blob(
      [JSON.stringify({ status: 429, title: 'Rate Limited', detail: 'Vượt quá giới hạn đọc.' })],
      { type: '' },
    )
    vi.spyOn(apiClient, 'post').mockResolvedValueOnce({ data: untypedBlob })

    await expect(playNaturalSpeech('Kiểm thử phần mềm')).rejects.toMatchObject({
      status: 429,
      title: 'Rate Limited',
      detail: 'Vượt quá giới hạn đọc.',
    })

    const textPlainBlob = new Blob(
      [JSON.stringify({ status: 503, title: 'Service Unavailable', detail: 'TTS đang bận.' })],
      { type: 'text/plain' },
    )
    vi.spyOn(apiClient, 'post').mockResolvedValueOnce({ data: textPlainBlob })

    await expect(playNaturalSpeech('Kiểm thử phần mềm')).rejects.toMatchObject({
      status: 503,
      title: 'Service Unavailable',
      detail: 'TTS đang bận.',
    })

    stubAudioPlayback()
    const audioBlob = new Blob([new Uint8Array([1, 2, 3, 4])], { type: 'audio/mpeg' })
    const textSpy = vi.spyOn(audioBlob, 'text')
    vi.spyOn(apiClient, 'post').mockResolvedValueOnce({ data: audioBlob })

    await playNaturalSpeech('Kiểm thử phần mềm')
    expect(textSpy).not.toHaveBeenCalled()
  })

  it('rejects with SpeechPlaybackCancelledError when stopSpeechPlayback aborts an in-flight request', async () => {
    vi.spyOn(apiClient, 'post').mockImplementation(
      (_url, _body, config) =>
        new Promise((_resolve, reject) => {
          const signal = config?.signal as AbortSignal | undefined
          signal?.addEventListener('abort', () => {
            const canceled = new Error('canceled')
            canceled.name = 'CanceledError'
            reject(canceled)
          })
        }),
    )

    const playPromise = playNaturalSpeech('Đọc câu hỏi kiểm thử')
    stopSpeechPlayback()

    const caught = await playPromise.catch((err: unknown) => err)
    expect(isSpeechPlaybackCancellation(caught)).toBe(true)
  })
})
