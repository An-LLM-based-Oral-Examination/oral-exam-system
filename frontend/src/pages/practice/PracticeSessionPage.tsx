import React, { useState } from 'react'
import { Card, CardHeader, CardTitle, CardDescription, CardContent, CardFooter } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { BufferScreen } from '@/components/practice/BufferScreen'
import { ScorecardModal } from '@/components/practice/ScorecardModal'
import { Volume2, Mic, MicOff, Send, Clock, Layers, Sparkles } from 'lucide-react'
import type { ScorecardPayload } from '@/types/exam.types'

export const PracticeSessionPage: React.FC = () => {
  const [mode, setMode] = useState<'PER_QUESTION' | 'FULL_SESSION'>('PER_QUESTION')
  const [isRecording, setIsRecording] = useState(false)
  const [transcript, setTranscript] = useState('Em xin trả lời: Trong mô hình Microservices, em dùng Docker để containerize các service, Redis làm Distributed Cache và giao tiếp giữa các service qua gRPC...')
  const [showBuffer, setShowBuffer] = useState(false)
  const [scorecard, setScorecard] = useState<ScorecardPayload | null>(null)

  const handleFinishSpeaking = () => {
    setIsRecording(false)
    setShowBuffer(true) // Trigger 30s Buffer Modal
  }

  const handleConfirmSubmit = (finalText: string) => {
    setShowBuffer(false)
    setTranscript(finalText)
    // Simulate AI grading response
    setScorecard({
      submissionId: 'sub-demo-01',
      totalScore: 8.5,
      criteriaScores: [
        { criterionId: 'Kiến thức cốt lõi', score: 4.5, comment: 'Nắm vững kiến trúc Microservices và Docker container.' },
        { criterionId: 'Khả năng lập luận', score: 2.5, comment: 'Giải thích mạch lạc vai trò của Distributed Cache.' },
        { criterionId: 'Thuật ngữ & Giao tiếp', score: 1.5, comment: 'Sử dụng chuẩn xác thuật ngữ kỹ thuật tiếng Anh.' },
      ],
      feedback: 'Câu trả lời rất rõ ràng, thể hiện tư duy thiết kế hệ thống tốt. Cần bổ sung thêm cơ chế Circuit Breaker khi một service bị lỗi.',
      followUpQuestion: 'Nếu một microservice trong chuỗi gọi gRPC bị quá tải dẫn đến cascading failure, bạn sẽ áp dụng pattern nào để cô lập lỗi?',
    })
  }

  return (
    <div className="space-y-6">
      {/* Header Info */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <div className="flex items-center space-x-2">
            <h2 className="text-xl font-bold text-slate-900 dark:text-slate-100">
              Luyện Tập Vấn Đáp Tự Do (MF-01)
            </h2>
            <Badge variant="info">Persist-First &lt; 100ms</Badge>
          </div>
          <p className="text-xs text-slate-500 mt-1">
            Mô phỏng phỏng vấn vấn đáp tương tác với TTS đọc đề, nhận diện giọng nói và Màn hình đệm 30s.
          </p>
        </div>

        {/* Mode Selector */}
        <div className="flex items-center space-x-2 bg-slate-200 dark:bg-slate-800 p-1 rounded-lg">
          <button
            onClick={() => setMode('PER_QUESTION')}
            className={`px-3 py-1.5 text-xs font-semibold rounded-md transition-colors ${
              mode === 'PER_QUESTION' ? 'bg-white dark:bg-slate-900 shadow-sm text-orange-600' : 'text-slate-600'
            }`}
          >
            Chấm từng câu
          </button>
          <button
            onClick={() => setMode('FULL_SESSION')}
            className={`px-3 py-1.5 text-xs font-semibold rounded-md transition-colors ${
              mode === 'FULL_SESSION' ? 'bg-white dark:bg-slate-900 shadow-sm text-orange-600' : 'text-slate-600'
            }`}
          >
            Làm cả bộ rồi chấm
          </button>
        </div>
      </div>

      {/* Main Question Card */}
      <Card className="border-orange-500/20 shadow-sm">
        <CardHeader>
          <div className="flex items-center justify-between">
            <div className="flex items-center space-x-2 text-xs font-bold text-orange-600 uppercase">
              <Layers className="w-4 h-4" />
              <span>Môn: PRN231 — Câu hỏi 1 / 5</span>
            </div>
            <Badge variant="success">Bloom: Vận dụng (Apply)</Badge>
          </div>
          <CardTitle className="text-lg mt-2">
            Hãy trình bày cách triển khai kiến trúc Microservices và cơ chế phân phối tải (Load Balancing) trong ứng dụng quy mô lớn?
          </CardTitle>
          <CardDescription>
            Barem điểm: Kiến thức kiến trúc (5.0đ) + Khả năng giải trình (3.0đ) + Thuật ngữ chuyên ngành (2.0đ) = 10.0đ
          </CardDescription>
        </CardHeader>

        <CardContent className="space-y-4">
          <div className="flex items-center space-x-3">
            <Button variant="outline" size="sm" onClick={() => {}}>
              <Volume2 className="w-4 h-4 mr-1.5 text-orange-600" />
              Nghe giám khảo đọc đề (TTS)
            </Button>
            <div className="text-xs text-slate-400 flex items-center">
              <Clock className="w-3.5 h-3.5 mr-1" />
              Gợi ý trả lời: 60 - 90 giây
            </div>
          </div>

          <div className="p-4 rounded-xl border border-dashed border-slate-300 dark:border-slate-700 bg-slate-50 dark:bg-slate-900/50 space-y-3">
            <div className="flex items-center justify-between">
              <span className="text-xs font-semibold text-slate-600 dark:text-slate-400">
                Câu trả lời của bạn:
              </span>
              <div className="flex items-center space-x-2">
                <Button
                  variant={isRecording ? 'danger' : 'primary'}
                  size="sm"
                  onClick={() => (isRecording ? handleFinishSpeaking() : setIsRecording(true))}
                >
                  {isRecording ? (
                    <>
                      <MicOff className="w-4 h-4 mr-1.5 animate-pulse" />
                      Dừng nói & Mở màn hình đệm
                    </>
                  ) : (
                    <>
                      <Mic className="w-4 h-4 mr-1.5" />
                      Bắt đầu nói qua Micro
                    </>
                  )}
                </Button>
              </div>
            </div>

            <textarea
              className="w-full h-32 p-3 text-sm rounded-lg border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-950 focus:ring-2 focus:ring-orange-500 focus:outline-none"
              value={transcript}
              onChange={(e) => setTranscript(e.target.value)}
              placeholder="Văn bản câu trả lời sẽ xuất hiện ở đây khi bạn nói vào micro..."
            />
          </div>
        </CardContent>

        <CardFooter className="justify-between">
          <div className="text-xs text-slate-400 flex items-center">
            <Sparkles className="w-3.5 h-3.5 mr-1 text-orange-500" />
            Được bảo vệ bởi Hàng đợi 4 tầng (Zero Data Loss)
          </div>

          <Button
            variant="primary"
            size="md"
            onClick={() => setShowBuffer(true)}
          >
            <Send className="w-4 h-4 mr-2" />
            Nộp câu hỏi để AI chấm
          </Button>
        </CardFooter>
      </Card>

      {/* 30s Buffer Modal */}
      {showBuffer && (
        <BufferScreen
          initialTranscript={transcript}
          durationSeconds={30}
          onConfirmSubmit={handleConfirmSubmit}
          onCancel={() => setShowBuffer(false)}
        />
      )}

      {/* Scorecard Result Modal */}
      {scorecard && (
        <ScorecardModal
          scorecard={scorecard}
          onClose={() => setScorecard(null)}
          onNextQuestion={() => {
            setScorecard(null)
            setTranscript('')
          }}
        />
      )}
    </div>
  )
}
