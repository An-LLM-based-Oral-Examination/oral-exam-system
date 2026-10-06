import React from 'react'
import { Routes, Route, Navigate } from 'react-router-dom'
import { AppLayout } from '@/components/layout/AppLayout'
import { StudentLayout } from '@/components/layout/StudentLayout'
import { ProtectedRoute } from '@/routes/ProtectedRoute'
import { RoleBasedRedirect } from '@/routes/RoleBasedRedirect'

// Pages
import { LoginPage } from '@/pages/auth/LoginPage'
import { UnauthorizedPage } from '@/pages/auth/UnauthorizedPage'
import { LecturerDashboardPage } from '@/pages/lecturer/LecturerDashboardPage'
import { QuestionStudioPage } from '@/pages/lecturer/QuestionStudioPage'
import { CreateQuestionPage } from '@/pages/lecturer/CreateQuestionPage'
import { AuditEvidencePage } from '@/pages/lecturer/AuditEvidencePage'
import { StudentDashboardPage } from '@/pages/student/StudentDashboardPage'
import { PracticePage } from '@/pages/student/PracticePage'
import { PracticeSessionPage } from '@/pages/student/PracticeSessionPage'
import { MockExamPage } from '@/pages/student/MockExamPage'
import { MockExamSessionPage } from '@/pages/student/MockExamSessionPage'
import { OfficialExamPage } from '@/pages/student/OfficialExamPage'
import { ExamHistoryPage } from '@/pages/student/ExamHistoryPage'

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

        {/* Instructor and Admin Portal */}
        <Route element={<ProtectedRoute allowedRoles={['Instructor', 'Admin']} />}>
          <Route element={<AppLayout />}>
            <Route path="/dashboard" element={<LecturerDashboardPage />} />
            <Route path="/question-bank" element={<QuestionStudioPage />} />
            <Route path="/question-bank/create" element={<CreateQuestionPage />} />
            <Route path="/lecturer/audit" element={<AuditEvidencePage />} />
          </Route>
        </Route>
      </Route>

      {/* Fallback */}
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}

export default AppRoutes
