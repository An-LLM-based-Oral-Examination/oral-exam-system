import { Navigate, useSearchParams } from 'react-router-dom'
import { AdminOverviewPage } from '@/routes/AppRoutePages'
import { ROUTE_PATHS } from '@/routes/routePaths'

const LEGACY_ADMIN_TAB_ROUTES: Record<string, string> = {
  users: ROUTE_PATHS.admin.users,
  semesters: ROUTE_PATHS.admin.semesters,
  courses: ROUTE_PATHS.admin.courses,
  configs: ROUTE_PATHS.admin.configs,
  notifications: ROUTE_PATHS.admin.notifications,
  audit: ROUTE_PATHS.admin.auditLogs,
}

export function AdminDashboardRouteEntry() {
  const [searchParams] = useSearchParams()
  const tab = searchParams.get('tab')
  if (tab && Object.prototype.hasOwnProperty.call(LEGACY_ADMIN_TAB_ROUTES, tab)) {
    return <Navigate to={LEGACY_ADMIN_TAB_ROUTES[tab]} replace />
  }
  return <AdminOverviewPage />
}
