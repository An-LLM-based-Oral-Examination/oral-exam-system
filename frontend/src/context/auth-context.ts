import { createContext } from 'react'
import type { User, UserRole } from '@/types/auth.types'

export interface AuthContextType {
  user: User | null
  token: string | null
  isAuthenticated: boolean
  isStudent: boolean
  isInstructor: boolean
  isDepartmentHead: boolean
  isProctor: boolean
  isAdmin: boolean
  loginWithGoogle: (idToken: string) => Promise<void>
  loginWithPassword: (username: string, password: string) => Promise<void>
  changePassword: (currentPassword: string, newPassword: string) => Promise<void>
  logout: () => Promise<void>
  hasRole: (roles: UserRole[]) => boolean
}

export const AuthContext = createContext<AuthContextType | undefined>(undefined)
