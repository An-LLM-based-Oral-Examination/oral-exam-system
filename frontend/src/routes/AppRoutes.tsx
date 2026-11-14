import { Suspense } from 'react'
import { Navigate, Outlet, Route, createRoutesFromElements } from 'react-router-dom'
import { AppLayout } from '@/components/layout/AppLayout'
import { StudentLayout } from '@/components/layout/StudentLayout'
import { ExaminationOfficerLayout } from '@/layouts/ExaminationOfficerLayout'
import { LegacyRoleRouteLayout } from '@/routes/LegacyRoleRouteLayout'
import { ProtectedRoute } from '@/routes/ProtectedRoute'
import { RoleBasedRedirect } from '@/routes/RoleBasedRedirect'
import {
  AdminDashboardPage,
  DepartmentHeadDashboardPage,
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
  ProctorAttendancePage,
  QuestionStudioPage,
  ExaminationOfficerDashboardPage,
  ExaminationOfficerExamListPage,
  ExaminationOfficerExamDetailPage,
  ExaminationOfficerTicketAssignmentPage,
  ExaminationOfficerMonitorSelectorPage,
  ExaminationOfficerShiftMonitorPage,
  ExaminationOfficerGradeSelectorPage,
  ExaminationOfficerGradePublicationPage,
  ExaminationOfficerAppealListPage,
  ExaminationOfficerAppealDetailPage,
  ExaminationOfficerDlqPage,
} from '@/routes/AppRoutePages'
import { ROUTE_PATHS } from '@/routes/routePaths'

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

      {/* Cổng riêng Cán bộ Khảo thí: chỉ vận hành MF-04 */}
      <Route element={<ProtectedRoute allowedRoles={['ExaminationOfficer']} />}>
        <Route element={<ExaminationOfficerLayout />}>
          <Route
            path="/examination-officer/dashboard"
            element={<ExaminationOfficerDashboardPage />}
          />
          <Route path="/examination-officer/exams" element={<ExaminationOfficerExamListPage />} />
          <Route
            path="/examination-officer/exams/:sessionId"
            element={<ExaminationOfficerExamDetailPage />}
          />
          <Route
            path="/examination-officer/tickets/assign"
            element={<ExaminationOfficerTicketAssignmentPage />}
          />
          <Route
            path="/examination-officer/monitor"
            element={<ExaminationOfficerMonitorSelectorPage />}
          />
          <Route
            path="/examination-officer/monitor/:shiftId"
            element={<ExaminationOfficerShiftMonitorPage />}
          />
          <Route
            path="/examination-officer/grades"
            element={<ExaminationOfficerGradeSelectorPage />}
          />
          <Route
            path="/examination-officer/grades/:shiftId"
            element={<ExaminationOfficerGradePublicationPage />}
          />
          <Route
            path="/examination-officer/appeals"
            element={<ExaminationOfficerAppealListPage />}
          />
          <Route
            path="/examination-officer/appeals/:appealId"
            element={<ExaminationOfficerAppealDetailPage />}
          />
          <Route path="/examination-officer/dlq" element={<ExaminationOfficerDlqPage />} />
        </Route>
      </Route>

      {/* Legacy URLs: redirect to the canonical portal without widening permissions. */}
      <Route
        path="/dashboard"
        element={<RoleBasedRedirect />}
      />
      <Route
        path="/question-bank"
        element={
          <LegacyRoleRouteLayout
            redirects={{
              Instructor: ROUTE_PATHS.lecturer.questionBank,
              DepartmentHead: ROUTE_PATHS.departmentHead.questionReview,
              Admin: '/unauthorized',
              ExaminationOfficer: '/unauthorized',
            }}
          />
        }
      />
      <Route
        path="/question-bank/create"
        element={
          <LegacyRoleRouteLayout
            redirects={{
              Instructor: ROUTE_PATHS.lecturer.createQuestion,
              DepartmentHead: '/unauthorized',
              Admin: '/unauthorized',
              ExaminationOfficer: '/unauthorized',
            }}
          />
        }
      />
      <Route
        path="/official-exams/manage"
        element={
          <LegacyRoleRouteLayout
            redirects={{
              ExaminationOfficer: ROUTE_PATHS.examinationOfficer.exams,
              Instructor: '/unauthorized',
              DepartmentHead: '/unauthorized',
              Admin: '/unauthorized',
            }}
          />
        }
      />
      <Route
        path="/appeals"
        element={
          <LegacyRoleRouteLayout
            redirects={{
              Instructor: ROUTE_PATHS.lecturer.appeals,
              DepartmentHead: ROUTE_PATHS.departmentHead.appeals,
              ExaminationOfficer: ROUTE_PATHS.examinationOfficer.appeals,
              Admin: '/unauthorized',
            }}
          />
        }
      />
      <Route
        path="/dlq"
        element={
          <LegacyRoleRouteLayout
            redirects={{
              Admin: ROUTE_PATHS.admin.dlq,
              ExaminationOfficer: ROUTE_PATHS.examinationOfficer.dlq,
              Instructor: '/unauthorized',
              DepartmentHead: '/unauthorized',
            }}
          />
        }
      />

      {/* Instructor portal: teaching, in-class shifts, question authoring and assigned review. */}
      <Route element={<ProtectedRoute allowedRoles={['Instructor']} />}>
        <Route element={<AppLayout />}>
          <Route
            element={
              <Suspense fallback={<InstructorPageFallback />}>
                <Outlet />
              </Suspense>
            }
          >
            <Route path={ROUTE_PATHS.lecturer.dashboard} element={<LecturerDashboardPage />} />
            <Route path={ROUTE_PATHS.lecturer.classes} element={<LecturerClassManagementPage />} />
            <Route path={`${ROUTE_PATHS.lecturer.classes}/:classId`} element={<LecturerClassDetailPage />} />
            <Route
              path={`${ROUTE_PATHS.lecturer.classes}/:classId/shifts/:shiftId/results`}
              element={<InClassShiftResultsPage />}
            />
            <Route path={ROUTE_PATHS.lecturer.questionBank} element={<QuestionStudioPage />} />
            <Route path={ROUTE_PATHS.lecturer.createQuestion} element={<CreateQuestionPage />} />
            <Route path={ROUTE_PATHS.lecturer.audit} element={<AuditEvidencePage />} />
            <Route path={ROUTE_PATHS.lecturer.appeals} element={<AppealManagementPage />} />
          </Route>
        </Route>
        <Route path="/in-class-shifts" element={<Navigate to={ROUTE_PATHS.lecturer.classes} replace />} />
        <Route path="/in-class-shifts/:shiftId/results" element={<InClassShiftResultsPage />} />
        <Route path="/in-class-shifts/:shiftId/live" element={<InClassLiveRedirect />} />
      </Route>

      {/* Department Head portal: academic review and appeal governance. */}
      <Route element={<ProtectedRoute allowedRoles={['DepartmentHead']} />}>
        <Route element={<AppLayout />}>
          <Route
            element={
              <Suspense fallback={<InstructorPageFallback />}>
                <Outlet />
              </Suspense>
            }
          >
            <Route path={ROUTE_PATHS.departmentHead.dashboard} element={<DepartmentHeadDashboardPage />} />
            <Route path={ROUTE_PATHS.departmentHead.questionReview} element={<QuestionStudioPage />} />
            <Route path={ROUTE_PATHS.departmentHead.appeals} element={<AppealManagementPage />} />
          </Route>
        </Route>
      </Route>

      {/* Proctor portal: attendance, seat monitor and anti-cheat violation logging */}
      <Route element={<ProtectedRoute allowedRoles={['Proctor']} />}>
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

      {/* Admin Portal: User management, system configs & audit logs */}
      <Route element={<ProtectedRoute allowedRoles={['Admin']} />}>
        <Route element={<AppLayout />}>
          <Route
            element={
              <Suspense fallback={<InstructorPageFallback />}>
                <Outlet />
              </Suspense>
            }
          >
            <Route path={ROUTE_PATHS.admin.dashboard} element={<AdminDashboardPage />} />
            <Route path={ROUTE_PATHS.admin.dlq} element={<DeadLetterQueuePage />} />
          </Route>
        </Route>
      </Route>
    </Route>

    {/* Fallback */}
    <Route path="*" element={<Navigate to="/" replace />} />
  </>,
)
