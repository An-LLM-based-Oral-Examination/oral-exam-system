import { AppLayout } from '@/components/layout/AppLayout'
import { ExaminationOfficerLayout } from '@/layouts/ExaminationOfficerLayout'
import { useAuth } from '@/hooks/useAuth'
import { canonicalRole } from '@/routes/routeAccess'

export function ProfileRouteLayout() {
  const { user } = useAuth()
  const role = user ? canonicalRole(user.role) : null

  if (role === 'ExaminationOfficer') return <ExaminationOfficerLayout />
  return <AppLayout />
}
