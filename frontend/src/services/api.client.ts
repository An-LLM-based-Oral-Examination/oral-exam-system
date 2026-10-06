import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios'
import type { ProblemDetails } from '@/types/auth.types'

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

// Response Interceptor: Format RFC 7807 ProblemDetails
apiClient.interceptors.response.use(
  (response) => response,
  (error: AxiosError<ProblemDetails>) => {
    if (error.response?.data) {
      const problem = error.response.data
      console.warn(
        `[API Error ${problem.status || error.response.status}]: ${problem.detail || problem.title}`,
      )
      return Promise.reject(problem)
    }
    return Promise.reject({
      status: error.response?.status || 500,
      title: 'Network or Server Error',
      detail: error.message || 'Unable to connect to the backend server.',
    } as ProblemDetails)
  },
)

export default apiClient
