import React, { useCallback, useEffect, useState, type ReactNode } from 'react'
import type { User, UserRole } from '@/types/auth.types'
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

  useEffect(() => {
    const handleSessionExpired = () => {
      authService.clearSession()
      setAuthState({ user: null, token: null })
    }

    const handleMustChangePassword = () => {
      setAuthState((prev) => {
        if (!prev.user) return prev
        const updatedUser: User = { ...prev.user, mustChangePassword: true }
        sessionStorage.setItem('user_profile', JSON.stringify(updatedUser))
        return { ...prev, user: updatedUser }
      })
    }

    window.addEventListener('auth:session-expired', handleSessionExpired)
    window.addEventListener('auth:must-change-password', handleMustChangePassword)
    return () => {
      window.removeEventListener('auth:session-expired', handleSessionExpired)
      window.removeEventListener('auth:must-change-password', handleMustChangePassword)
    }
  }, [])

  const loginWithGoogle = useCallback(async (idToken: string) => {
    const data = await authService.googleLogin({ idToken })
    setAuthState({ user: data.user, token: data.accessToken })
  }, [])

  const loginWithPassword = useCallback(async (username: string, password: string) => {
    const data = await authService.passwordLogin({ username, password })
    setAuthState({ user: data.user, token: data.accessToken })
  }, [])

  const changePassword = useCallback(async (currentPassword: string, newPassword: string) => {
    await authService.changePassword({ currentPassword, newPassword })
    const currentUser = authService.getCurrentUser()
    const token = authService.getToken()
    setAuthState({ user: currentUser, token })
  }, [])

  const logout = async () => {
    await authService.logout()
    setAuthState({ user: null, token: null })
  }

  const hasRole = (roles: UserRole[]): boolean => {
    if (!user) return false
    return roles.includes(user.role)
  }

  const isStudent = user?.role === 'Student'
  const isInstructor = user?.role === 'Instructor'
  const isDepartmentHead = user?.role === 'DepartmentHead'
  const isProctor = user?.role === 'Proctor'
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
        isDepartmentHead,
        isProctor,
        isAdmin,
        loginWithPassword,
        loginWithGoogle,
        changePassword,
        logout,
        hasRole,
      }}
    >
      {children}
    </AuthContext.Provider>
  )
}
