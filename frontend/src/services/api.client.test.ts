import { afterEach, describe, expect, it, vi } from 'vitest'
import { AxiosError, AxiosHeaders, type AxiosAdapter } from 'axios'
import apiClient from './api.client'

afterEach(() => { sessionStorage.clear(); vi.restoreAllMocks() })

function rejectingAdapter(status: number, data: unknown): AxiosAdapter {
  return async (config) => {
    throw new AxiosError('request failed', 'ERR_BAD_REQUEST', config, null, {
      data, status, statusText: 'Conflict', config, headers: new AxiosHeaders(),
    })
  }
}

describe('ProblemDetails consumer', () => {
  it('uses actual HTTP status and retains errorCode/traceId/extensions', async () => {
    vi.spyOn(console, 'warn').mockImplementation(() => {})
    await expect(apiClient.get('/contract', {
      adapter: rejectingAdapter(409, { status: 400, title: 'Conflict', detail: 'CLO đang được dùng', errorCode: 'CLO_IN_USE', traceId: 'trace-1', references: 2 }),
    })).rejects.toMatchObject({ status: 409, detail: 'CLO đang được dùng', errorCode: 'CLO_IN_USE', traceId: 'trace-1', references: 2 })
  })

  it('continues reading legacy PascalCase errors during rollout', async () => {
    vi.spyOn(console, 'warn').mockImplementation(() => {})
    await expect(apiClient.get('/contract', {
      adapter: rejectingAdapter(403, { Title: 'Không được phép', Detail: 'Sai môn', Status: 403 }),
    })).rejects.toMatchObject({ status: 403, title: 'Không được phép', detail: 'Sai môn' })
  })

  it('extracts RFC 7807 ProblemDetails from application/problem+json Blob responses', async () => {
    vi.spyOn(console, 'warn').mockImplementation(() => {})
    const blob = new Blob(
      [
        JSON.stringify({
          status: 400,
          title: 'Dữ liệu TTS không hợp lệ',
          detail: 'Nội dung tổng hợp giọng nói vượt quá giới hạn.',
          errorCode: 'TTS_TEXT_INVALID',
          traceId: 'trace-blob-1',
        }),
      ],
      { type: 'application/problem+json' },
    )
    await expect(
      apiClient.post('/speech/tts', { text: 'hello' }, {
        responseType: 'blob',
        adapter: rejectingAdapter(400, blob),
      }),
    ).rejects.toMatchObject({
      status: 400,
      title: 'Dữ liệu TTS không hợp lệ',
      detail: 'Nội dung tổng hợp giọng nói vượt quá giới hạn.',
      errorCode: 'TTS_TEXT_INVALID',
      traceId: 'trace-blob-1',
    })
  })

  it('parses untyped Blob and PascalCase ProblemDetails inside Blob responses', async () => {
    vi.spyOn(console, 'warn').mockImplementation(() => {})
    const untypedBlob = new Blob([
      JSON.stringify({
        Title: 'Tạm khóa thao tác',
        Detail: 'Không thể xuất file báo cáo lúc này.',
        ErrorCode: 'EXPORT_LOCKED',
      }),
    ])
    await expect(
      apiClient.get('/official-exams/shifts/1/export-excel', {
        responseType: 'blob',
        adapter: rejectingAdapter(422, untypedBlob),
      }),
    ).rejects.toMatchObject({
      status: 422,
      title: 'Tạm khóa thao tác',
      detail: 'Không thể xuất file báo cáo lúc này.',
      ErrorCode: 'EXPORT_LOCKED',
    })
  })

  it('falls back safely when error Blob is HTML, empty, or binary audio', async () => {
    vi.spyOn(console, 'warn').mockImplementation(() => {})

    const htmlBlob = new Blob(['<html><body>502 Bad Gateway</body></html>'], { type: 'text/html' })
    let htmlRejection: { status: number; detail: string } | null = null
    try {
      await apiClient.get('/speech/tts', { responseType: 'blob', adapter: rejectingAdapter(502, htmlBlob) })
    } catch (err: unknown) {
      htmlRejection = err as { status: number; detail: string }
    }
    expect(htmlRejection).not.toBeNull()
    expect(htmlRejection?.status).toBe(502)
    expect(htmlRejection?.detail).toBe('request failed')
    expect(htmlRejection?.detail).not.toContain('<html>')

    const emptyBlob = new Blob([], { type: 'application/problem+json' })
    await expect(
      apiClient.get('/speech/tts', { responseType: 'blob', adapter: rejectingAdapter(500, emptyBlob) }),
    ).rejects.toMatchObject({ status: 500, detail: 'request failed' })

    const binaryBlob = new Blob([new Uint8Array([0xff, 0xfb, 0x90, 0x00])], { type: 'audio/mpeg' })
    await expect(
      apiClient.get('/speech/tts', { responseType: 'blob', adapter: rejectingAdapter(503, binaryBlob) }),
    ).rejects.toMatchObject({ status: 503, detail: 'request failed' })
  })

  it('detects 403 MUST_CHANGE_PASSWORD inside Blob and dispatches auth:must-change-password', async () => {
    vi.spyOn(console, 'warn').mockImplementation(() => {})
    sessionStorage.setItem('user_profile', JSON.stringify({ id: 'u1', mustChangePassword: false }))
    const listener = vi.fn()
    window.addEventListener('auth:must-change-password', listener)

    try {
      const blob = new Blob(
        [
          JSON.stringify({
            title: 'Cần đổi mật khẩu',
            detail: 'Bạn phải đổi mật khẩu trước khi tiếp tục.',
            errorCode: 'MUST_CHANGE_PASSWORD',
          }),
        ],
        { type: 'application/problem+json' },
      )
      await expect(
        apiClient.post('/speech/tts', { text: 'test' }, {
          responseType: 'blob',
          adapter: rejectingAdapter(403, blob),
        }),
      ).rejects.toMatchObject({
        status: 403,
        errorCode: 'MUST_CHANGE_PASSWORD',
        detail: 'Bạn phải đổi mật khẩu trước khi tiếp tục.',
      })
      expect(listener).toHaveBeenCalledTimes(1)
      expect(JSON.parse(sessionStorage.getItem('user_profile') || '{}')).toMatchObject({
        mustChangePassword: true,
      })
    } finally {
      window.removeEventListener('auth:must-change-password', listener)
    }
  })
})
