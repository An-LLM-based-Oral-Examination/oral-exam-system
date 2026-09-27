import React, { useState } from 'react'
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/Card'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { AudioPlayer } from '@/components/audit/AudioPlayer'
import { ScoreOverrideForm } from '@/components/audit/ScoreOverrideForm'
import { Download, Lock } from 'lucide-react'

export const AuditPortalPage: React.FC = () => {
  const [selectedCandidate, setSelectedCandidate] = useState<string>('c-01')
  const [isLocked, setIsLocked] = useState<boolean>(false)

  const mockCandidates = [
    {
      id: 'c-01',
      seatNumber: 1,
      studentCode: 'SE180011',
      fullName: 'Trần Văn An',
      aiScore: 8.0,
      officialScore: 8.5,
      isLocked: false,
    },
    {
      id: 'c-02',
      seatNumber: 2,
      studentCode: 'SE180022',
      fullName: 'Lê Thị Bình',
      aiScore: 7.5,
      officialScore: 7.5,
      isLocked: true,
    },
    {
      id: 'c-03',
      seatNumber: 3,
      studentCode: 'SE180033',
      fullName: 'Phạm Đức Cường',
      aiScore: 9.0,
      officialScore: 9.0,
      isLocked: false,
    },
  ]

  const activeCandidate = mockCandidates.find((c) => c.id === selectedCandidate) || mockCandidates[0]

  return (
    <div className="space-y-6">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h2 className="text-xl font-bold text-slate-900 dark:text-slate-100 flex items-center space-x-2">
            <span>Cổng Thẩm Định & Hậu Kiểm Điểm Thi Lab (MF-04)</span>
            <Badge variant="info">Ca thi: LAB-302</Badge>
          </h2>
          <p className="text-xs text-slate-500 mt-1">
            Nghe lại bản ghi âm STT_MSSV.webm, đối chiếu rubric, điều chỉnh điểm kèm giải trình và Khóa điểm một chiều.
          </p>
        </div>

        <Button
          variant="outline"
          size="sm"
          onClick={() => alert('Đang kết xuất bảng điểm Excel định dạng FAP Khảo thí...')}
        >
          <Download className="w-4 h-4 mr-1.5" />
          Xuất Bảng Điểm FAP (.xlsx)
        </Button>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Candidate List (Seat Map) */}
        <Card className="lg:col-span-1">
          <CardHeader className="py-3 px-4 flex flex-row items-center justify-between">
            <CardTitle className="text-sm">Danh Sách Thí Sinh (STT = Số Máy)</CardTitle>
            <Badge variant="outline">{mockCandidates.length} máy</Badge>
          </CardHeader>
          <CardContent className="p-2 space-y-1">
            {mockCandidates.map((c) => (
              <button
                key={c.id}
                onClick={() => {
                  setSelectedCandidate(c.id)
                  setIsLocked(c.isLocked)
                }}
                className={`w-full text-left p-3 rounded-lg flex items-center justify-between text-xs transition-colors cursor-pointer ${
                  selectedCandidate === c.id
                    ? 'bg-orange-50 dark:bg-orange-950/40 border border-orange-300 dark:border-orange-800'
                    : 'hover:bg-slate-100 dark:hover:bg-slate-800'
                }`}
              >
                <div className="flex items-center space-x-3">
                  <div className="w-7 h-7 rounded-md bg-slate-200 dark:bg-slate-700 flex items-center justify-center font-mono font-bold text-slate-800 dark:text-slate-200">
                    {c.seatNumber < 10 ? `0${c.seatNumber}` : c.seatNumber}
                  </div>
                  <div>
                    <p className="font-semibold text-slate-800 dark:text-slate-200">{c.fullName}</p>
                    <p className="text-[11px] text-slate-400 font-mono">{c.studentCode}</p>
                  </div>
                </div>

                <div className="text-right">
                  <span className="font-mono font-bold text-sm text-orange-600 block">
                    {c.officialScore} đ
                  </span>
                  {c.isLocked ? (
                    <span className="text-[10px] text-red-500 flex items-center justify-end">
                      <Lock className="w-2.5 h-2.5 mr-0.5" /> Khóa
                    </span>
                  ) : (
                    <span className="text-[10px] text-amber-500">Chưa khóa</span>
                  )}
                </div>
              </button>
            ))}
          </CardContent>
        </Card>

        {/* Audit Details */}
        <div className="lg:col-span-2 space-y-6">
          <AudioPlayer
            studentCode={activeCandidate.studentCode}
            seatNumber={activeCandidate.seatNumber}
          />

          <ScoreOverrideForm
            isLocked={isLocked}
            onSaveOverride={(score, reason) => {
              alert(`Đã lưu điểm thẩm định mới: ${score}đ. Lý do: ${reason}`)
            }}
            onConfirmLock={() => {
              if (confirm('CẢNH BÁO: Thao tác khóa điểm là BẤT BIẾN (One-Way Lock) và không thể hoàn tác. Bạn có chắc chắn muốn khóa chính thức?')) {
                setIsLocked(true)
                alert('Đã khóa điểm chính thức một chiều thành công! Toàn bộ ô sửa điểm đã bị vô hiệu hóa.')
              }
            }}
          />
        </div>
      </div>
    </div>
  )
}
