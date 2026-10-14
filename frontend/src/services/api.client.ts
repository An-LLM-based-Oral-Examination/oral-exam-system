import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios'
import type { ProblemDetails } from '@/types/auth.types'

type RetriableRequest = InternalAxiosRequestConfig & { _retry?: boolean }

/**
 * Enterprise Axios Instance configured for .NET 8 Web API
 */
export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api/v1',
  headers: {
    'Content-Type': 'application/json',
  },
  timeout: 15000,
})

function toProblem(error: AxiosError<ProblemDetails>): ProblemDetails {
  if (error.response?.data && typeof error.response.data === 'object') {
    const problem = error.response.data
    return {
      ...problem,
      status: problem.status || error.response.status,
      title: problem.title || 'API Error',
      detail: problem.detail || problem.title || error.message,
    }
  }

  return {
    status: error.response?.status || 500,
    title: 'Network or Server Error',
    detail: error.message || 'Unable to connect to the backend server.',
  }
}

function isAuthEndpoint(url: string | undefined) {
  if (!url) return false
  return (
    url.includes('/auth/google-login') ||
    url.includes('/auth/refresh-token') ||
    url.includes('/auth/sign-out')
  )
}

// Request Interceptor: Attach JWT Bearer Token
apiClient.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    const token = sessionStorage.getItem('access_token')
    if (token && config.headers) {
      config.headers.Authorization = `Bearer ${token}`
    }
    return config
  },
  (error) => Promise.reject(error),
)

let isRefreshing = false
let failedQueue: Array<{ resolve: (token: string) => void; reject: (error: unknown) => void }> = []

function processQueue(error: unknown, token: string | null = null) {
  failedQueue.forEach((pending) => {
    if (error || !token) {
      pending.reject(error ?? new Error('Refresh failed'))
    } else {
      pending.resolve(token)
    }
  })
  failedQueue = []
}

// Response Interceptor: refresh once on 401, then surface RFC 7807 ProblemDetails
apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<ProblemDetails>) => {
    const originalRequest = error.config as RetriableRequest | undefined
    const shouldRefresh =
      error.response?.status === 401 &&
      originalRequest &&
      !originalRequest._retry &&
      !isAuthEndpoint(originalRequest.url)

    if (shouldRefresh && originalRequest) {
      if (isRefreshing) {
        return new Promise<string>((resolve, reject) => {
          failedQueue.push({ resolve, reject })
        }).then((token) => {
          originalRequest.headers.Authorization = `Bearer ${token}`
          return apiClient(originalRequest)
        })
      }

      originalRequest._retry = true
      isRefreshing = true

      try {
        const { authService } = await import('./auth.service')
        const data = await authService.refreshAccessToken()
        processQueue(null, data.accessToken)
        originalRequest.headers.Authorization = `Bearer ${data.accessToken}`
        return apiClient(originalRequest)
      } catch (refreshError) {
        processQueue(refreshError, null)
        const { authService } = await import('./auth.service')
        await authService.logout()
        if (!window.location.pathname.startsWith('/login')) {
          window.location.assign('/login')
        }
        return Promise.reject(
          refreshError && typeof refreshError === 'object' && 'detail' in refreshError
            ? refreshError
            : toProblem(error),
        )
      } finally {
        isRefreshing = false
      }
    }

    const problem = toProblem(error)
    console.warn(`[API Error ${problem.status}]: ${problem.detail || problem.title}`)
    return Promise.reject(problem)
  },
)

export default apiClient
