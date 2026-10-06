import React, { useState } from 'react'
import { Card, CardHeader, CardTitle, CardContent, CardFooter } from '@/components/common/Card'
import { Button } from '@/components/common/Button'
import { Badge } from '@/components/common/Badge'
import { Lock, Save, AlertCircle } from 'lucide-react'

interface ScoreOverrideFormProps {
  isLocked: boolean
  onSaveOverride: (newScore: number, reason: string) => void
  onConfirmLock: () => void
}

export const ScoreOverrideForm: React.FC<ScoreOverrideFormProps> = ({
  isLocked,
  onSaveOverride,
  onConfirmLock,
}) => {
  const [score, setScore] = useState<number>(8.5)
  const [reason, setReason] = useState<string>('')
  const [error, setError] = useState<string>('')

  const handleSave = () => {
    if (!reason.trim()) {
      setError('Bắt buộc phải nhập lý do giải trình khi điều chỉnh điểm!')
      return
    }
    setError('')
    onSaveOverride(score, reason)
  }

  return (
    <Card className="border-slate-200 dark:border-slate-800">
      <CardHeader>
        <div className="flex items-center justify-between">
          <CardTitle className="text-base">Thẩm Định & Điều Chỉnh Điểm (MF-04)</CardTitle>
          {isLocked ? (
            <Badge variant="danger" className="flex items-center space-x-1">
              <Lock className="mr-1 h-3 w-3" />
              Đã Khóa Điểm 1 Chiều (Locked)
            </Badge>
          ) : (
            <Badge variant="warning">Đang Chờ Duyệt</Badge>
          )}
        </div>
      </CardHeader>

      <CardContent className="space-y-4">
        <div>
          <label className="text-xs font-semibold text-slate-500 uppercase">
            Điểm số chính thức sau thẩm định:
          </label>
          <input
            type="number"
            min="0"
            max="10"
            step="0.5"
            disabled={isLocked}
            value={score}
            onChange={(e) => setScore(parseFloat(e.target.value) || 0)}
            className="mt-1 w-32 rounded-lg border px-3 py-2 font-mono text-lg font-bold disabled:opacity-50"
          />
        </div>

        <div>
          <label className="flex items-center justify-between text-xs font-semibold text-slate-500 uppercase">
            <span>Lý do giải trình (Bắt buộc nếu override):</span>
            <span className="text-[10px] text-red-500">* Bắt buộc</span>
          </label>
          <textarea
            disabled={isLocked}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder="Ví dụ: Sinh viên phát âm tiếng Anh từ mượn tốt, giải thích rõ cơ chế Distributed Cache..."
            className="mt-1 h-24 w-full resize-none rounded-lg border p-3 text-sm disabled:opacity-50"
          />
          {error && (
            <p className="mt-1 flex items-center text-xs text-red-600">
              <AlertCircle className="mr-1 h-3.5 w-3.5" />
              {error}
            </p>
          )}
        </div>
      </CardContent>

      <CardFooter className="justify-between">
        <Button variant="outline" size="sm" disabled={isLocked} onClick={handleSave}>
          <Save className="mr-1.5 h-4 w-4" />
          Lưu điểm thẩm định
        </Button>

        <Button variant="danger" size="sm" disabled={isLocked} onClick={onConfirmLock}>
          <Lock className="mr-1.5 h-4 w-4" />
          Khóa Điểm Một Chiều (One-Way Lock)
        </Button>
      </CardFooter>
    </Card>
  )
}
