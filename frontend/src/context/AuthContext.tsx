import React, { useCallback, useEffect, useRef, useState, type ReactNode } from 'react'
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
  // A cached profile is a lookup hint, never authorization evidence.
  return { user: null, token }
}

export const AuthProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [authState, setAuthState] = useState<AuthState>(getInitialAuthState)
  const [isLoading, setIsLoading] = useState(() => Boolean(authService.getToken()))
  const [authError, setAuthError] = useState<string | null>(null)
  const verificationGeneration = useRef(0)
  const { user, token } = authState
  const invalidateVerification = useCallback(() => { ++verificationGeneration.current }, [])

  const retryAuthentication = useCallback(() => {
    const generation = ++verificationGeneration.current
    const current = () => generation === verificationGeneration.current
    const cachedUser = authService.getCurrentUser()
    if (!authService.getToken() || !cachedUser || !browserAuthSession.isIdentityAllowed(cachedUser.id)) {
      setAuthState({ user: null, token: null })
      setIsLoading(false)
      setAuthError(null)
      return
    }
    setIsLoading(true)
    setAuthError(null)
    setAuthState({ user: null, token: authService.getToken() })
    void authService.synchronizeCurrentUser(current).then((verifiedUser) => {
      if (!current()) return
      if (!verifiedUser) throw new Error('Phiên đăng nhập đã thay đổi. Vui lòng thử lại.')
      setAuthState({ user: verifiedUser, token: authService.getToken() })
      setIsLoading(false)
    }).catch(() => {
      if (!current()) return
      setAuthState({ user: null, token: authService.getToken() })
      setIsLoading(false)
      setAuthError('Không thể xác minh phiên đăng nhập với máy chủ. Vui lòng kiểm tra kết nối và thử lại.')
    })
  }, [])

  useEffect(() => {
    const handleSessionExpired = () => {
      ++verificationGeneration.current
      const currentUser = authService.getCurrentUser()
      authService.clearSession()
      if (currentUser) browserAuthSession.deactivateIdentity(currentUser.id)
      setAuthState({ user: null, token: null })
      setIsLoading(false)
      setAuthError(null)
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

      ++verificationGeneration.current
      setAuthState({ user: null, token: null })
      setIsLoading(false)
      setAuthError(null)
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
        ++verificationGeneration.current
        authService.clearSession()
        setAuthState({ user: null, token: null })
        setIsLoading(false)
        toast.warning('Phiên đăng nhập đang được sử dụng bởi tài khoản khác trong trình duyệt này.')
      })
    }

    let mounted = true
    queueMicrotask(() => { if (mounted) retryAuthentication() })

    return () => {
      mounted = false
      invalidateVerification()
      window.removeEventListener('auth:session-expired', handleSessionExpired)
      window.removeEventListener('auth:must-change-password', handleMustChangePassword)
      unsubscribeBrowserAuth()
    }
  }, [retryAuthentication, invalidateVerification])

  const loginWithGoogle = useCallback(async (idToken: string, signal?: AbortSignal) => {
    const data = await authService.googleLogin({ idToken }, signal)
    signal?.throwIfAborted()
    ++verificationGeneration.current
    browserAuthSession.activateIdentity(data.user.id)
    setAuthState({ user: data.user, token: data.accessToken })
    setIsLoading(false)
    setAuthError(null)
  }, [])

  const loginWithPassword = useCallback(async (username: string, password: string) => {
    const data = await authService.passwordLogin({ username, password })
    ++verificationGeneration.current
    browserAuthSession.activateIdentity(data.user.id)
    setAuthState({ user: data.user, token: data.accessToken })
    setIsLoading(false)
    setAuthError(null)
  }, [])

  const changePassword = useCallback(async (currentPassword: string, newPassword: string) => {
    await authService.changePassword({ currentPassword, newPassword })
    const currentUser = authService.getCurrentUser()
    const token = authService.getToken()
    setAuthState({ user: currentUser, token })
  }, [])

  const logout = async () => {
    ++verificationGeneration.current
    const currentUser = authService.getCurrentUser()
    setAuthState({ user: null, token: null })
    setIsLoading(false)
    setAuthError(null)
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
        isLoading,
        authError,
        retryAuthentication,
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
