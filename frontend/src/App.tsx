import { BrowserRouter } from 'react-router-dom'
import { ToastHost } from '@/components/common/toast'
import { AuthProvider } from '@/context/AuthContext'
import { AppRoutes } from '@/routes/AppRoutes'

export function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <AppRoutes />
        <ToastHost />
      </BrowserRouter>
    </AuthProvider>
  )
}

export default App
