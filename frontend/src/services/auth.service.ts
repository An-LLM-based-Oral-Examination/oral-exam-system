import apiClient from './api.client'
import type {
  LoginRequest,
  LoginResponse,
  ProblemDetails,
  User,
  UserRole,
} from '@/types/auth.types'

function normalizePortalRole(role: unknown): UserRole | null {
  if (role === 'Student' || role === 'student') return 'Student'
  if (role === 'Instructor' || role === 'instructor' || role === 'lecturer') return 'Instructor'
  if (role === 'Admin' || role === 'admin') return 'Admin'
  return null
}

const LEGACY_DEV_USER_IDS: Record<string, string> = {
  'usr-stu-01': '44444444-4444-4444-4444-444444444444',
  'usr-ins-01': '22222222-2222-2222-2222-222222222222',
  'usr-adm-01': '11111111-1111-1111-1111-111111111111',
}

export const authService = {
  /**
   * Authenticate with email & password
   */
  async login(credentials: LoginRequest): Promise<LoginResponse> {
    const response = await apiClient.post<LoginResponse>('/auth/login', credentials)
    const role = normalizePortalRole(response.data.user?.role)
    if (!response.data.accessToken || !response.data.user || !role) {
      throw {
        status: 502,
        title: 'Phản hồi đăng nhập không hợp lệ',
        detail: 'Backend không trả về phiên đăng nhập hoặc vai trò được cổng hiện tại hỗ trợ.',
      } satisfies ProblemDetails
    }

    const data: LoginResponse = {
      ...response.data,
      user: { ...response.data.user, role },
    }
    sessionStorage.setItem('access_token', data.accessToken)
    sessionStorage.setItem('user_profile', JSON.stringify(data.user))
    return data
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
      const parsed = JSON.parse(userStr) as Partial<User> & { role?: unknown }
      const role = normalizePortalRole(parsed.role)
      if (
        !role ||
        typeof parsed.id !== 'string' ||
        typeof parsed.email !== 'string' ||
        typeof parsed.fullName !== 'string'
      ) {
        return null
      }

      const migratedId = import.meta.env.DEV
        ? (LEGACY_DEV_USER_IDS[parsed.id] ?? parsed.id)
        : parsed.id
      const user: User = { ...parsed, id: migratedId, role } as User
      if (migratedId !== parsed.id || role !== parsed.role) {
        sessionStorage.setItem('user_profile', JSON.stringify(user))
      }
      return user
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
