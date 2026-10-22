import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios'
import type { ProblemDetails } from '@/types/auth.types'

declare module 'axios' {
  interface AxiosRequestConfig {
    _retry?: boolean
    _skipAuthRefresh?: boolean
  }
}

type JsonRecord = Record<string, unknown>
type RetryConfig = InternalAxiosRequestConfig & { _retry?: boolean; _skipAuthRefresh?: boolean }

function isRecord(value: unknown): value is JsonRecord {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function readString(record: JsonRecord, ...keys: string[]) {
  for (const key of keys) {
    const value = record[key]
    if (typeof value === 'string' && value.trim()) return value
  }
  return undefined
}

export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api/v1',
  headers: { 'Content-Type': 'application/json' },
  timeout: 15000,
})

apiClient.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    const token = sessionStorage.getItem('access_token')
    if (token && config.headers) config.headers.Authorization = `Bearer ${token}`
    // Default Content-Type là application/json — FormData phải để browser gắn multipart + boundary.
    if (typeof FormData !== 'undefined' && config.data instanceof FormData && config.headers) {
      config.headers.delete('Content-Type')
    }
    return config
  },
  (error) => Promise.reject(error),
)

let refreshPromise: Promise<string> | null = null

async function refreshAccessToken(): Promise<string> {
  const refreshToken = sessionStorage.getItem('refresh_token')
  if (!refreshToken) throw new Error('Không có refresh token')

  const response = await apiClient.post<{ accessToken: string; refreshToken: string }>(
    '/auth/refresh-token',
    { refreshToken },
    { _skipAuthRefresh: true },
  )
  if (!response.data.accessToken || !response.data.refreshToken) {
    throw new Error('Backend không trả về token mới hợp lệ')
  }

  sessionStorage.setItem('access_token', response.data.accessToken)
  sessionStorage.setItem('refresh_token', response.data.refreshToken)
  return response.data.accessToken
}

apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<unknown>) => {
    const config = error.config as RetryConfig | undefined
    const status = error.response?.status
    const url = config?.url ?? ''
    const isAuthEndpoint =
      url.includes('/auth/login') ||
      url.includes('/auth/refresh-token') ||
      url.includes('/auth/google-login') ||
      url.includes('/auth/sign-out')

    if (
      status === 401 &&
      config &&
      !config._retry &&
      !config._skipAuthRefresh &&
      !isAuthEndpoint &&
      sessionStorage.getItem('refresh_token')
    ) {
      config._retry = true
      try {
        refreshPromise ??= refreshAccessToken().finally(() => {
          refreshPromise = null
        })
        const token = await refreshPromise
        if (config.headers) config.headers.Authorization = `Bearer ${token}`
        return apiClient.request(config)
      } catch {
        sessionStorage.removeItem('access_token')
        sessionStorage.removeItem('refresh_token')
        sessionStorage.removeItem('user_profile')
        window.dispatchEvent(new Event('auth:session-expired'))
      }
    }

    const responseStatus = error.response?.status ?? 500
    const responseData = error.response?.data
    const payload = isRecord(responseData) ? responseData : {}
    const payloadStatus = payload.status ?? payload.Status
    const normalizedStatus =
      typeof payloadStatus === 'number' && Number.isFinite(payloadStatus)
        ? payloadStatus
        : responseStatus
    const detail =
      readString(payload, 'detail', 'Detail', 'error', 'Error', 'message', 'Message') ??
      error.message ??
      'Không thể kết nối đến máy chủ.'
    const title =
      readString(payload, 'title', 'Title') ??
      (error.response ? 'Không thể xử lý yêu cầu' : 'Lỗi kết nối hoặc máy chủ')

    const problem: ProblemDetails = { ...payload, status: normalizedStatus, title, detail }
    console.warn(`[API Error ${normalizedStatus}]: ${detail}`)
    return Promise.reject(problem)
  },
)

export default apiClient
