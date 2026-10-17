import apiClient from './api.client'
import type {
  ChangePasswordRequest,
  GoogleLoginRequest,
  LoginResponse,
  PasswordLoginRequest,
  ProblemDetails,
  User,
  UserRole,
} from '@/types/auth.types'
import { clearMockExamSession } from '@/utils/mockExamSessionStorage'
import { clearPracticeRuntimes } from '@/utils/practiceSessionStorage'

function normalizePortalRole(role: unknown): UserRole | null {
  if (role === 'Student' || role === 'student') return 'Student'
  if (role === 'Instructor' || role === 'instructor' || role === 'lecturer') return 'Instructor'
  if (
    role === 'DepartmentHead' ||
    role === 'department_head' ||
    role === 'Department_Head' ||
    role === 'head'
  ) {
    return 'DepartmentHead'
  }
  if (role === 'Proctor' || role === 'proctor') return 'Proctor'
  if (role === 'Admin' || role === 'admin') return 'Admin'
  return null
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

  return {
    ...data,
    user: {
      ...data.user,
      role,
      mustChangePassword: data.mustChangePassword ?? data.user.mustChangePassword,
    },
  }
}

function persistSession(data: LoginResponse): LoginResponse {
  const normalized = normalizeResponse(data)
  clearPracticeRuntimes()
  sessionStorage.setItem('access_token', normalized.accessToken)
  sessionStorage.setItem('refresh_token', normalized.refreshToken)
  sessionStorage.setItem('user_profile', JSON.stringify(normalized.user))
  return normalized
}

function buildOfflineFallbackLogin(idToken: string): LoginResponse {
  const raw = idToken.startsWith('dev-google:') ? idToken.slice('dev-google:'.length) : idToken
  const [emailPart = 'thanhnqse184236@fpt.edu.vn', namePart] = raw.split('|')
  const email = emailPart.trim().toLowerCase() || 'thanhnqse184236@fpt.edu.vn'

  let role: UserRole = 'Student'
  let userId = '00000000-0000-0000-0000-00000000000b'
  let fullName = namePart?.trim() || 'Nguyễn Quang Thành'
  let studentCode: string | null

  if (email.startsWith('admin@')) {
    role = 'Admin'
    userId = '00000000-0000-0000-0000-000000000001'
    fullName = namePart?.trim() || 'Nguyễn Quang Thành'
    studentCode = null
  } else if (email.startsWith('headse@')) {
    role = 'DepartmentHead'
    userId = '00000000-0000-0000-0000-000000000002'
    fullName = namePart?.trim() || 'Trần Văn Trưởng'
    studentCode = null
  } else if (email.startsWith('totnt@')) {
    role = 'Instructor'
    userId = '00000000-0000-0000-0000-000000000003'
    fullName = namePart?.trim() || 'Nguyễn Trọng Tốt'
    studentCode = null
  } else if (email.startsWith('haind@')) {
    role = 'Proctor'
    userId = '00000000-0000-0000-0000-000000000004'
    fullName = namePart?.trim() || 'Nguyễn Đăng Hải'
    studentCode = null
  } else if (email.includes('se170001')) {
    role = 'Student'
    userId = '00000000-0000-0000-0000-000000000005'
    fullName = namePart?.trim() || 'Lê Vũ Hoàng'
    studentCode = 'SE170001'
  } else {
    const codeMatch = email.match(/(se\d{6})/i)
    studentCode = codeMatch ? codeMatch[1].toUpperCase() : 'SE184236'
  }

  return {
    accessToken: `fptu-session-token-${userId}`,
    refreshToken: `fptu-refresh-token-${userId}`,
    tokenType: 'Bearer',
    expiresIn: 3600,
    user: {
      id: userId,
      email,
      fullName,
      role,
      studentCode: studentCode ?? undefined,
    },
  }
}

function buildOfflineFallbackPasswordLogin(request: PasswordLoginRequest): LoginResponse {
  const normalizedUsername = request.username.trim().toLowerCase()
  const provisionedMap: Record<string, { email: string; fullName: string }> = {
    admin: { email: 'admin@fpt.edu.vn', fullName: 'Nguyễn Quang Thành' },
    headse: { email: 'headse@fe.edu.vn', fullName: 'Trần Văn Trưởng' },
    totnt: { email: 'totnt@fe.edu.vn', fullName: 'Nguyễn Trọng Tốt' },
    haind: { email: 'haind@fe.edu.vn', fullName: 'Nguyễn Đăng Hải' },
    hoalvse170001: { email: 'hoanglvse170001@fpt.edu.vn', fullName: 'Lê Vũ Hoàng' },
    maittse170002: { email: 'maittse170002@fpt.edu.vn', fullName: 'Trần Thị Mai' },
    thanhnqse184236: { email: 'thanhnqse184236@fpt.edu.vn', fullName: 'Nguyễn Quang Thành' },
  }

  const matched = provisionedMap[normalizedUsername] ?? {
    email: normalizedUsername.includes('@')
      ? normalizedUsername
      : `${normalizedUsername}@fpt.edu.vn`,
    fullName: 'Nguyễn Quang Thành',
  }

  return buildOfflineFallbackLogin(`dev-google:${matched.email}|${matched.fullName}`)
}

export const authService = {
  async googleLogin(request: GoogleLoginRequest): Promise<LoginResponse> {
    try {
      const response = await apiClient.post<LoginResponse>('/auth/google-login', request)
      clearMockExamSession()
      return persistSession(response.data)
    } catch (error) {
      const problem = error as Partial<ProblemDetails> | undefined
      if (import.meta.env.DEV && (!problem?.status || problem.status >= 500)) {
        clearMockExamSession()
        return persistSession(buildOfflineFallbackLogin(request.idToken))
      }
      throw error
    }
  },

  async passwordLogin(request: PasswordLoginRequest): Promise<LoginResponse> {
    try {
      const response = await apiClient.post<LoginResponse>('/auth/password-login', request)
      clearMockExamSession()
      return persistSession(response.data)
    } catch (error) {
      const problem = error as Partial<ProblemDetails> | undefined
      if (import.meta.env.DEV && (!problem?.status || problem.status >= 500)) {
        clearMockExamSession()
        return persistSession(buildOfflineFallbackPasswordLogin(request))
      }
      throw error
    }
  },

  async changePassword(request: ChangePasswordRequest): Promise<LoginResponse> {
    const response = await apiClient.post<
      Pick<LoginResponse, 'accessToken' | 'refreshToken' | 'tokenType' | 'expiresIn' | 'mustChangePassword'>
    >('/auth/change-password', request)

    const user = this.getCurrentUser()
    if (!user) throw new Error('Không có hồ sơ người dùng')
    return persistSession({
      ...response.data,
      user: { ...user, mustChangePassword: false },
    })
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
      const user: User = { ...parsed, id: migratedId, role } as User
      if (migratedId !== parsed.id || role !== parsed.role) {
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
