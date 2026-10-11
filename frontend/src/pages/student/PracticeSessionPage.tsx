import React, { useMemo, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
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
import {
  ArrowLeft,
  Clock,
  Infinity as InfinityIcon,
  Layers,
  LockKeyhole,
  Mic,
  MicOff,
  Send,
  Sparkles,
  Square,
  Volume2,
  X,
} from 'lucide-react'
import type { PracticeDifficulty, PracticeMode, ScorecardPayload } from '@/types/practice.types'

const FULL_SESSION_MIN_QUESTIONS = 3
const FULL_SESSION_MAX_QUESTIONS = 10

const DIFFICULTY_META: Record<PracticeDifficulty, { label: string; className: string }> = {
  easy: { label: 'Dễ', className: 'border-emerald-200 bg-emerald-50 text-emerald-700' },
  medium: { label: 'Trung bình', className: 'border-amber-200 bg-amber-50 text-amber-700' },
  hard: { label: 'Khó', className: 'border-rose-200 bg-rose-50 text-rose-700' },
}

function parseDifficulties(value: string | null): PracticeDifficulty[] {
  const validDifficulties = new Set<PracticeDifficulty>(['easy', 'medium', 'hard'])
  const parsed = (value ?? '')
    .split(',')
    .filter((difficulty): difficulty is PracticeDifficulty =>
      validDifficulties.has(difficulty as PracticeDifficulty),
    )
  return [...new Set(parsed)].length > 0 ? [...new Set(parsed)] : ['medium']
}

function getQuestionDifficulty(
  difficulties: PracticeDifficulty[],
  questionNumber: number,
): PracticeDifficulty {
  if (difficulties.length === 1) return difficulties[0]
  const pairIndex = Math.floor((questionNumber - 1) / 2)
  return difficulties[pairIndex % difficulties.length]
}

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
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const requestedMode = searchParams.get('mode')
  const courseId = searchParams.get('course') ?? 'INT3204'
  const requestedQuestionCountValue = searchParams.get('questions')
  const requestedQuestionCount = Number(requestedQuestionCountValue)
  const questionCount =
    requestedQuestionCountValue !== null && Number.isFinite(requestedQuestionCount)
      ? Math.min(
          FULL_SESSION_MAX_QUESTIONS,
          Math.max(FULL_SESSION_MIN_QUESTIONS, Math.round(requestedQuestionCount)),
        )
      : 5
  const sessionDetails = SESSION_DETAILS[courseId] ?? SESSION_DETAILS.INT3204
  const mode: PracticeMode = requestedMode === 'FULL_SESSION' ? 'FULL_SESSION' : 'PER_QUESTION'
  const selectedDifficulties = useMemo(
    () => parseDifficulties(searchParams.get('difficulties')),
    [searchParams],
  )
  const [currentQuestionNumber, setCurrentQuestionNumber] = useState(1)
  const [isRecording, setIsRecording] = useState(false)
  const [transcript, setTranscript] = useState('')
  const [showBuffer, setShowBuffer] = useState(false)
  const [scorecard, setScorecard] = useState<ScorecardPayload | null>(null)
  const [sessionNotice, setSessionNotice] = useState('')
  const [showStopConfirmation, setShowStopConfirmation] = useState(false)
  const currentDifficulty = getQuestionDifficulty(selectedDifficulties, currentQuestionNumber)
  const currentDifficultyMeta = DIFFICULTY_META[currentDifficulty]

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
    const result: ScorecardPayload = {
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
      ...(mode === 'PER_QUESTION'
        ? {
            followUpQuestion:
              'Nếu điều kiện vận hành thay đổi, bạn sẽ điều chỉnh giải pháp vừa trình bày như thế nào và vì sao?',
          }
        : {}),
    }

    if (mode === 'FULL_SESSION' && currentQuestionNumber < questionCount) {
      setCurrentQuestionNumber((current) => current + 1)
      setTranscript('')
      setSessionNotice(
        `Đã hoàn thành câu ${currentQuestionNumber}/${questionCount}. Scorecard sẽ hiển thị sau câu cuối cùng.`,
      )
      return
    }

    setSessionNotice('')
    setScorecard(result)
  }

  const moveToNextPerQuestion = () => {
    setScorecard(null)
    setTranscript('')
    setCurrentQuestionNumber((current) => current + 1)
    setSessionNotice('')
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

        <div className="flex flex-wrap items-center gap-2">
          <div className="inline-flex min-h-10 items-center gap-2 rounded-xl border border-slate-200 bg-white px-3 text-xs font-semibold text-slate-700 shadow-sm dark:border-slate-700 dark:bg-slate-900 dark:text-slate-200">
            {mode === 'PER_QUESTION' ? (
              <InfinityIcon className="size-4 text-blue-600" />
            ) : (
              <Layers className="size-4 text-blue-600" />
            )}
            {mode === 'PER_QUESTION'
              ? 'Luyện từng câu · Không giới hạn'
              : `Full-Session · ${questionCount} câu`}
            <LockKeyhole className="size-3 text-slate-400" aria-label="Cấu hình đã khóa" />
          </div>
          {mode === 'PER_QUESTION' && (
            <button
              type="button"
              onClick={() => setShowStopConfirmation(true)}
              className="inline-flex min-h-10 items-center gap-2 rounded-xl border border-rose-200 bg-rose-50 px-3 text-xs font-bold text-rose-700 transition hover:bg-rose-100 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-rose-500"
            >
              <Square className="size-3.5 fill-current" />
              Dừng luyện tập
            </button>
          )}
        </div>
      </div>

      {mode === 'PER_QUESTION' && (
        <section className="flex flex-col gap-3 rounded-xl border border-[#dce5f1] bg-white p-3 shadow-sm sm:flex-row sm:items-center sm:justify-between">
          <div>
            <p className="text-[9px] font-bold tracking-[0.06em] text-[#75859a] uppercase">
              Mức độ đã chọn
            </p>
            <div className="mt-1.5 flex flex-wrap gap-1.5">
              {selectedDifficulties.map((difficulty) => (
                <span
                  key={difficulty}
                  className={`rounded-full border px-2.5 py-1 text-[9px] font-bold ${DIFFICULTY_META[difficulty].className}`}
                >
                  {DIFFICULTY_META[difficulty].label}
                </span>
              ))}
            </div>
          </div>
          <p className="max-w-lg text-[9px] leading-relaxed text-[#6f7f94] sm:text-right">
            {selectedDifficulties.length > 1
              ? 'Mỗi 3 câu liên tiếp có đúng 2 câu cùng mức và 1 câu khác mức; không có 3 câu cùng mức.'
              : `Toàn bộ câu hỏi ở mức ${DIFFICULTY_META[selectedDifficulties[0]].label}.`}
          </p>
        </section>
      )}

      {sessionNotice && (
        <div
          className="rounded-xl border border-blue-200 bg-blue-50 px-4 py-3 text-xs font-semibold text-blue-700"
          role="status"
        >
          {sessionNotice}
        </div>
      )}

      {/* Main Question Card */}
      <Card className="border-orange-500/20 shadow-sm">
        <CardHeader>
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div className="flex items-center space-x-2 text-xs font-bold text-orange-600 uppercase">
              <Layers className="h-4 w-4" />
              <span>
                Môn: {sessionDetails.subject} — Câu {currentQuestionNumber}
                {mode === 'FULL_SESSION' ? ` / ${questionCount}` : ' · Không giới hạn'}
              </span>
            </div>
            <div className="flex items-center gap-2">
              {mode === 'PER_QUESTION' && (
                <span
                  className={`rounded-full border px-2.5 py-1 text-[9px] font-bold ${currentDifficultyMeta.className}`}
                >
                  Mức: {currentDifficultyMeta.label}
                </span>
              )}
              <Badge variant="success">Bloom: Vận dụng (Apply)</Badge>
            </div>
          </div>
          {mode === 'FULL_SESSION' && (
            <div className="mt-3 h-1.5 overflow-hidden rounded-full bg-slate-100 dark:bg-slate-800">
              <div
                className="h-full rounded-full bg-blue-600 transition-[width] duration-300"
                style={{ width: `${(currentQuestionNumber / questionCount) * 100}%` }}
              />
            </div>
          )}
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
            {mode === 'PER_QUESTION'
              ? 'Nộp câu hỏi để AI chấm'
              : currentQuestionNumber === questionCount
                ? 'Nộp câu cuối & xem Scorecard'
                : `Hoàn tất câu ${currentQuestionNumber} & tiếp tục`}
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
          onClose={() => {
            setScorecard(null)
            if (mode === 'FULL_SESSION') navigate('/practice')
          }}
          onNextQuestion={mode === 'PER_QUESTION' ? moveToNextPerQuestion : undefined}
        />
      )}

      {showStopConfirmation && (
        <div
          className="fixed inset-0 z-[60] flex items-center justify-center bg-slate-950/60 p-4 backdrop-blur-sm"
          role="presentation"
        >
          <section
            role="dialog"
            aria-modal="true"
            aria-labelledby="stop-practice-title"
            className="w-full max-w-md overflow-hidden rounded-2xl border border-white/20 bg-white shadow-2xl"
          >
            <div className="flex items-start justify-between gap-4 border-b border-slate-100 p-5">
              <div className="flex gap-3">
                <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-rose-50 text-rose-600">
                  <Square className="size-4 fill-current" />
                </span>
                <div>
                  <h2 id="stop-practice-title" className="text-sm font-bold text-slate-900">
                    Dừng phiên luyện từng câu?
                  </h2>
                  <p className="mt-1 text-[10px] leading-relaxed text-slate-500">
                    Chế độ này không giới hạn số câu. Bạn có thể dừng tại đây hoặc tiếp tục với câu{' '}
                    {currentQuestionNumber}.
                  </p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setShowStopConfirmation(false)}
                className="flex size-8 shrink-0 items-center justify-center rounded-lg text-slate-500 transition hover:bg-slate-100 focus-visible:outline-2 focus-visible:outline-blue-600"
                aria-label="Đóng xác nhận dừng"
              >
                <X className="size-4" />
              </button>
            </div>
            <div className="flex justify-end gap-2 bg-slate-50 p-4">
              <button
                type="button"
                onClick={() => setShowStopConfirmation(false)}
                className="min-h-10 rounded-xl border border-slate-200 bg-white px-4 text-[10px] font-bold text-slate-700 transition hover:bg-slate-100 focus-visible:outline-2 focus-visible:outline-blue-600"
              >
                Tiếp tục luyện
              </button>
              <button
                type="button"
                onClick={() => navigate('/practice')}
                className="min-h-10 rounded-xl bg-rose-600 px-4 text-[10px] font-bold text-white transition hover:bg-rose-700 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-rose-600"
              >
                Dừng phiên
              </button>
            </div>
          </section>
        </div>
      )}
    </div>
  )
}
