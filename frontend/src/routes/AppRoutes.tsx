import { Suspense } from 'react'
import {
  Navigate,
  Outlet,
  Route,
  createRoutesFromElements,
} from 'react-router-dom'
import { AppLayout } from '@/components/layout/AppLayout'
import { StudentLayout } from '@/components/layout/StudentLayout'
import { ProtectedRoute } from '@/routes/ProtectedRoute'
import { RoleBasedRedirect } from '@/routes/RoleBasedRedirect'
import {
  AdminDashboardPage,
  AppealManagementPage,
  AuditEvidencePage,
  CreateQuestionPage,
  DeadLetterQueuePage,
  InClassLiveRedirect,
  InClassShiftResultsPage,
  InstructorPageFallback,
  LecturerClassDetailPage,
  LecturerClassManagementPage,
  LecturerDashboardPage,
  OfficialExamManagementPage,
  ProctorAttendancePage,
  QuestionStudioPage,
} from '@/routes/AppRoutePages'

import { LoginPage } from '@/pages/auth/LoginPage'
import { ChangePasswordPage } from '@/pages/auth/ChangePasswordPage'
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

export const appRoutes = createRoutesFromElements(
  <>
    {/* Public Routes */}
    <Route path="/login" element={<LoginPage />} />
    <Route path="/unauthorized" element={<UnauthorizedPage />} />

    {/* Route each authenticated role to its own portal. */}
    <Route element={<ProtectedRoute />}>
      <Route path="/" element={<RoleBasedRedirect />} />
      <Route path="/change-password" element={<ChangePasswordPage />} />

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

      {/* Instructor, Department Head, Examination Officer and Admin portal */}
      <Route
        element={
          <ProtectedRoute
            allowedRoles={['Instructor', 'DepartmentHead', 'ExaminationOfficer', 'Admin']}
          />
        }
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
            <Route path="/lecturer/classes/:classId" element={<LecturerClassDetailPage />} />
            <Route path="/lecturer/classes/:classId/shifts/:shiftId/results" element={<InClassShiftResultsPage />} />
            <Route path="/question-bank" element={<QuestionStudioPage />} />
            <Route path="/question-bank/create" element={<CreateQuestionPage />} />
            <Route path="/in-class-shifts" element={<Navigate to="/lecturer/classes" replace />} />
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

      {/* Proctor portal: attendance, seat monitor and anti-cheat violation logging */}
      <Route
        element={
          <ProtectedRoute
            allowedRoles={['Proctor']}
          />
        }
      >
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

      {/* Dead Letter Queue portal: Examination Officer (MF-04) & Admin (MF-01/02) */}
      <Route
        element={
          <ProtectedRoute
            allowedRoles={['ExaminationOfficer', 'Admin']}
          />
        }
      >
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

      {/* Admin Portal: User management, system configs & audit logs */}
      <Route
        element={
          <ProtectedRoute
            allowedRoles={['Admin']}
          />
        }
      >
        <Route element={<AppLayout />}>
          <Route
            element={
              <Suspense fallback={<InstructorPageFallback />}>
                <Outlet />
              </Suspense>
            }
          >
            <Route path="/admin" element={<AdminDashboardPage />} />
          </Route>
        </Route>
      </Route>
    </Route>

    {/* Fallback */}
    <Route path="*" element={<Navigate to="/" replace />} />
  </>,
)
