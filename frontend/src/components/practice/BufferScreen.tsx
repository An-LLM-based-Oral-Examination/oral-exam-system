import React, { useState, useEffect } from 'react'
import { Card, CardHeader, CardTitle, CardContent, CardFooter } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
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
    <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
      <Card className="w-full max-w-2xl shadow-2xl border-orange-500/20">
        <CardHeader>
          <div className="flex items-center justify-between">
            <div className="flex items-center space-x-2 text-orange-600">
              <Edit3 className="w-5 h-5" />
              <CardTitle>Màn Hình Đệm Rà Soát (Buffer Screen - 30s)</CardTitle>
            </div>
            <div className="flex items-center space-x-1.5 font-mono text-sm font-bold text-orange-600 bg-orange-50 dark:bg-orange-950/50 px-2.5 py-1 rounded-md">
              <Clock className="w-4 h-4 animate-pulse" />
              <span>00:{timeLeft < 10 ? `0${timeLeft}` : timeLeft}</span>
            </div>
          </div>
          {/* Progress bar */}
          <div className="w-full bg-slate-200 dark:bg-slate-700 h-2 rounded-full overflow-hidden mt-3">
            <div
              className="bg-orange-600 h-full transition-all duration-1000 ease-linear"
              style={{ width: `${progressPercent}%` }}
            />
          </div>
          <p className="text-xs text-slate-500 mt-2">
            Bạn có 30 giây để sửa lỗi nhận diện thuật ngữ tiếng Anh chuyên ngành (Code-Switching) trước khi hệ thống nộp bài lên AI.
          </p>
        </CardHeader>

        <CardContent className="space-y-3">
          <label className="text-xs font-semibold uppercase text-slate-600 dark:text-slate-400">
            Nội dung câu trả lời bóc băng:
          </label>
          <textarea
            className="w-full h-44 rounded-lg border border-slate-300 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 p-3 text-sm focus:ring-2 focus:ring-orange-500 focus:outline-none"
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
          ) : <div />}
          <Button
            variant="primary"
            size="md"
            onClick={() => onConfirmSubmit(transcript)}
          >
            <CheckCircle className="w-4 h-4 mr-2" />
            Xác nhận nộp ngay
          </Button>
        </CardFooter>
      </Card>
    </div>
  )
}
