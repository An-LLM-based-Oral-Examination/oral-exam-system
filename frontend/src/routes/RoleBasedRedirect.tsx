import { Navigate } from 'react-router-dom'
import { useAuth } from '@/hooks/useAuth'
import { getRoleHomePath } from '@/routes/routeAccess'

export function RoleBasedRedirect() {
  const { user, isLoading } = useAuth()

  if (isLoading) return null

  if (user?.mustChangePassword) {
    return <Navigate to="/change-password" replace />
  }

  if (!user) return <Navigate to="/login" replace />

  return <Navigate to={getRoleHomePath(user.role)} replace />
}

export { RoleBasedRedirect as RoleHomeRedirect }
