import React, { useState } from 'react'
import { Card, CardHeader, CardTitle, CardDescription, CardContent, CardFooter } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { CountdownTimer } from '@/components/exam/CountdownTimer'
import { VoiceFirstGate } from '@/components/exam/VoiceFirstGate'
import { ShieldCheck, Play, ArrowRight } from 'lucide-react'

export const MockExamPage: React.FC = () => {
  const [isExamStarted, setIsExamStarted] = useState(false)
  const [isRecordingFinished, setIsRecordingFinished] = useState(false)
  const [transcript, setTranscript] = useState('')
  const [dailyQuota] = useState(3) // 3 times per day per subject

  return (
    <div className="space-y-6">
      {/* Top Banner with Quota Guard */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 bg-white dark:bg-slate-900 p-4 rounded-xl border border-slate-200 dark:border-slate-800">
        <div>
          <h2 className="text-xl font-bold text-slate-900 dark:text-slate-100 flex items-center space-x-2">
            <span>Thi Thử Vấn Đáp Bấm Giờ (MF-02)</span>
            <Badge variant="warning">Voice-First Gate</Badge>
          </h2>
          <p className="text-xs text-slate-500 mt-1">
            Mô phỏng áp lực phòng thi thật với đồng hồ đếm ngược, kiểm soát gian lận và phân nhánh Dual-Path AI.
          </p>
        </div>

        <div className="flex items-center space-x-3">
          <div className="p-2.5 bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-800 rounded-lg text-xs font-semibold text-amber-800 dark:text-amber-300 flex items-center space-x-1.5">
            <ShieldCheck className="w-4 h-4 text-amber-600" />
            <span>Hạn mức hôm nay: {dailyQuota}/3 lượt</span>
          </div>

          {isExamStarted && (
            <CountdownTimer
              totalSeconds={600}
              label="Đồng hồ thi"
              onExpire={() => alert('Hết giờ làm bài! Tự động nộp bài.')}
            />
          )}
        </div>
      </div>

      {!isExamStarted ? (
        <Card className="border-orange-500/20 text-center py-10 px-4">
          <CardHeader className="items-center border-none">
            <div className="w-16 h-16 rounded-2xl bg-orange-100 dark:bg-orange-950/50 flex items-center justify-center text-orange-600 mb-3">
              <Play className="w-8 h-8 ml-1" />
            </div>
            <CardTitle className="text-xl">Bắt Đầu Phiên Thi Thử</CardTitle>
            <CardDescription className="max-w-md mx-auto">
              Đề thi gồm 5 câu hỏi ngẫu nhiên bao quát các chương. Hệ thống yêu cầu trả lời qua micro trước khi cho phép hiệu chỉnh văn bản.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-3 max-w-sm mx-auto">
            <div className="text-xs text-slate-500 space-y-1 text-left bg-slate-50 dark:bg-slate-800/60 p-3 rounded-lg">
              <p>• <strong>Thời gian tối đa:</strong> 10 phút toàn bài</p>
              <p>• <strong>Quy định Voice-First:</strong> Bắt buộc nói qua Micro</p>
              <p>• <strong>Hạn ngạch (Quota):</strong> Tối đa 3 lượt/ngày/môn học</p>
            </div>
          </CardContent>
          <CardFooter className="justify-center border-none pt-2">
            <Button variant="primary" size="lg" onClick={() => setIsExamStarted(true)}>
              <Play className="w-5 h-5 mr-2" />
              Bắt đầu làm bài thi thử
            </Button>
          </CardFooter>
        </Card>
      ) : (
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold text-orange-600 uppercase">
                Câu hỏi 2 / 5 — Môn PRN231
              </span>
              <CountdownTimer totalSeconds={90} label="Thời gian câu" />
            </div>
            <CardTitle className="text-lg mt-2">
              Trình bày nguyên lý hoạt động của Dependency Injection (DI) trong ASP.NET Core và phân biệt Transient, Scoped, Singleton?
            </CardTitle>
          </CardHeader>

          <CardContent className="space-y-4">
            <div className="flex items-center space-x-2">
              <Button
                variant={isRecordingFinished ? 'outline' : 'primary'}
                size="sm"
                onClick={() => setIsRecordingFinished(true)}
              >
                {isRecordingFinished ? 'Đã hoàn thành thu âm' : 'Bấm để giả lập thu âm xong'}
              </Button>
            </div>

            <VoiceFirstGate
              isRecordingFinished={isRecordingFinished}
              transcript={transcript}
              onChange={setTranscript}
            />
          </CardContent>

          <CardFooter className="justify-between">
            <Button variant="outline" size="sm" onClick={() => setIsExamStarted(false)}>
              Hủy phiên thi
            </Button>
            <Button
              variant="primary"
              size="md"
              disabled={!isRecordingFinished}
              onClick={() => {
                alert('Đã lưu câu hỏi 2! Chuyển sang câu 3.')
                setIsRecordingFinished(false)
                setTranscript('')
              }}
            >
              Lưu câu & Tiếp tục
              <ArrowRight className="w-4 h-4 ml-1.5" />
            </Button>
          </CardFooter>
        </Card>
      )}
    </div>
  )
}
