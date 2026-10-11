import React from 'react'
import { Link } from 'react-router-dom'
import { Card, CardHeader, CardTitle, CardContent } from '@/components/common/Card'
import { Button } from '@/components/common/Button'
import { ShieldX, ArrowLeft } from 'lucide-react'

export const UnauthorizedPage: React.FC = () => {
  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-100 p-4 dark:bg-slate-950">
      <Card className="w-full max-w-md p-6 text-center shadow-xl">
        <CardHeader className="items-center border-none pb-2">
          <div className="mb-2 flex h-14 w-14 items-center justify-center rounded-full bg-red-100 text-red-600 dark:bg-red-950/50">
            <ShieldX className="h-8 w-8" />
          </div>
          <CardTitle className="text-xl text-red-600">403 — Không Có Quyền Truy Cập</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <p className="text-sm text-slate-600 dark:text-slate-400">
            Bạn không có quyền hạn cần thiết để truy cập phân hệ này (Yêu cầu vai trò Giảng viên
            hoặc Quản trị viên).
          </p>
          <Link to="/">
            <Button variant="primary" size="md" className="w-full">
              <ArrowLeft className="mr-2 h-4 w-4" />
              Quay lại Bảng điều khiển chính
            </Button>
          </Link>
        </CardContent>
      </Card>
    </div>
  )
}
