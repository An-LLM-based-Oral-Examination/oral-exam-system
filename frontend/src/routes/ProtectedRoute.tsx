import React from 'react'
import { Navigate, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '@/hooks/useAuth'
import type { UserRole } from '@/types/auth.types'

interface ProtectedRouteProps {
  allowedRoles?: UserRole[]
}

export const ProtectedRoute: React.FC<ProtectedRouteProps> = ({ allowedRoles }) => {
  const { isAuthenticated, user, hasRole, isLoading, authError, retryAuthentication, logout } =
    useAuth()
  const location = useLocation()
  const navigate = useNavigate()

  if (isLoading) {
    return (
      <div role="status" className="flex min-h-[50vh] items-center justify-center p-8 text-center text-sm font-medium text-[#0F2854]">
        Đang xác minh phiên đăng nhập…
      </div>
    )
  }

  if (authError) {
    const handleReturnToLogin = () => {
      void Promise.resolve(logout())
      navigate('/login', { replace: true })
    }

    return (
      <div role="alert" className="flex min-h-[60vh] items-center justify-center p-6">
        <div className="w-full max-w-md space-y-4 rounded-2xl border border-[#82aedb]/55 bg-white p-6 text-center shadow-[0_14px_32px_rgba(15,40,84,0.08)]">
          <p className="text-sm font-semibold text-[#0F2854]">{authError}</p>
          <div className="flex flex-wrap items-center justify-center gap-2.5">
            <button
              type="button"
              onClick={retryAuthentication}
              className="inline-flex min-h-10 items-center justify-center rounded-xl border border-[#82aedb] bg-white px-4 py-2 text-sm font-semibold text-[#1C4D8D] transition hover:bg-[#E7F3FB]"
            >
              Thử lại
            </button>
            <button
              type="button"
              onClick={handleReturnToLogin}
              className="inline-flex min-h-10 items-center justify-center rounded-xl bg-[#1C4D8D] px-4 py-2 text-sm font-semibold text-white transition hover:bg-[#0F2854]"
            >
              Đăng nhập lại
            </button>
          </div>
        </div>
      </div>
    )
  }

  if (!isAuthenticated || !user) {
    return <Navigate to="/login" replace />
  }

  // Force first-time password change if required
  if (user.mustChangePassword && location.pathname !== '/change-password') {
    return <Navigate to="/change-password" replace />
  }

  if (allowedRoles && !hasRole(allowedRoles)) {
    return <Navigate to="/unauthorized" replace />
  }

  return <Outlet />
}

export { ProtectedRoute as AuthGuard }
