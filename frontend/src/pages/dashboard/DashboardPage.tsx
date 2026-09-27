import React from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '@/context/AuthContext'
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '@/components/ui/Card'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Mic, Clock, BookOpen, ClipboardCheck, ArrowRight, Sparkles } from 'lucide-react'

export const DashboardPage: React.FC = () => {
  const { user, hasRole } = useAuth()

  const flows = [
    {
      code: 'MF-01',
      title: 'Luyện Tập Vấn Đáp Tự Do',
      description: 'Luyện tập đa phương thức, TTS đọc câu hỏi, Mic/Text trả lời, màn hình đệm 30s xử lý Code-Switching, phòng thủ 4 tầng Zero Data Loss.',
      path: '/practice',
      icon: Mic,
      color: 'text-sky-600 bg-sky-50 dark:bg-sky-950/40 border-sky-200',
      badge: 'Interactive Voice/Text',
      allowed: true,
    },
    {
      code: 'MF-02',
      title: 'Thi Thử Vấn Đáp Bấm Giờ',
      description: 'Mô phỏng phòng thi với Voice-First Gate, Server Timer, AutoSubmitGuard và kiểm soát hạn mức Quota 3 lượt/môn/ngày bằng PostgreSQL.',
      path: '/mock-exam',
      icon: Clock,
      color: 'text-amber-600 bg-amber-50 dark:bg-amber-950/40 border-amber-200',
      badge: 'Quota <= 3/ngày',
      allowed: true,
    },
    {
      code: 'MF-03',
      title: 'Ngân Hàng Đề & Rubric 10.0',
      description: 'Quản trị câu hỏi theo Bloom, kiểm thực Barem Rubric tổng bằng chính xác 10.0 điểm, AI Simulator giả lập chấm thử câu trả lời mẫu.',
      path: '/question-bank',
      icon: BookOpen,
      color: 'text-emerald-600 bg-emerald-50 dark:bg-emerald-950/40 border-emerald-200',
      badge: 'Instructor Studio',
      allowed: hasRole(['Instructor', 'Admin']),
    },
    {
      code: 'MF-04',
      title: 'Cổng Thẩm Định Điểm Khảo Thí',
      description: 'Hậu kiểm ca thi Lab, nghe audio STT_MSSV.webm trên Cloudflare R2, điều chỉnh điểm kèm giải trình và Khóa điểm một chiều (One-Way Lock).',
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
      <div className="bg-gradient-to-r from-orange-600 to-amber-600 rounded-2xl p-6 text-white shadow-md flex items-center justify-between">
        <div>
          <div className="flex items-center space-x-2 text-orange-200 text-xs font-bold uppercase tracking-wider mb-1">
            <Sparkles className="w-4 h-4" />
            <span>Đồ Án Tốt Nghiệp SEP490 — FA26SE166</span>
          </div>
          <h2 className="text-2xl font-bold">
            Xin chào, {user?.fullName || 'Người dùng'}!
          </h2>
          <p className="text-sm text-orange-100 mt-1 max-w-xl">
            Hệ thống luyện thi và đánh giá vấn đáp AI chuẩn hóa 4 luồng nghiệp vụ cốt lõi (MF-01 đến MF-04).
          </p>
        </div>
        <Badge variant="outline" className="bg-white/20 text-white border-white/30 text-xs px-3 py-1 hidden sm:block">
          Vai trò: {user?.role || 'Khách'}
        </Badge>
      </div>

      {/* 4 Core Main Flows Grid */}
      <div>
        <h3 className="text-base font-bold text-slate-900 dark:text-slate-100 mb-4">
          Bốn Phân Hệ Nghiệp Vụ Cốt Lõi (4 Core Main Flows)
        </h3>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
          {flows.map((flow) => (
            <Card key={flow.code} className="hover:shadow-md transition-shadow flex flex-col justify-between">
              <CardHeader>
                <div className="flex items-center justify-between mb-2">
                  <div className="flex items-center space-x-2">
                    <span className="font-mono font-bold text-xs px-2 py-0.5 rounded bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300">
                      {flow.code}
                    </span>
                    <Badge variant="outline">{flow.badge}</Badge>
                  </div>
                  <div className={`p-2 rounded-lg border ${flow.color}`}>
                    <flow.icon className="w-5 h-5" />
                  </div>
                </div>
                <CardTitle>{flow.title}</CardTitle>
                <CardDescription className="line-clamp-2 mt-1">{flow.description}</CardDescription>
              </CardHeader>
              <CardContent className="pt-0">
                {flow.allowed ? (
                  <Link to={flow.path}>
                    <Button variant="outline" size="sm" className="w-full justify-between">
                      <span>Truy cập phân hệ</span>
                      <ArrowRight className="w-4 h-4" />
                    </Button>
                  </Link>
                ) : (
                  <div className="text-xs text-slate-400 italic py-2">
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
