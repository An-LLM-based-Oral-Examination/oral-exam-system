import { useState } from 'react'
import { AlertTriangle, ChevronDown, MessageSquareText, Trophy } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { MockExamScorecard as MockExamScorecardData } from '@/types/mockExam.types'

const CLASSIFICATION_STYLES: Record<string, string> = {
  'Xuất sắc': 'border-[#b7ebda] bg-[#edfbf6] text-[#0f8f6b]',
  Giỏi: 'border-[#b7ebda] bg-[#eefbf4] text-[#16886c]',
  Khá: 'border-[#c9d7fb] bg-[#eef3ff] text-[#315fc4]',
  'Trung bình': 'border-[#f0dfb4] bg-[#fff8eb] text-[#9a6414]',
  Yếu: 'border-[#f3c6ce] bg-[#fff1f3] text-[#c4475c]',
}

function formatDateTime(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return new Intl.DateTimeFormat('vi-VN', {
    hour: '2-digit',
    minute: '2-digit',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour12: false,
    timeZone: 'Asia/Ho_Chi_Minh',
  }).format(date)
}

function formatScore(value: number, digits = 2) {
  return value.toFixed(digits)
}

export function MockExamScorecard({ scorecard }: { scorecard: MockExamScorecardData }) {
  const [openQuestionId, setOpenQuestionId] = useState<string | null>(
    scorecard.questions[0]?.questionId ?? null,
  )
  const classificationClassName =
    CLASSIFICATION_STYLES[scorecard.classification] ??
    'border-[#dce4ef] bg-[#f7f9fc] text-[#52627a]'

  return (
    <div className="space-y-4">
      <section className="overflow-hidden rounded-2xl border border-[#dce5f1] bg-white shadow-[0_12px_34px_rgba(31,61,105,0.06)]">
        <div className="bg-[linear-gradient(125deg,#071a35_0%,#123263_58%,#24528e_100%)] px-5 py-5 text-white sm:px-6">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
            <div className="min-w-0">
              <p className="flex items-center gap-1.5 text-[9px] font-bold tracking-[0.1em] text-[#9cecf5] uppercase">
                <Trophy className="size-3.5" />
                Bảng điểm thi thử tức thì
              </p>
              <h2 className="mt-2 text-lg font-bold tracking-[-0.03em]">{scorecard.courseName}</h2>
              <p className="mt-1 text-[10px] text-[#c5d6ea]">
                {scorecard.courseCode} · {scorecard.answeredQuestions}/{scorecard.totalQuestions}{' '}
                câu đã trả lời · {scorecard.durationMinutes} phút
              </p>
            </div>
            <div className="flex items-end gap-3 self-start">
              <span
                className={cn(
                  'rounded-full border px-2.5 py-1 text-[9px] font-bold',
                  classificationClassName,
                )}
              >
                {scorecard.classification}
              </span>
              <div aria-label={`Điểm tổng ${formatScore(scorecard.totalScore)} trên 10`}>
                <p className="text-[9px] font-semibold tracking-[0.08em] text-[#b9cee7] uppercase">
                  Điểm
                </p>
                <p className="text-3xl leading-none font-black tracking-[-0.05em]">
                  {formatScore(scorecard.totalScore)}
                  <span className="ml-1 text-xs font-semibold text-[#a9c1df]">/10</span>
                </p>
              </div>
            </div>
          </div>
        </div>
        <div className="grid gap-2 px-5 py-4 text-[10px] text-[#66758a] sm:grid-cols-2 sm:px-6">
          <p>Bắt đầu: {formatDateTime(scorecard.startedAt)}</p>
          <p>Nộp bài: {formatDateTime(scorecard.submittedAt)}</p>
        </div>
        {scorecard.isOverdue && (
          <div className="mx-5 mb-4 flex items-start gap-2 rounded-xl border border-[#f1c7ce] bg-[#fff5f6] p-3 text-[10px] leading-relaxed text-[#ae3d50] sm:mx-6">
            <AlertTriangle className="mt-0.5 size-4 shrink-0" />
            Bài được nộp sau giờ máy chủ cho phép. Các câu chưa trả lời được tính 0 điểm.
          </div>
        )}
      </section>

      <div className="space-y-2.5">
        {scorecard.questions.map((question) => {
          const isOpen = openQuestionId === question.questionId
          return (
            <article
              key={question.questionId}
              className="overflow-hidden rounded-2xl border border-[#e0e7f0] bg-white"
            >
              <button
                type="button"
                onClick={() => setOpenQuestionId(isOpen ? null : question.questionId)}
                className="flex w-full items-center justify-between gap-3 px-4 py-3 text-left transition hover:bg-[#f8faff] focus-visible:outline-2 focus-visible:outline-[#245eea]"
                aria-expanded={isOpen}
              >
                <span className="min-w-0">
                  <span className="text-[9px] font-bold tracking-[0.06em] text-[#6072c7] uppercase">
                    Câu {question.orderIndex} · {question.bloomLevel} · {question.difficulty}
                  </span>
                  <span className="mt-1 block truncate text-[12px] font-bold text-[#1e2f47]">
                    {question.title}
                  </span>
                </span>
                <span className="flex shrink-0 items-center gap-2">
                  <span className="rounded-full bg-[#eef3ff] px-2 py-1 text-[10px] font-bold text-[#315fc4]">
                    {formatScore(question.score, 1)}/10
                  </span>
                  <ChevronDown
                    className={cn('size-4 text-[#74839a] transition', isOpen && 'rotate-180')}
                  />
                </span>
              </button>

              {isOpen && (
                <div className="space-y-3 border-t border-[#edf1f6] px-4 py-4">
                  <p className="text-[11px] leading-relaxed text-[#3d4e66]">{question.content}</p>
                  <div className="rounded-xl bg-[#f7f9fc] p-3">
                    <p className="text-[9px] font-bold tracking-[0.06em] text-[#6072c7] uppercase">
                      Transcript đã nói
                    </p>
                    <p className="mt-1.5 text-[11px] leading-relaxed text-[#4d5e76]">
                      {question.transcript?.trim() || 'Không có câu trả lời.'}
                    </p>
                  </div>
                  <div className="rounded-xl border border-[#d9f3ea] bg-[#f4fbf8] p-3">
                    <p className="flex items-center gap-1.5 text-[9px] font-bold tracking-[0.06em] text-[#16886c] uppercase">
                      <MessageSquareText className="size-3.5" />
                      Nhận xét của AI
                    </p>
                    <p className="mt-1.5 text-[11px] leading-relaxed text-[#3f5c52]">
                      {question.feedback?.trim() || 'Chưa có nhận xét cho câu này.'}
                    </p>
                    {question.confidenceScore !== null && (
                      <p className="mt-2 text-[9px] text-[#5f8f80]">
                        Độ tin cậy: {Math.round(question.confidenceScore * 100)}%
                      </p>
                    )}
                  </div>
                  <div className="space-y-2">
                    {question.criteriaScores.map((criterion) => {
                      const ratio =
                        criterion.maxScore > 0
                          ? Math.min(100, Math.max(0, (criterion.score / criterion.maxScore) * 100))
                          : 0
                      return (
                        <div
                          key={criterion.criterionId}
                          className="rounded-xl border border-[#e6edf5] px-3 py-2.5"
                        >
                          <div className="flex items-start justify-between gap-3">
                            <p className="text-[11px] font-bold text-[#25364e]">
                              {criterion.criterionName}
                            </p>
                            <p className="shrink-0 text-[10px] font-bold text-[#315fc4]">
                              {formatScore(criterion.score, 1)}/{formatScore(criterion.maxScore, 1)}
                            </p>
                          </div>
                          <div className="mt-2 h-1.5 overflow-hidden rounded-full bg-[#eef2f7]">
                            <div
                              className="h-full rounded-full bg-[#4f7df0]"
                              style={{ width: `${ratio}%` }}
                            />
                          </div>
                          {criterion.comment && (
                            <p className="mt-2 text-[10px] leading-relaxed text-[#6a7b90]">
                              {criterion.comment}
                            </p>
                          )}
                        </div>
                      )
                    })}
                  </div>
                </div>
              )}
            </article>
          )
        })}
      </div>
    </div>
  )
}
