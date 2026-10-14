import apiClient from './api.client'
import type {
  GoogleLoginResponse,
  LoginRequest,
  LoginResponse,
  ProblemDetails,
  RefreshTokenResponse,
  User,
  UserRole,
} from '@/types/auth.types'

const ACCESS_TOKEN_KEY = 'access_token'
const REFRESH_TOKEN_KEY = 'refresh_token'
const USER_PROFILE_KEY = 'user_profile'

function normalizePortalRole(role: unknown): UserRole | null {
  if (role === 'Student' || role === 'student') return 'Student'
  if (
    role === 'Instructor' ||
    role === 'instructor' ||
    role === 'lecturer' ||
    role === 'proctor'
  ) {
    return 'Instructor'
  }
  if (role === 'Admin' || role === 'admin' || role === 'department_head') return 'Admin'
  return null
}

const LEGACY_DEV_USER_IDS: Record<string, string> = {
  'usr-stu-01': '44444444-4444-4444-4444-444444444444',
  'usr-ins-01': '22222222-2222-2222-2222-222222222222',
  'usr-adm-01': '11111111-1111-1111-1111-111111111111',
}

function persistSession(accessToken: string, refreshToken: string, user: User) {
  sessionStorage.setItem(ACCESS_TOKEN_KEY, accessToken)
  sessionStorage.setItem(REFRESH_TOKEN_KEY, refreshToken)
  sessionStorage.setItem(USER_PROFILE_KEY, JSON.stringify(user))
}

function clearSession() {
  sessionStorage.removeItem(ACCESS_TOKEN_KEY)
  sessionStorage.removeItem(REFRESH_TOKEN_KEY)
  sessionStorage.removeItem(USER_PROFILE_KEY)
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

    const user: User = { ...response.data.user, role }
    const data: LoginResponse = { ...response.data, user }
    if (data.refreshToken) {
      persistSession(data.accessToken, data.refreshToken, user)
    } else {
      sessionStorage.setItem(ACCESS_TOKEN_KEY, data.accessToken)
      sessionStorage.setItem(USER_PROFILE_KEY, JSON.stringify(user))
    }
    return data
  },

  /**
   * Exchange a Google ID token for an application session.
   */
  async loginWithGoogle(idToken: string): Promise<GoogleLoginResponse & { user: User }> {
    const response = await apiClient.post<GoogleLoginResponse>('/auth/google-login', { idToken })
    const role = normalizePortalRole(response.data.user?.role)
    if (!response.data.accessToken || !response.data.refreshToken || !response.data.user || !role) {
      throw {
        status: 502,
        title: 'Phản hồi đăng nhập không hợp lệ',
        detail: 'Backend không trả về phiên đăng nhập hoặc vai trò được cổng hiện tại hỗ trợ.',
      } satisfies ProblemDetails
    }

    const user: User = {
      id: response.data.user.id,
      email: response.data.user.email,
      fullName: response.data.user.fullName,
      role,
      studentCode: response.data.user.studentCode ?? undefined,
    }
    persistSession(response.data.accessToken, response.data.refreshToken, user)
    return { ...response.data, user }
  },

  /**
   * Rotate the refresh token and store the new pair.
   */
  async refreshAccessToken(): Promise<RefreshTokenResponse> {
    const currentRefreshToken = sessionStorage.getItem(REFRESH_TOKEN_KEY)
    if (!currentRefreshToken) {
      throw {
        status: 401,
        title: 'Token không hợp lệ.',
        detail: 'Không có refresh token để làm mới phiên đăng nhập.',
      } satisfies ProblemDetails
    }

    const response = await apiClient.post<RefreshTokenResponse>('/auth/refresh-token', {
      refreshToken: currentRefreshToken,
    })

    if (!response.data.accessToken || !response.data.refreshToken) {
      throw {
        status: 502,
        title: 'Phản hồi làm mới phiên không hợp lệ',
        detail: 'Backend không trả về cặp token mới.',
      } satisfies ProblemDetails
    }

    sessionStorage.setItem(ACCESS_TOKEN_KEY, response.data.accessToken)
    sessionStorage.setItem(REFRESH_TOKEN_KEY, response.data.refreshToken)
    return response.data
  },

  /**
   * Revoke the refresh token on the server, then clear the local session.
   */
  async logout(): Promise<void> {
    const refreshToken = sessionStorage.getItem(REFRESH_TOKEN_KEY)
    try {
      if (refreshToken) {
        await apiClient.post('/auth/sign-out', { refreshToken })
      }
    } catch {
      // Local session is cleared even when the network call fails.
    } finally {
      clearSession()
    }
  },

  /**
   * Get cached user profile from session
   */
  getCurrentUser(): User | null {
    const userStr = sessionStorage.getItem(USER_PROFILE_KEY)
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
        sessionStorage.setItem(USER_PROFILE_KEY, JSON.stringify(user))
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
    return sessionStorage.getItem(ACCESS_TOKEN_KEY)
  },
}
