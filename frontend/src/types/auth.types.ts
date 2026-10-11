/**
 * Role-Based Access Control (RBAC) User Roles
 */
export type UserRole = 'Student' | 'Instructor' | 'Admin'

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
