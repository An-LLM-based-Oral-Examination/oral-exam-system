import { useRef, useState, type FormEvent } from 'react'
import { FileCheck2, LoaderCircle, Send } from 'lucide-react'
import { DialogFooter, FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { FieldError } from '@/components/common/formField'
import { fieldInvalidClass, fieldLabelClass } from '@/components/common/formFieldClasses'
import { toast } from '@/components/common/toastStore'
import { examPreparationService } from '@/services/examPreparation.service'
import { preparationError } from '@/lib/examPreparation'

interface RequestExamPreparationDialogProps {
  open: boolean
  sessionId: string
  sessionTitle: string
  examDate?: string
  defaultRequiredSetCount?: number
  onClose: () => void
  onRequested: () => Promise<void> | void
}

function buildDefaultDeadlineLocal(examDate?: string): string {
  const now = new Date()
  let target = new Date(now.getTime() + 7 * 24 * 60 * 60 * 1000)
  if (examDate) {
    const parsedExam = new Date(`${examDate.slice(0, 10)}T17:00:00`)
    if (!Number.isNaN(parsedExam.getTime())) {
      const oneDayBefore = new Date(parsedExam.getTime() - 24 * 60 * 60 * 1000)
      if (oneDayBefore.getTime() > now.getTime() + 60 * 60 * 1000) {
        target = oneDayBefore
      }
    }
  }
  const pad = (value: number) => String(value).padStart(2, '0')
  return `${target.getFullYear()}-${pad(target.getMonth() + 1)}-${pad(target.getDate())}T${pad(target.getHours())}:${pad(target.getMinutes())}`
}

export function RequestExamPreparationDialog({
  open,
  sessionId,
  sessionTitle,
  examDate,
  defaultRequiredSetCount = 2,
  onClose,
  onRequested,
}: RequestExamPreparationDialogProps) {
  const [requiredSetCount, setRequiredSetCount] = useState(
    String(Math.max(1, defaultRequiredSetCount)),
  )
  const [deadline, setDeadline] = useState(() => buildDefaultDeadlineLocal(examDate))
  const [notes, setNotes] = useState('')
  const [countError, setCountError] = useState('')
  const [deadlineError, setDeadlineError] = useState('')
  const [notesError, setNotesError] = useState('')
  const [apiError, setApiError] = useState('')
  const [busy, setBusy] = useState(false)
  const pendingRef = useRef(false)

  if (!open) return null

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (pendingRef.current) return

    const parsedCount = Number(requiredSetCount)
    const parsedDeadline = new Date(deadline)
    const trimmedNotes = notes.trim()

    let hasError = false
    if (!Number.isInteger(parsedCount) || parsedCount < 1) {
      setCountError('Số bộ đề cần chuẩn bị phải là số nguyên lớn hơn 0.')
      hasError = true
    } else {
      setCountError('')
    }

    if (!deadline || Number.isNaN(parsedDeadline.getTime()) || parsedDeadline.getTime() <= Date.now()) {
      setDeadlineError('Hạn bàn giao bộ đề phải nằm ở thời điểm tương lai.')
      hasError = true
    } else {
      setDeadlineError('')
    }

    if (trimmedNotes.length > 2000) {
      setNotesError('Ghi chú không được vượt quá 2000 ký tự.')
      hasError = true
    } else {
      setNotesError('')
    }

    if (hasError) return

    pendingRef.current = true
    setBusy(true)
    setApiError('')
    try {
      await examPreparationService.request(sessionId, {
        requiredSetCount: parsedCount,
        deadline: parsedDeadline.toISOString(),
        notes: trimmedNotes || null,
      })
      toast.success('Đã gửi yêu cầu chuẩn bị đề thi cho Trưởng Bộ môn.')
      await Promise.resolve(onRequested())
      onClose()
    } catch (requestError) {
      const message = preparationError(requestError)
      setApiError(message)
      toast.error(message)
    } finally {
      pendingRef.current = false
      setBusy(false)
    }
  }

  return (
    <FormDialog
      open={open}
      align="center"
      size="md"
      variant="calm"
      icon={FileCheck2}
      eyebrow="CHUẨN BỊ ĐỀ THI · MF-03 / MF-04"
      title="Gửi yêu cầu chuẩn bị đề thi"
      busy={busy}
      onClose={() => !busy && onClose()}
      footer={
        <DialogFooter variant="calm">
          <button
            type="button"
            className={ds.outlineButton}
            onClick={onClose}
            disabled={busy}
          >
            Hủy
          </button>
          <button
            type="submit"
            form="eo-request-preparation-form"
            className={ds.primaryButton}
            disabled={busy}
          >
            {busy ? <LoaderCircle className="size-4 animate-spin" /> : <Send className="size-4" />}
            {busy ? 'Đang gửi…' : 'Gửi yêu cầu cho Trưởng BM'}
          </button>
        </DialogFooter>
      }
    >
      <form
        id="eo-request-preparation-form"
        noValidate
        onSubmit={(event) => void handleSubmit(event)}
        className="grid gap-3.5"
      >
        <p className="rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] px-3.5 py-2.5 text-xs text-[#52657d]">
          Kỳ thi: <strong className="text-[#0F2854]">{sessionTitle}</strong>. Sau khi gửi, yêu cầu
          sẽ hiển thị trên cổng Trưởng Bộ môn để cấu hình cấu trúc đề, phân công giảng viên soạn và
          bàn giao bộ đề <code>HANDED_OVER</code>.
        </p>

        {apiError ? (
          <div
            role="alert"
            className="rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-4 py-3 text-xs font-semibold text-[#a43d4b]"
          >
            {apiError}
          </div>
        ) : null}

        <div className="grid gap-3 sm:grid-cols-2">
          <label
            htmlFor="prep-required-sets"
            className={cn('grid gap-1.5', fieldLabelClass(Boolean(countError)))}
          >
            Số bộ đề cần chuẩn bị *
            <input
              id="prep-required-sets"
              type="number"
              min={1}
              max={50}
              step={1}
              disabled={busy}
              aria-invalid={Boolean(countError)}
              className={cn(ds.field, countError && fieldInvalidClass)}
              value={requiredSetCount}
              onChange={(event) => {
                setRequiredSetCount(event.target.value)
                if (countError) setCountError('')
              }}
            />
            <FieldError message={countError} />
          </label>

          <label
            htmlFor="prep-deadline"
            className={cn('grid gap-1.5', fieldLabelClass(Boolean(deadlineError)))}
          >
            Hạn bàn giao bộ đề *
            <input
              id="prep-deadline"
              type="datetime-local"
              disabled={busy}
              aria-invalid={Boolean(deadlineError)}
              className={cn(ds.field, deadlineError && fieldInvalidClass)}
              value={deadline}
              onChange={(event) => {
                setDeadline(event.target.value)
                if (deadlineError) setDeadlineError('')
              }}
            />
            <FieldError message={deadlineError} />
          </label>
        </div>

        <label
          htmlFor="prep-notes"
          className={cn('grid gap-1.5', fieldLabelClass(Boolean(notesError)))}
        >
          Ghi chú cho Trưởng Bộ môn (tùy chọn)
          <textarea
            id="prep-notes"
            rows={3}
            maxLength={2000}
            disabled={busy}
            aria-invalid={Boolean(notesError)}
            className={cn(ds.field, 'min-h-24 py-2', notesError && fieldInvalidClass)}
            placeholder="Ví dụ: Cần bàn giao trước ngày thi 2 ngày, đảm bảo đủ bộ đề không trùng cho mỗi phòng 24 SV…"
            value={notes}
            onChange={(event) => {
              setNotes(event.target.value)
              if (notesError) setNotesError('')
            }}
          />
          <FieldError message={notesError} />
        </label>
      </form>
    </FormDialog>
  )
}
