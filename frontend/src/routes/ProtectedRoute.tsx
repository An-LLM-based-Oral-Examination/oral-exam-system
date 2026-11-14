import React from 'react'
import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '@/hooks/useAuth'
import type { UserRole } from '@/types/auth.types'

interface ProtectedRouteProps {
  allowedRoles?: UserRole[]
}

export const ProtectedRoute: React.FC<ProtectedRouteProps> = ({ allowedRoles }) => {
  const { isAuthenticated, user, hasRole, isLoading, authError, retryAuthentication } = useAuth()
  const location = useLocation()

  if (isLoading) return <div role="status" className="p-8 text-center">Đang xác minh phiên đăng nhập…</div>
  if (authError) return <div role="alert" className="p-8 text-center space-y-4">
    <p>{authError}</p>
    <button type="button" onClick={retryAuthentication} className="rounded border px-4 py-2">Thử lại</button>
  </div>

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
