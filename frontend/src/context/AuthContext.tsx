import React, { useCallback, useEffect, useRef, useState, type ReactNode } from 'react'
import type { User, UserRole } from '@/types/auth.types'
import { toast } from '@/components/common/toastStore'
import { ensureFreshAccessToken, getJwtExpiryMs } from '@/services/api.client'
import { authService } from '@/services/auth.service'
import { browserAuthSession } from '@/services/browserAuthSession'
import { AuthContext } from '@/context/auth-context'

const PROACTIVE_REFRESH_LEAD_SECONDS = 180
const PROACTIVE_REFRESH_LEAD_MS = PROACTIVE_REFRESH_LEAD_SECONDS * 1000
const ACTIVE_WINDOW_MS = 15 * 60 * 1000
const TRANSIENT_RETRY_MS = 30_000
const MAX_TRANSIENT_RETRIES = 3

function isWithinActiveExamRoute(): boolean {
  if (typeof window === 'undefined') return false
  const pathname = window.location.pathname
  return (
    /^\/(in-class-exam|mock-exam|practice|exam)(\/session)?$/.test(pathname) ||
    /^\/classes\/[^/]+/.test(pathname) ||
    /^\/lecturer\/classes\/[^/]+\/(?:in-class-shifts|shifts)\/[^/]+\/results/.test(pathname) ||
    /^\/in-class-shifts\/[^/]+\/results/.test(pathname)
  )
}

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
  const [isLoading, setIsLoading] = useState(true)
  const [authError, setAuthError] = useState<string | null>(null)
  const verificationGeneration = useRef(0)
  const lastActivityAtRef = useRef(0)
  const { user, token } = authState
  const invalidateVerification = useCallback(() => {
    ++verificationGeneration.current
  }, [])

  const retryAuthentication = useCallback(() => {
    const generation = ++verificationGeneration.current
    const current = () => generation === verificationGeneration.current
    setIsLoading(true)
    setAuthError(null)

    void (async () => {
      const cachedUser = authService.getCurrentUser()
      const cachedToken = authService.getToken()
      if (cachedToken && cachedUser && browserAuthSession.isIdentityAllowed(cachedUser.id)) {
        setAuthState({ user: null, token: cachedToken })
        const verifiedUser = await authService.synchronizeCurrentUser(current)
        if (!current()) return
        if (!verifiedUser) throw new Error('Phiên đăng nhập đã thay đổi. Vui lòng thử lại.')
        setAuthState({ user: verifiedUser, token: authService.getToken() })
        setIsLoading(false)
        return
      }

      if (cachedToken || cachedUser) authService.clearSession()
      setAuthState({ user: null, token: null })
      const restored = await authService.restoreSession(current)
      if (!current()) return
      if (!restored) {
        setIsLoading(false)
        return
      }

      browserAuthSession.activateIdentity(restored.user.id)
      setAuthState({ user: restored.user, token: restored.accessToken })
      setIsLoading(false)
    })().catch(() => {
      if (!current()) return
      authService.clearSession()
      setAuthState({ user: null, token: null })
      setIsLoading(false)
      setAuthError(null)
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

    const handleTokenRefreshed = (event: Event) => {
      const nextToken =
        event instanceof CustomEvent &&
        typeof (event.detail as { token?: unknown } | null)?.token === 'string'
          ? (event.detail as { token: string }).token
          : authService.getToken()
      if (!nextToken) return
      setAuthState((prev) => {
        if (!prev.user || prev.token === nextToken) return prev
        return { ...prev, token: nextToken }
      })
    }

    window.addEventListener('auth:session-expired', handleSessionExpired)
    window.addEventListener('auth:must-change-password', handleMustChangePassword)
    window.addEventListener('auth:token-refreshed', handleTokenRefreshed)

    const unsubscribeBrowserAuth = browserAuthSession.subscribe((event) => {
      const activeUser = authService.getCurrentUser()
      if (event.type === 'LOGOUT') {
        if (!activeUser || event.userId !== activeUser.id) return
        ++verificationGeneration.current
        authService.clearSession()
        setAuthState({ user: null, token: null })
        setIsLoading(false)
        setAuthError(null)
        toast.warning('Bạn đã đăng xuất ở một tab khác.')
        return
      }

      if (activeUser?.id === event.userId) return
      ++verificationGeneration.current
      authService.clearSession()
      setAuthState({ user: null, token: null })
      setIsLoading(true)
      setAuthError(null)
      retryAuthentication()
      if (activeUser) toast.warning('Phiên đăng nhập đã chuyển sang tài khoản khác ở một tab khác.')
    })

    let mounted = true
    queueMicrotask(() => {
      if (mounted) retryAuthentication()
    })

    return () => {
      mounted = false
      invalidateVerification()
      window.removeEventListener('auth:session-expired', handleSessionExpired)
      window.removeEventListener('auth:must-change-password', handleMustChangePassword)
      window.removeEventListener('auth:token-refreshed', handleTokenRefreshed)
      unsubscribeBrowserAuth()
    }
  }, [retryAuthentication, invalidateVerification])

  const isAuthenticated = !!token && !!user
  const mustChangePassword = Boolean(user?.mustChangePassword)

  useEffect(() => {
    lastActivityAtRef.current = isAuthenticated ? Date.now() : 0
  }, [isAuthenticated])

  useEffect(() => {
    if (!isAuthenticated || !token || mustChangePassword) return

    const expMs = getJwtExpiryMs(token)
    if (expMs === null) return

    let disposed = false
    let retryTimerId: number | undefined
    let retryCount = 0

    const markActivity = () => {
      lastActivityAtRef.current = Date.now()
    }

    const isActiveOrInExam = () =>
      isWithinActiveExamRoute() || Date.now() - lastActivityAtRef.current <= ACTIVE_WINDOW_MS

    const attemptProactiveRefresh = async (forceIfIdle: boolean) => {
      if (disposed) return
      if (typeof navigator !== 'undefined' && navigator.onLine === false) return

      const currentToken = sessionStorage.getItem('access_token') ?? token
      const currentExpMs = getJwtExpiryMs(currentToken)
      if (currentExpMs === null || currentExpMs - Date.now() > PROACTIVE_REFRESH_LEAD_MS) {
        return
      }
      if (!forceIfIdle && !isActiveOrInExam()) {
        return
      }

      const nextToken = await ensureFreshAccessToken(PROACTIVE_REFRESH_LEAD_SECONDS)
      if (disposed || !nextToken) return

      if (nextToken !== currentToken) {
        retryCount = 0
        setAuthState((prev) => {
          if (!prev.user || prev.token === nextToken) return prev
          return { ...prev, token: nextToken }
        })
        return
      }

      if (currentExpMs > Date.now() && retryCount < MAX_TRANSIENT_RETRIES) {
        retryCount += 1
        if (retryTimerId !== undefined) window.clearTimeout(retryTimerId)
        retryTimerId = window.setTimeout(() => {
          retryTimerId = undefined
          void attemptProactiveRefresh(forceIfIdle)
        }, TRANSIENT_RETRY_MS)
      }
    }

    const delayMs = Math.max(1_000, expMs - Date.now() - PROACTIVE_REFRESH_LEAD_MS)
    const refreshTimerId = window.setTimeout(() => {
      void attemptProactiveRefresh(false)
    }, delayMs)

    const onVisibilityChange = () => {
      if (document.visibilityState !== 'visible') return
      markActivity()
      void attemptProactiveRefresh(true)
    }

    const onOnline = () => {
      void attemptProactiveRefresh(false)
    }

    window.addEventListener('pointerdown', markActivity, { passive: true })
    window.addEventListener('keydown', markActivity, { passive: true })
    window.addEventListener('touchstart', markActivity, { passive: true })
    window.addEventListener('scroll', markActivity, { passive: true })
    document.addEventListener('visibilitychange', onVisibilityChange)
    window.addEventListener('online', onOnline)

    return () => {
      disposed = true
      window.clearTimeout(refreshTimerId)
      if (retryTimerId !== undefined) window.clearTimeout(retryTimerId)
      window.removeEventListener('pointerdown', markActivity)
      window.removeEventListener('keydown', markActivity)
      window.removeEventListener('touchstart', markActivity)
      window.removeEventListener('scroll', markActivity)
      document.removeEventListener('visibilitychange', onVisibilityChange)
      window.removeEventListener('online', onOnline)
    }
  }, [isAuthenticated, token, mustChangePassword])

  const loginWithGoogle = useCallback(async (idToken: string, signal?: AbortSignal) => {
    const data = await authService.googleLogin({ idToken }, signal)
    signal?.throwIfAborted()
    ++verificationGeneration.current
    browserAuthSession.activateIdentity(data.user.id, 'google')
    setAuthState({ user: data.user, token: data.accessToken })
    setIsLoading(false)
    setAuthError(null)
  }, [])

  const loginWithPassword = useCallback(async (username: string, password: string) => {
    const data = await authService.passwordLogin({ username, password })
    ++verificationGeneration.current
    browserAuthSession.activateIdentity(data.user.id, 'password')
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

  const hasRole = (roles: UserRole | UserRole[]): boolean => {
    if (!user) return false
    const roleList = Array.isArray(roles) ? roles : [roles]
    return roleList.some(
      (r) => r === user.role || r.toLowerCase() === user.role.toLowerCase(),
    )
  }

  const isStudent = user?.role === 'Student'
  const isInstructor = user?.role === 'Instructor'
  const isDepartmentHead = user?.role === 'DepartmentHead'
  const isExaminationOfficer = hasRole('ExaminationOfficer')
  const isProctor = user?.role === 'Proctor'
  const isAdmin = user?.role === 'Admin'

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
        isExaminationOfficer,
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
