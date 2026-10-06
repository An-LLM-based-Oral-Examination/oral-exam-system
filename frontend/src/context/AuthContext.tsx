import React, { useState, type ReactNode } from 'react'
import type { User, LoginRequest, UserRole } from '@/types/auth.types'
import { authService } from '@/services/auth.service'
import { AuthContext } from '@/context/auth-context'

interface AuthState {
  user: User | null
  token: string | null
}

function getInitialAuthState(): AuthState {
  const user = authService.getCurrentUser()
  const token = authService.getToken()

  return user && token ? { user, token } : { user: null, token: null }
}

export const AuthProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [authState, setAuthState] = useState<AuthState>(getInitialAuthState)
  const { user, token } = authState

  const login = async (credentials: LoginRequest) => {
    const data = await authService.login(credentials)
    setAuthState({ user: data.user, token: data.accessToken })
  }

  const logout = () => {
    authService.logout()
    setAuthState({ user: null, token: null })
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
