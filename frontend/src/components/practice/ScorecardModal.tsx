import React, { useEffect, useRef } from 'react'
import { Card, CardHeader, CardTitle, CardContent, CardFooter } from '@/components/common/Card'
import { Button } from '@/components/common/Button'
import { Badge } from '@/components/common/Badge'
import type { ScorecardPayload } from '@/types/practice.types'
import { Award, HelpCircle, Check, ArrowRight } from 'lucide-react'

interface ScorecardModalProps {
  scorecard: ScorecardPayload
  onClose: () => void
  onNextQuestion?: () => void
  nextActionLabel?: string
  title?: string
  errorMessage?: string
  isActionPending?: boolean
}

export const ScorecardModal: React.FC<ScorecardModalProps> = ({
  scorecard,
  onClose,
  onNextQuestion,
  nextActionLabel,
  title = 'Bảng điểm đánh giá',
  errorMessage,
  isActionPending = false,
}) => {
  const dialogRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const previouslyFocusedElement =
      document.activeElement instanceof HTMLElement ? document.activeElement : null
    const previousBodyOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    dialogRef.current?.focus()

    const handleDialogKeyboard = (event: KeyboardEvent) => {
      if (event.key !== 'Tab') return

      const focusableElements = Array.from(
        dialogRef.current?.querySelectorAll<HTMLElement>(
          'button:not(:disabled), [href], [tabindex]:not([tabindex="-1"])',
        ) ?? [],
      ).filter((element) => element.getAttribute('aria-hidden') !== 'true')

      if (focusableElements.length === 0) {
        event.preventDefault()
        dialogRef.current?.focus()
        return
      }

      const firstElement = focusableElements[0]
      const lastElement = focusableElements[focusableElements.length - 1]
      if (event.shiftKey && document.activeElement === firstElement) {
        event.preventDefault()
        lastElement.focus()
      } else if (!event.shiftKey && document.activeElement === lastElement) {
        event.preventDefault()
        firstElement.focus()
      }
    }
    window.addEventListener('keydown', handleDialogKeyboard)
    return () => {
      window.removeEventListener('keydown', handleDialogKeyboard)
      document.body.style.overflow = previousBodyOverflow
      previouslyFocusedElement?.focus()
    }
  }, [])

  const questionNumbers = Array.from(
    new Set(
      scorecard.criteriaScores
        .map((item) => item.questionNumber)
        .filter((questionNumber): questionNumber is number => questionNumber !== undefined),
    ),
  )
  const hasQuestionBreakdown = questionNumbers.length > 0

  return (
    <div
      ref={dialogRef}
      role="dialog"
      aria-modal="true"
      aria-labelledby="scorecard-modal-title"
      aria-describedby="scorecard-modal-description"
      aria-busy={isActionPending}
      tabIndex={-1}
      className="fixed inset-0 z-50 flex items-center justify-center overflow-y-auto bg-[#0F2854]/70 px-3 py-4 backdrop-blur-[3px] outline-none sm:p-6"
    >
      <Card className="flex max-h-[92dvh] w-full max-w-[720px] flex-col overflow-hidden rounded-[22px] border border-[#4988C4]/45 bg-white text-[#0F2854] shadow-[0_28px_80px_rgba(4,20,48,0.3)]">
        <CardHeader className="relative shrink-0 space-y-0 overflow-hidden border-b border-white/10 bg-[linear-gradient(125deg,#071a35_0%,#123263_58%,#24528e_100%)] p-0 text-white">
          <div className="pointer-events-none absolute inset-0 [background-image:linear-gradient(rgba(129,220,255,0.18)_1px,transparent_1px),linear-gradient(90deg,rgba(129,220,255,0.18)_1px,transparent_1px)] [background-size:28px_28px] opacity-[0.13]" />
          <div className="pointer-events-none absolute -top-20 -right-12 size-56 rounded-full bg-[#5de0f2]/16 blur-3xl" />

          <div className="relative flex flex-col gap-4 px-4 py-5 sm:flex-row sm:items-center sm:justify-between sm:px-6">
            <div className="flex min-w-0 items-start gap-3">
              <span className="flex size-11 shrink-0 items-center justify-center rounded-2xl border border-white/12 bg-white/10 text-[#72e6f3] shadow-[0_10px_28px_rgba(2,15,38,0.2)] backdrop-blur-sm">
                <Award className="size-5" aria-hidden="true" />
              </span>
              <div className="min-w-0">
                <p className="text-xs font-bold tracking-[0.1em] text-[#9cecf5] uppercase">
                  Phản hồi luyện tập từ AI
                </p>
                <CardTitle
                  id="scorecard-modal-title"
                  className="mt-1.5 text-base leading-tight font-bold tracking-[-0.025em] text-white sm:text-lg"
                >
                  {title}
                </CardTitle>
                <p
                  id="scorecard-modal-description"
                  className="mt-1.5 text-xs leading-relaxed text-[#c7d9ee]"
                >
                  Đối chiếu điểm theo từng tiêu chí và nhận xét để cải thiện câu trả lời tiếp theo.
                </p>
              </div>
            </div>

            <div
              className="flex shrink-0 items-end gap-1 self-start rounded-2xl border border-white/12 bg-white/10 px-4 py-3 shadow-[0_10px_28px_rgba(2,15,38,0.16)] backdrop-blur-sm sm:self-auto"
              aria-label={`Điểm tổng ${scorecard.totalScore.toFixed(1)} trên 10`}
            >
              <span className="text-xs font-semibold tracking-[0.06em] text-[#b9cee7] uppercase">
                Điểm
              </span>
              <span className="ml-1 text-3xl leading-none font-black tracking-[-0.05em] text-white">
                {scorecard.totalScore.toFixed(1)}
              </span>
              <span className="pb-0.5 text-sm font-semibold text-[#a9c1df]">/10.0</span>
            </div>
          </div>
        </CardHeader>

        <CardContent className="min-h-0 flex-1 space-y-4 overflow-y-auto bg-[#4988C4]/16 p-4 sm:p-5">
          <section aria-labelledby="scorecard-rubric-title">
            <div className="mb-3 flex items-center justify-between gap-3">
              <div>
                <h4
                  id="scorecard-rubric-title"
                  className="text-xs font-bold tracking-[0.08em] text-[#52647d] uppercase"
                >
                  Chi tiết tiêu chí chấm
                </h4>
                <p className="mt-1 text-xs text-[#8592a5]">
                  Điểm và nhận xét được phân tích theo từng tiêu chí đánh giá.
                </p>
              </div>
              <span className="shrink-0 rounded-full border border-[#dce6f4] bg-white px-2.5 py-1 text-xs font-bold text-[#1C4D8D]">
                {hasQuestionBreakdown ? `${questionNumbers.length} câu` : `${scorecard.criteriaScores.length} tiêu chí`}
              </span>
            </div>

            <div className="space-y-2.5">
              {scorecard.criteriaScores.map((item, idx) => {
                const previousItem = scorecard.criteriaScores[idx - 1]
                const showQuestionHeading =
                  item.questionNumber !== undefined &&
                  item.questionNumber !== previousItem?.questionNumber

                return (
                  <React.Fragment key={`${item.criterionId}-${idx}`}>
                    {showQuestionHeading && (
                      <div className="pt-2 text-xs font-bold tracking-[0.08em] text-[#1C4D8D] uppercase">
                        Câu {item.questionNumber}
                      </div>
                    )}
                    <article className="flex flex-col gap-3 rounded-xl border border-[#4988C4]/45 bg-white p-3.5 shadow-[0_5px_18px_rgba(31,61,105,0.035)] sm:flex-row sm:items-start sm:justify-between">
                      <div className="min-w-0">
                        <p className="text-sm font-bold text-[#25364e] sm:text-xs">
                          {item.criterionName || `Tiêu chí: ${item.criterionId}`}
                        </p>
                        <p className="mt-1.5 text-xs leading-relaxed text-[#65758b]">
                          {item.comment}
                        </p>
                      </div>
                      <Badge
                        variant="success"
                        className="w-fit shrink-0 border border-[#cfe8df] bg-[#effaf6] px-2.5 py-1 font-mono text-xs font-bold text-[#168367]"
                      >
                        {item.score.toFixed(1)} đ
                      </Badge>
                    </article>
                  </React.Fragment>
                )
              })}
            </div>
          </section>

          <section
            aria-labelledby="scorecard-feedback-title"
            className="rounded-xl border border-[#d7e4f7] bg-[linear-gradient(135deg,#f4f8ff_0%,#eef5ff_100%)] p-4"
          >
            <h4
              id="scorecard-feedback-title"
              className="text-xs font-bold tracking-[0.08em] text-[#1C4D8D] uppercase"
            >
              Nhận xét sư phạm
            </h4>
            <p className="mt-2 text-xs leading-relaxed whitespace-pre-line text-[#4e617a]">
              {scorecard.feedback}
            </p>
          </section>

          {(scorecard.followUpPrompt || scorecard.followUpQuestion) && (
            <section className="rounded-xl border border-[#cde8ed] bg-[#f1fbfc] p-4">
              <div className="flex items-center gap-2 text-[#17778a]">
                <span className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-white shadow-sm">
                  <HelpCircle className="size-4" aria-hidden="true" />
                </span>
                <h4 className="text-xs font-bold tracking-[0.07em] uppercase">
                  Câu hỏi đào sâu tiếp theo
                </h4>
              </div>
              <p className="mt-3 text-sm leading-relaxed font-semibold text-[#31566a] italic">
                “{scorecard.followUpPrompt || scorecard.followUpQuestion}”
              </p>
            </section>
          )}

          {errorMessage && (
            <p
              className="rounded-xl border border-[#f1c7ce] bg-[#fff5f6] p-3 text-xs leading-relaxed font-semibold text-[#ae3d50]"
              role="alert"
            >
              {errorMessage}
            </p>
          )}
        </CardContent>

        <CardFooter className="mt-0 shrink-0 flex-col-reverse justify-end gap-2 border-t border-[#e8edf4] bg-white p-4 sm:flex-row sm:px-5">
          {onNextQuestion ? (
            <Button
              variant="primary"
              size="md"
              onClick={onNextQuestion}
              isLoading={isActionPending}
              className="min-h-11 w-full rounded-xl bg-[#1C4D8D] px-5 text-sm font-bold text-white shadow-[0_10px_24px_rgba(36,94,234,0.22)] hover:bg-[#0F2854] focus:ring-[#1C4D8D] sm:w-auto"
            >
              {nextActionLabel ||
                (scorecard.needsFollowUp ? 'Trả lời câu hỏi đào sâu' : 'Luyện tập câu tiếp theo')}
              <ArrowRight className="size-4" aria-hidden="true" />
            </Button>
          ) : (
            <Button
              variant="outline"
              size="sm"
              onClick={onClose}
              disabled={isActionPending}
              className="min-h-10 w-full rounded-xl border-[#4988C4]/45 bg-white px-4 text-xs font-bold text-[#42536d] hover:bg-[#4988C4]/18 focus:ring-[#1C4D8D] sm:w-auto"
            >
              <Check className="size-4 text-[#168367]" aria-hidden="true" />
              {nextActionLabel ?? 'Đóng bảng điểm'}
            </Button>
          )}
        </CardFooter>
      </Card>
    </div>
  )
}
