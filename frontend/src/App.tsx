import React from 'react'
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { AuthProvider } from '@/context/AuthContext'
import { AppLayout } from '@/components/layout/AppLayout'
import { AuthGuard } from '@/components/layout/AuthGuard'

// Pages
import { LoginPage } from '@/pages/auth/LoginPage'
import { UnauthorizedPage } from '@/pages/auth/UnauthorizedPage'
import { DashboardPage } from '@/pages/dashboard/DashboardPage'
import { PracticeSessionPage } from '@/pages/practice/PracticeSessionPage'
import { MockExamPage } from '@/pages/mock-exam/MockExamPage'
import { QuestionListPage } from '@/pages/question-bank/QuestionListPage'
import { CreateQuestionPage } from '@/pages/question-bank/CreateQuestionPage'
import { AuditPortalPage } from '@/pages/lecturer/AuditPortalPage'

export const App: React.FC = () => {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          {/* Public Routes */}
          <Route path="/login" element={<LoginPage />} />
          <Route path="/unauthorized" element={<UnauthorizedPage />} />

          {/* Protected Routes for All Authenticated Users */}
          <Route element={<AuthGuard />}>
            <Route element={<AppLayout />}>
              <Route path="/" element={<DashboardPage />} />
              <Route path="/practice" element={<PracticeSessionPage />} />
              <Route path="/mock-exam" element={<MockExamPage />} />

              {/* Protected Routes for Instructor and Admin */}
              <Route element={<AuthGuard allowedRoles={['Instructor', 'Admin']} />}>
                <Route path="/question-bank" element={<QuestionListPage />} />
                <Route path="/question-bank/create" element={<CreateQuestionPage />} />
                <Route path="/lecturer/audit" element={<AuditPortalPage />} />
              </Route>
            </Route>
          </Route>

          {/* Fallback */}
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  )
}

export default App
