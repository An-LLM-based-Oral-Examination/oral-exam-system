import React from 'react'
import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '@/hooks/useAuth'
import type { UserRole } from '@/types/auth.types'

interface ProtectedRouteProps {
  allowedRoles?: UserRole[]
}

export const ProtectedRoute: React.FC<ProtectedRouteProps> = ({ allowedRoles }) => {
  const { isAuthenticated, user, hasRole } = useAuth()
  const location = useLocation()

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
