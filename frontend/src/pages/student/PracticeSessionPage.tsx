import React, { useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import {
  Card,
  CardHeader,
  CardTitle,
  CardDescription,
  CardContent,
  CardFooter,
} from '@/components/common/Card'
import { Button } from '@/components/common/Button'
import { Badge } from '@/components/common/Badge'
import { BufferScreen } from '@/components/practice/BufferScreen'
import { ScorecardModal } from '@/components/practice/ScorecardModal'
import { ArrowLeft, Volume2, Mic, MicOff, Send, Clock, Layers, Sparkles } from 'lucide-react'
import type { ScorecardPayload } from '@/types/practice.types'

const SESSION_DETAILS: Record<string, { subject: string; question: string; rubric: string }> = {
  INT3204: {
    subject: 'Mạng máy tính (INT3204)',
    question:
      'Hãy so sánh OSPF và BGP, đồng thời giải thích khi nào một hệ thống mạng doanh nghiệp nên sử dụng từng giao thức?',
    rubric:
      'Kiến thức định tuyến (5.0đ) + Lập luận tình huống (3.0đ) + Thuật ngữ chuyên ngành (2.0đ)',
  },
  DSA201: {
    subject: 'Cấu trúc dữ liệu và giải thuật (DSA201)',
    question:
      'Hãy giải thích cơ chế tự cân bằng của cây AVL và minh họa trường hợp cần thực hiện phép quay kép?',
    rubric: 'Nguyên lý cấu trúc cây (5.0đ) + Ví dụ minh họa (3.0đ) + Diễn đạt chính xác (2.0đ)',
  },
  OS202: {
    subject: 'Hệ điều hành (OS202)',
    question:
      'Hãy phân biệt mutex và semaphore, sau đó đề xuất cách xử lý race condition trong một tình huống thực tế?',
    rubric:
      'Kiến thức đồng bộ (5.0đ) + Phân tích tình huống (3.0đ) + Thuật ngữ chuyên ngành (2.0đ)',
  },
  SWE301: {
    subject: 'Thiết kế phần mềm (SWE301)',
    question:
      'Hãy trình bày cách lựa chọn Strategy Pattern và State Pattern, kèm ví dụ cho thấy sự khác biệt giữa hai mẫu thiết kế?',
    rubric: 'Kiến thức design pattern (5.0đ) + So sánh và ví dụ (3.0đ) + Trình bày mạch lạc (2.0đ)',
  },
}

export const PracticeSessionPage: React.FC = () => {
  const [searchParams] = useSearchParams()
  const requestedMode = searchParams.get('mode')
  const courseId = searchParams.get('course') ?? 'INT3204'
  const requestedQuestionCount = Number(searchParams.get('questions'))
  const questionCount = [3, 5, 10].includes(requestedQuestionCount) ? requestedQuestionCount : 5
  const sessionDetails = SESSION_DETAILS[courseId] ?? SESSION_DETAILS.INT3204
  const [mode, setMode] = useState<'PER_QUESTION' | 'FULL_SESSION'>(
    requestedMode === 'FULL_SESSION' ? 'FULL_SESSION' : 'PER_QUESTION',
  )
  const [isRecording, setIsRecording] = useState(false)
  const [transcript, setTranscript] = useState('')
  const [showBuffer, setShowBuffer] = useState(false)
  const [scorecard, setScorecard] = useState<ScorecardPayload | null>(null)

  const handleFinishSpeaking = () => {
    setIsRecording(false)
    setShowBuffer(true) // Trigger 30s Buffer Modal
  }

  const handleReadQuestion = () => {
    if (!('speechSynthesis' in window)) return

    const utterance = new SpeechSynthesisUtterance(sessionDetails.question)
    utterance.lang = 'vi-VN'
    utterance.rate = 0.95
    window.speechSynthesis.cancel()
    window.speechSynthesis.speak(utterance)
  }

  const handleConfirmSubmit = (finalText: string) => {
    setShowBuffer(false)
    setTranscript(finalText)
    // Simulate AI grading response
    setScorecard({
      submissionId: 'sub-demo-01',
      totalScore: 8.5,
      criteriaScores: [
        {
          criterionId: 'Kiến thức cốt lõi',
          score: 4.5,
          comment: `Nắm vững kiến thức trọng tâm của học phần ${sessionDetails.subject}.`,
        },
        {
          criterionId: 'Khả năng lập luận',
          score: 2.5,
          comment: 'Giải thích mạch lạc vai trò của Distributed Cache.',
        },
        {
          criterionId: 'Thuật ngữ & Giao tiếp',
          score: 1.5,
          comment: 'Sử dụng chuẩn xác thuật ngữ kỹ thuật tiếng Anh.',
        },
      ],
      feedback:
        'Câu trả lời rõ ràng và có cấu trúc. Hãy bổ sung thêm một ví dụ phản biện để phần lập luận thuyết phục hơn.',
      followUpQuestion:
        'Nếu điều kiện vận hành thay đổi, bạn sẽ điều chỉnh giải pháp vừa trình bày như thế nào và vì sao?',
    })
  }

  return (
    <div className="space-y-6">
      <Link
        to="/practice"
        className="inline-flex min-h-9 items-center gap-1.5 rounded-lg px-2 text-xs font-semibold text-[#315fbd] transition hover:bg-[#eaf1ff] focus-visible:outline-2 focus-visible:outline-[#245eea]"
      >
        <ArrowLeft className="size-4" />
        Quay lại cấu hình
      </Link>

      {/* Header Info */}
      <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-center">
        <div>
          <div className="flex items-center space-x-2">
            <h2 className="text-xl font-bold text-slate-900 dark:text-slate-100">
              Luyện Tập Vấn Đáp Tự Do (MF-01)
            </h2>
            <Badge variant="info">Persist-First &lt; 100ms</Badge>
          </div>
          <p className="mt-1 text-xs text-slate-500">
            Mô phỏng phỏng vấn vấn đáp tương tác với TTS đọc đề, nhận diện giọng nói và Màn hình đệm
            30s.
          </p>
        </div>

        {/* Mode Selector */}
        <div className="flex items-center space-x-2 rounded-lg bg-slate-200 p-1 dark:bg-slate-800">
          <button
            onClick={() => setMode('PER_QUESTION')}
            className={`rounded-md px-3 py-1.5 text-xs font-semibold transition-colors ${
              mode === 'PER_QUESTION'
                ? 'bg-white text-orange-600 shadow-sm dark:bg-slate-900'
                : 'text-slate-600'
            }`}
          >
            Chấm từng câu
          </button>
          <button
            onClick={() => setMode('FULL_SESSION')}
            className={`rounded-md px-3 py-1.5 text-xs font-semibold transition-colors ${
              mode === 'FULL_SESSION'
                ? 'bg-white text-orange-600 shadow-sm dark:bg-slate-900'
                : 'text-slate-600'
            }`}
          >
            Làm bộ câu hỏi
          </button>
        </div>
      </div>

      {/* Main Question Card */}
      <Card className="border-orange-500/20 shadow-sm">
        <CardHeader>
          <div className="flex items-center justify-between">
            <div className="flex items-center space-x-2 text-xs font-bold text-orange-600 uppercase">
              <Layers className="h-4 w-4" />
              <span>
                Môn: {sessionDetails.subject} — Câu hỏi 1 / {questionCount}
              </span>
            </div>
            <Badge variant="success">Bloom: Vận dụng (Apply)</Badge>
          </div>
          <CardTitle className="mt-2 text-lg">{sessionDetails.question}</CardTitle>
          <CardDescription>Barem điểm: {sessionDetails.rubric} = 10.0đ</CardDescription>
        </CardHeader>

        <CardContent className="space-y-4">
          <div className="flex items-center space-x-3">
            <Button variant="outline" size="sm" onClick={handleReadQuestion}>
              <Volume2 className="mr-1.5 h-4 w-4 text-orange-600" />
              Nghe giám khảo đọc đề (TTS)
            </Button>
            <div className="flex items-center text-xs text-slate-400">
              <Clock className="mr-1 h-3.5 w-3.5" />
              Gợi ý trả lời: 60 - 90 giây
            </div>
          </div>

          <div className="space-y-3 rounded-xl border border-dashed border-slate-300 bg-slate-50 p-4 dark:border-slate-700 dark:bg-slate-900/50">
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
                      <MicOff className="mr-1.5 h-4 w-4 animate-pulse" />
                      Dừng nói & Mở màn hình đệm
                    </>
                  ) : (
                    <>
                      <Mic className="mr-1.5 h-4 w-4" />
                      Bắt đầu nói qua Micro
                    </>
                  )}
                </Button>
              </div>
            </div>

            <textarea
              className="h-32 w-full rounded-lg border border-slate-200 bg-white p-3 text-sm focus:ring-2 focus:ring-orange-500 focus:outline-none dark:border-slate-800 dark:bg-slate-950"
              value={transcript}
              onChange={(e) => setTranscript(e.target.value)}
              placeholder="Văn bản câu trả lời sẽ xuất hiện ở đây khi bạn nói vào micro..."
            />
          </div>
        </CardContent>

        <CardFooter className="justify-between">
          <div className="flex items-center text-xs text-slate-400">
            <Sparkles className="mr-1 h-3.5 w-3.5 text-orange-500" />
            Được bảo vệ bởi Hàng đợi 4 tầng (Zero Data Loss)
          </div>

          <Button variant="primary" size="md" onClick={() => setShowBuffer(true)}>
            <Send className="mr-2 h-4 w-4" />
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
