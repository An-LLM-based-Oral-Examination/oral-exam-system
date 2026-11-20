import apiClient from './api.client'
import type {
  CurrentUserResponse,
  GoogleLoginRequest,
  LoginRequest,
  LoginResponse,
  ProblemDetails,
  RefreshTokenResponse,
  User,
  UserRole,
} from '@/types/auth.types'
import { clearMockExamSession } from '@/utils/mockExamSessionStorage'
import { clearPracticeRuntimes } from '@/utils/practiceSessionStorage'
import { safeJsonParse } from '@/utils/json.utils'
import { browserAuthSession } from '@/services/browserAuthSession'
import {
  getSessionEpoch,
  isSessionEpochCurrent,
  rotateSessionEpoch,
} from '@/services/authSessionState'

function normalizePortalRole(role: unknown): UserRole | null {
  if (role === 'Student' || role === 'student') return 'Student'
  if (role === 'Instructor' || role === 'instructor' || role === 'lecturer' || role === 'Lecturer')
    return 'Instructor'
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
    const decoded = atob(padded)
    const payload = safeJsonParse<{ must_change_password?: unknown }>(decoded, {})
    if (payload.must_change_password === 'true' || payload.must_change_password === true) {
      return true
    }
    if (payload.must_change_password === 'false' || payload.must_change_password === false) {
      return false
    }
    return undefined
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
  if (!data.accessToken || !data.user || !role) {
    throw {
      status: 502,
      title: 'Phản hồi đăng nhập không hợp lệ',
      detail: 'Backend không trả về access token hoặc vai trò người dùng được hỗ trợ.',
    } satisfies ProblemDetails
  }

  const jwtMustChange = extractMustChangePasswordFromJwt(data.accessToken)
  const mustChange = Boolean(
    data.mustChangePassword ??
    jwtMustChange ??
    (data.user as { mustChangePassword?: boolean })?.mustChangePassword,
  )
  return {
    ...data,
    mustChangePassword: mustChange,
    user: { ...data.user, role, mustChangePassword: mustChange },
  }
}

function persistSession(data: LoginResponse): LoginResponse {
  const normalized = normalizeResponse(data)
  clearPracticeRuntimes()
  rotateSessionEpoch()
  sessionStorage.setItem('access_token', normalized.accessToken)
  sessionStorage.removeItem('refresh_token')
  sessionStorage.setItem('user_profile', JSON.stringify(normalized.user))
  return normalized
}

function toUser(data: CurrentUserResponse): User {
  return {
    id: data.id,
    email: data.email,
    fullName: data.fullName,
    role: data.role,
    studentCode: data.studentCode ?? undefined,
    mustChangePassword: data.mustChangePassword,
  }
}

function readFailureStatus(error: unknown): number | undefined {
  if (!error || typeof error !== 'object') return undefined
  const direct = (error as { status?: unknown }).status
  if (typeof direct === 'number') return direct
  const response = (error as { response?: { status?: unknown } }).response
  return typeof response?.status === 'number' ? response.status : undefined
}

export interface RestoredAuthSession {
  accessToken: string
  user: User
}

export const authService = {
  async fetchCurrentUser(requestConfig?: {
    _skipAuthRefresh?: boolean
    _silentAuthFailure?: boolean
  }): Promise<CurrentUserResponse> {
    const { data } = requestConfig
      ? await apiClient.get<CurrentUserResponse>('/auth/me', requestConfig)
      : await apiClient.get<CurrentUserResponse>('/auth/me')
    const role = normalizePortalRole(data.role)
    if (
      !role ||
      typeof data.id !== 'string' ||
      !data.id.trim() ||
      typeof data.email !== 'string' ||
      !data.email.trim() ||
      typeof data.fullName !== 'string' ||
      !data.fullName.trim() ||
      typeof data.isActive !== 'boolean' ||
      typeof data.mustChangePassword !== 'boolean'
    ) {
      throw {
        status: 502,
        title: 'Hồ sơ người dùng không hợp lệ',
        detail: 'Backend trả về vai trò không được hỗ trợ.',
      } satisfies ProblemDetails
    }
    return { ...data, role }
  },

  /** Only persist a profile verified for the session that actually sent this request. */
  async synchronizeCurrentUser(isCurrent: () => boolean = () => true): Promise<User | null> {
    const expectedUser = this.getCurrentUser()
    const initialToken = this.getToken()
    if (!expectedUser || !initialToken) return null
    const response = await apiClient.get<CurrentUserResponse>('/auth/me')
    const data = response.data
    const role = normalizePortalRole(data?.role)
    if (
      !data ||
      !role ||
      typeof data.id !== 'string' ||
      data.id !== expectedUser.id ||
      typeof data.email !== 'string' ||
      !data.email.trim() ||
      typeof data.fullName !== 'string' ||
      !data.fullName.trim() ||
      data.isActive !== true ||
      typeof data.mustChangePassword !== 'boolean' ||
      (data.studentCode != null && typeof data.studentCode !== 'string')
    ) {
      throw {
        status: 403,
        title: 'Không thể xác minh phiên đăng nhập',
        detail: 'Hồ sơ máy chủ không hợp lệ, không hoạt động hoặc không khớp tài khoản hiện tại.',
      } satisfies ProblemDetails
    }
    // Axios may have retried /me with a newly refreshed access token. Compare with
    // the request that produced the response, not the old token captured at boot.
    const sentAuthorization = response.config?.headers?.Authorization
    const sentToken =
      typeof sentAuthorization === 'string' && sentAuthorization.startsWith('Bearer ')
        ? sentAuthorization.slice(7)
        : initialToken
    if (
      !isCurrent() ||
      this.getCurrentUser()?.id !== expectedUser.id ||
      this.getToken() !== sentToken
    )
      return null
    const user: User = {
      id: data.id,
      email: data.email,
      fullName: data.fullName,
      role,
      studentCode: data.studentCode ?? undefined,
      mustChangePassword: data.mustChangePassword,
    }
    sessionStorage.setItem('user_profile', JSON.stringify(user))
    return user
  },

  async passwordLogin(request: LoginRequest): Promise<LoginResponse> {
    const response = await apiClient.post<LoginResponse>('/auth/password-login', request)
    clearMockExamSession()
    return persistSession(response.data)
  },

  async googleLogin(request: GoogleLoginRequest, signal?: AbortSignal): Promise<LoginResponse> {
    signal?.throwIfAborted()
    const response = await apiClient.post<LoginResponse>('/auth/google-login', request, {
      withCredentials: true,
      _skipAuthRefresh: true,
      signal,
    })
    signal?.throwIfAborted()
    clearMockExamSession()
    return persistSession(response.data)
  },

  async createGoogleChallenge(signal?: AbortSignal): Promise<{ nonce: string; expiresIn: number }> {
    const { data } = await apiClient.post<{ nonce: string; expiresIn: number }>(
      '/auth/google-challenge',
      null,
      { withCredentials: true, _skipAuthRefresh: true, signal },
    )
    if (
      typeof data.nonce !== 'string' ||
      !/^[A-Za-z0-9_-]{43}$/.test(data.nonce) ||
      !Number.isFinite(data.expiresIn) ||
      data.expiresIn <= 0 ||
      data.expiresIn > 300
    )
      throw new Error('Phản hồi phiên đăng nhập Google không hợp lệ.')
    return data
  },

  async changePassword(request: { currentPassword: string; newPassword: string }): Promise<void> {
    const response = await apiClient.post<{
      accessToken: string
      refreshToken?: string
      tokenType: string
      expiresIn: number
      mustChangePassword?: boolean
      message?: string
    }>('/auth/change-password', request)

    rotateSessionEpoch()
    sessionStorage.setItem('access_token', response.data.accessToken)
    sessionStorage.removeItem('refresh_token')
    const currentUser = this.getCurrentUser()
    if (currentUser) {
      const updatedUser: User = { ...currentUser, mustChangePassword: false }
      sessionStorage.setItem('user_profile', JSON.stringify(updatedUser))
    }
  },

  async refreshToken(): Promise<LoginResponse> {
    const epoch = getSessionEpoch()
    const response = await browserAuthSession.withRefreshLock(() =>
      apiClient.post<RefreshTokenResponse>(
        '/auth/refresh-token',
        {},
        { _skipAuthRefresh: true, withCredentials: true },
      ),
    )

    if (!isSessionEpochCurrent(epoch)) {
      throw new Error('Phiên đăng nhập đã thay đổi trong khi làm mới token')
    }
    if (!response.data.accessToken) {
      throw new Error('Backend không trả về token mới hợp lệ')
    }

    const user = this.getCurrentUser()
    if (!user) throw new Error('Không có hồ sơ người dùng')
    sessionStorage.setItem('access_token', response.data.accessToken)
    sessionStorage.removeItem('refresh_token')
    return normalizeResponse({ ...response.data, user })
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

  async restoreSession(isCurrent: () => boolean = () => true): Promise<RestoredAuthSession | null> {
    const epoch = getSessionEpoch()
    let response
    try {
      response = await browserAuthSession.withRefreshLock(() =>
        apiClient.post<RefreshTokenResponse>(
          '/auth/refresh-token',
          {},
          { _skipAuthRefresh: true, _silentAuthFailure: true, withCredentials: true },
        ),
      )
    } catch (error) {
      const status = readFailureStatus(error)
      if (status === 400 || status === 401 || status === 403 || status === 404 || status === 409) {
        return null
      }
      throw error
    }

    if (!isCurrent() || !isSessionEpochCurrent(epoch)) return null
    if (!response.data.accessToken) {
      throw new Error('Backend không trả về access token hợp lệ')
    }

    sessionStorage.setItem('access_token', response.data.accessToken)
    sessionStorage.removeItem('refresh_token')
    let profile: CurrentUserResponse
    try {
      profile = await this.fetchCurrentUser({
        _skipAuthRefresh: true,
        _silentAuthFailure: true,
      })
    } catch (error) {
      if (this.getToken() === response.data.accessToken) {
        sessionStorage.removeItem('access_token')
      }
      const status = readFailureStatus(error)
      if (status === 400 || status === 401 || status === 403 || status === 404) {
        return null
      }
      throw error
    }
    if (
      !isCurrent() ||
      !isSessionEpochCurrent(epoch) ||
      this.getToken() !== response.data.accessToken
    ) {
      if (this.getToken() === response.data.accessToken) {
        sessionStorage.removeItem('access_token')
      }
      return null
    }

    const user = toUser(profile)
    sessionStorage.setItem('user_profile', JSON.stringify(user))
    return { accessToken: response.data.accessToken, user }
  },

  async logout(): Promise<void> {
    const accessToken = this.getToken()
    // Invalidate locally before network I/O, including any pending refresh response.
    this.clearSession()
    try {
      await apiClient.post(
        '/auth/sign-out',
        {},
        {
          _skipAuthRefresh: true,
          withCredentials: true,
          headers: accessToken ? { Authorization: `Bearer ${accessToken}` } : undefined,
        },
      )
    } catch {
      // Local logout already completed; never clear a newer login after this request.
    }
  },

  clearSession(): void {
    rotateSessionEpoch()
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
      const parsed = safeJsonParse<(Partial<User> & { role?: unknown }) | null>(userStr, null)
      if (!parsed) return null
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
      // /me can be newer than the issued JWT. Do not replace its verified flag
      // with an older token claim; legacy profiles without a flag may fall back.
      const mustChangePassword =
        typeof parsed.mustChangePassword === 'boolean'
          ? parsed.mustChangePassword
          : Boolean(jwtMustChange)
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
