import { createBrowserRouter, RouterProvider } from 'react-router-dom'
import { ToastHost } from '@/components/common/toast'
import { AuthProvider } from '@/context/AuthContext'
import { appRoutes } from '@/routes/AppRoutes'

const router = createBrowserRouter(appRoutes)

export function App() {
  return (
    <AuthProvider>
      <RouterProvider router={router} />
      <ToastHost />
    </AuthProvider>
  )
}

export default App
