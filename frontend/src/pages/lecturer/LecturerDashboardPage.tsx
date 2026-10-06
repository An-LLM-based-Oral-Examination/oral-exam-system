import React from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '@/hooks/useAuth'
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '@/components/common/Card'
import { Badge } from '@/components/common/Badge'
import { Button } from '@/components/common/Button'
import { Mic, Clock, BookOpen, ClipboardCheck, ArrowRight, Sparkles } from 'lucide-react'

export const LecturerDashboardPage: React.FC = () => {
  const { user, hasRole } = useAuth()

  const flows = [
    {
      code: 'MF-01',
      title: 'Luyện Tập Vấn Đáp Tự Do',
      description:
        'Luyện tập đa phương thức, TTS đọc câu hỏi, Mic/Text trả lời, màn hình đệm 30s xử lý Code-Switching, phòng thủ 4 tầng Zero Data Loss.',
      path: '/practice',
      icon: Mic,
      color: 'text-sky-600 bg-sky-50 dark:bg-sky-950/40 border-sky-200',
      badge: 'Interactive Voice/Text',
      allowed: true,
    },
    {
      code: 'MF-02',
      title: 'Thi Thử Vấn Đáp Bấm Giờ',
      description:
        'Mô phỏng phòng thi với Voice-First Gate, Server Timer, AutoSubmitGuard và kiểm soát hạn mức Quota 3 lượt/môn/ngày bằng PostgreSQL.',
      path: '/mock-exam',
      icon: Clock,
      color: 'text-amber-600 bg-amber-50 dark:bg-amber-950/40 border-amber-200',
      badge: 'Quota <= 3/ngày',
      allowed: true,
    },
    {
      code: 'MF-03',
      title: 'Ngân Hàng Đề & Rubric 10.0',
      description:
        'Quản trị câu hỏi theo Bloom, kiểm thực Barem Rubric tổng bằng chính xác 10.0 điểm, AI Simulator giả lập chấm thử câu trả lời mẫu.',
      path: '/question-bank',
      icon: BookOpen,
      color: 'text-emerald-600 bg-emerald-50 dark:bg-emerald-950/40 border-emerald-200',
      badge: 'Instructor Studio',
      allowed: hasRole(['Instructor', 'Admin']),
    },
    {
      code: 'MF-04',
      title: 'Cổng Thẩm Định Điểm Khảo Thí',
      description:
        'Hậu kiểm ca thi Lab, nghe audio STT_MSSV.webm trên Cloudflare R2, điều chỉnh điểm kèm giải trình và Khóa điểm một chiều (One-Way Lock).',
      path: '/lecturer/audit',
      icon: ClipboardCheck,
      color: 'text-purple-600 bg-purple-50 dark:bg-purple-950/40 border-purple-200',
      badge: 'Audit & Lock',
      allowed: hasRole(['Instructor', 'Admin']),
    },
  ]

  return (
    <div className="space-y-6">
      {/* Welcome Banner */}
      <div className="flex items-center justify-between rounded-2xl bg-gradient-to-r from-orange-600 to-amber-600 p-6 text-white shadow-md">
        <div>
          <div className="mb-1 flex items-center space-x-2 text-xs font-bold tracking-wider text-orange-200 uppercase">
            <Sparkles className="h-4 w-4" />
            <span>Đồ Án Tốt Nghiệp SEP490 — FA26SE166</span>
          </div>
          <h2 className="text-2xl font-bold">Xin chào, {user?.fullName || 'Người dùng'}!</h2>
          <p className="mt-1 max-w-xl text-sm text-orange-100">
            Hệ thống luyện thi và đánh giá vấn đáp AI chuẩn hóa 4 luồng nghiệp vụ cốt lõi (MF-01 đến
            MF-04).
          </p>
        </div>
        <Badge
          variant="outline"
          className="hidden border-white/30 bg-white/20 px-3 py-1 text-xs text-white sm:block"
        >
          Vai trò: {user?.role || 'Khách'}
        </Badge>
      </div>

      {/* 4 Core Main Flows Grid */}
      <div>
        <h3 className="mb-4 text-base font-bold text-slate-900 dark:text-slate-100">
          Bốn Phân Hệ Nghiệp Vụ Cốt Lõi (4 Core Main Flows)
        </h3>
        <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
          {flows.map((flow) => (
            <Card
              key={flow.code}
              className="flex flex-col justify-between transition-shadow hover:shadow-md"
            >
              <CardHeader>
                <div className="mb-2 flex items-center justify-between">
                  <div className="flex items-center space-x-2">
                    <span className="rounded bg-slate-100 px-2 py-0.5 font-mono text-xs font-bold text-slate-700 dark:bg-slate-800 dark:text-slate-300">
                      {flow.code}
                    </span>
                    <Badge variant="outline">{flow.badge}</Badge>
                  </div>
                  <div className={`rounded-lg border p-2 ${flow.color}`}>
                    <flow.icon className="h-5 w-5" />
                  </div>
                </div>
                <CardTitle>{flow.title}</CardTitle>
                <CardDescription className="mt-1 line-clamp-2">{flow.description}</CardDescription>
              </CardHeader>
              <CardContent className="pt-0">
                {flow.allowed ? (
                  <Link to={flow.path}>
                    <Button variant="outline" size="sm" className="w-full justify-between">
                      <span>Truy cập phân hệ</span>
                      <ArrowRight className="h-4 w-4" />
                    </Button>
                  </Link>
                ) : (
                  <div className="py-2 text-xs text-slate-400 italic">
                    🔒 Yêu cầu tài khoản Giảng viên hoặc Admin để truy cập
                  </div>
                )}
              </CardContent>
            </Card>
          ))}
        </div>
      </div>
    </div>
  )
}

export { LecturerDashboardPage as DashboardPage }
