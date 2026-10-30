import React, { useCallback, useEffect, useRef, useState } from 'react'
import { Card, CardHeader, CardTitle, CardContent, CardFooter } from '@/components/common/Card'
import { Button } from '@/components/common/Button'
import { checkTranscriptRevision } from '@/lib/transcriptGuards'
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
  const baselineTranscriptRef = useRef(initialTranscript)
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

  const handleConfirm = useCallback(
    (fromDeadline = false) => {
      if (hasSubmittedRef.current || isSubmittingRef.current) return
      const finalText = transcriptRef.current.trim()
      const baseline = baselineTranscriptRef.current.trim()

      if (finalText.length < 10) {
        if (fromDeadline) {
          if (baseline.length >= 10) {
            hasSubmittedRef.current = true
            setValidationMessage('')
            onConfirmSubmitRef.current(baseline)
            return
          }
          onCancel?.(transcriptRef.current)
          return
        }
        setValidationMessage('Câu trả lời cần ít nhất 10 ký tự trước khi nộp.')
        return
      }

      if (baseline.length >= 10) {
        const revision = checkTranscriptRevision(baseline, finalText)
        if (!revision.isAllowed) {
          if (fromDeadline) {
            hasSubmittedRef.current = true
            setValidationMessage('')
            onConfirmSubmitRef.current(baseline)
            return
          }
          setValidationMessage(
            revision.reason ??
              'VOICE_FIRST_GATE: Chỉ được hiệu đính thuật ngữ trên bản bóc băng giọng nói.',
          )
          return
        }
      }

      hasSubmittedRef.current = true
      setValidationMessage('')
      onConfirmSubmitRef.current(finalText)
    },
    [onCancel],
  )

  useEffect(() => {
    if (timeLeft <= 0) {
      if (!hasReachedDeadlineRef.current) {
        hasReachedDeadlineRef.current = true
        handleConfirm(true)
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
      className="fixed inset-0 z-50 flex items-center justify-center overflow-y-auto bg-[#06152d]/76 p-3 backdrop-blur-md outline-none sm:p-6"
    >
      <div
        className="pointer-events-none absolute inset-0 [background-image:linear-gradient(rgba(115,181,255,0.12)_1px,transparent_1px),linear-gradient(90deg,rgba(115,181,255,0.12)_1px,transparent_1px)] [background-size:32px_32px] opacity-30"
        aria-hidden="true"
      />
      <div
        className="pointer-events-none absolute top-[-12rem] right-[-10rem] size-[30rem] rounded-full bg-[#2e6dff]/20 blur-3xl"
        aria-hidden="true"
      />
      <Card
        role="dialog"
        aria-modal="true"
        aria-labelledby="buffer-screen-title"
        aria-describedby="buffer-screen-description"
        className="relative flex max-h-[calc(100dvh-1.5rem)] w-full max-w-3xl flex-col overflow-hidden rounded-[22px] border border-white/70 bg-white shadow-[0_32px_90px_rgba(2,18,45,0.34)] sm:max-h-[calc(100dvh-3rem)]"
      >
        <CardHeader className="shrink-0 space-y-0 border-b border-[#e7edf6] bg-[linear-gradient(135deg,#f8fbff_0%,#edf4ff_100%)] p-4 sm:p-6">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
            <div className="flex min-w-0 items-start gap-3">
              <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-[#1C4D8D] text-white shadow-[0_8px_20px_rgba(36,94,234,0.24)] sm:size-11">
                <Edit3 className="size-5" aria-hidden="true" />
              </span>
              <div className="min-w-0">
                <p className="text-xs font-bold tracking-[0.1em] text-[#52709c] uppercase">
                  Rà soát trước khi nộp
                </p>
                <CardTitle
                  id="buffer-screen-title"
                  className="mt-1 text-lg leading-tight font-bold text-[#12213b] sm:text-xl"
                >
                  Hiệu đính bản trả lời
                </CardTitle>
                <p
                  id="buffer-screen-description"
                  className="mt-1.5 max-w-xl text-xs leading-relaxed text-[#62738c]"
                >
                  Kiểm tra nội dung lần cuối trước khi hệ thống tự động nộp câu trả lời khi hết thời
                  gian.
                </p>
              </div>
            </div>

            <div
              role="timer"
              aria-label={`Còn ${minutes} phút ${seconds} giây để hiệu đính`}
              className={`flex min-h-11 shrink-0 items-center justify-center gap-2 self-start rounded-xl border px-3.5 font-mono text-sm font-black tabular-nums shadow-sm sm:min-w-28 ${
                timeLeft <= 10
                  ? 'border-rose-200 bg-rose-50 text-rose-700'
                  : timeLeft <= safeDuration / 3
                    ? 'border-amber-200 bg-amber-50 text-amber-700'
                    : 'border-[#cddcff] bg-white text-[#1C4D8D]'
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

          <div className="mt-5 h-2 w-full overflow-hidden rounded-full bg-[#dce6f5]">
            <div
              role="progressbar"
              aria-label="Thời gian hiệu đính bản trả lời còn lại"
              aria-valuemin={0}
              aria-valuemax={safeDuration}
              aria-valuenow={timeLeft}
              aria-valuetext={`Còn ${minutes} phút ${seconds} giây`}
              className={`h-full rounded-full transition-[width] duration-1000 ease-linear motion-reduce:transition-none ${
                timeLeft <= 10
                  ? 'bg-rose-500'
                  : timeLeft <= safeDuration / 3
                    ? 'bg-amber-500'
                    : 'bg-[linear-gradient(90deg,#4988C4,#43bde8)]'
              }`}
              style={{ width: `${progressPercent}%` }}
            />
          </div>
        </CardHeader>

        <CardContent className="min-h-0 flex-1 space-y-4 overflow-y-auto bg-white p-4 sm:p-6">
          <div className="flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
            <div>
              <label
                htmlFor="practice-transcript-editor"
                className="text-xs font-bold text-[#273953]"
              >
                Bản trả lời của bạn
              </label>
              <p className="mt-1 text-sm leading-relaxed text-[#78879a]">
                Bạn có thể sửa lỗi phiên âm hoặc lỗi nhập liệu trước khi xác nhận nộp.
              </p>
            </div>
            <span className="shrink-0 text-sm font-semibold text-[#8794a6]" aria-live="polite">
              {transcript.trim().length} ký tự
            </span>
          </div>

          <div className="rounded-2xl border border-[#dce5f2] bg-[#4988C4]/16 p-2 shadow-[inset_0_1px_0_rgba(255,255,255,0.8)] focus-within:border-[#7fa4ff] focus-within:ring-3 focus-within:ring-[#1C4D8D]/10">
            <textarea
              ref={transcriptElementRef}
              id="practice-transcript-editor"
              aria-invalid={Boolean(validationMessage || errorMessage)}
              aria-describedby={
                validationMessage || errorMessage
                  ? 'practice-transcript-help practice-transcript-error'
                  : 'practice-transcript-help'
              }
              className="min-h-44 w-full resize-y rounded-xl border-0 bg-white p-3.5 text-sm leading-6 text-[#24344c] shadow-sm outline-none placeholder:text-[#a1adbd] disabled:cursor-wait disabled:bg-[#f6f8fb] sm:min-h-56 sm:p-4"
              value={transcript}
              onChange={(event) => {
                setTranscript(event.target.value)
                transcriptRef.current = event.target.value
                onTranscriptChange?.(event.target.value)
                setValidationMessage('')
                hasSubmittedRef.current = false
              }}
              onPaste={(event) => event.preventDefault()}
              onCopy={(event) => event.preventDefault()}
              onCut={(event) => event.preventDefault()}
              onDrop={(event) => event.preventDefault()}
              placeholder="Nội dung câu trả lời…"
              disabled={isSubmitting}
            />
          </div>

          <div
            id="practice-transcript-help"
            className="flex items-start gap-2.5 rounded-xl border border-[#d8e5f8] bg-[#4988C4]/16 px-3.5 py-3 text-sm leading-relaxed text-[#536a8a]"
          >
            <CheckCircle className="mt-0.5 size-4 shrink-0 text-[#1C4D8D]" aria-hidden="true" />
            <span>
              Hãy chú ý tên công nghệ, từ viết tắt và thuật ngữ tiếng Anh. Hệ thống chỉ lưu nội dung
              văn bản cuối cùng trong phiên luyện tập.
            </span>
          </div>

          {(validationMessage || errorMessage) && (
            <p
              id="practice-transcript-error"
              className="rounded-xl border border-rose-200 bg-rose-50 px-3.5 py-3 text-xs font-semibold text-rose-700"
              role="alert"
            >
              {validationMessage || errorMessage}
            </p>
          )}
        </CardContent>

        <CardFooter className="mt-0 shrink-0 flex-col-reverse gap-2.5 border-t border-[#e7edf6] bg-[#4988C4]/16 p-4 pt-4 sm:flex-row sm:justify-between sm:px-6 sm:py-4">
          {onCancel ? (
            <Button
              variant="outline"
              size="sm"
              onClick={() => onCancel(transcriptRef.current)}
              disabled={isSubmitting}
              className="min-h-11 w-full rounded-xl border-[#4988C4]/45 bg-white px-4 text-[#50627b] hover:bg-[#f1f5fb] focus:ring-[#1C4D8D] sm:w-auto"
            >
              Quay lại câu hỏi
            </Button>
          ) : (
            <div />
          )}
          <Button
            variant="primary"
            size="md"
            onClick={() => handleConfirm(false)}
            isLoading={isSubmitting}
            disabled={transcript.trim().length < 10}
            className="min-h-11 w-full rounded-xl bg-[#1C4D8D] px-5 text-sm font-bold text-white shadow-[0_10px_24px_rgba(36,94,234,0.22)] hover:bg-[#0F2854] focus:ring-[#1C4D8D] disabled:shadow-none sm:w-auto"
          >
            <CheckCircle className="size-4" aria-hidden="true" />
            {isSubmitting ? 'Đang nộp câu trả lời…' : 'Xác nhận nộp ngay'}
          </Button>
        </CardFooter>
      </Card>
    </div>
  )
}
