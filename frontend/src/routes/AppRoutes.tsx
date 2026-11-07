import React, { lazy, Suspense } from 'react'
import { Routes, Route, Navigate, Outlet, useParams } from 'react-router-dom'
import { AppLayout } from '@/components/layout/AppLayout'
import { StudentLayout } from '@/components/layout/StudentLayout'
import { ProtectedRoute } from '@/routes/ProtectedRoute'
import { RoleBasedRedirect } from '@/routes/RoleBasedRedirect'

// Pages
import { LoginPage } from '@/pages/auth/LoginPage'
import { UnauthorizedPage } from '@/pages/auth/UnauthorizedPage'
import { StudentDashboardPage } from '@/pages/student/StudentDashboardPage'
import { PracticePage } from '@/pages/student/PracticePage'
import { PracticeSessionPage } from '@/pages/student/PracticeSessionPage'
import { MockExamPage } from '@/pages/student/MockExamPage'
import { MockExamSessionPage } from '@/pages/student/MockExamSessionPage'
import { OfficialExamPage } from '@/pages/student/OfficialExamPage'
import { ExamHistoryPage } from '@/pages/student/ExamHistoryPage'
import { StudentInClassExamPage } from '@/pages/student/StudentInClassExamPage'
import { InClassExamSessionPage } from '@/pages/student/InClassExamSessionPage'

const LecturerDashboardPage = lazy(() =>
  import('@/pages/lecturer/LecturerDashboardPage').then((module) => ({
    default: module.LecturerDashboardPage,
  })),
)
const QuestionStudioPage = lazy(() =>
  import('@/pages/lecturer/QuestionStudioPage').then((module) => ({
    default: module.QuestionStudioPage,
  })),
)
const CreateQuestionPage = lazy(() =>
  import('@/pages/lecturer/CreateQuestionPage').then((module) => ({
    default: module.CreateQuestionPage,
  })),
)
const AuditEvidencePage = lazy(() =>
  import('@/pages/lecturer/AuditEvidencePage').then((module) => ({
    default: module.AuditEvidencePage,
  })),
)
const OfficialExamManagementPage = lazy(() =>
  import('@/pages/lecturer/OfficialExamManagementPage').then((module) => ({
    default: module.OfficialExamManagementPage,
  })),
)
const AppealManagementPage = lazy(() =>
  import('@/pages/lecturer/AppealManagementPage').then((module) => ({
    default: module.AppealManagementPage,
  })),
)
const ProctorAttendancePage = lazy(() =>
  import('@/pages/proctor/ProctorAttendancePage').then((module) => ({
    default: module.ProctorAttendancePage,
  })),
)
const DeadLetterQueuePage = lazy(() =>
  import('@/pages/lecturer/DeadLetterQueuePage').then((module) => ({
    default: module.DeadLetterQueuePage,
  })),
)
const LecturerClassManagementPage = lazy(() =>
  import('@/pages/lecturer/LecturerClassManagementPage').then((module) => ({
    default: module.LecturerClassManagementPage,
  })),
)
const InClassShiftHubPage = lazy(() =>
  import('@/pages/lecturer/InClassShiftHubPage').then((module) => ({
    default: module.InClassShiftHubPage,
  })),
)
const InClassShiftResultsPage = lazy(() =>
  import('@/pages/lecturer/InClassShiftResultsPage').then((module) => ({
    default: module.InClassShiftResultsPage,
  })),
)

function InstructorPageFallback() {
  return (
    <div className="flex min-h-[55vh] flex-col items-center justify-center" role="status">
      <span className="size-9 animate-spin rounded-full border-[3px] border-[#82aedb]/55 border-t-[#82aedb]/55 motion-reduce:animate-none" />
      <span className="mt-3 text-sm font-semibold text-[#64758c]">
        Đang mở không gian giảng viên...
      </span>
    </div>
  )
}

function InClassLiveRedirect() {
  const { shiftId = '' } = useParams()
  if (!shiftId) return <Navigate to="/in-class-shifts" replace />
  return <Navigate to={`/in-class-shifts/${shiftId}/results`} replace />
}

export const AppRoutes: React.FC = () => {
  return (
    <Routes>
      {/* Public Routes */}
      <Route path="/login" element={<LoginPage />} />
      <Route path="/unauthorized" element={<UnauthorizedPage />} />

      {/* Route each authenticated role to its own portal. */}
      <Route element={<ProtectedRoute />}>
        <Route path="/" element={<RoleBasedRedirect />} />

        {/* Student Portal */}
        <Route element={<ProtectedRoute allowedRoles={['Student']} />}>
          <Route path="/mock-exam/session" element={<MockExamSessionPage />} />
          <Route path="/in-class-exam/session" element={<InClassExamSessionPage />} />
          <Route element={<StudentLayout />}>
            <Route path="/student/overview" element={<StudentDashboardPage />} />
            <Route path="/classes" element={<Navigate to="/student/overview" replace />} />
            <Route path="/in-class-exam" element={<StudentInClassExamPage />} />
            <Route path="/practice" element={<PracticePage />} />
            <Route path="/practice/session" element={<PracticeSessionPage />} />
            <Route path="/mock-exam" element={<MockExamPage />} />
            <Route path="/exam" element={<OfficialExamPage />} />
            <Route path="/results" element={<ExamHistoryPage />} />
          </Route>
        </Route>

        {/* Instructor, Department Head and Admin portal. Proctor has no standalone portal. */}
        <Route
          element={<ProtectedRoute allowedRoles={['Instructor', 'DepartmentHead', 'Admin']} />}
        >
          <Route element={<AppLayout />}>
            <Route
              element={
                <Suspense fallback={<InstructorPageFallback />}>
                  <Outlet />
                </Suspense>
              }
            >
              <Route path="/dashboard" element={<LecturerDashboardPage />} />
              <Route path="/lecturer/classes" element={<LecturerClassManagementPage />} />
              <Route path="/question-bank" element={<QuestionStudioPage />} />
              <Route path="/question-bank/create" element={<CreateQuestionPage />} />
              <Route path="/in-class-shifts" element={<InClassShiftHubPage />} />
              <Route path="/in-class-shifts/:shiftId/results" element={<InClassShiftResultsPage />} />
              <Route path="/lecturer/audit" element={<AuditEvidencePage />} />
              <Route path="/official-exams/manage" element={<OfficialExamManagementPage />} />
              <Route path="/appeals" element={<AppealManagementPage />} />
            </Route>
          </Route>
          {/* Legacy live URL → bảng điểm (chiếu mã giờ là popup) */}
          <Route
            path="/in-class-shifts/:shiftId/live"
            element={<InClassLiveRedirect />}
          />
        </Route>

        {/* Proctor portal: attendance, seat monitor and Kiosk suspension. */}
        <Route element={<ProtectedRoute allowedRoles={['Proctor', 'Instructor', 'DepartmentHead', 'Admin']} />}>
          <Route element={<AppLayout />}>
            <Route
              element={
                <Suspense fallback={<InstructorPageFallback />}>
                  <Outlet />
                </Suspense>
              }
            >
              <Route path="/proctor/attendance" element={<ProctorAttendancePage />} />
            </Route>
          </Route>
        </Route>

        <Route element={<ProtectedRoute allowedRoles={['DepartmentHead', 'Admin']} />}>
          <Route element={<AppLayout />}>
            <Route
              element={
                <Suspense fallback={<InstructorPageFallback />}>
                  <Outlet />
                </Suspense>
              }
            >
              <Route path="/dlq" element={<DeadLetterQueuePage />} />
            </Route>
          </Route>
        </Route>
      </Route>

      {/* Fallback */}
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}

export default AppRoutes
