import { Navigate } from 'react-router-dom'
import { AppLayout } from '@/components/layout/AppLayout'
import { useAuth } from '@/hooks/useAuth'
import type { UserRole } from '@/types/auth.types'
import { canonicalRole, type CanonicalRole } from '@/routes/routeAccess'

interface LegacyRoleRouteLayoutProps {
  redirects: Partial<Record<CanonicalRole, string>>
  allowedRoles?: UserRole[]
}

export function LegacyRoleRouteLayout({ redirects, allowedRoles }: LegacyRoleRouteLayoutProps) {
  const { user, hasRole } = useAuth()
  const normalizedRole = user ? canonicalRole(user.role) : null
  const redirectPath = normalizedRole ? redirects[normalizedRole] : undefined

  if (redirectPath) {
    return <Navigate to={redirectPath} replace />
  }

  if (!normalizedRole || !allowedRoles || !hasRole(allowedRoles)) {
    return <Navigate to="/unauthorized" replace />
  }

  return <AppLayout />
}
