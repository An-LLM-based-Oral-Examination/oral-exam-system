import React, { useState, useEffect } from 'react'
import { Card, CardHeader, CardTitle, CardContent, CardFooter } from '@/components/common/Card'
import { Button } from '@/components/common/Button'
import { Clock, CheckCircle, Edit3 } from 'lucide-react'

interface BufferScreenProps {
  initialTranscript: string
  durationSeconds?: number
  onConfirmSubmit: (finalText: string) => void
  onCancel?: () => void
}

export const BufferScreen: React.FC<BufferScreenProps> = ({
  initialTranscript,
  durationSeconds = 30,
  onConfirmSubmit,
  onCancel,
}) => {
  const [timeLeft, setTimeLeft] = useState(durationSeconds)
  const [transcript, setTranscript] = useState(initialTranscript)

  useEffect(() => {
    if (timeLeft <= 0) {
      onConfirmSubmit(transcript)
      return
    }
    const timer = setInterval(() => {
      setTimeLeft((prev) => prev - 1)
    }, 1000)
    return () => clearInterval(timer)
  }, [timeLeft, transcript, onConfirmSubmit])

  const progressPercent = Math.max(0, (timeLeft / durationSeconds) * 100)

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
      <Card className="w-full max-w-2xl border-orange-500/20 shadow-2xl">
        <CardHeader>
          <div className="flex items-center justify-between">
            <div className="flex items-center space-x-2 text-orange-600">
              <Edit3 className="h-5 w-5" />
              <CardTitle>Màn Hình Đệm Rà Soát (Buffer Screen - 30s)</CardTitle>
            </div>
            <div className="flex items-center space-x-1.5 rounded-md bg-orange-50 px-2.5 py-1 font-mono text-sm font-bold text-orange-600 dark:bg-orange-950/50">
              <Clock className="h-4 w-4 animate-pulse" />
              <span>00:{timeLeft < 10 ? `0${timeLeft}` : timeLeft}</span>
            </div>
          </div>
          {/* Progress bar */}
          <div className="mt-3 h-2 w-full overflow-hidden rounded-full bg-slate-200 dark:bg-slate-700">
            <div
              className="h-full bg-orange-600 transition-all duration-1000 ease-linear"
              style={{ width: `${progressPercent}%` }}
            />
          </div>
          <p className="mt-2 text-xs text-slate-500">
            Bạn có 30 giây để sửa lỗi nhận diện thuật ngữ tiếng Anh chuyên ngành (Code-Switching)
            trước khi hệ thống nộp bài lên AI.
          </p>
        </CardHeader>

        <CardContent className="space-y-3">
          <label className="text-xs font-semibold text-slate-600 uppercase dark:text-slate-400">
            Nội dung câu trả lời bóc băng:
          </label>
          <textarea
            className="h-44 w-full rounded-lg border border-slate-300 bg-slate-50 p-3 text-sm focus:ring-2 focus:ring-orange-500 focus:outline-none dark:border-slate-700 dark:bg-slate-800"
            value={transcript}
            onChange={(e) => setTranscript(e.target.value)}
            placeholder="Nội dung câu trả lời..."
          />
        </CardContent>

        <CardFooter className="justify-between">
          {onCancel ? (
            <Button variant="outline" size="sm" onClick={onCancel}>
              Hủy bỏ
            </Button>
          ) : (
            <div />
          )}
          <Button variant="primary" size="md" onClick={() => onConfirmSubmit(transcript)}>
            <CheckCircle className="mr-2 h-4 w-4" />
            Xác nhận nộp ngay
          </Button>
        </CardFooter>
      </Card>
    </div>
  )
}
