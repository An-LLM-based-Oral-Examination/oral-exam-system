import { StudentClassesPage } from '@/pages/student/StudentClassesPage'
import { Suspense } from 'react'
import { Navigate, Outlet, Route, createRoutesFromElements } from 'react-router-dom'
import { AdminDashboardRouteEntry } from '@/routes/AdminDashboardRouteEntry'
import { AppLayout } from '@/components/layout/AppLayout'
import { ExaminationOfficerLayout } from '@/layouts/ExaminationOfficerLayout'
import { RoleRouteErrorBoundary, RouterErrorElement } from '@/components/common/ErrorBoundary'
import { LegacyRoleRouteLayout } from '@/routes/LegacyRoleRouteLayout'
import { ProtectedRoute } from '@/routes/ProtectedRoute'
import { ProfileRouteLayout } from '@/routes/ProfileRouteLayout'
import { RoleBasedRedirect } from '@/routes/RoleBasedRedirect'
import {
  AdminAuditLogsPage,
  AdminConfigsPage,
  AdminCoursesPage,
  AdminNotificationsPage,
  AdminSemestersPage,
  AdminUsersPage,
  DepartmentHeadDashboardPage,
  DepartmentLecturerAssignmentPage,
  DepartmentCourseClosPage,
  DepartmentCoursesPage,
  DepartmentCourseDetailPage,
  QuestionReviewRequestsPage,
  OfficialExamManagementPage,
  AppealManagementPage,
  AuditEvidencePage,
  CreateQuestionPage,
  DeadLetterQueuePage,
  InClassLiveRedirect,
  InClassShiftResultsPage,
  PortalPageFallback,
  LecturerClassDetailPage,
  LecturerClassManagementPage,
  LecturerDashboardPage,
  ProctorAttendancePage,
  QuestionStudioPage,
  ExaminationOfficerDashboardPage,
  ExaminationOfficerExamListPage,
  ExaminationOfficerExamDetailPage,
  ExaminationOfficerTicketAssignmentPage,
  ExaminationOfficerRosterImportPage,
  ExaminationOfficerMonitorSelectorPage,
  ExaminationOfficerShiftMonitorPage,
  ExaminationOfficerGradeSelectorPage,
  ExaminationOfficerGradePublicationPage,
  ExaminationOfficerDlqPage,
  GradeVerificationPage,
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
import { ProfilePage } from '@/pages/profile/ProfilePage'

export const appRoutes = createRoutesFromElements(
  <Route errorElement={<RouterErrorElement />}>
    {/* Public Routes */}
    <Route path="/login" element={<LoginPage />} />
    <Route path="/unauthorized" element={<UnauthorizedPage />} />
    <Route
      path="/verify/grades"
      element={
        <Suspense fallback={<PortalPageFallback label="Đang tải cổng xác thực chứng thư…" />}>
          <GradeVerificationPage />
        </Suspense>
      }
    />
    <Route
      path="/verify/grades/:certificateHash"
      element={
        <Suspense fallback={<PortalPageFallback label="Đang xác thực chứng thư bảng điểm…" />}>
          <GradeVerificationPage />
        </Suspense>
      }
    />

    {/* Route each authenticated role to its own portal. */}
    <Route element={<ProtectedRoute />}>
      <Route path="/" element={<RoleBasedRedirect />} />
      <Route path="/change-password" element={<ChangePasswordPage />} />
      <Route element={<ProfileRouteLayout />}>
        <Route element={<RoleRouteErrorBoundary portalLabel="Hồ sơ cá nhân" />}>
          <Route path="/profile" element={<ProfilePage />} />
        </Route>
      </Route>

      {/* Student Portal */}
      <Route element={<ProtectedRoute allowedRoles={['Student']} />}>
        <Route element={<RoleRouteErrorBoundary scope="app" portalLabel="Cổng Sinh viên" />}>
          <Route path="/mock-exam/session" element={<MockExamSessionPage />} />
          <Route path="/in-class-exam/session" element={<InClassExamSessionPage />} />
        </Route>
        <Route element={<AppLayout />}>
          <Route element={<RoleRouteErrorBoundary portalLabel="Cổng Sinh viên" />}>
            <Route path="/student/overview" element={<StudentDashboardPage />} />
            <Route path="/classes" element={<StudentClassesPage />} />
            <Route path="/classes/:classId" element={<StudentInClassExamPage />} />
            <Route path="/in-class-exam" element={<StudentInClassExamPage />} />
            <Route path="/practice" element={<PracticePage />} />
            <Route path="/practice/session" element={<PracticeSessionPage />} />
            <Route path="/mock-exam" element={<MockExamPage />} />
            <Route path="/exam" element={<OfficialExamPage />} />
            <Route path="/results" element={<ExamHistoryPage />} />
          </Route>
        </Route>
      </Route>

      {/* Cổng riêng Cán bộ Khảo thí: chỉ vận hành MF-04 */}
      <Route element={<ProtectedRoute allowedRoles={['ExaminationOfficer']} />}>
        <Route element={<ExaminationOfficerLayout />}>
          <Route
            element={
              <RoleRouteErrorBoundary portalLabel="Cổng Cán bộ Khảo thí">
                <Suspense fallback={<PortalPageFallback label="Đang tải cổng Khảo thí…" />}>
                  <Outlet />
                </Suspense>
              </RoleRouteErrorBoundary>
            }
          >
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
              path="/examination-officer/shifts/:shiftId/tickets/import"
              element={<ExaminationOfficerRosterImportPage />}
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
              element={<AppealManagementPage />}
            />
            <Route
              path="/examination-officer/appeals/:appealId"
              element={<AppealManagementPage />}
            />
            <Route path="/examination-officer/dlq" element={<ExaminationOfficerDlqPage />} />
          </Route>
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
              DepartmentHead: '/unauthorized',
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
        <Route path="/lecturer/dlq" element={<Navigate to="/unauthorized" replace />} />
        <Route element={<AppLayout />}>
          <Route
            element={
              <RoleRouteErrorBoundary portalLabel="Cổng Giảng viên">
                <Suspense fallback={<PortalPageFallback />}>
                  <Outlet />
                </Suspense>
              </RoleRouteErrorBoundary>
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
            <Route
              path={`${ROUTE_PATHS.lecturer.appeals}/:appealId`}
              element={<AppealManagementPage />}
            />
          </Route>
        </Route>
        <Route path="/in-class-shifts" element={<Navigate to={ROUTE_PATHS.lecturer.classes} replace />} />
        <Route element={<RoleRouteErrorBoundary scope="app" portalLabel="Cổng Giảng viên" />}>
          <Route path="/in-class-shifts/:shiftId/results" element={<InClassShiftResultsPage />} />
        </Route>
        <Route path="/in-class-shifts/:shiftId/live" element={<InClassLiveRedirect />} />
      </Route>

      {/* Department Head portal: academic review and appeal governance. */}
      <Route element={<ProtectedRoute allowedRoles={['DepartmentHead']} />}>
        <Route path={ROUTE_PATHS.departmentHead.appeals} element={<Navigate to="/unauthorized" replace />} />
        <Route element={<AppLayout />}>
          <Route
            element={
              <RoleRouteErrorBoundary portalLabel="Cổng Trưởng Bộ môn">
                <Suspense fallback={<PortalPageFallback />}>
                  <Outlet />
                </Suspense>
              </RoleRouteErrorBoundary>
            }
          >
            <Route path="/department-head/lecturer-assignments" element={<DepartmentLecturerAssignmentPage />} />
            <Route path="/department-head/clos" element={<DepartmentCourseClosPage />} />
            <Route path={ROUTE_PATHS.departmentHead.courses} element={<DepartmentCoursesPage />} />
            <Route path={`${ROUTE_PATHS.departmentHead.courses}/:courseId`} element={<DepartmentCourseDetailPage />} />
            <Route path="/department-head/exam-structures" element={<OfficialExamManagementPage />} />
            <Route path="/department/clos" element={<Navigate to="/department-head/clos" replace />} />
            <Route path={ROUTE_PATHS.departmentHead.dashboard} element={<DepartmentHeadDashboardPage />} />
            <Route path={ROUTE_PATHS.departmentHead.questionReview} element={<QuestionReviewRequestsPage />} />
            <Route path={ROUTE_PATHS.departmentHead.questionBank} element={<QuestionStudioPage />} />
            <Route path={`${ROUTE_PATHS.departmentHead.questionReview}/bank`} element={<Navigate to={ROUTE_PATHS.departmentHead.questionBank} replace />} />
            <Route path={`${ROUTE_PATHS.departmentHead.questionReview}/:requestId`} element={<QuestionReviewRequestsPage />} />
          </Route>
        </Route>
      </Route>

      {/* Proctor portal: attendance, seat monitor and anti-cheat violation logging */}
      <Route element={<ProtectedRoute allowedRoles={['Proctor']} />}>
        <Route element={<AppLayout />}>
          <Route
            element={
              <RoleRouteErrorBoundary portalLabel="Cổng Giám thị">
                <Suspense fallback={<PortalPageFallback />}>
                  <Outlet />
                </Suspense>
              </RoleRouteErrorBoundary>
            }
          >
            <Route path="/proctor/attendance" element={<ProctorAttendancePage />} />
          </Route>
        </Route>
      </Route>

      {/* Admin Portal: User management, academic catalog, configs, notifications, audit & MF-01/02/05 DLQ */}
      <Route element={<ProtectedRoute allowedRoles={['Admin']} />}>
        <Route path="/admin" element={<Navigate to={ROUTE_PATHS.admin.dashboard} replace />} />
        <Route element={<AppLayout />}>
          <Route
            element={
              <RoleRouteErrorBoundary portalLabel="Cổng Quản trị viên">
                <Suspense fallback={<PortalPageFallback label="Đang tải cổng quản trị…" />}>
                  <Outlet />
                </Suspense>
              </RoleRouteErrorBoundary>
            }
          >
            <Route path={ROUTE_PATHS.admin.dashboard} element={<AdminDashboardRouteEntry />} />
            <Route path={ROUTE_PATHS.admin.users} element={<AdminUsersPage />} />
            <Route path={ROUTE_PATHS.admin.notifications} element={<AdminNotificationsPage />} />
            <Route path={ROUTE_PATHS.admin.auditLogs} element={<AdminAuditLogsPage />} />
            <Route path={ROUTE_PATHS.admin.dlq} element={<DeadLetterQueuePage />} />
            <Route path={ROUTE_PATHS.admin.semesters} element={<AdminSemestersPage />} />
            <Route path={ROUTE_PATHS.admin.courses} element={<AdminCoursesPage />} />
            <Route path={ROUTE_PATHS.admin.configs} element={<AdminConfigsPage />} />
          </Route>
        </Route>
      </Route>
    </Route>

    {/* Fallback */}
    <Route path="*" element={<Navigate to="/" replace />} />
  </Route>,
)
