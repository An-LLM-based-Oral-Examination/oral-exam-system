import { useMemo, useState } from 'react'
import { ChevronDown, MessageSquareText, Sparkles, Trophy } from 'lucide-react'
import { cn } from '@/lib/utils'
import { QuestionImage } from '@/components/question/QuestionImage'
import type {
  PracticeAnswerDetail,
  PracticeEvaluationDetail,
  PracticeQuestion,
  PracticeSessionDetail,
} from '@/types/practice.types'

const CLASSIFICATION_STYLES: Record<string, string> = {
  'Xuất sắc': 'bg-[#edfbf6] text-[#0f8f6b]',
  Giỏi: 'bg-[#eefbf4] text-[#16886c]',
  Khá: 'bg-[#4988C4]/16 text-[#1C4D8D]',
  'Trung bình': 'bg-[#fff8eb] text-[#9a6414]',
  Yếu: 'bg-[#fff1f3] text-[#c4475c]',
  'Đang chấm': 'bg-[#fff8eb] text-[#9a6414]',
  'Chưa có điểm': 'bg-white/20 text-white',
}

function classifyScore(score: number | null): string {
  if (score === null || !Number.isFinite(score)) return 'Chưa có điểm'
  if (score >= 9.0) return 'Xuất sắc'
  if (score >= 8.0) return 'Giỏi'
  if (score >= 6.5) return 'Khá'
  if (score >= 5.0) return 'Trung bình'
  return 'Yếu'
}

function formatDateTime(value: string | null) {
  if (!value) return 'Chưa kết thúc'
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

function formatScore(value: number | null, digits = 2) {
  if (value === null || !Number.isFinite(value)) return '—'
  return value.toFixed(digits)
}

function AnswerBlock({
  label,
  transcript,
}: {
  label: string
  transcript: string | null | undefined
}) {
  const cleaned = transcript?.trim()
  const isSkipped =
    !cleaned || cleaned === '[Không trả lời — Nộp bài sớm]' || cleaned === '[Bỏ qua câu hỏi]'
  return (
    <div className="rounded-xl bg-[#4988C4]/16 p-3">
      <p className="text-xs font-bold text-[#1C4D8D]">{label}</p>
      <p className="mt-1.5 text-sm leading-relaxed whitespace-pre-line text-[#0F2854]">
        {isSkipped ? 'Không có câu trả lời (Bỏ trống hoặc nộp sớm).' : cleaned}
      </p>
    </div>
  )
}

function FeedbackBlock({
  feedback,
  confidenceScore,
  status,
}: {
  feedback: string | null | undefined
  confidenceScore?: number | null
  status?: string
}) {
  return (
    <div className="rounded-xl border border-[#d9f3ea] bg-[#f4fbf8] p-3">
      <p className="flex items-center gap-1.5 text-xs font-bold text-[#16886c]">
        <MessageSquareText className="size-3.5" />
        Nhận xét đánh giá chi tiết
      </p>
      <p className="mt-1.5 text-sm leading-relaxed whitespace-pre-line text-[#3f5c52]">
        {feedback?.trim() ||
          (status === 'pending' || status === 'grading'
            ? 'Đang chấm điểm câu trả lời này...'
            : 'Chưa có nhận xét cho câu hỏi này.')}
      </p>
      {confidenceScore !== null && confidenceScore !== undefined && (
        <p className="mt-2 text-xs text-[#5f8f80]">
          Độ khớp tiêu chí: {Math.round(confidenceScore * 100)}%
        </p>
      )}
    </div>
  )
}

function CriteriaBlock({ criteriaScores }: { criteriaScores: PracticeEvaluationDetail[] }) {
  if (!criteriaScores || criteriaScores.length === 0) return null
  return (
    <div className="space-y-2">
      <p className="text-xs font-bold tracking-[0.04em] text-[#1C4D8D] uppercase">
        Chi tiết theo tiêu chí Rubric
      </p>
      {criteriaScores.map((criterion, idx) => {
        const ratio = Math.min(100, Math.max(0, (criterion.score / 10) * 100))
        return (
          <div
            key={`${criterion.criterionId}-${idx}`}
            className="rounded-xl border border-[#e6edf5] bg-white px-3 py-2.5"
          >
            <div className="flex items-start justify-between gap-3">
              <p className="text-sm font-bold text-[#25364e]">{criterion.criterionName}</p>
              <p className="shrink-0 text-sm font-bold text-[#1C4D8D]">
                {formatScore(criterion.score, 1)} điểm
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

interface CombinedQuestionItem {
  key: string
  orderIndex: number
  questionId: string
  content: string
  imageUrl?: string | null
  imageAlt?: string | null
  rubricCriteria: string[]
  mainAnswer: PracticeAnswerDetail | null
  followUpAnswers: Array<{
    answer: PracticeAnswerDetail
    promptText: string
  }>
}

export function PracticeSessionScorecard({ session }: { session: PracticeSessionDetail }) {
  const combinedQuestions = useMemo<CombinedQuestionItem[]>(() => {
    const questionMap = new Map<string, PracticeQuestion>()
    session.questions.forEach((q) => questionMap.set(q.questionId, q))

    const mainAnswers = session.answers.filter((a) => !a.isFollowUp)
    const followUpAnswers = session.answers.filter((a) => a.isFollowUp)

    const orderedQuestionIds: string[] = []
    session.questions.forEach((q) => {
      if (!orderedQuestionIds.includes(q.questionId)) {
        orderedQuestionIds.push(q.questionId)
      }
    })
    mainAnswers.forEach((a) => {
      if (!orderedQuestionIds.includes(a.questionId)) {
        orderedQuestionIds.push(a.questionId)
      }
    })

    return orderedQuestionIds.map((questionId, idx) => {
      const qMeta = questionMap.get(questionId)
      const qMainAnswers = mainAnswers.filter((a) => a.questionId === questionId)
      const mainAnswer = qMainAnswers[qMainAnswers.length - 1] ?? null

      const relatedFollowUps = followUpAnswers.filter(
        (fu) =>
          (mainAnswer && fu.parentAnswerId === mainAnswer.answerId) ||
          fu.questionId === questionId,
      )

      const followUpsWithPrompts = relatedFollowUps.map((fu, fuIdx) => {
        let promptText = 'Câu hỏi phụ đào sâu theo ngữ cảnh câu trả lời'
        if (fuIdx === 0 && mainAnswer?.followUpPrompt?.trim()) {
          promptText = mainAnswer.followUpPrompt.trim()
        } else if (fuIdx > 0 && relatedFollowUps[fuIdx - 1]?.followUpPrompt?.trim()) {
          promptText = relatedFollowUps[fuIdx - 1].followUpPrompt!.trim()
        }
        return {
          answer: fu,
          promptText,
        }
      })

      return {
        key: `${questionId}-${idx}`,
        orderIndex: idx + 1,
        questionId,
        content: qMeta?.content ?? `Câu hỏi luyện tập #${idx + 1}`,
        imageUrl: qMeta?.imageUrl,
        imageAlt: qMeta?.imageAlt,
        rubricCriteria: qMeta?.rubricCriteria ?? [],
        mainAnswer,
        followUpAnswers: followUpsWithPrompts,
      }
    })
  }, [session.questions, session.answers])

  const [openQuestionKey, setOpenQuestionKey] = useState<string | null>(() =>
    combinedQuestions[0] ? combinedQuestions[0].key : null,
  )

  const summary = useMemo(() => {
    const mainAnsweredCount = combinedQuestions.filter(
      (item) =>
        item.mainAnswer !== null &&
        item.mainAnswer.answerText.trim() !== '' &&
        item.mainAnswer.answerText.trim() !== '[Không trả lời — Nộp bài sớm]',
    ).length

    const hasPendingAnswers = session.answers.some(
      (a) => a.status === 'pending' || a.status === 'grading',
    )

    let averageScore: number | null = null
    if (!hasPendingAnswers) {
      if (session.practiceMode === 'full_session' && session.status === 'completed') {
        const totalMain = Math.max(1, combinedQuestions.length)
        const sumMain = combinedQuestions.reduce(
          (acc, q) => acc + (q.mainAnswer?.totalScore ?? 0),
          0,
        )
        averageScore = Math.round((sumMain / totalMain) * 100) / 100
      } else {
        const mainEffectiveScores: number[] = []
        combinedQuestions.forEach((q) => {
          const mainScore = q.mainAnswer?.totalScore
          if (mainScore === null || mainScore === undefined || !Number.isFinite(mainScore)) return
          const fuScores = q.followUpAnswers
            .map((fu) => fu.answer.totalScore)
            .filter((s): s is number => s !== null && s !== undefined && Number.isFinite(s))
          if (fuScores.length === 0) {
            mainEffectiveScores.push(mainScore)
          } else {
            const avgFu = fuScores.reduce((acc, s) => acc + s, 0) / fuScores.length
            if (avgFu >= mainScore) {
              mainEffectiveScores.push(Math.max(mainScore, ...fuScores))
            } else {
              const all = [mainScore, ...fuScores]
              mainEffectiveScores.push(all.reduce((acc, s) => acc + s, 0) / all.length)
            }
          }
        })
        if (mainEffectiveScores.length > 0) {
          const sum = mainEffectiveScores.reduce((acc, s) => acc + s, 0)
          averageScore = Math.round((sum / mainEffectiveScores.length) * 100) / 100
        }
      }
    }

    const classification = hasPendingAnswers ? 'Đang chấm' : classifyScore(averageScore)
    return {
      mainAnsweredCount,
      totalMainQuestions: combinedQuestions.length,
      totalFollowUps: session.answers.filter((a) => a.isFollowUp).length,
      averageScore,
      classification,
    }
  }, [combinedQuestions, session.answers, session.practiceMode, session.status])

  const classificationClassName =
    CLASSIFICATION_STYLES[summary.classification] ?? 'bg-white/20 text-white'

  return (
    <div className="space-y-3">
      <section className="overflow-hidden rounded-2xl border border-[#4988C4]/45 bg-white shadow-[0_10px_28px_rgba(23,48,96,0.06)]">
        <div className="bg-[linear-gradient(118deg,#0F2854_0%,#1C4D8D_58%,#4988C4_100%)] px-5 py-5 text-white">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
            <div className="min-w-0">
              <p className="flex items-center gap-1.5 text-xs font-bold tracking-[0.08em] text-[#BDE8F5] uppercase">
                <Trophy className="size-3.5" />
                Bảng điểm & nhận xét phiên luyện tập
              </p>
              <div className="mt-3 flex flex-wrap items-center gap-2">
                <span className="inline-flex rounded-full bg-white/15 px-2.5 py-1 text-xs font-bold">
                  {session.courseCode}
                </span>
                <span className="inline-flex items-center gap-1 rounded-full bg-[#BDE8F5]/20 px-2.5 py-1 text-xs font-semibold text-[#BDE8F5]">
                  <Sparkles className="size-3" />
                  {session.practiceMode === 'full_session'
                    ? 'Luyện trọn gói'
                    : 'Luyện từng câu'}
                </span>
              </div>
              <h2 className="mt-2 text-xl leading-snug font-bold">{session.courseName}</h2>
              <p className="mt-2 text-sm text-[#d5e2f7]">
                Đã trả lời {summary.mainAnsweredCount}/{summary.totalMainQuestions} câu chính
                {summary.totalFollowUps > 0 ? ` · ${summary.totalFollowUps} câu hỏi phụ` : ''}
              </p>
            </div>

            <div className="flex items-center gap-3 self-start rounded-2xl bg-white/10 px-4 py-3">
              <div>
                <p className="text-xs font-semibold text-[#BDE8F5]">Điểm trung bình</p>
                <p className="text-4xl leading-none font-black tracking-[-0.04em]">
                  {formatScore(summary.averageScore)}
                  <span className="ml-1 text-sm font-semibold text-[#BDE8F5]">/10</span>
                </p>
              </div>
              <span
                className={cn(
                  'rounded-full px-3 py-1 text-xs font-bold',
                  classificationClassName,
                )}
              >
                {summary.classification}
              </span>
            </div>
          </div>
        </div>

        <div className="grid gap-1 px-5 py-3 text-sm text-[#5b6b84] sm:grid-cols-2">
          <p>Bắt đầu: {formatDateTime(session.startedAt)}</p>
          <p>Kết thúc: {formatDateTime(session.endedAt)}</p>
        </div>
      </section>

      <div className="space-y-2">
        {combinedQuestions.map((item) => {
          const isOpen = openQuestionKey === item.key
          const questionScore =
            item.mainAnswer?.totalScore ?? (session.status === 'completed' ? 0 : null)

          return (
            <article
              key={item.key}
              className="overflow-hidden rounded-2xl border border-[#4988C4]/45 bg-white"
            >
              <button
                type="button"
                onClick={() => setOpenQuestionKey(isOpen ? null : item.key)}
                className="flex w-full items-center justify-between gap-3 px-4 py-3 text-left transition hover:bg-[#4988C4]/14"
                aria-expanded={isOpen}
              >
                <span className="min-w-0">
                  <span className="text-xs font-bold text-[#1C4D8D]">
                    Câu {item.orderIndex}
                    {item.followUpAnswers.length > 0
                      ? ` · ${item.followUpAnswers.length} câu phụ`
                      : ''}
                    {!item.mainAnswer ? ' · Bỏ trống' : ''}
                  </span>
                  <span
                    className={cn(
                      'mt-1 block text-sm leading-relaxed font-semibold whitespace-pre-line text-[#0F2854]',
                      !isOpen && 'line-clamp-2',
                    )}
                  >
                    {item.content}
                  </span>
                </span>
                <span className="flex shrink-0 items-center gap-2">
                  <span
                    className={cn(
                      'rounded-full px-2.5 py-1 text-sm font-bold',
                      questionScore !== null
                        ? 'bg-[#4988C4]/16 text-[#1C4D8D]'
                        : 'bg-[#fff8eb] text-[#9a6414]',
                    )}
                  >
                    {questionScore !== null ? `${formatScore(questionScore, 1)}/10` : 'Chưa chấm'}
                  </span>
                  <ChevronDown
                    className={cn('size-4 text-[#74839a] transition', isOpen && 'rotate-180')}
                  />
                </span>
              </button>

              {isOpen && (
                <div className="space-y-3 border-t border-[#4988C4]/45 px-4 py-4">
                  {item.imageUrl?.trim() ? (
                    <div className="overflow-hidden rounded-xl border border-[#4988C4]/45 bg-white p-3">
                      <QuestionImage
                        imageUrl={item.imageUrl}
                        imageAlt={item.imageAlt}
                        framed={false}
                        showCaption={false}
                        className="w-full overflow-hidden rounded-xl"
                      />
                    </div>
                  ) : null}

                  <AnswerBlock
                    label="Câu trả lời của bạn"
                    transcript={item.mainAnswer?.answerText}
                  />
                  <FeedbackBlock
                    feedback={
                      item.mainAnswer?.feedback ??
                      (session.status === 'completed' && !item.mainAnswer
                        ? 'Sinh viên không nộp câu trả lời cho câu hỏi này (0.0/10 điểm).'
                        : null)
                    }
                    confidenceScore={item.mainAnswer?.confidenceScore}
                    status={item.mainAnswer?.status}
                  />
                  <CriteriaBlock criteriaScores={item.mainAnswer?.criteriaScores ?? []} />

                  {item.followUpAnswers.map((fuItem, index) => (
                    <div
                      key={fuItem.answer.answerId}
                      className="space-y-3 rounded-2xl border border-[#4988C4]/35 bg-[#F4FBFD] p-3.5"
                    >
                      <div className="flex items-start justify-between gap-3">
                        <div className="min-w-0">
                          <p className="text-xs font-bold text-[#1C4D8D]">
                            Câu hỏi phụ {index + 1}
                          </p>
                          <p className="mt-1.5 text-sm leading-relaxed font-semibold whitespace-pre-line text-[#0F2854]">
                            {fuItem.promptText}
                          </p>
                        </div>
                        <span className="shrink-0 rounded-full bg-white px-2.5 py-1 text-sm font-bold text-[#1C4D8D]">
                          {fuItem.answer.totalScore !== null
                            ? `${formatScore(fuItem.answer.totalScore, 1)}/10`
                            : 'Đang chấm'}
                        </span>
                      </div>
                      <AnswerBlock
                        label="Câu trả lời phụ"
                        transcript={fuItem.answer.answerText}
                      />
                      <FeedbackBlock
                        feedback={fuItem.answer.feedback}
                        confidenceScore={fuItem.answer.confidenceScore}
                        status={fuItem.answer.status}
                      />
                      <CriteriaBlock criteriaScores={fuItem.answer.criteriaScores} />
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
