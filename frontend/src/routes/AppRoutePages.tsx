import { lazy } from 'react'
import { Navigate, useParams } from 'react-router-dom'

export const LecturerDashboardPage = lazy(() =>
  import('@/pages/lecturer/LecturerDashboardPage').then((module) => ({
    default: module.LecturerDashboardPage,
  })),
)
export const QuestionStudioPage = lazy(() =>
  import('@/pages/lecturer/QuestionStudioPage').then((module) => ({
    default: module.QuestionStudioPage,
  })),
)
export const CreateQuestionPage = lazy(() =>
  import('@/pages/lecturer/CreateQuestionPage').then((module) => ({
    default: module.CreateQuestionPage,
  })),
)
export const AuditEvidencePage = lazy(() =>
  import('@/pages/lecturer/AuditEvidencePage').then((module) => ({
    default: module.AuditEvidencePage,
  })),
)
export const OfficialExamManagementPage = lazy(() =>
  import('@/pages/lecturer/OfficialExamManagementPage').then((module) => ({
    default: module.OfficialExamManagementPage,
  })),
)
export const AppealManagementPage = lazy(() =>
  import('@/pages/lecturer/AppealManagementPage').then((module) => ({
    default: module.AppealManagementPage,
  })),
)
export const ProctorAttendancePage = lazy(() =>
  import('@/pages/proctor/ProctorAttendancePage').then((module) => ({
    default: module.ProctorAttendancePage,
  })),
)
export const DeadLetterQueuePage = lazy(() =>
  import('@/pages/lecturer/DeadLetterQueuePage').then((module) => ({
    default: module.DeadLetterQueuePage,
  })),
)
export const LecturerClassManagementPage = lazy(() =>
  import('@/pages/lecturer/LecturerClassManagementPage').then((module) => ({
    default: module.LecturerClassManagementPage,
  })),
)
export const LecturerClassDetailPage = lazy(() =>
  import('@/pages/lecturer/LecturerClassDetailPage').then((module) => ({
    default: module.LecturerClassDetailPage,
  })),
)
export const InClassShiftResultsPage = lazy(() =>
  import('@/pages/lecturer/InClassShiftResultsPage').then((module) => ({
    default: module.InClassShiftResultsPage,
  })),
)

export function InstructorPageFallback() {
  return (
    <div className="flex min-h-[55vh] flex-col items-center justify-center" role="status">
      <span className="size-9 animate-spin rounded-full border-[3px] border-[#82aedb]/55 border-t-[#82aedb]/55 motion-reduce:animate-none" />
      <span className="mt-3 text-sm font-semibold text-[#64758c]">
        Đang mở không gian giảng viên...
      </span>
    </div>
  )
}

export function InClassLiveRedirect() {
  const { shiftId = '' } = useParams()
  if (!shiftId) return <Navigate to="/in-class-shifts" replace />
  return <Navigate to={`/in-class-shifts/${shiftId}/results`} replace />
}
