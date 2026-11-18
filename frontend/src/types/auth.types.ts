/**
 * Role-Based Access Control (RBAC) User Roles used by the portals.
 * Backend returns lowercase roles; the client maps them onto these three portals.
 */
export type UserRole =
  | 'Student'
  | 'Instructor'
  | 'Lecturer'
  | 'DepartmentHead'
  | 'ExaminationOfficer'
  | 'Proctor'
  | 'Admin'
  | 'student'
  | 'lecturer'
  | 'department_head'
  | 'examination_officer'
  | 'proctor'
  | 'admin'

/**
 * Authenticated User Profile Contract
 */
export interface User {
  id: string
  email: string
  fullName: string
  role: UserRole
  studentCode?: string
  username?: string
  isActive?: boolean
  createdAt?: string
  mustChangePassword?: boolean
}

/** Fresh database profile from GET /auth/me; never contains session secrets. */
export interface CurrentUserResponse {
  id: string
  email: string
  username: string | null
  fullName: string
  studentCode: string | null
  role: UserRole
  isActive: boolean
  mustChangePassword: boolean
}

/**
 * Login Request Payload
 */
export interface GoogleLoginRequest {
  idToken: string
}

export interface LoginRequest {
  username: string
  password: string
}

/**
 * Login Response Payload matching Backend API Contract
 */
export interface LoginResponse {
  accessToken: string
  refreshToken?: string
  tokenType: string
  expiresIn: number
  mustChangePassword?: boolean
  user: User
}

/**
 * Google login response from POST /auth/google-login
 */
export interface GoogleLoginResponse {
  accessToken: string
  refreshToken?: string
  tokenType: string
  expiresIn: number
  user: {
    id: string
    email: string
    fullName: string
    studentCode?: string | null
    role: string
  }
}

/**
 * Silent refresh response from POST /auth/refresh-token
 */
export interface RefreshTokenResponse {
  accessToken: string
  refreshToken?: string
  tokenType: string
  expiresIn: number
}

/**
 * RFC 7807 Standard ProblemDetails Error Response
 */
export interface ProblemDetails {
  type?: string
  title: string
  status: number
  detail: string
  instance?: string
  errorCode?: string
  code?: string
  traceId?: string
  errors?: Record<string, string[]>
}
