import apiClient from './api.client'
import type {
  GoogleLoginRequest,
  LoginRequest,
  LoginResponse,
  ProblemDetails,
  User,
  UserRole,
} from '@/types/auth.types'
import { clearMockExamSession } from '@/utils/mockExamSessionStorage'
import { clearPracticeRuntimes } from '@/utils/practiceSessionStorage'

function normalizePortalRole(role: unknown): UserRole | null {
  if (role === 'Student' || role === 'student') return 'Student'
  if (role === 'Instructor' || role === 'instructor' || role === 'lecturer' || role === 'Lecturer') return 'Instructor'
  if (
    role === 'DepartmentHead' ||
    role === 'department_head' ||
    role === 'Department_Head' ||
    role === 'head'
  ) {
    return 'DepartmentHead'
  }
  if (role === 'ExaminationOfficer' || role === 'examination_officer') return 'ExaminationOfficer'
  if (role === 'Proctor' || role === 'proctor') return 'Proctor'
  if (role === 'Admin' || role === 'admin') return 'Admin'
  return null
}

function extractMustChangePasswordFromJwt(token: string | null | undefined): boolean | undefined {
  if (!token) return undefined
  const parts = token.split('.')
  if (parts.length < 2) return undefined
  try {
    const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/')
    const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=')
    const payload = JSON.parse(atob(padded)) as { must_change_password?: unknown }
    if (payload.must_change_password === 'true' || payload.must_change_password === true) {
      return true
    }
    if (payload.must_change_password === 'false' || payload.must_change_password === false) {
      return false
    }
    return false
  } catch {
    return undefined
  }
}

const LEGACY_DEV_USER_IDS: Record<string, string> = {
  'usr-stu-01': '00000000-0000-0000-0000-000000000005',
  '44444444-4444-4444-4444-444444444444': '00000000-0000-0000-0000-000000000005',
  'usr-ins-01': '22222222-2222-2222-2222-222222222222',
  'usr-adm-01': '11111111-1111-1111-1111-111111111111',
}

function normalizeResponse(data: LoginResponse): LoginResponse {
  const role = normalizePortalRole(data.user?.role)
  if (!data.accessToken || !data.refreshToken || !data.user || !role) {
    throw {
      status: 502,
      title: 'Phản hồi đăng nhập không hợp lệ',
      detail: 'Backend không trả về đủ token hoặc vai trò người dùng được hỗ trợ.',
    } satisfies ProblemDetails
  }

  const jwtMustChange = extractMustChangePasswordFromJwt(data.accessToken)
  const mustChange = Boolean(
    data.mustChangePassword ??
      jwtMustChange ??
      (data.user as { mustChangePassword?: boolean })?.mustChangePassword,
  )
  return { ...data, mustChangePassword: mustChange, user: { ...data.user, role, mustChangePassword: mustChange } }
}

function persistSession(data: LoginResponse): LoginResponse {
  const normalized = normalizeResponse(data)
  clearPracticeRuntimes()
  sessionStorage.setItem('access_token', normalized.accessToken)
  sessionStorage.setItem('refresh_token', normalized.refreshToken)
  sessionStorage.setItem('user_profile', JSON.stringify(normalized.user))
  return normalized
}

export const authService = {
  async passwordLogin(request: LoginRequest): Promise<LoginResponse> {
    const response = await apiClient.post<LoginResponse>('/auth/password-login', request)
    clearMockExamSession()
    return persistSession(response.data)
  },

  async googleLogin(request: GoogleLoginRequest): Promise<LoginResponse> {
    const response = await apiClient.post<LoginResponse>('/auth/google-login', request)
    clearMockExamSession()
    return persistSession(response.data)
  },

  async changePassword(request: { currentPassword: string; newPassword: string }): Promise<void> {
    const response = await apiClient.post<{
      accessToken: string
      refreshToken: string
      tokenType: string
      expiresIn: number
      mustChangePassword?: boolean
      message?: string
    }>('/auth/change-password', request)

    sessionStorage.setItem('access_token', response.data.accessToken)
    sessionStorage.setItem('refresh_token', response.data.refreshToken)
    const currentUser = this.getCurrentUser()
    if (currentUser) {
      const updatedUser: User = { ...currentUser, mustChangePassword: false }
      sessionStorage.setItem('user_profile', JSON.stringify(updatedUser))
    }
  },

  async refreshToken(): Promise<LoginResponse> {
    const refreshToken = sessionStorage.getItem('refresh_token')
    if (!refreshToken) throw new Error('Không có refresh token')

    const response = await apiClient.post<
      Pick<LoginResponse, 'accessToken' | 'refreshToken' | 'tokenType' | 'expiresIn'>
    >('/auth/refresh-token', { refreshToken }, { _skipAuthRefresh: true })

    if (!response.data.accessToken || !response.data.refreshToken) {
      throw new Error('Backend không trả về token mới hợp lệ')
    }

    const user = this.getCurrentUser()
    if (!user) throw new Error('Không có hồ sơ người dùng')
    return persistSession({ ...response.data, user })
  },

  // Giữ tên tương thích với các interceptor hoặc màn hình tích hợp ở nhánh develop.
  async refreshAccessToken(): Promise<
    Pick<LoginResponse, 'accessToken' | 'refreshToken' | 'tokenType' | 'expiresIn'>
  > {
    const data = await this.refreshToken()
    return {
      accessToken: data.accessToken,
      refreshToken: data.refreshToken,
      tokenType: data.tokenType,
      expiresIn: data.expiresIn,
    }
  },

  async logout(): Promise<void> {
    const refreshToken = sessionStorage.getItem('refresh_token')
    try {
      if (refreshToken) {
        await apiClient.post('/auth/sign-out', { refreshToken }, { _skipAuthRefresh: true })
      }
    } catch {
      // Luôn xoá phiên cục bộ kể cả khi máy chủ không còn khả dụng.
    } finally {
      this.clearSession()
    }
  },

  clearSession(): void {
    clearPracticeRuntimes()
    clearMockExamSession()
    sessionStorage.removeItem('access_token')
    sessionStorage.removeItem('refresh_token')
    sessionStorage.removeItem('user_profile')
  },

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
      const jwtMustChange = extractMustChangePasswordFromJwt(sessionStorage.getItem('access_token'))
      const mustChangePassword = Boolean(jwtMustChange ?? parsed.mustChangePassword)
      const user: User = { ...parsed, id: migratedId, role, mustChangePassword } as User
      if (
        migratedId !== parsed.id ||
        role !== parsed.role ||
        mustChangePassword !== Boolean(parsed.mustChangePassword)
      ) {
        sessionStorage.setItem('user_profile', JSON.stringify(user))
      }
      return user
    } catch {
      return null
    }
  },

  getToken(): string | null {
    return sessionStorage.getItem('access_token')
  },
}
