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
})
