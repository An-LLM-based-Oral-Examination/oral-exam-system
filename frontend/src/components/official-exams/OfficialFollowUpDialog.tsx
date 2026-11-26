import { useRef, useState, type FormEvent } from 'react'
import { MessageSquareMore } from 'lucide-react'
import { FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { FieldError } from '@/components/common/formField'
import { fieldInvalidClass, fieldLabelClass } from '@/components/common/formFieldClasses'
import { SubmitDialogFooter } from '@/components/common/SubmitDialogFooter'
import { toast } from '@/components/common/toastStore'
import { officialExamService } from '@/services/officialExam.service'
import { preparationError } from '@/lib/examPreparation'
import type { OfficialExamSession } from '@/types/officialExam.types'
import { ExamLoadError } from './ExamPreparationShared'
export function OfficialFollowUpDialog({
  session,
  onClose,
  onSaved,
}: {
  session: OfficialExamSession
  onClose: () => void
  onSaved: () => void
}) {
  const [enabled, setEnabled] = useState(session.hasFollowUp)
  const [count, setCount] = useState(String(session.maxFollowUpQuestions || 2))
  const [invalid, setInvalid] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const pending = useRef(false)
  async function save(event: FormEvent) {
    event.preventDefault()
    if (pending.current) return
    const valid = Number.isInteger(Number(count)) && Number(count) >= 1 && Number(count) <= 5
    if (enabled && !valid) {
      setInvalid('Số câu hỏi phụ phải từ 1 đến 5.')
      return
    }
    pending.current = true
    setBusy(true)
    setError('')
    try {
      await officialExamService.configureFollowUp(session.id, {
        hasFollowUp: enabled,
        maxFollowUpQuestions: valid ? Number(count) : 2,
      })
      toast.success('Đã lưu cấu hình câu hỏi phụ.')
      onSaved()
      onClose()
    } catch (failure) {
      setError(preparationError(failure))
    } finally {
      pending.current = false
      setBusy(false)
    }
  }
  return (
    <FormDialog
      title="Câu hỏi phụ"
      eyebrow={session.title}
      icon={MessageSquareMore}
      size="sm"
      busy={busy}
      onClose={onClose}
      footer={
        <SubmitDialogFooter
          form="official-follow-up"
          label="Lưu cấu hình"
          busyLabel="Đang lưu…"
          busy={busy}
          onCancel={onClose}
        />
      }
    >
      <form
        id="official-follow-up"
        noValidate
        onSubmit={(event) => void save(event)}
        className="space-y-3"
      >
        <label className="flex cursor-pointer items-center justify-between gap-3 rounded-xl border border-[#4988C4] bg-white p-3 text-sm font-bold text-[#0F2854]">
          Bật câu hỏi phụ
          <input
            type="checkbox"
            checked={enabled}
            disabled={busy}
            onChange={(e) => {
              setEnabled(e.target.checked)
              setInvalid('')
            }}
            className="size-5 accent-[#1C4D8D]"
          />
        </label>
        <div>
          <label htmlFor="follow-up-count" className={fieldLabelClass(Boolean(invalid))}>
            Số câu hỏi phụ tối đa
          </label>
          <input
            id="follow-up-count"
            type="number"
            min={1}
            max={5}
            step={1}
            value={count}
            disabled={!enabled || busy}
            aria-invalid={Boolean(invalid)}
            className={cn(
              ds.field,
              'mt-1.5 h-10 disabled:bg-slate-100 disabled:text-[#78879a]',
              invalid && fieldInvalidClass,
            )}
            onChange={(e) => {
              setCount(e.target.value)
              setInvalid('')
            }}
          />
          <FieldError message={invalid} />
        </div>
        {error && <ExamLoadError message={error} />}
      </form>
    </FormDialog>
  )
}
