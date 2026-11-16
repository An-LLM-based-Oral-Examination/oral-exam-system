import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios'
import type { ProblemDetails } from '@/types/auth.types'
import { safeJsonParse } from '@/utils/json.utils'
import { browserAuthSession } from '@/services/browserAuthSession'
import {
  getSessionEpoch,
  isSessionEpochCurrent,
  rotateSessionEpoch,
} from '@/services/authSessionState'

declare module 'axios' {
  interface AxiosRequestConfig {
    _retry?: boolean
    _skipAuthRefresh?: boolean
    _silentAuthFailure?: boolean
  }
}

type JsonRecord = Record<string, unknown>
type RetryConfig = InternalAxiosRequestConfig & {
  _retry?: boolean
  _skipAuthRefresh?: boolean
  _silentAuthFailure?: boolean
}

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
  withCredentials: true,
  headers: { 'Content-Type': 'application/json' },
  timeout: 15000,
  transformResponse: [
    (data) => {
      if (typeof data === 'string') {
        const trimmed = data.trim()
        if (!trimmed) return null
        try {
          return JSON.parse(trimmed)
        } catch {
          return data
        }
      }
      return data
    },
  ],
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

let refreshInFlight: { epoch: string; promise: Promise<string> } | null = null

function readMustChangeFromToken(token: string): boolean | undefined {
  const parts = token.split('.')
  if (parts.length < 2) return undefined
  try {
    const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/')
    const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=')
    const decoded = atob(padded)
    const payload = safeJsonParse<{ must_change_password?: unknown }>(decoded, {})
    return payload.must_change_password === 'true' || payload.must_change_password === true
  } catch {
    return undefined
  }
}

async function refreshAccessToken(): Promise<string> {
  const epoch = getSessionEpoch()
  const response = await browserAuthSession.withRefreshLock(() =>
    apiClient.post<{ accessToken: string }>(
      '/auth/refresh-token',
      {},
      { _skipAuthRefresh: true, withCredentials: true },
    ),
  )
  if (!response.data.accessToken) {
    throw new Error('Backend không trả về token mới hợp lệ')
  }

  // A response from an old session must never resurrect logout or replace a new login.
  if (!isSessionEpochCurrent(epoch)) {
    throw new Error('Phiên đăng nhập đã thay đổi trong khi làm mới token')
  }

  sessionStorage.setItem('access_token', response.data.accessToken)
  sessionStorage.removeItem('refresh_token')
  const mustChange = readMustChangeFromToken(response.data.accessToken)
  const rawProfile = sessionStorage.getItem('user_profile')
  if (rawProfile && mustChange !== undefined) {
    const parsedProfile = safeJsonParse<unknown>(rawProfile, null)
    if (isRecord(parsedProfile)) {
      sessionStorage.setItem(
        'user_profile',
        JSON.stringify({ ...parsedProfile, mustChangePassword: mustChange }),
      )
    }
  }
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
      url.includes('/auth/password-login') ||
      url.includes('/auth/refresh-token') ||
      url.includes('/auth/google-login') ||
      url.includes('/auth/sign-out')

    if (
      status === 401 &&
      config &&
      !config._retry &&
      !config._skipAuthRefresh &&
      !isAuthEndpoint &&
      config.headers.Authorization === `Bearer ${sessionStorage.getItem('access_token')}`
    ) {
      config._retry = true
      const originalAccessToken = sessionStorage.getItem('access_token')
      const originalEpoch = getSessionEpoch()
      try {
        if (!refreshInFlight || refreshInFlight.epoch !== originalEpoch) {
          const entry = { epoch: originalEpoch, promise: Promise.resolve('') }
          entry.promise = refreshAccessToken().finally(() => {
            if (refreshInFlight === entry) refreshInFlight = null
          })
          refreshInFlight = entry
        }
        const token = await refreshInFlight.promise
        if (sessionStorage.getItem('access_token') !== token)
          throw new Error('Phiên đăng nhập đã thay đổi')
        if (config.headers) config.headers.Authorization = `Bearer ${token}`
        return apiClient.request(config)
      } catch (refreshError) {
        const failureStatus = isRecord(refreshError) ? refreshError.status : undefined
        if (
          isSessionEpochCurrent(originalEpoch) &&
          sessionStorage.getItem('access_token') === originalAccessToken &&
          (failureStatus === 401 || failureStatus === 403)
        ) {
          rotateSessionEpoch()
          sessionStorage.removeItem('access_token')
          sessionStorage.removeItem('refresh_token')
          sessionStorage.removeItem('user_profile')
          window.dispatchEvent(new Event('auth:session-expired'))
        }
      }
    }

    const responseStatus = error.response?.status ?? 500
    const responseData = error.response?.data
    const payload = isRecord(responseData) ? responseData : {}
    // Transport status is authoritative, including during mixed-version rollout.
    const normalizedStatus = responseStatus
    const detail =
      readString(payload, 'detail', 'Detail', 'error', 'Error', 'message', 'Message') ??
      error.message ??
      'Không thể kết nối đến máy chủ.'
    const title =
      readString(payload, 'title', 'Title') ??
      (error.response ? 'Không thể xử lý yêu cầu' : 'Lỗi kết nối hoặc máy chủ')

    const problem: ProblemDetails = { ...payload, status: normalizedStatus, title, detail }

    const isMustChangePasswordError =
      normalizedStatus === 403 &&
      (payload.code === 'MUST_CHANGE_PASSWORD' ||
        payload.errorCode === 'MUST_CHANGE_PASSWORD' ||
        title.includes('Cần đổi mật khẩu') ||
        detail.includes('Bạn phải đổi mật khẩu'))

    if (isMustChangePasswordError) {
      const rawProfile = sessionStorage.getItem('user_profile')
      if (rawProfile) {
        const parsedProfile = safeJsonParse<unknown>(rawProfile, null)
        if (isRecord(parsedProfile)) {
          sessionStorage.setItem(
            'user_profile',
            JSON.stringify({ ...parsedProfile, mustChangePassword: true }),
          )
        }
      }
      window.dispatchEvent(new Event('auth:must-change-password'))
      console.warn(`[API Error 403 MUST_CHANGE_PASSWORD]: ${detail}`)
      return Promise.reject(problem)
    }

    if (!config?._silentAuthFailure) console.warn(`[API Error ${normalizedStatus}]: ${detail}`)
    return Promise.reject(problem)
  },
)

export default apiClient
