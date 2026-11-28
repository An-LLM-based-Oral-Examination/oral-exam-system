import { createBrowserRouter, RouterProvider } from 'react-router-dom'
import { ErrorBoundary } from '@/components/common/ErrorBoundary'
import { ToastHost } from '@/components/common/toast'
import { AuthProvider } from '@/context/AuthContext'
import { appRoutes } from '@/routes/AppRoutes'

const router = createBrowserRouter(appRoutes)

export function App() {
  return (
    <ErrorBoundary scope="app" portalLabel="Hệ thống Vấn đáp AI">
      <AuthProvider>
        <RouterProvider router={router} />
        <ToastHost />
      </AuthProvider>
    </ErrorBoundary>
  )
}

export default App
