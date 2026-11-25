import { lazy } from 'react'
import { Navigate, useParams } from 'react-router-dom'
import { LoadingState } from '@/components/common/LoadingState'

const CHUNK_RELOAD_STORAGE_KEY = 'oralexam:chunk-reload-attempted'

function isDynamicImportError(error: unknown): boolean {
  if (!(error instanceof Error)) return false
  const message = error.message.toLowerCase()
  return (
    message.includes('failed to fetch dynamically imported module') ||
    message.includes('importing a module script failed') ||
    message.includes('error loading dynamically imported module') ||
    message.includes('unable to preload css')
  )
}

function withChunkRetry<T>(factory: () => Promise<T>): () => Promise<T> {
  return async () => {
    try {
      const loaded = await factory()
      if (typeof window !== 'undefined') {
        try {
          window.sessionStorage.removeItem(CHUNK_RELOAD_STORAGE_KEY)
        } catch {
          // Ignore storage access errors
        }
      }
      return loaded
    } catch (error) {
      if (typeof window !== 'undefined' && isDynamicImportError(error)) {
        try {
          const alreadyAttempted = window.sessionStorage.getItem(CHUNK_RELOAD_STORAGE_KEY)
          if (!alreadyAttempted) {
            window.sessionStorage.setItem(CHUNK_RELOAD_STORAGE_KEY, '1')
            window.location.reload()
            return new Promise<T>(() => {})
          }
          window.sessionStorage.removeItem(CHUNK_RELOAD_STORAGE_KEY)
        } catch {
          // Ignore storage access errors and rethrow
        }
      }
      throw error
    }
  }
}

export const QuestionReviewRequestsPage = lazy(
  withChunkRetry(() => import('@/pages/department-head/QuestionReviewRequestsPage')),
)
export const DepartmentLecturerAssignmentPage = lazy(
  withChunkRetry(() => import('@/pages/lecturer/DepartmentLecturerAssignmentPage')),
)
export const DepartmentCourseClosPage = lazy(
  withChunkRetry(() => import('@/pages/lecturer/DepartmentCourseClosPage')),
)
export const DepartmentCoursesPage = lazy(
  withChunkRetry(() => import('@/pages/lecturer/DepartmentCoursesPage')),
)
export const DepartmentCourseDetailPage = lazy(
  withChunkRetry(() => import('@/pages/lecturer/DepartmentCourseDetailPage')),
)

export const LecturerDashboardPage = lazy(
  withChunkRetry(() =>
    import('@/pages/lecturer/LecturerDashboardPage').then((module) => ({
      default: module.LecturerDashboardPage,
    })),
  ),
)
export const QuestionStudioPage = lazy(
  withChunkRetry(() =>
    import('@/pages/lecturer/QuestionStudioPage').then((module) => ({
      default: module.QuestionStudioPage,
    })),
  ),
)
export const CreateQuestionPage = lazy(
  withChunkRetry(() =>
    import('@/pages/lecturer/CreateQuestionPage').then((module) => ({
      default: module.CreateQuestionPage,
    })),
  ),
)
export const AuditEvidencePage = lazy(
  withChunkRetry(() =>
    import('@/pages/lecturer/AuditEvidencePage').then((module) => ({
      default: module.AuditEvidencePage,
    })),
  ),
)
export const OfficialExamManagementPage = lazy(
  withChunkRetry(() =>
    import('@/pages/lecturer/OfficialExamManagementPage').then((module) => ({
      default: module.OfficialExamManagementPage,
    })),
  ),
)
export const AppealManagementPage = lazy(
  withChunkRetry(() =>
    import('@/pages/lecturer/AppealManagementPage').then((module) => ({
      default: module.AppealManagementPage,
    })),
  ),
)
export const ProctorAttendancePage = lazy(
  withChunkRetry(() =>
    import('@/pages/proctor/ProctorAttendancePage').then((module) => ({
      default: module.ProctorAttendancePage,
    })),
  ),
)
export const DeadLetterQueuePage = lazy(
  withChunkRetry(() =>
    import('@/pages/lecturer/DeadLetterQueuePage').then((module) => ({
      default: module.DeadLetterQueuePage,
    })),
  ),
)
export const LecturerClassManagementPage = lazy(
  withChunkRetry(() =>
    import('@/pages/lecturer/LecturerClassManagementPage').then((module) => ({
      default: module.LecturerClassManagementPage,
    })),
  ),
)
export const LecturerClassDetailPage = lazy(
  withChunkRetry(() =>
    import('@/pages/lecturer/LecturerClassDetailPage').then((module) => ({
      default: module.LecturerClassDetailPage,
    })),
  ),
)
export const InClassShiftResultsPage = lazy(
  withChunkRetry(() =>
    import('@/pages/lecturer/InClassShiftResultsPage').then((module) => ({
      default: module.InClassShiftResultsPage,
    })),
  ),
)
export const AdminOverviewPage = lazy(
  withChunkRetry(() =>
    import('@/pages/admin/AdminOverviewPage').then((module) => ({
      default: module.AdminOverviewPage,
    })),
  ),
)
export const AdminUsersPage = lazy(
  withChunkRetry(() =>
    import('@/pages/admin/AdminUsersPage').then((module) => ({
      default: module.AdminUsersPage,
    })),
  ),
)
export const AdminSemestersPage = lazy(
  withChunkRetry(() =>
    import('@/pages/admin/AdminSemestersPage').then((module) => ({
      default: module.AdminSemestersPage,
    })),
  ),
)
export const AdminCoursesPage = lazy(
  withChunkRetry(() =>
    import('@/pages/admin/AdminCoursesPage').then((module) => ({
      default: module.AdminCoursesPage,
    })),
  ),
)
export const AdminConfigsPage = lazy(
  withChunkRetry(() =>
    import('@/pages/admin/AdminConfigsPage').then((module) => ({
      default: module.AdminConfigsPage,
    })),
  ),
)
export const AdminNotificationsPage = lazy(
  withChunkRetry(() =>
    import('@/pages/admin/AdminNotificationsPage').then((module) => ({
      default: module.AdminNotificationsPage,
    })),
  ),
)
export const AdminAuditLogsPage = lazy(
  withChunkRetry(() =>
    import('@/pages/admin/AdminAuditLogsPage').then((module) => ({
      default: module.AdminAuditLogsPage,
    })),
  ),
)
export const DepartmentHeadDashboardPage = lazy(
  withChunkRetry(() =>
    import('@/pages/department-head/DepartmentHeadDashboardPage').then((module) => ({
      default: module.DepartmentHeadDashboardPage,
    })),
  ),
)

export const ExaminationOfficerDashboardPage = lazy(
  withChunkRetry(() =>
    import('@/pages/examination-officer/ExaminationOfficerDashboardPage').then((module) => ({
      default: module.ExaminationOfficerDashboardPage,
    })),
  ),
)
export const ExaminationOfficerExamListPage = lazy(
  withChunkRetry(() =>
    import('@/pages/examination-officer/ExaminationOfficerExamListPage').then((module) => ({
      default: module.ExaminationOfficerExamListPage,
    })),
  ),
)
export const ExaminationOfficerExamDetailPage = lazy(
  withChunkRetry(() =>
    import('@/pages/examination-officer/ExaminationOfficerExamDetailPage').then((module) => ({
      default: module.ExaminationOfficerExamDetailPage,
    })),
  ),
)
export const ExaminationOfficerTicketAssignmentPage = lazy(
  withChunkRetry(() =>
    import('@/pages/examination-officer/ExaminationOfficerTicketAssignmentPage').then((module) => ({
      default: module.ExaminationOfficerTicketAssignmentPage,
    })),
  ),
)
export const ExaminationOfficerRosterImportPage = lazy(
  withChunkRetry(() =>
    import('@/pages/examination-officer/ExaminationOfficerRosterImportPage').then((module) => ({
      default: module.ExaminationOfficerRosterImportPage,
    })),
  ),
)
export const ExaminationOfficerMonitorSelectorPage = lazy(
  withChunkRetry(() =>
    import('@/pages/examination-officer/ExaminationOfficerMonitorSelectorPage').then((module) => ({
      default: module.ExaminationOfficerMonitorSelectorPage,
    })),
  ),
)
export const ExaminationOfficerShiftMonitorPage = lazy(
  withChunkRetry(() =>
    import('@/pages/examination-officer/ExaminationOfficerShiftMonitorPage').then((module) => ({
      default: module.ExaminationOfficerShiftMonitorPage,
    })),
  ),
)
export const ExaminationOfficerGradeSelectorPage = lazy(
  withChunkRetry(() =>
    import('@/pages/examination-officer/ExaminationOfficerGradeSelectorPage').then((module) => ({
      default: module.ExaminationOfficerGradeSelectorPage,
    })),
  ),
)
export const ExaminationOfficerGradePublicationPage = lazy(
  withChunkRetry(() =>
    import('@/pages/examination-officer/ExaminationOfficerGradePublicationPage').then((module) => ({
      default: module.ExaminationOfficerGradePublicationPage,
    })),
  ),
)
export const ExaminationOfficerDlqPage = lazy(
  withChunkRetry(() =>
    import('@/pages/examination-officer/ExaminationOfficerDlqPage').then((module) => ({
      default: module.ExaminationOfficerDlqPage,
    })),
  ),
)
export const GradeVerificationPage = lazy(
  withChunkRetry(() =>
    import('@/pages/public/GradeVerificationPage').then((module) => ({
      default: module.GradeVerificationPage,
    })),
  ),
)

export function PortalPageFallback({
  label = 'Đang tải không gian làm việc…',
}: {
  label?: string
}) {
  return <LoadingState label={label} className="min-h-[55vh]" />
}

export { PortalPageFallback as InstructorPageFallback }

export function InClassLiveRedirect() {
  const { shiftId = '' } = useParams()
  if (!shiftId) return <Navigate to="/in-class-shifts" replace />
  return <Navigate to={`/in-class-shifts/${shiftId}/results`} replace />
}
