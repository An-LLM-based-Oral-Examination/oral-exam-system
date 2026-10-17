import React, { lazy, Suspense } from 'react'
import { Routes, Route, Navigate, Outlet } from 'react-router-dom'
import { AppLayout } from '@/components/layout/AppLayout'
import { StudentLayout } from '@/components/layout/StudentLayout'
import { ProtectedRoute } from '@/routes/ProtectedRoute'
import { RoleBasedRedirect } from '@/routes/RoleBasedRedirect'

// Pages
import { LoginPage } from '@/pages/auth/LoginPage'
import { UnauthorizedPage } from '@/pages/auth/UnauthorizedPage'

const StudentDashboardPage = lazy(() =>
  import('@/pages/student/StudentDashboardPage').then((module) => ({
    default: module.StudentDashboardPage,
  })),
)
const PracticePage = lazy(() =>
  import('@/pages/student/PracticePage').then((module) => ({
    default: module.PracticePage,
  })),
)
const PracticeSessionPage = lazy(() =>
  import('@/pages/student/PracticeSessionPage').then((module) => ({
    default: module.PracticeSessionPage,
  })),
)
const MockExamPage = lazy(() =>
  import('@/pages/student/MockExamPage').then((module) => ({
    default: module.MockExamPage,
  })),
)
const MockExamSessionPage = lazy(() =>
  import('@/pages/student/MockExamSessionPage').then((module) => ({
    default: module.MockExamSessionPage,
  })),
)
const OfficialExamPage = lazy(() =>
  import('@/pages/student/OfficialExamPage').then((module) => ({
    default: module.OfficialExamPage,
  })),
)
const ExamHistoryPage = lazy(() =>
  import('@/pages/student/ExamHistoryPage').then((module) => ({
    default: module.ExamHistoryPage,
  })),
)

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
const DepartmentHeadPortalPage = lazy(() =>
  import('@/pages/department_head/DepartmentHeadPortalPage').then((module) => ({
    default: module.DepartmentHeadPortalPage,
  })),
)
const ProctorPortalPage = lazy(() =>
  import('@/pages/proctor/ProctorPortalPage').then((module) => ({
    default: module.ProctorPortalPage,
  })),
)
const AdminPortalPage = lazy(() =>
  import('@/pages/admin/AdminPortalPage').then((module) => ({
    default: module.AdminPortalPage,
  })),
)

function PortalPageFallback() {
  return (
    <div className="flex min-h-[55vh] flex-col items-center justify-center" role="status">
      <span className="size-9 animate-spin rounded-full border-[3px] border-[#FED7AA] border-t-[#F26F21] motion-reduce:animate-none" />
      <span className="mt-3 text-xs font-semibold text-[#475569]">
        Đang mở không gian làm việc...
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
          <Route
            path="/practice/session"
            element={
              <Suspense fallback={<PortalPageFallback />}>
                <PracticeSessionPage />
              </Suspense>
            }
          />
          <Route
            path="/mock-exam/session"
            element={
              <Suspense fallback={<PortalPageFallback />}>
                <MockExamSessionPage />
              </Suspense>
            }
          />
          <Route element={<StudentLayout />}>
            <Route
              element={
                <Suspense fallback={<PortalPageFallback />}>
                  <Outlet />
                </Suspense>
              }
            >
              <Route path="/student/overview" element={<StudentDashboardPage />} />
              <Route path="/practice" element={<PracticePage />} />
              <Route path="/mock-exam" element={<MockExamPage />} />
              <Route path="/exam" element={<OfficialExamPage />} />
              <Route path="/official-exam" element={<Navigate to="/exam" replace />} />
              <Route path="/results" element={<ExamHistoryPage />} />
            </Route>
          </Route>
        </Route>

        {/* Staff Portals — Tách biệt phân quyền RBAC từng vai trò */}
        <Route
          element={
            <ProtectedRoute allowedRoles={['Instructor', 'DepartmentHead', 'Proctor', 'Admin']} />
          }
        >
          <Route element={<AppLayout />}>
            <Route
              element={
                <Suspense fallback={<PortalPageFallback />}>
                  <Outlet />
                </Suspense>
              }
            >
              {/* Giảng viên & Trưởng Bộ môn (Trưởng BM có quyền tạo đề và chấm thi) */}
              <Route
                element={
                  <ProtectedRoute allowedRoles={['Instructor', 'DepartmentHead', 'Admin']} />
                }
              >
                <Route path="/dashboard" element={<LecturerDashboardPage />} />
                <Route path="/question-bank" element={<QuestionStudioPage />} />
                <Route path="/question-bank/create" element={<CreateQuestionPage />} />
                <Route path="/lecturer/audit" element={<AuditEvidencePage />} />
              </Route>

              {/* Trưởng Bộ môn */}
              <Route element={<ProtectedRoute allowedRoles={['DepartmentHead', 'Admin']} />}>
                <Route path="/department-head" element={<DepartmentHeadPortalPage />} />
              </Route>

              {/* Cán bộ Coi thi */}
              <Route element={<ProtectedRoute allowedRoles={['Proctor', 'Admin']} />}>
                <Route path="/proctor" element={<ProctorPortalPage />} />
              </Route>

              {/* Quản trị hệ thống */}
              <Route element={<ProtectedRoute allowedRoles={['Admin']} />}>
                <Route path="/admin" element={<AdminPortalPage />} />
              </Route>
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
