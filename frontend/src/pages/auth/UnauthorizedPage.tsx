import React from 'react'
import { Link } from 'react-router-dom'
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { ShieldX, ArrowLeft } from 'lucide-react'

export const UnauthorizedPage: React.FC = () => {
  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-100 dark:bg-slate-950 p-4">
      <Card className="w-full max-w-md text-center shadow-xl p-6">
        <CardHeader className="items-center border-none pb-2">
          <div className="w-14 h-14 rounded-full bg-red-100 dark:bg-red-950/50 flex items-center justify-center text-red-600 mb-2">
            <ShieldX className="w-8 h-8" />
          </div>
          <CardTitle className="text-xl text-red-600">403 — Không Có Quyền Truy Cập</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <p className="text-sm text-slate-600 dark:text-slate-400">
            Bạn không có quyền hạn cần thiết để truy cập phân hệ này (Yêu cầu vai trò Giảng viên hoặc Quản trị viên).
          </p>
          <Link to="/">
            <Button variant="primary" size="md" className="w-full">
              <ArrowLeft className="w-4 h-4 mr-2" />
              Quay lại Bảng điều khiển chính
            </Button>
          </Link>
        </CardContent>
      </Card>
    </div>
  )
}
