import { createContext } from 'react'
import type { LoginRequest, User, UserRole } from '@/types/auth.types'

export interface AuthContextType {
  user: User | null
  token: string | null
  isAuthenticated: boolean
  isStudent: boolean
  isInstructor: boolean
  isAdmin: boolean
  login: (credentials: LoginRequest) => Promise<void>
  logout: () => void
  hasRole: (roles: UserRole[]) => boolean
}

export const AuthContext = createContext<AuthContextType | undefined>(undefined)
