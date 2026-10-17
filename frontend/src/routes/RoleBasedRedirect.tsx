import { Navigate } from 'react-router-dom'
import { useAuth } from '@/hooks/useAuth'

export function RoleBasedRedirect() {
  const { user } = useAuth()

  if (user?.role === 'Student') {
    return <Navigate to="/student/overview" replace />
  }

  if (user?.role === 'Instructor') {
    return <Navigate to="/dashboard" replace />
  }

  if (user?.role === 'DepartmentHead') {
    return <Navigate to="/department-head" replace />
  }

  if (user?.role === 'Proctor') {
    return <Navigate to="/proctor" replace />
  }

  if (user?.role === 'Admin') {
    return <Navigate to="/admin" replace />
  }

  return <Navigate to="/unauthorized" replace />
}

export { RoleBasedRedirect as RoleHomeRedirect }
