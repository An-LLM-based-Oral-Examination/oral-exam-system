import { Navigate } from 'react-router-dom'
import { useAuth } from '@/hooks/useAuth'

export function RoleBasedRedirect() {
  const { user } = useAuth()

  if (user?.role === 'Student') {
    return <Navigate to="/student/overview" replace />
  }

  if (user?.role === 'Proctor') {
    return <Navigate to="/proctor/attendance" replace />
  }

  if (user?.role === 'ExaminationOfficer') {
    return <Navigate to="/official-exams/manage" replace />
  }

  if (
    user?.role === 'Instructor' ||
    user?.role === 'DepartmentHead' ||
    user?.role === 'Admin'
  ) {
    return <Navigate to="/dashboard" replace />
  }

  return <Navigate to="/unauthorized" replace />
}

export { RoleBasedRedirect as RoleHomeRedirect }
