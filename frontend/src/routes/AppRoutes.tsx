import React, { lazy, Suspense } from 'react'
import { Routes, Route, Navigate, Outlet } from 'react-router-dom'
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

function InstructorPageFallback() {
  return (
    <div className="flex min-h-[55vh] flex-col items-center justify-center" role="status">
      <span className="size-9 animate-spin rounded-full border-[3px] border-[#d6e1f4] border-t-[#245eea] motion-reduce:animate-none" />
      <span className="mt-3 text-[11px] font-semibold text-[#64758c]">
        Đang mở không gian giảng viên...
      </span>
    </div>
  )
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
          <Route element={<StudentLayout />}>
            <Route path="/student/overview" element={<StudentDashboardPage />} />
            <Route path="/practice" element={<PracticePage />} />
            <Route path="/practice/session" element={<PracticeSessionPage />} />
            <Route path="/mock-exam" element={<MockExamPage />} />
            <Route path="/mock-exam/session" element={<MockExamSessionPage />} />
            <Route path="/exam" element={<OfficialExamPage />} />
            <Route path="/results" element={<ExamHistoryPage />} />
          </Route>
        </Route>

        {/* Instructor, Department Head and Admin portal. Proctor has no standalone portal. */}
        <Route element={<ProtectedRoute allowedRoles={['Instructor', 'DepartmentHead', 'Admin']} />}>
          <Route element={<AppLayout />}>
            <Route
              element={
                <Suspense fallback={<InstructorPageFallback />}>
                  <Outlet />
                </Suspense>
              }
            >
              <Route path="/dashboard" element={<LecturerDashboardPage />} />
              <Route path="/question-bank" element={<QuestionStudioPage />} />
              <Route path="/question-bank/create" element={<CreateQuestionPage />} />
              <Route path="/lecturer/audit" element={<AuditEvidencePage />} />
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
