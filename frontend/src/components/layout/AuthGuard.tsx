import React from 'react'
import { Navigate, Outlet } from 'react-router-dom'
import { useAuth } from '@/context/AuthContext'
import type { UserRole } from '@/types/auth.types'

interface AuthGuardProps {
  allowedRoles?: UserRole[]
}

export const AuthGuard: React.FC<AuthGuardProps> = ({ allowedRoles }) => {
  const { isAuthenticated, user, hasRole } = useAuth()

  if (!isAuthenticated || !user) {
    return <Navigate to="/login" replace />
  }

  if (allowedRoles && !hasRole(allowedRoles)) {
    return <Navigate to="/unauthorized" replace />
  }

  return <Outlet />
}
