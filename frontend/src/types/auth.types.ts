/**
 * Role-Based Access Control (RBAC) User Roles used by the portals.
 * Backend returns lowercase roles; the client maps them onto these three portals.
 */
export type UserRole = 'Student' | 'Instructor' | 'DepartmentHead' | 'Proctor' | 'Admin'

/**
 * Authenticated User Profile Contract
 */
export interface User {
  id: string
  email: string
  fullName: string
  role: UserRole
  studentCode?: string
  createdAt?: string
}

/**
 * Login Request Payload
 */
export interface GoogleLoginRequest {
  idToken: string
}

export interface LoginRequest {
  email: string
  password: string
}

/**
 * Login Response Payload matching Backend API Contract
 */
export interface LoginResponse {
  accessToken: string
  refreshToken: string
  tokenType: string
  expiresIn: number
  user: User
}

/**
 * Google login response from POST /auth/google-login
 */
export interface GoogleLoginResponse {
  accessToken: string
  refreshToken: string
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
  refreshToken: string
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
  errors?: Record<string, string[]>
}
