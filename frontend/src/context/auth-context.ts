import { createContext } from 'react'
import type { User, UserRole } from '@/types/auth.types'

export interface AuthContextType {
  user: User | null
  token: string | null
  isAuthenticated: boolean
  isLoading?: boolean
  authError?: string | null
  retryAuthentication?: () => void
  isStudent: boolean
  isInstructor: boolean
  isDepartmentHead: boolean
  isExaminationOfficer: boolean
  isProctor: boolean
  isAdmin: boolean
  loginWithPassword: (username: string, password: string) => Promise<void>
  loginWithGoogle: (idToken: string, signal?: AbortSignal) => Promise<void>
  changePassword: (currentPassword: string, newPassword: string) => Promise<void>
  logout: () => Promise<void>
  hasRole: (roles: UserRole | UserRole[]) => boolean
}

export const AuthContext = createContext<AuthContextType | undefined>(undefined)
