import React, { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '@/context/AuthContext'
import { Card, CardHeader, CardTitle, CardDescription, CardContent, CardFooter } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Input'
import { BookOpenCheck, ShieldCheck } from 'lucide-react'

export const LoginPage: React.FC = () => {
  const navigate = useNavigate()
  const { login } = useAuth()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault()
    setIsLoading(true)
    setError(null)

    try {
      await login({ email, password })
      navigate('/')
    } catch (err: any) {
      setError(err?.detail || err?.title || 'Đăng nhập thất bại. Vui lòng kiểm tra lại thông tin.')
    } finally {
      setIsLoading(false)
    }
  }

  // Quick Demo Login Handler
  const handleQuickLogin = (role: 'Student' | 'Instructor' | 'Admin') => {
    const mockUsers = {
      Student: { id: 'usr-stu-01', email: 'student@fpt.edu.vn', fullName: 'Nguyễn Văn Sinh Viên', role: 'Student' as const },
      Instructor: { id: 'usr-ins-01', email: 'instructor@fpt.edu.vn', fullName: 'ThS. Nguyễn Thị Cẩm Hương', role: 'Instructor' as const },
      Admin: { id: 'usr-adm-01', email: 'admin@fpt.edu.vn', fullName: 'Quản Trị Viên Hệ Thống', role: 'Admin' as const },
    }
    sessionStorage.setItem('access_token', 'mock_dev_jwt_token')
    sessionStorage.setItem('user_profile', JSON.stringify(mockUsers[role]))
    window.location.href = '/'
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-100 dark:bg-slate-950 p-4">
      <Card className="w-full max-w-md shadow-xl border-slate-200 dark:border-slate-800">
        <CardHeader className="text-center space-y-2">
          <div className="mx-auto w-12 h-12 rounded-xl bg-orange-600 flex items-center justify-center text-white shadow-md">
            <BookOpenCheck className="w-6 h-6" />
          </div>
          <CardTitle className="text-xl">Hệ Thống Luyện Thi Vấn Đáp AI</CardTitle>
          <CardDescription>Đồ án tốt nghiệp SEP490 (FA26SE166) — ĐH FPT</CardDescription>
        </CardHeader>

        <form onSubmit={handleLogin}>
          <CardContent className="space-y-4">
            {error && (
              <div className="p-3 bg-red-50 text-red-700 text-xs rounded-lg border border-red-200">
                {error}
              </div>
            )}
            <Input
              label="Email FPT"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="ten@fpt.edu.vn"
              required
            />
            <Input
              label="Mật khẩu"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="••••••••"
              required
            />
          </CardContent>

          <CardFooter className="flex-col space-y-4">
            <Button type="submit" variant="primary" size="md" className="w-full" isLoading={isLoading}>
              Đăng nhập hệ thống
            </Button>

            <div className="w-full pt-4 border-t border-slate-200 dark:border-slate-800">
              <p className="text-[11px] text-center text-slate-400 uppercase font-semibold mb-2 flex items-center justify-center">
                <ShieldCheck className="w-3.5 h-3.5 mr-1 text-orange-600" />
                Đăng nhập nhanh cho kiểm thử (Dev Role Switch):
              </p>
              <div className="grid grid-cols-3 gap-2">
                <Button type="button" variant="outline" size="sm" onClick={() => handleQuickLogin('Student')}>
                  Sinh Viên
                </Button>
                <Button type="button" variant="outline" size="sm" onClick={() => handleQuickLogin('Instructor')}>
                  Giảng Viên
                </Button>
                <Button type="button" variant="outline" size="sm" onClick={() => handleQuickLogin('Admin')}>
                  Admin
                </Button>
              </div>
            </div>
          </CardFooter>
        </form>
      </Card>
    </div>
  )
}
