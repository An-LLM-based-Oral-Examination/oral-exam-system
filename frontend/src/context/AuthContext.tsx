import React, { useCallback, useEffect, useState, type ReactNode } from 'react'
import type { User, UserRole } from '@/types/auth.types'
import { toast } from '@/components/common/toastStore'
import { authService } from '@/services/auth.service'
import { browserAuthSession } from '@/services/browserAuthSession'
import { AuthContext } from '@/context/auth-context'

interface AuthState {
  user: User | null
  token: string | null
}

function getInitialAuthState(): AuthState {
  const user = authService.getCurrentUser()
  const token = authService.getToken()

  if (!user || !token) return { user: null, token: null }
  if (!browserAuthSession.isIdentityAllowed(user.id)) return { user: null, token: null }
  return { user, token }
}

export const AuthProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [authState, setAuthState] = useState<AuthState>(getInitialAuthState)
  const { user, token } = authState

  useEffect(() => {
    const handleSessionExpired = () => {
      const currentUser = authService.getCurrentUser()
      authService.clearSession()
      if (currentUser) browserAuthSession.deactivateIdentity(currentUser.id)
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

    const unsubscribeBrowserAuth = browserAuthSession.subscribe((event) => {
      const activeUser = authService.getCurrentUser()
      if (!activeUser) return

      const accountChanged = event.type === 'LOGIN' && event.userId !== activeUser.id
      const accountLoggedOut = event.type === 'LOGOUT' && event.userId === activeUser.id
      if (!accountChanged && !accountLoggedOut) return

      setAuthState({ user: null, token: null })
      void authService.logout()
      toast.warning(
        accountChanged
          ? 'Phiên đăng nhập đã chuyển sang tài khoản khác ở một tab khác.'
          : 'Bạn đã đăng xuất ở một tab khác.',
      )
    })

    const currentUser = authService.getCurrentUser()
    if (currentUser && !browserAuthSession.claimIdentity(currentUser.id)) {
      queueMicrotask(() => {
        authService.clearSession()
        setAuthState({ user: null, token: null })
        toast.warning('Phiên đăng nhập đang được sử dụng bởi tài khoản khác trong trình duyệt này.')
      })
    }

    return () => {
      window.removeEventListener('auth:session-expired', handleSessionExpired)
      window.removeEventListener('auth:must-change-password', handleMustChangePassword)
      unsubscribeBrowserAuth()
    }
  }, [])

  useEffect(() => {
    if (!user?.mustChangePassword || !sessionStorage.getItem('refresh_token')) return

    let active = true
    void authService
      .refreshToken()
      .then((refreshed) => {
        if (!active) return
        setAuthState({ user: refreshed.user, token: refreshed.accessToken })
      })
      .catch(() => {
        // Giữ nguyên trạng thái hiện tại nếu không làm mới được token
      })

    return () => {
      active = false
    }
  }, [user?.id, user?.mustChangePassword])

  const loginWithGoogle = useCallback(async (idToken: string) => {
    const data = await authService.googleLogin({ idToken })
    browserAuthSession.activateIdentity(data.user.id)
    setAuthState({ user: data.user, token: data.accessToken })
  }, [])

  const loginWithPassword = useCallback(async (username: string, password: string) => {
    const data = await authService.passwordLogin({ username, password })
    browserAuthSession.activateIdentity(data.user.id)
    setAuthState({ user: data.user, token: data.accessToken })
  }, [])

  const changePassword = useCallback(async (currentPassword: string, newPassword: string) => {
    await authService.changePassword({ currentPassword, newPassword })
    const currentUser = authService.getCurrentUser()
    const token = authService.getToken()
    setAuthState({ user: currentUser, token })
  }, [])

  const logout = async () => {
    const currentUser = authService.getCurrentUser()
    setAuthState({ user: null, token: null })
    if (currentUser) browserAuthSession.deactivateIdentity(currentUser.id)
    await authService.logout()
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
