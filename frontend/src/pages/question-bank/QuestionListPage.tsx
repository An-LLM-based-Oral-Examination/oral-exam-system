import React, { useState } from 'react'
import { Link } from 'react-router-dom'
import { Card, CardHeader, CardTitle } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Plus, Search, Filter, Edit } from 'lucide-react'

export const QuestionListPage: React.FC = () => {
  const [filterSubject, setFilterSubject] = useState('ALL')

  const mockQuestions = [
    {
      id: 'q-01',
      subject: 'PRN231',
      title: 'Trình bày cơ chế hoạt động của Middleware trong pipeline ASP.NET Core?',
      bloom: 'Understand',
      scope: 'SHARED',
      totalRubric: 10.0,
      createdAt: '2026-09-20',
    },
    {
      id: 'q-02',
      subject: 'PRN231',
      title: 'Phân tích các cấp độ cách ly (Isolation Levels) trong giao dịch CSDL PostgreSQL?',
      bloom: 'Analyze',
      scope: 'EXAM_ONLY',
      totalRubric: 10.0,
      createdAt: '2026-09-22',
    },
    {
      id: 'q-03',
      subject: 'SWD392',
      title: 'Trình bày mô hình Clean Architecture 4 tầng và nguyên tắc Dependency Inversion?',
      bloom: 'Apply',
      scope: 'PRACTICE_ONLY',
      totalRubric: 10.0,
      createdAt: '2026-09-25',
    },
  ]

  return (
    <div className="space-y-6">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h2 className="text-xl font-bold text-slate-900 dark:text-slate-100 flex items-center space-x-2">
            <span>Ngân Hàng Câu Hỏi & Barem Rubric (MF-03)</span>
          </h2>
          <p className="text-xs text-slate-500 mt-1">
            Quản trị kho câu hỏi vấn đáp chuẩn hóa Bloom và Barem Rubric cố định 10.0 điểm.
          </p>
        </div>

        <Link to="/question-bank/create">
          <Button variant="primary" size="md">
            <Plus className="w-4 h-4 mr-2" />
            Tạo mới câu hỏi & Rubric 10.0
          </Button>
        </Link>
      </div>

      {/* Filter bar */}
      <Card className="p-4">
        <div className="flex flex-col sm:flex-row items-center justify-between gap-3">
          <div className="relative w-full sm:w-80">
            <Search className="w-4 h-4 absolute left-3 top-3 text-slate-400" />
            <input
              type="text"
              placeholder="Tìm kiếm nội dung câu hỏi..."
              className="w-full pl-9 pr-3 py-2 text-sm border rounded-lg focus:ring-2 focus:ring-orange-500 focus:outline-none"
            />
          </div>

          <div className="flex items-center space-x-2 w-full sm:w-auto">
            <Filter className="w-4 h-4 text-slate-400" />
            <select
              value={filterSubject}
              onChange={(e) => setFilterSubject(e.target.value)}
              className="border rounded-lg px-3 py-2 text-sm bg-white dark:bg-slate-900"
            >
              <option value="ALL">Tất cả môn học</option>
              <option value="PRN231">PRN231 - .NET Web API</option>
              <option value="SWD392">SWD392 - Software Architecture</option>
              <option value="SWP391">SWP391 - Software Project</option>
            </select>
          </div>
        </div>
      </Card>

      {/* Table list */}
      <Card>
        <CardHeader className="py-3 px-6">
          <CardTitle className="text-sm">Danh Sách Câu Hỏi ({mockQuestions.length})</CardTitle>
        </CardHeader>
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead className="bg-slate-50 dark:bg-slate-800/50 text-xs uppercase text-slate-500 border-b">
              <tr>
                <th className="px-6 py-3">Môn học</th>
                <th className="px-6 py-3">Nội dung câu hỏi</th>
                <th className="px-6 py-3">Cấp độ Bloom</th>
                <th className="px-6 py-3">Phạm vi</th>
                <th className="px-6 py-3 text-center">Barem</th>
                <th className="px-6 py-3 text-right">Thao tác</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
              {mockQuestions.map((q) => (
                <tr key={q.id} className="hover:bg-slate-50/50 dark:hover:bg-slate-800/30">
                  <td className="px-6 py-4 font-mono font-bold text-xs text-orange-600">
                    {q.subject}
                  </td>
                  <td className="px-6 py-4 font-medium text-slate-900 dark:text-slate-100 max-w-md truncate">
                    {q.title}
                  </td>
                  <td className="px-6 py-4">
                    <Badge variant="info">{q.bloom}</Badge>
                  </td>
                  <td className="px-6 py-4">
                    <Badge variant="outline">{q.scope}</Badge>
                  </td>
                  <td className="px-6 py-4 text-center font-mono font-bold text-emerald-600">
                    {q.totalRubric.toFixed(1)} đ
                  </td>
                  <td className="px-6 py-4 text-right">
                    <Button variant="ghost" size="sm">
                      <Edit className="w-4 h-4 mr-1 text-slate-500" />
                      Sửa
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>
    </div>
  )
}
