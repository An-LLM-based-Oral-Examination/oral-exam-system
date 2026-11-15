import { AppLayout } from '@/components/layout/AppLayout'
import { Navigate } from 'react-router-dom'
import { ExaminationOfficerLayout } from '@/layouts/ExaminationOfficerLayout'
import { useAuth } from '@/hooks/useAuth'
import { canonicalRole } from '@/routes/routeAccess'

export function ProfileRouteLayout() {
  const { user } = useAuth()
  const role = user ? canonicalRole(user.role) : null

  if (role === 'Instructor') return <Navigate to="/lecturer/dashboard" replace />
  if (role === 'ExaminationOfficer') return <ExaminationOfficerLayout />
  return <AppLayout />
}
