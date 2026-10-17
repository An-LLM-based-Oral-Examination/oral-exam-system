import React, { useCallback, useEffect, useRef, useState } from 'react'
import { Card, CardHeader, CardTitle, CardContent, CardFooter } from '@/components/common/Card'
import { Button } from '@/components/common/Button'
import { Clock, CheckCircle, Edit3 } from 'lucide-react'

interface BufferScreenProps {
  initialTranscript: string
  durationSeconds?: number
  deadlineTimestamp?: number | null
  onConfirmSubmit: (finalText: string) => void
  onTranscriptChange?: (text: string) => void
  onCancel?: (currentTranscript: string) => void
  isSubmitting?: boolean
  errorMessage?: string
}

export const BufferScreen: React.FC<BufferScreenProps> = ({
  initialTranscript,
  durationSeconds = 60,
  deadlineTimestamp = null,
  onConfirmSubmit,
  onTranscriptChange,
  onCancel,
  isSubmitting = false,
  errorMessage,
}) => {
  const safeDuration = Math.min(300, Math.max(10, Math.round(durationSeconds)))
  const getTimeLeft = useCallback(
    () =>
      deadlineTimestamp === null
        ? safeDuration
        : Math.max(0, Math.ceil((deadlineTimestamp - Date.now()) / 1_000)),
    [deadlineTimestamp, safeDuration],
  )
  const [timeLeft, setTimeLeft] = useState(getTimeLeft)
  const [transcript, setTranscript] = useState(initialTranscript)
  const [validationMessage, setValidationMessage] = useState('')
  const hasSubmittedRef = useRef(false)
  const hasReachedDeadlineRef = useRef(false)
  const transcriptRef = useRef(initialTranscript)
  const isSubmittingRef = useRef(isSubmitting)
  const onConfirmSubmitRef = useRef(onConfirmSubmit)
  const transcriptElementRef = useRef<HTMLTextAreaElement>(null)
  const dialogContainerRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    isSubmittingRef.current = isSubmitting
    onConfirmSubmitRef.current = onConfirmSubmit
  }, [isSubmitting, onConfirmSubmit])

  useEffect(() => {
    if (errorMessage) hasSubmittedRef.current = false
  }, [errorMessage])

  useEffect(() => {
    const previouslyFocusedElement =
      document.activeElement instanceof HTMLElement ? document.activeElement : null
    const previousBodyOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    transcriptElementRef.current?.focus()

    const handleDialogKeyboard = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && onCancel && !isSubmittingRef.current) {
        onCancel(transcriptRef.current)
        return
      }
      if (event.key !== 'Tab') return

      const focusableElements = Array.from(
        dialogContainerRef.current?.querySelectorAll<HTMLElement>(
          'button:not(:disabled), textarea:not(:disabled), [href], [tabindex]:not([tabindex="-1"])',
        ) ?? [],
      ).filter((element) => element.getAttribute('aria-hidden') !== 'true')

      if (focusableElements.length === 0) {
        event.preventDefault()
        dialogContainerRef.current?.focus()
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
  }, [onCancel])

  const handleConfirm = useCallback(() => {
    const finalText = transcriptRef.current.trim()
    if (finalText.length < 10) {
      setValidationMessage('Câu trả lời cần ít nhất 10 ký tự trước khi nộp.')
      return
    }
    if (hasSubmittedRef.current || isSubmittingRef.current) return

    hasSubmittedRef.current = true
    setValidationMessage('')
    onConfirmSubmitRef.current(finalText)
  }, [])

  useEffect(() => {
    if (timeLeft <= 0) {
      if (!hasReachedDeadlineRef.current) {
        hasReachedDeadlineRef.current = true
        handleConfirm()
      }
      return undefined
    }
    const timer = window.setTimeout(
      () => setTimeLeft(deadlineTimestamp === null ? (current) => current - 1 : getTimeLeft()),
      1_000,
    )
    return () => window.clearTimeout(timer)
  }, [deadlineTimestamp, getTimeLeft, handleConfirm, timeLeft])

  const progressPercent = Math.max(0, (timeLeft / safeDuration) * 100)
  const minutes = Math.floor(timeLeft / 60)
  const seconds = timeLeft % 60

  return (
    <div
      ref={dialogContainerRef}
      tabIndex={-1}
      className="fixed inset-0 z-50 flex items-center justify-center overflow-y-auto bg-slate-900/35 p-3 backdrop-blur-xs outline-none sm:p-6"
    >
      <Card
        role="dialog"
        aria-modal="true"
        aria-labelledby="buffer-screen-title"
        aria-describedby="buffer-screen-description"
        className="relative flex max-h-[90dvh] w-full max-w-2xl flex-col overflow-hidden rounded-2xl border border-[#FED7AA] border-t-4 border-t-[#F26F21] bg-white shadow-2xl"
      >
        <CardHeader className="shrink-0 space-y-0 border-b border-[#E2E8F0] bg-[#FFF7ED]/50 p-4 sm:p-5">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
            <div className="flex min-w-0 items-start gap-3">
              <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-[#F26F21] text-white shadow-xs">
                <Edit3 className="size-4" aria-hidden="true" />
              </span>
              <div className="min-w-0">
                <span className="inline-flex items-center rounded-full border border-[#FED7AA] bg-white px-2.5 py-0.5 text-xs font-bold text-[#C2410C]">
                  Rà soát câu trả lời trước khi nộp
                </span>
                <CardTitle
                  id="buffer-screen-title"
                  className="mt-1 text-base leading-tight font-bold text-[#1E293B] sm:text-lg"
                >
                  Hiệu đính bản trả lời
                </CardTitle>
                <p
                  id="buffer-screen-description"
                  className="mt-0.5 max-w-xl text-xs leading-relaxed text-[#64748B]"
                >
                  Kiểm tra và chỉnh sửa thuật ngữ chuyên ngành trước khi nộp câu trả lời.
                </p>
              </div>
            </div>

            <div
              role="timer"
              aria-label={`Còn ${minutes} phút ${seconds} giây để hiệu đính`}
              className={`flex min-h-10 shrink-0 items-center justify-center gap-2 self-start rounded-xl border px-3.5 font-mono text-sm font-bold tabular-nums ${
                timeLeft <= 10
                  ? 'border-rose-200 bg-rose-50 text-rose-700'
                  : timeLeft <= safeDuration / 3
                    ? 'border-amber-200 bg-amber-50 text-amber-700'
                    : 'border-[#FED7AA] bg-white text-[#C2410C]'
              }`}
            >
              <Clock
                className="size-4 animate-pulse motion-reduce:animate-none"
                aria-hidden="true"
              />
              <span aria-hidden="true">
                {String(minutes).padStart(2, '0')}:{String(seconds).padStart(2, '0')}
              </span>
            </div>
          </div>

          <div className="mt-3.5 h-1.5 w-full overflow-hidden rounded-full bg-[#E2E8F0]">
            <div
              role="progressbar"
              aria-label="Thời gian hiệu đính bản trả lời còn lại"
              aria-valuemin={0}
              aria-valuemax={safeDuration}
              aria-valuenow={timeLeft}
              aria-valuetext={`Còn ${minutes} phút ${seconds} giây`}
              className={`h-full rounded-full transition-[width] duration-1000 ease-linear motion-reduce:transition-none ${
                timeLeft <= 10
                  ? 'bg-rose-600'
                  : timeLeft <= safeDuration / 3
                    ? 'bg-amber-500'
                    : 'bg-[#F26F21]'
              }`}
              style={{ width: `${progressPercent}%` }}
            />
          </div>
        </CardHeader>

        <CardContent className="min-h-0 flex-1 space-y-3.5 overflow-y-auto bg-white p-4 sm:p-5">
          <div className="flex items-center justify-between">
            <label
              htmlFor="practice-transcript-editor"
              className="text-xs font-bold text-[#1E293B]"
            >
              Bản trả lời của bạn (hỗ trợ Code-Switching tiếng Việt + thuật ngữ SE)
            </label>
            <span className="shrink-0 text-xs font-semibold text-[#64748B]" aria-live="polite">
              {transcript.trim().length} ký tự
            </span>
          </div>

          <div className="rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] p-1.5 focus-within:border-[#F26F21] focus-within:ring-2 focus-within:ring-[#F26F21]/20">
            <textarea
              ref={transcriptElementRef}
              id="practice-transcript-editor"
              aria-invalid={Boolean(validationMessage || errorMessage)}
              aria-describedby={
                validationMessage || errorMessage
                  ? 'practice-transcript-help practice-transcript-error'
                  : 'practice-transcript-help'
              }
              className="min-h-40 w-full resize-y rounded-lg border-0 bg-white p-3.5 text-sm leading-relaxed text-[#1E293B] outline-none placeholder:text-[#94A3B8] disabled:cursor-wait disabled:bg-[#F8FAFC] sm:min-h-48"
              value={transcript}
              onChange={(event) => {
                setTranscript(event.target.value)
                transcriptRef.current = event.target.value
                onTranscriptChange?.(event.target.value)
                setValidationMessage('')
                hasSubmittedRef.current = false
              }}
              placeholder="Nội dung câu trả lời…"
              disabled={isSubmitting}
            />
          </div>

          <div
            id="practice-transcript-help"
            className="flex items-start gap-2 rounded-xl border border-[#FED7AA] bg-[#FFF7ED] px-3.5 py-2.5 text-xs leading-relaxed text-[#9A3412]"
          >
            <CheckCircle className="mt-0.5 size-4 shrink-0 text-[#F26F21]" aria-hidden="true" />
            <span>
              Hệ thống tự động chuẩn hóa thuật ngữ Kỹ thuật Phần mềm (Clean Architecture, CQRS,
              MediatR, .NET, C#). Bạn có thể chỉnh sửa thêm nếu cần.
            </span>
          </div>

          {(validationMessage || errorMessage) && (
            <p
              id="practice-transcript-error"
              className="rounded-xl border border-rose-200 bg-rose-50 px-3.5 py-2.5 text-xs font-semibold text-rose-700"
              role="alert"
            >
              {validationMessage || errorMessage}
            </p>
          )}
        </CardContent>

        <CardFooter className="mt-0 shrink-0 flex-col-reverse gap-2.5 border-t border-[#E2E8F0] bg-[#F8FAFC] p-4 sm:flex-row sm:justify-between sm:px-5">
          {onCancel ? (
            <Button
              variant="outline"
              size="sm"
              onClick={() => onCancel(transcriptRef.current)}
              disabled={isSubmitting}
              className="min-h-10 w-full rounded-xl border-[#E2E8F0] bg-white px-4 text-xs font-semibold text-[#475569] hover:bg-[#F8FAFC] focus:ring-[#F26F21] sm:w-auto"
            >
              Quay lại câu hỏi
            </Button>
          ) : (
            <div />
          )}
          <Button
            variant="primary"
            size="md"
            onClick={handleConfirm}
            isLoading={isSubmitting}
            disabled={transcript.trim().length < 10}
            className="min-h-10 w-full rounded-xl bg-[#F26F21] px-5 text-xs font-bold text-white shadow-xs hover:bg-[#D95B16] focus:ring-[#F26F21] disabled:shadow-none sm:w-auto"
          >
            <CheckCircle className="size-4" aria-hidden="true" />
            {isSubmitting ? 'Đang nộp câu trả lời…' : 'Xác nhận nộp ngay'}
          </Button>
        </CardFooter>
      </Card>
    </div>
  )
}
