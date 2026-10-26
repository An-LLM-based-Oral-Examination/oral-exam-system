import { useMemo, useState } from 'react'
import { AlertTriangle, ChevronDown, MessageSquareText, Trophy } from 'lucide-react'
import { cn } from '@/lib/utils'
import { QuestionImage } from '@/components/question/QuestionImage'
import type {
  MockExamQuestionScore,
  MockExamScorecard as MockExamScorecardData,
} from '@/types/mockExam.types'

const CLASSIFICATION_STYLES: Record<string, string> = {
  'Xuất sắc': 'bg-[#edfbf6] text-[#0f8f6b]',
  Giỏi: 'bg-[#eefbf4] text-[#16886c]',
  Khá: 'bg-[#4988C4]/16 text-[#1C4D8D]',
  'Trung bình': 'bg-[#fff8eb] text-[#9a6414]',
  Yếu: 'bg-[#fff1f3] text-[#c4475c]',
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

function difficultyLabel(difficulty: string) {
  if (difficulty === 'easy') return 'Dễ'
  if (difficulty === 'medium') return 'Trung bình'
  if (difficulty === 'hard') return 'Khó'
  return difficulty
}

function questionKey(question: MockExamQuestionScore) {
  return question.answerId ?? `${question.questionId}-${question.orderIndex}-${question.isFollowUp ? 'fu' : 'main'}`
}

function AnswerBlock({
  label,
  transcript,
}: {
  label: string
  transcript: string | null | undefined
}) {
  return (
    <div className="rounded-xl bg-[#4988C4]/16 p-3">
      <p className="text-xs font-bold text-[#1C4D8D]">{label}</p>
      <p className="mt-1.5 text-sm leading-relaxed whitespace-pre-line text-[#0F2854]">
        {transcript?.trim() || 'Không có câu trả lời.'}
      </p>
    </div>
  )
}

function FeedbackBlock({ question }: { question: MockExamQuestionScore }) {
  return (
    <div className="rounded-xl border border-[#d9f3ea] bg-[#f4fbf8] p-3">
      <p className="flex items-center gap-1.5 text-xs font-bold text-[#16886c]">
        <MessageSquareText className="size-3.5" />
        Nhận xét
      </p>
      <p className="mt-1.5 text-sm leading-relaxed text-[#3f5c52]">
        {question.feedback?.trim() || 'Chưa có nhận xét cho câu này.'}
      </p>
      {question.confidenceScore !== null && question.confidenceScore !== undefined && (
        <p className="mt-2 text-xs text-[#5f8f80]">
          Độ tin cậy {Math.round(question.confidenceScore * 100)}%
        </p>
      )}
    </div>
  )
}

function CriteriaBlock({ question }: { question: MockExamQuestionScore }) {
  if (question.criteriaScores.length === 0) return null
  return (
    <div className="space-y-2">
      {question.criteriaScores.map((criterion) => {
        const ratio =
          criterion.maxScore > 0
            ? Math.min(100, Math.max(0, (criterion.score / criterion.maxScore) * 100))
            : 0
        return (
          <div key={criterion.criterionId} className="rounded-xl border border-[#e6edf5] px-3 py-2.5">
            <div className="flex items-start justify-between gap-3">
              <p className="text-sm font-bold text-[#25364e]">{criterion.criterionName}</p>
              <p className="shrink-0 text-sm font-bold text-[#1C4D8D]">
                {formatScore(criterion.score, 1)}/{formatScore(criterion.maxScore, 1)}
              </p>
            </div>
            <div className="mt-2 h-1.5 overflow-hidden rounded-full bg-[#eef2f7]">
              <div className="h-full rounded-full bg-[#4f7df0]" style={{ width: `${ratio}%` }} />
            </div>
            {criterion.comment && (
              <p className="mt-2 text-sm leading-relaxed text-[#5b6b84]">{criterion.comment}</p>
            )}
          </div>
        )
      })}
    </div>
  )
}

export function MockExamScorecard({ scorecard }: { scorecard: MockExamScorecardData }) {
  const mainQuestions = useMemo(
    () => scorecard.questions.filter((item) => !item.isFollowUp),
    [scorecard.questions],
  )
  const followUpsByParent = useMemo(() => {
    const map = new Map<string, MockExamQuestionScore[]>()
    for (const item of scorecard.questions) {
      if (!item.isFollowUp) continue
      const parentKey =
        item.parentAnswerId ??
        mainQuestions.find((main) => main.orderIndex === item.orderIndex)?.answerId ??
        `order-${item.orderIndex}`
      const list = map.get(parentKey) ?? []
      list.push(item)
      map.set(parentKey, list)
    }
    return map
  }, [mainQuestions, scorecard.questions])

  const [openQuestionKey, setOpenQuestionKey] = useState<string | null>(() =>
    mainQuestions[0] ? questionKey(mainQuestions[0]) : null,
  )
  const classificationClassName =
    CLASSIFICATION_STYLES[scorecard.classification] ?? 'bg-[#4988C4]/16 text-[#5b6b84]'

  return (
    <div className="space-y-3">
      <section className="overflow-hidden rounded-2xl border border-[#4988C4]/45 bg-white shadow-[0_10px_28px_rgba(23,48,96,0.06)]">
        <div className="bg-[linear-gradient(118deg,#0F2854_0%,#1C4D8D_58%,#4988C4_100%)] px-5 py-5 text-white">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
            <div className="min-w-0">
              <p className="flex items-center gap-1.5 text-xs font-bold tracking-[0.08em] text-[#BDE8F5] uppercase">
                <Trophy className="size-3.5" />
                Bảng điểm thi thử
              </p>
              <p className="mt-3 inline-flex rounded-full bg-white/15 px-2.5 py-1 text-xs font-bold">
                {scorecard.courseCode}
              </p>
              <h2 className="mt-2 text-xl leading-snug font-bold">{scorecard.courseName}</h2>
              <p className="mt-2 text-sm text-[#d5e2f7]">
                {scorecard.answeredQuestions}/{scorecard.totalQuestions} câu đã trả lời ·{' '}
                {scorecard.durationMinutes} phút
              </p>
            </div>
            <div className="flex items-center gap-3 self-start rounded-2xl bg-white/10 px-4 py-3">
              <div>
                <p className="text-xs font-semibold text-[#BDE8F5]">Điểm</p>
                <p className="text-4xl leading-none font-black tracking-[-0.04em]">
                  {formatScore(scorecard.totalScore)}
                  <span className="ml-1 text-sm font-semibold text-[#BDE8F5]">/10</span>
                </p>
              </div>
              <span
                className={cn(
                  'rounded-full px-3 py-1 text-xs font-bold',
                  classificationClassName,
                )}
              >
                {scorecard.classification}
              </span>
            </div>
          </div>
        </div>
        <div className="grid gap-1 px-5 py-3 text-sm text-[#5b6b84] sm:grid-cols-2">
          <p>Bắt đầu: {formatDateTime(scorecard.startedAt)}</p>
          <p>Nộp bài: {formatDateTime(scorecard.submittedAt)}</p>
        </div>
        {scorecard.isOverdue && (
          <div className="mx-5 mb-4 flex items-start gap-2 rounded-xl border border-[#f1c7ce] bg-[#fff5f6] p-3 text-sm leading-relaxed text-[#ae3d50]">
            <AlertTriangle className="mt-0.5 size-4 shrink-0" />
            Bài được nộp sau giờ cho phép. Các câu chưa trả lời được tính 0 điểm.
          </div>
        )}
      </section>

      <div className="space-y-2">
        {mainQuestions.map((question) => {
          const key = questionKey(question)
          const isOpen = openQuestionKey === key
          const followUps =
            followUpsByParent.get(question.answerId ?? '') ??
            followUpsByParent.get(`order-${question.orderIndex}`) ??
            []

          return (
            <article
              key={key}
              className="overflow-hidden rounded-2xl border border-[#4988C4]/45 bg-white"
            >
              <button
                type="button"
                onClick={() => setOpenQuestionKey(isOpen ? null : key)}
                className="flex w-full items-center justify-between gap-3 px-4 py-3 text-left hover:bg-[#4988C4]/18"
                aria-expanded={isOpen}
              >
                <span className="min-w-0">
                  <span className="text-xs font-bold text-[#1C4D8D]">
                    Câu {question.orderIndex} · {question.bloomLevel} ·{' '}
                    {difficultyLabel(question.difficulty)}
                    {followUps.length > 0 ? ` · ${followUps.length} câu phụ` : ''}
                  </span>
                  <span
                    className={cn(
                      'mt-1 block text-sm leading-relaxed font-semibold whitespace-pre-line text-[#0F2854]',
                      !isOpen && 'line-clamp-2',
                    )}
                  >
                    {question.content}
                  </span>
                </span>
                <span className="flex shrink-0 items-center gap-2">
                  <span className="rounded-full bg-[#4988C4]/16 px-2.5 py-1 text-sm font-bold text-[#1C4D8D]">
                    {formatScore(question.score, 1)}/10
                  </span>
                  <ChevronDown
                    className={cn('size-4 text-[#74839a] transition', isOpen && 'rotate-180')}
                  />
                </span>
              </button>

              {isOpen && (
                <div className="space-y-3 border-t border-[#4988C4]/45 px-4 py-4">
                  {question.imageUrl?.trim() ? (
                    <div className="overflow-hidden rounded-xl border border-[#4988C4]/45 bg-white p-3">
                      <QuestionImage
                        imageUrl={question.imageUrl}
                        imageAlt={question.imageAlt}
                        framed={false}
                        showCaption={false}
                        className="w-full overflow-hidden rounded-xl"
                      />
                    </div>
                  ) : null}
                  <AnswerBlock label="Câu trả lời" transcript={question.transcript} />
                  <FeedbackBlock question={question} />
                  <CriteriaBlock question={question} />

                  {followUps.map((followUp, index) => (
                    <div
                      key={questionKey(followUp)}
                      className="space-y-3 rounded-2xl border border-[#4988C4]/35 bg-[#F4FBFD] p-3.5"
                    >
                      <div className="flex items-start justify-between gap-3">
                        <div className="min-w-0">
                          <p className="text-xs font-bold text-[#1C4D8D]">
                            Câu hỏi phụ {index + 1}
                          </p>
                          <p className="mt-1.5 text-sm leading-relaxed font-semibold whitespace-pre-line text-[#0F2854]">
                            {followUp.content}
                          </p>
                        </div>
                        <span className="shrink-0 rounded-full bg-white px-2.5 py-1 text-sm font-bold text-[#1C4D8D]">
                          {formatScore(followUp.score, 1)}/10
                        </span>
                      </div>
                      <AnswerBlock label="Câu trả lời phụ" transcript={followUp.transcript} />
                      <FeedbackBlock question={followUp} />
                    </div>
                  ))}
                </div>
              )}
            </article>
          )
        })}
      </div>
    </div>
  )
}
