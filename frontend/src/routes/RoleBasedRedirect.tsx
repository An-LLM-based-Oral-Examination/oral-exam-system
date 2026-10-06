import { Navigate } from 'react-router-dom'
import { useAuth } from '@/hooks/useAuth'

export function RoleBasedRedirect() {
  const { user } = useAuth()

  if (user?.role === 'Student') {
    return <Navigate to="/student/overview" replace />
  }

  return <Navigate to="/dashboard" replace />
}

export { RoleBasedRedirect as RoleHomeRedirect }
