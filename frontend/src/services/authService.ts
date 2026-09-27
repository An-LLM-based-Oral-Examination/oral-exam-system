import apiClient from './api'
import type { LoginRequest, LoginResponse, User } from '@/types/auth.types'

export const authService = {
  /**
   * Authenticate with email & password
   */
  async login(credentials: LoginRequest): Promise<LoginResponse> {
    const response = await apiClient.post<LoginResponse>('/auth/login', credentials)
    if (response.data.accessToken) {
      sessionStorage.setItem('access_token', response.data.accessToken)
      sessionStorage.setItem('user_profile', JSON.stringify(response.data.user))
    }
    return response.data
  },

  /**
   * Clear session storage on logout
   */
  logout(): void {
    sessionStorage.removeItem('access_token')
    sessionStorage.removeItem('user_profile')
  },

  /**
   * Get cached user profile from session
   */
  getCurrentUser(): User | null {
    const userStr = sessionStorage.getItem('user_profile')
    if (!userStr) return null
    try {
      return JSON.parse(userStr) as User
    } catch {
      return null
    }
  },

  /**
   * Get raw access token
   */
  getToken(): string | null {
    return sessionStorage.getItem('access_token')
  },
}
