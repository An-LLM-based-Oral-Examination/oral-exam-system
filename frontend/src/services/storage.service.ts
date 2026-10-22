import apiClient from './api.client'
import type { UploadAudioResponse } from '@/types/practice.types'

const MAX_AUDIO_SIZE_BYTES = 25 * 1024 * 1024

function getAudioExtension(mimeType: string) {
  if (mimeType.includes('ogg')) return 'ogg'
  if (mimeType.includes('mp4')) return 'm4a'
  return 'webm'
}

function getErrorMessage(error: unknown) {
  if (typeof error !== 'object' || error === null) return 'Không thể bóc băng bản ghi âm.'
  const candidate = error as Record<string, unknown>
  const message = candidate.error ?? candidate.detail ?? candidate.message
  return typeof message === 'string' && message.trim() ? message : 'Không thể bóc băng bản ghi âm.'
}

export const storageService = {
  async uploadPracticeAudio(
    audio: Blob,
    options: { clientTranscript?: string; vocabularyHint?: string } = {},
  ): Promise<UploadAudioResponse> {
    if (audio.size === 0) {
      throw { status: 400, title: 'Bản ghi âm không hợp lệ', detail: 'Bản ghi âm đang trống.' }
    }
    if (audio.size > MAX_AUDIO_SIZE_BYTES) {
      throw {
        status: 400,
        title: 'Bản ghi âm quá lớn',
        detail: 'Dung lượng bản ghi âm vượt quá giới hạn 25MB.',
      }
    }

    const formData = new FormData()
    const extension = getAudioExtension(audio.type)
    formData.append('file', audio, `practice-answer-${Date.now()}.${extension}`)
    const clientTranscript = options.clientTranscript?.trim()
    const vocabularyHint = options.vocabularyHint?.trim()
    if (clientTranscript) formData.append('clientTranscript', clientTranscript)
    if (vocabularyHint) formData.append('vocabularyHint', vocabularyHint)

    try {
      const response = await apiClient.post<unknown>('/storage/upload-audio', formData, {
        headers: { 'Content-Type': 'multipart/form-data' },
        timeout: 120_000,
      })
      if (
        typeof response.data !== 'object' ||
        response.data === null ||
        typeof (
          (response.data as Record<string, unknown>).transcript ??
          (response.data as Record<string, unknown>).Transcript
        ) !== 'string'
      ) {
        throw {
          status: 502,
          title: 'Phản hồi API không hợp lệ',
          detail: 'Dịch vụ bóc băng không trả về transcript hợp lệ.',
        }
      }

      const rawTranscript =
        (response.data as Record<string, string>).transcript ??
        (response.data as Record<string, string>).Transcript
      const transcript = rawTranscript.trim()
      if (!transcript) {
        throw {
          status: 422,
          title: 'Không nhận diện được giọng nói',
          detail: 'Không tìm thấy nội dung giọng nói trong bản ghi. Vui lòng ghi âm lại.',
        }
      }
      return { transcript }
    } catch (error) {
      const candidate = error as Partial<{ status: number; title: string; detail: string }>
      throw {
        status:
          candidate.status ??
          (typeof error === 'object' && error !== null && 'error' in error ? 400 : 500),
        title: candidate.title ?? 'Không thể bóc băng âm thanh',
        detail: getErrorMessage(error),
      }
    }
  },
}
