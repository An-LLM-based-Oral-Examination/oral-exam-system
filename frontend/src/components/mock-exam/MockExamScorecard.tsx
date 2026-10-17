import { useState } from 'react'
import { AlertTriangle, ChevronDown, MessageSquareText, Trophy } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { MockExamScorecard as MockExamScorecardData } from '@/types/mockExam.types'

const CLASSIFICATION_STYLES: Record<string, string> = {
  'Xuất sắc': 'border-emerald-300 bg-emerald-50 text-[#16A34A]',
  Giỏi: 'border-emerald-200 bg-emerald-50 text-[#16A34A]',
  Khá: 'border-[#FED7AA] bg-[#FFF7ED] text-[#C2410C]',
  'Trung bình': 'border-amber-200 bg-amber-50 text-amber-700',
  Yếu: 'border-rose-200 bg-rose-50 text-rose-700',
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
    'border-[#E2E8F0] bg-[#F8FAFC] text-[#475569]'

  return (
    <div className="space-y-4">
      <section className="overflow-hidden rounded-2xl border border-[#FED7AA] border-t-4 border-t-[#F26F21] bg-white shadow-xs">
        <div className="bg-[#FFF7ED]/60 px-5 py-4 text-[#1E293B] sm:px-6">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
            <div className="min-w-0">
              <p className="flex items-center gap-1.5 text-xs font-bold tracking-wider text-[#C2410C] uppercase">
                <Trophy className="size-4 text-[#F26F21]" />
                Bảng điểm thi thử chi tiết
              </p>
              <h2 className="mt-1 text-base font-bold tracking-tight text-[#1E293B] sm:text-lg">
                {scorecard.courseName}
              </h2>
              <p className="mt-0.5 text-xs text-[#64748B]">
                {scorecard.courseCode} · {scorecard.answeredQuestions}/{scorecard.totalQuestions}{' '}
                câu đã trả lời · {scorecard.durationMinutes} phút
              </p>
            </div>
            <div className="flex items-end gap-2.5 self-start">
              <span
                className={cn(
                  'rounded-full border px-3 py-1 text-xs font-bold',
                  classificationClassName,
                )}
              >
                {scorecard.classification}
              </span>
              <div
                className="rounded-2xl border border-[#FED7AA] bg-white px-4 py-2 shadow-2xs"
                aria-label={`Điểm tổng ${formatScore(scorecard.totalScore)} trên 10`}
              >
                <p className="text-xs font-semibold tracking-wider text-[#64748B] uppercase">
                  Tổng điểm
                </p>
                <p className="mt-0.5 text-2xl leading-none font-black tracking-tight text-[#C2410C]">
                  {formatScore(scorecard.totalScore)}
                  <span className="ml-1 text-xs font-semibold text-[#64748B]">/10</span>
                </p>
              </div>
            </div>
          </div>
        </div>
        <div className="grid gap-2 border-t border-[#E2E8F0] bg-[#F8FAFC] px-5 py-3 text-xs font-medium text-[#64748B] sm:grid-cols-2 sm:px-6">
          <p>Bắt đầu: {formatDateTime(scorecard.startedAt)}</p>
          <p>Nộp bài: {formatDateTime(scorecard.submittedAt)}</p>
        </div>
        {scorecard.isOverdue && (
          <div className="mx-5 my-3 flex items-start gap-2 rounded-xl border border-rose-200 bg-rose-50 p-3 text-xs leading-relaxed font-medium text-rose-700 sm:mx-6">
            <AlertTriangle className="mt-0.5 size-4 shrink-0" />
            Bài được nộp sau giờ máy chủ cho phép. Các câu chưa trả lời được tính 0 điểm.
          </div>
        )}
      </section>

      <div className="space-y-2.5">
        {scorecard.questions.map((question) => {
          const isOpen = openQuestionId === question.questionId
          const isGoodScore = question.score >= 5
          return (
            <article
              key={question.questionId}
              className="overflow-hidden rounded-2xl border border-[#E2E8F0] bg-white shadow-2xs"
            >
              <button
                type="button"
                onClick={() => setOpenQuestionId(isOpen ? null : question.questionId)}
                className="flex w-full items-center justify-between gap-3 px-4 py-3 text-left transition hover:bg-[#FFF7ED]/40 focus-visible:outline-2 focus-visible:outline-[#F26F21] sm:px-5"
                aria-expanded={isOpen}
              >
                <span className="min-w-0">
                  <span className="text-xs font-bold tracking-wider text-[#C2410C] uppercase">
                    Câu {question.orderIndex} · {question.bloomLevel} · {question.difficulty}
                  </span>
                  <span className="mt-0.5 block truncate text-sm font-bold text-[#1E293B]">
                    {question.title}
                  </span>
                </span>
                <span className="flex shrink-0 items-center gap-2">
                  <span
                    className={cn(
                      'rounded-full border px-2.5 py-0.5 font-mono text-xs font-bold',
                      isGoodScore
                        ? 'border-emerald-200 bg-emerald-50 text-[#16A34A]'
                        : 'border-[#FED7AA] bg-[#FFF7ED] text-[#C2410C]',
                    )}
                  >
                    {formatScore(question.score, 1)}/10
                  </span>
                  <ChevronDown
                    className={cn('size-4 text-[#64748B] transition', isOpen && 'rotate-180')}
                  />
                </span>
              </button>

              {isOpen && (
                <div className="space-y-3 border-t border-[#E2E8F0] px-4 py-3.5 sm:px-5">
                  <p className="text-sm leading-relaxed font-medium text-[#1E293B]">
                    {question.content}
                  </p>
                  <div className="rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] p-3">
                    <p className="text-xs font-bold tracking-wider text-[#64748B] uppercase">
                      Transcript đã nói
                    </p>
                    <p className="mt-1 text-sm leading-relaxed text-[#1E293B]">
                      {question.transcript?.trim() || 'Không có câu trả lời.'}
                    </p>
                  </div>
                  <div className="rounded-xl border border-[#FED7AA] bg-[#FFF7ED] p-3">
                    <p className="flex items-center gap-1.5 text-xs font-bold tracking-wider text-[#C2410C] uppercase">
                      <MessageSquareText className="size-4 text-[#F26F21]" />
                      Nhận xét tổng quan của AI
                    </p>
                    <p className="mt-1 text-sm leading-relaxed text-[#1E293B]">
                      {question.feedback?.trim() || 'Chưa có nhận xét cho câu này.'}
                    </p>
                    {question.confidenceScore !== null && (
                      <p className="mt-1.5 text-xs font-medium text-[#9A3412]">
                        Độ tin cậy đánh giá: {Math.round(question.confidenceScore * 100)}%
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
                          className="rounded-xl border border-[#E2E8F0] bg-white px-3.5 py-2.5"
                        >
                          <div className="flex items-start justify-between gap-3">
                            <p className="text-xs font-bold text-[#1E293B] sm:text-sm">
                              {criterion.criterionName}
                            </p>
                            <p className="shrink-0 font-mono text-xs font-bold text-[#16A34A]">
                              {formatScore(criterion.score, 1)}/{formatScore(criterion.maxScore, 1)}
                            </p>
                          </div>
                          <div className="mt-1.5 h-1.5 overflow-hidden rounded-full bg-[#E2E8F0]">
                            <div
                              className={cn(
                                'h-full rounded-full',
                                ratio >= 70 ? 'bg-[#16A34A]' : 'bg-[#F26F21]',
                              )}
                              style={{ width: `${ratio}%` }}
                            />
                          </div>
                          {criterion.comment && (
                            <p className="mt-1.5 text-xs leading-relaxed text-[#475569]">
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
