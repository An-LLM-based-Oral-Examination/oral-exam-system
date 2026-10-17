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

  return (
    <div
      ref={dialogRef}
      role="dialog"
      aria-modal="true"
      aria-labelledby="scorecard-modal-title"
      aria-describedby="scorecard-modal-description"
      aria-busy={isActionPending}
      tabIndex={-1}
      className="fixed inset-0 z-50 flex items-center justify-center overflow-y-auto bg-slate-900/35 px-3 py-4 backdrop-blur-xs outline-none sm:p-6"
    >
      <Card className="flex max-h-[90dvh] w-full max-w-2xl flex-col overflow-hidden rounded-2xl border border-[#FED7AA] border-t-4 border-t-[#F26F21] bg-white text-[#1E293B] shadow-2xl">
        <CardHeader className="relative space-y-0 border-b border-[#E2E8F0] bg-[#FFF7ED]/60 p-0">
          <div className="relative flex flex-col gap-3 px-5 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-6">
            <div className="flex min-w-0 items-start gap-3">
              <span className="flex size-10 shrink-0 items-center justify-center rounded-xl border border-[#FED7AA] bg-white text-[#F26F21] shadow-2xs">
                <Award className="size-5" aria-hidden="true" />
              </span>
              <div className="min-w-0">
                <p className="text-xs font-bold tracking-wider text-[#C2410C] uppercase">
                  Kết quả đánh giá câu trả lời
                </p>
                <CardTitle
                  id="scorecard-modal-title"
                  className="mt-0.5 text-base leading-tight font-bold text-[#1E293B] sm:text-lg"
                >
                  {title}
                </CardTitle>
                <p
                  id="scorecard-modal-description"
                  className="mt-0.5 text-xs leading-relaxed text-[#64748B]"
                >
                  Đối chiếu điểm theo từng tiêu chí Rubric và nhận xét của AI.
                </p>
              </div>
            </div>

            <div
              className="flex shrink-0 items-end gap-1.5 self-start rounded-2xl border border-[#FED7AA] bg-white px-4 py-2.5 shadow-2xs sm:self-auto"
              aria-label={`Điểm tổng ${scorecard.totalScore.toFixed(1)} trên 10`}
            >
              <span className="text-xs font-semibold tracking-wider text-[#64748B] uppercase">
                Điểm
              </span>
              <span className="ml-1 text-2xl leading-none font-black tracking-tight text-[#C2410C]">
                {scorecard.totalScore.toFixed(1)}
              </span>
              <span className="pb-0.5 text-xs font-semibold text-[#64748B]">/10.0</span>
            </div>
          </div>
        </CardHeader>

        <CardContent className="min-h-0 flex-1 space-y-4 overflow-y-auto bg-white p-4 sm:p-5">
          <section
            aria-labelledby="scorecard-feedback-title"
            className="rounded-xl border border-[#FED7AA] bg-[#FFF7ED] p-3.5"
          >
            <h4
              id="scorecard-feedback-title"
              className="text-xs font-bold tracking-wider text-[#C2410C] uppercase"
            >
              Nhận xét sư phạm tổng quan
            </h4>
            <p className="mt-1.5 text-sm leading-relaxed whitespace-pre-line text-[#1E293B]">
              {scorecard.feedback}
            </p>
          </section>

          <section aria-labelledby="scorecard-rubric-title">
            <div className="mb-2.5 flex items-center justify-between gap-3">
              <h4
                id="scorecard-rubric-title"
                className="text-xs font-bold tracking-wider text-[#1E293B] uppercase"
              >
                Chi tiết tiêu chí chấm Rubric ({scorecard.criteriaScores.length})
              </h4>
            </div>

            <div className="space-y-2">
              {scorecard.criteriaScores.map((item, idx) => (
                <article
                  key={idx}
                  className="flex flex-col gap-2.5 rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] p-3.5 sm:flex-row sm:items-start sm:justify-between"
                >
                  <div className="min-w-0">
                    <p className="text-sm font-bold text-[#1E293B]">
                      {item.criterionName || `Tiêu chí: ${item.criterionId}`}
                    </p>
                    <p className="mt-1 text-xs leading-relaxed text-[#475569]">{item.comment}</p>
                  </div>
                  <Badge
                    variant="success"
                    className="w-fit shrink-0 border border-emerald-200 bg-emerald-50 px-2.5 py-1 font-mono text-xs font-bold text-[#16A34A]"
                  >
                    {item.score.toFixed(1)} đ
                  </Badge>
                </article>
              ))}
            </div>
          </section>

          {(scorecard.followUpPrompt || scorecard.followUpQuestion) && (
            <section className="rounded-xl border border-[#FED7AA] bg-[#FFF7ED] p-3.5">
              <div className="flex items-center gap-2 text-[#C2410C]">
                <HelpCircle className="size-4 text-[#F26F21]" aria-hidden="true" />
                <h4 className="text-xs font-bold tracking-wider uppercase">
                  Câu hỏi đào sâu tiếp theo
                </h4>
              </div>
              <p className="mt-1.5 text-sm leading-relaxed font-semibold text-[#1E293B] italic">
                “{scorecard.followUpPrompt || scorecard.followUpQuestion}”
              </p>
            </section>
          )}

          {errorMessage && (
            <p
              className="rounded-xl border border-rose-200 bg-rose-50 p-3 text-xs leading-relaxed font-semibold text-rose-700"
              role="alert"
            >
              {errorMessage}
            </p>
          )}
        </CardContent>

        <CardFooter className="mt-0 flex-col-reverse justify-end gap-2.5 border-t border-[#E2E8F0] bg-[#F8FAFC] p-4 sm:flex-row sm:px-5">
          {onNextQuestion ? (
            <>
              <Button
                variant="outline"
                size="sm"
                onClick={onClose}
                disabled={isActionPending}
                className="min-h-10 w-full rounded-xl border-rose-200 bg-rose-50 px-4 text-xs font-bold text-rose-700 hover:bg-rose-100 focus:ring-rose-500 sm:w-auto"
              >
                Kết thúc phiên luyện tập
              </Button>
              <Button
                variant="primary"
                size="md"
                onClick={onNextQuestion}
                isLoading={isActionPending}
                className="min-h-10 w-full rounded-xl bg-[#F26F21] px-5 text-xs font-bold text-white shadow-xs hover:bg-[#D95B16] focus:ring-[#F26F21] sm:w-auto"
              >
                {nextActionLabel ||
                  (scorecard.needsFollowUp ? 'Trả lời câu hỏi đào sâu' : 'Luyện tập câu tiếp theo')}
                <ArrowRight className="size-4" aria-hidden="true" />
              </Button>
            </>
          ) : (
            <Button
              variant="outline"
              size="sm"
              onClick={onClose}
              disabled={isActionPending}
              className="min-h-10 w-full rounded-xl border-[#E2E8F0] bg-white px-4 text-xs font-bold text-[#1E293B] hover:bg-[#F8FAFC] focus:ring-[#F26F21] sm:w-auto"
            >
              <Check className="size-4 text-[#16A34A]" aria-hidden="true" />
              {nextActionLabel ?? 'Đóng bảng điểm'}
            </Button>
          )}
        </CardFooter>
      </Card>
    </div>
  )
}
