import React, { useState } from 'react'
import { Card, CardHeader, CardTitle, CardContent, CardFooter } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
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
              <Lock className="w-3 h-3 mr-1" />
              Đã Khóa Điểm 1 Chiều (Locked)
            </Badge>
          ) : (
            <Badge variant="warning">Đang Chờ Duyệt</Badge>
          )}
        </div>
      </CardHeader>

      <CardContent className="space-y-4">
        <div>
          <label className="text-xs font-semibold uppercase text-slate-500">
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
            className="mt-1 w-32 px-3 py-2 border rounded-lg font-mono font-bold text-lg disabled:opacity-50"
          />
        </div>

        <div>
          <label className="text-xs font-semibold uppercase text-slate-500 flex items-center justify-between">
            <span>Lý do giải trình (Bắt buộc nếu override):</span>
            <span className="text-[10px] text-red-500">* Bắt buộc</span>
          </label>
          <textarea
            disabled={isLocked}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder="Ví dụ: Sinh viên phát âm tiếng Anh từ mượn tốt, giải thích rõ cơ chế Distributed Cache..."
            className="mt-1 w-full h-24 p-3 border rounded-lg text-sm resize-none disabled:opacity-50"
          />
          {error && (
            <p className="text-xs text-red-600 flex items-center mt-1">
              <AlertCircle className="w-3.5 h-3.5 mr-1" />
              {error}
            </p>
          )}
        </div>
      </CardContent>

      <CardFooter className="justify-between">
        <Button
          variant="outline"
          size="sm"
          disabled={isLocked}
          onClick={handleSave}
        >
          <Save className="w-4 h-4 mr-1.5" />
          Lưu điểm thẩm định
        </Button>

        <Button
          variant="danger"
          size="sm"
          disabled={isLocked}
          onClick={onConfirmLock}
        >
          <Lock className="w-4 h-4 mr-1.5" />
          Khóa Điểm Một Chiều (One-Way Lock)
        </Button>
      </CardFooter>
    </Card>
  )
}
