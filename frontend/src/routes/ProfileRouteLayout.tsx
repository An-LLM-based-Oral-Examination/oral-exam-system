import { AppLayout } from '@/components/layout/AppLayout'
import { Navigate } from 'react-router-dom'
import { useAuth } from '@/hooks/useAuth'
import { canonicalRole, getRoleHomePath } from '@/routes/routeAccess'

export function ProfileRouteLayout() {
  const { user } = useAuth()
  const role = user ? canonicalRole(user.role) : null

  if (!user) return <Navigate to="/login" replace />
  if (role !== 'Student') return <Navigate to={getRoleHomePath(user.role)} replace />
  return <AppLayout />
}
