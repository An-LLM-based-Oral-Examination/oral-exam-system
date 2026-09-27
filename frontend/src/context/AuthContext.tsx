import React, { createContext, useContext, useState, useEffect, type ReactNode } from 'react'
import type { User, UserRole, LoginRequest } from '@/types/auth.types'
import { authService } from '@/services/authService'

interface AuthContextType {
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

const AuthContext = createContext<AuthContextType | undefined>(undefined)

export const AuthProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<User | null>(null)
  const [token, setToken] = useState<string | null>(null)

  useEffect(() => {
    const cachedUser = authService.getCurrentUser()
    const cachedToken = authService.getToken()
    if (cachedUser && cachedToken) {
      setUser(cachedUser)
      setToken(cachedToken)
    }
  }, [])

  const login = async (credentials: LoginRequest) => {
    const data = await authService.login(credentials)
    setUser(data.user)
    setToken(data.accessToken)
  }

  const logout = () => {
    authService.logout()
    setUser(null)
    setToken(null)
  }

  const hasRole = (roles: UserRole[]): boolean => {
    if (!user) return false
    return roles.includes(user.role)
  }

  const isStudent = user?.role === 'Student'
  const isInstructor = user?.role === 'Instructor'
  const isAdmin = user?.role === 'Admin'
  const isAuthenticated = !!token && !!user

  return (
    <AuthContext.Provider
      value={{
        user,
        token,
        isAuthenticated,
        isStudent,
        isInstructor,
        isAdmin,
        login,
        logout,
        hasRole,
      }}
    >
      {children}
    </AuthContext.Provider>
  )
}

export const useAuth = (): AuthContextType => {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return context
}
