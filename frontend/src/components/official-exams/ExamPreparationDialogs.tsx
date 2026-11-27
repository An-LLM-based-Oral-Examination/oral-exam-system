import { DateField, ClockField } from '@/components/common/DateTimeFields'
import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Check, Layers, Minus, Plus, Save, Send, Settings2, UsersRound, X } from 'lucide-react'
import { FormDialog, DialogFooter } from '@/design/overlays'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { FieldError } from '@/components/common/formField'
import { fieldInvalidClass, fieldLabelClass } from '@/components/common/formFieldClasses'
import { TextareaField } from '@/components/common/TextareaField'
import { SubmitDialogFooter } from '@/components/common/SubmitDialogFooter'
import { EmptyState } from '@/components/common/EmptyState'
import { LoadingState } from '@/components/common/LoadingState'
import { toast } from '@/components/common/toastStore'
import { examPreparationService as service } from '@/services/examPreparation.service'
import { officialExamService } from '@/services/officialExam.service'
import type { OfficialExamSession } from '@/types/officialExam.types'
import { lecturerAssignmentService } from '@/services/lecturerAssignment.service'
import {
  isDeliverable,
  isValidExamDeadline,
  localDateTime,
  preparationError,
} from '@/lib/examPreparation'
import type { AssignedLecturer } from '@/types/lecturerAssignment.types'
import type { CourseClo } from '@/types/courseClo.types'
import type {
  BlueprintInput,
  ExamBlueprint,
  ExamPaper,
  ExamPreparation,
} from '@/types/examPreparation.types'
import { ExamLoadError } from './ExamPreparationShared'
import { ExamReuseField } from './ExamReuseField'
import { NumberStepper } from '@/components/common/NumberStepper'

function BlueprintSliderField({
  id,
  label,
  value,
  onChange,
  error,
  disabled,
  sliderMax,
}: {
  id: string
  label: string
  value: string
  onChange: (value: string) => void
  error?: string
  disabled: boolean
  sliderMax: number
}) {
  const maximum = id === 'blueprint-follow-up' ? sliderMax : Math.max(sliderMax, Number(value) || 1)
  return (
    <div className="min-w-0">
      <div className="flex items-center justify-between gap-3">
        <label htmlFor={id} className={fieldLabelClass(Boolean(error))}>
          {label}
        </label>
        <input
          id={id}
          type="number"
          min={1}
          step={1}
          value={value}
          disabled={disabled}
          onChange={(e) => onChange(e.target.value)}
          aria-invalid={Boolean(error)}
          aria-describedby={error ? `${id}-error` : undefined}
          className={cn(
            ds.field,
            'h-10 w-16 shrink-0 px-2 text-center tabular-nums disabled:bg-slate-100 disabled:text-slate-400',
            error && fieldInvalidClass,
          )}
        />
      </div>
      <input
        type="range"
        min={1}
        max={maximum}
        step={1}
        disabled={disabled}
        aria-label={`${label} thanh trượt`}
        value={Math.min(maximum, Math.max(1, Number(value) || 1))}
        onChange={(e) => onChange(e.target.value)}
        className="mt-3 h-2 w-full cursor-pointer accent-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-45"
      />
      <div id={`${id}-error`}>
        <FieldError message={error} />
      </div>
    </div>
  )
}
const validNumber = (value: string, min: number) =>
  value.trim() !== '' &&
  Number.isInteger(Number(value)) &&
  Number(value) >= min &&
  Number(value) <= 2147483647

export function ExamBlueprintDialog({
  sessionId,
  session,
  current,
  clos,
  locked,
  onClose,
  onSaved,
  reusePercent: initialReusePercent = 30,
  requiredSetCount,
}: {
  sessionId: string
  session?: OfficialExamSession
  current?: ExamBlueprint
  clos: CourseClo[]
  locked: boolean
  onClose: () => void
  onSaved: () => void
  reusePercent?: number
  requiredSetCount?: number
}) {
  const [editing, setEditing] = useState(!current || current.cloIds.length === 0)
  const [reusePercent, setReusePercent] = useState(initialReusePercent)
  const [form, setForm] = useState({
    name: current?.name ?? '',
    durationMinutes: String(current?.durationMinutes ?? 30),
    easyCount: String(current?.easyCount ?? 1),
    mediumCount: String(current?.mediumCount ?? 3),
    hardCount: String(current?.hardCount ?? 1),
  })
  const [cloIds, setCloIds] = useState<string[]>(current?.cloIds ?? [])
  const [closExpanded, setClosExpanded] = useState(false)
  const [followUp, setFollowUp] = useState(session?.hasFollowUp ?? false)
  const [followUpCount, setFollowUpCount] = useState(String(session?.maxFollowUpQuestions || 2))
  const [picker, setPicker] = useState(false)
  const [draftClos, setDraftClos] = useState<string[]>([])
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const pending = useRef(false)
  const activeClos = clos.filter((c) => c.isActive)
  const total = Number(form.easyCount) + Number(form.mediumCount) + Number(form.hardCount)
  function update(key: keyof typeof form, value: string) {
    setForm((f) => ({ ...f, [key]: value }))
    setErrors((e) => ({ ...e, [key]: '', distribution: '' }))
  }
  async function save(event: FormEvent) {
    event.preventDefault()
    if (pending.current || locked) return
    const next: Record<string, string> = {}
    if (!form.name.trim() || form.name.length > 200)
      next.name = 'Nhập tên cấu trúc, tối đa 200 ký tự.'
    for (const key of ['durationMinutes', 'easyCount', 'mediumCount', 'hardCount'] as const)
      if (!validNumber(form[key], key === 'durationMinutes' ? 1 : 0))
        next[key] = 'Nhập số nguyên hợp lệ.'
    if (!Number.isInteger(total) || total < 1 || total > 2147483647)
      next.distribution = 'Tổng số câu phải là số nguyên dương hợp lệ.'
    if (!cloIds.length || cloIds.some((id) => !activeClos.some((c) => c.id === id)))
      next.clos = 'Chọn ít nhất một CLO đang hoạt động của môn học.'
    if (session && followUp && (!validNumber(followUpCount, 1) || Number(followUpCount) > 5))
      next.followUp = 'Số câu hỏi phụ phải từ 1 đến 5.'
    setErrors(next)
    if (Object.keys(next).length) return
    pending.current = true
    setBusy(true)
    setError('')
    const input: BlueprintInput = {
      name: form.name.trim(),
      totalQuestions: total,
      durationMinutes: Number(form.durationMinutes),
      easyCount: Number(form.easyCount),
      mediumCount: Number(form.mediumCount),
      hardCount: Number(form.hardCount),
      cloIds,
      reusePercent,
    }
    try {
      if (
        session &&
        (followUp !== session.hasFollowUp ||
          (followUp && Number(followUpCount) !== session.maxFollowUpQuestions))
      ) {
        await officialExamService.configureFollowUp(sessionId, {
          hasFollowUp: followUp,
          maxFollowUpQuestions: followUp
            ? Number(followUpCount)
            : session.maxFollowUpQuestions || 2,
        })
      }
      await service.configure(sessionId, input)
      toast.success('Đã lưu cấu trúc đề thi.')
      onSaved()
      onClose()
    } catch (failure) {
      setError(preparationError(failure))
    } finally {
      pending.current = false
      setBusy(false)
    }
  }
  async function saveReuse() {
    if (pending.current || locked) return
    pending.current = true
    setBusy(true)
    setError('')
    try {
      await service.updateReuse(sessionId, reusePercent)
      toast.success('Đã cập nhật tỷ lệ tái sử dụng.')
      onSaved()
    } catch (failure) {
      setError(preparationError(failure))
    } finally {
      pending.current = false
      setBusy(false)
    }
  }
  return (
    <>
      <FormDialog
        title={editing ? 'Cấu hình đề thi' : 'Cấu trúc đề thi'}
        spacing="custom"
        eyebrow={session?.title ?? 'Chuẩn bị đề thi'}
        icon={Settings2}
        size="md"
        busy={busy}
        onClose={onClose}
        footer={
          editing && !locked ? (
            <SubmitDialogFooter
              form="exam-blueprint"
              label="Lưu cấu trúc"
              busyLabel="Đang lưu…"
              busy={busy}
              icon={Save}
              onCancel={onClose}
            />
          ) : (
            <DialogFooter>
              <button type="button" className={ds.outlineButton} disabled={busy} onClick={onClose}>
                Đóng
              </button>
              {!locked && (
                <button
                  type="button"
                  className={
                    reusePercent !== initialReusePercent ? ds.outlineButton : ds.primaryButton
                  }
                  disabled={busy}
                  onClick={() => setEditing(true)}
                >
                  Tạo phiên bản mới
                </button>
              )}
              {!locked && current && reusePercent !== initialReusePercent && (
                <button
                  type="button"
                  className={ds.primaryButton}
                  disabled={busy}
                  onClick={() => void saveReuse()}
                >
                  <Save className="size-4" />
                  {busy ? 'Đang lưu…' : 'Lưu thay đổi'}
                </button>
              )}
            </DialogFooter>
          )
        }
      >
        {!editing || locked ? (
          <div className="space-y-3">
            <section className="min-w-0 space-y-3 rounded-2xl border border-[#82aedb]/55 bg-white p-3.5">
              {current && (
                <>
                  <div className="grid grid-cols-3 gap-3">
                    {[
                      { label: 'Số câu mỗi đề', value: current.totalQuestions, unit: 'câu' },
                      { label: 'Thời gian làm bài', value: current.durationMinutes, unit: 'phút' },
                      { label: 'Phạm vi CLO', value: current.cloIds.length, unit: 'CLO' },
                    ].map((item) => (
                      <div
                        key={item.label}
                        className="min-w-0 rounded-xl border border-[#88b2de]/55 bg-[linear-gradient(145deg,#edf7fd_0%,#d4e9f8_100%)] p-3"
                      >
                        <p className="text-xs font-semibold text-[#5b6b84]">{item.label}</p>
                        <p className="mt-1 text-xl font-bold text-[#1C4D8D] tabular-nums">
                          {item.value}{' '}
                          <span className="text-xs font-medium text-[#5b6b84]">{item.unit}</span>
                        </p>
                      </div>
                    ))}
                  </div>
                  <div className="border-t border-[#82aedb]/35 pt-3">
                    <p className={fieldLabelClass()}>Phân bổ độ khó</p>
                    <div className="mt-2 grid grid-cols-3 gap-3">
                      {[
                        {
                          label: 'Dễ',
                          value: current.easyCount,
                          color: cn(
                            'border-[#82cfad]/55 text-[#16836a]',
                            'bg-[linear-gradient(145deg,#f1faf6_0%,#e0f2e8_100%)]',
                          ),
                        },
                        {
                          label: 'Trung bình',
                          value: current.mediumCount,
                          color: cn(
                            'border-[#e8c77f]/55 text-[#99620b]',
                            'bg-[linear-gradient(145deg,#fffbf1_0%,#fbefcf_100%)]',
                          ),
                        },
                        {
                          label: 'Khó',
                          value: current.hardCount,
                          color: cn(
                            'border-[#efb5d3]/55 text-[#c2415b]',
                            'bg-[linear-gradient(145deg,#fff5f9_0%,#fbe5ef_100%)]',
                          ),
                        },
                      ].map((item) => (
                        <div
                          key={item.label}
                          className={cn(
                            'flex min-w-0 flex-wrap items-center justify-between gap-1 rounded-xl border px-3 py-2 text-sm',
                            item.color,
                          )}
                        >
                          <span className="font-semibold">{item.label}</span>
                          <span className="font-bold whitespace-nowrap tabular-nums">
                            {item.value} câu
                          </span>
                        </div>
                      ))}
                    </div>
                  </div>
                </>
              )}
              {session && (
                <div className="flex flex-wrap items-center justify-between gap-2 border-t border-[#82aedb]/35 pt-3">
                  <span className="text-sm font-semibold text-[#5b6b84]">Câu hỏi phụ</span>
                  <span
                    className={cn(
                      'rounded-full px-3 py-1 text-xs font-bold',
                      session.hasFollowUp
                        ? 'bg-[#E8F8F3] text-[#16836a]'
                        : 'bg-[#eef2f6] text-[#5b6b84]',
                    )}
                  >
                    {session.hasFollowUp
                      ? `Tối đa ${session.maxFollowUpQuestions} câu`
                      : 'Không sử dụng'}
                  </span>
                </div>
              )}
            </section>
            <section className="rounded-2xl border border-[#82aedb]/55 bg-white p-3.5">
              <div className="flex items-center justify-between gap-3">
                <p className="text-sm font-bold text-[#0F2854]">
                  Nội dung các CLO · {cloIds.length} CLO
                </p>
                <button
                  type="button"
                  className="shrink-0 text-sm font-bold text-[#1C4D8D] hover:underline"
                  aria-expanded={closExpanded}
                  aria-controls="blueprint-clo-details"
                  disabled={!cloIds.length}
                  onClick={() => setClosExpanded((v) => !v)}
                >
                  {closExpanded ? 'Thu gọn' : 'Xem thêm'}
                </button>
              </div>
              {closExpanded && (
                <ul id="blueprint-clo-details" className="mt-3 space-y-3">
                  {clos
                    .filter((c) => cloIds.includes(c.id))
                    .map((c) => (
                      <li
                        key={c.id}
                        className="rounded-xl border border-[#82aedb]/55 bg-white p-3 text-sm"
                      >
                        <span className={ds.infoChip}>{c.cloCode}</span>
                        <p className="mt-2 leading-relaxed wrap-anywhere text-[#0F2854]">
                          {c.description}
                        </p>
                      </li>
                    ))}
                </ul>
              )}
            </section>
          </div>
        ) : (
          <form
            id="exam-blueprint"
            noValidate
            onSubmit={(event) => void save(event)}
            className="min-w-0 space-y-3"
          >
            {current && (
              <p className="text-sm leading-relaxed text-[#5b6b84]">
                Lưu thành phiên bản mới. Các phân công đã giao giữ cấu trúc cũ; bàn giao sử dụng
                phiên bản hiện tại.
              </p>
            )}
            <div className="grid min-w-0 grid-cols-[minmax(0,1fr)_auto] items-start gap-3">
              <div className="min-w-0">
                <label htmlFor="blueprint-name" className={fieldLabelClass(Boolean(errors.name))}>
                  Tên cấu trúc
                </label>
                <input
                  id="blueprint-name"
                  className={cn(ds.field, 'mt-1.5 h-10', errors.name && fieldInvalidClass)}
                  value={form.name}
                  maxLength={200}
                  onChange={(e) => update('name', e.target.value)}
                  aria-invalid={Boolean(errors.name)}
                  placeholder="Nhập tên cấu trúc đề thi"
                />
                <FieldError message={errors.name} />
              </div>
              <div className="min-w-0">
                <label
                  htmlFor="blueprint-duration"
                  className={fieldLabelClass(Boolean(errors.durationMinutes))}
                >
                  Thời gian làm bài
                </label>
                <div className="mt-1.5">
                  <NumberStepper
                    id="blueprint-duration"
                    label="Thời gian làm bài"
                    value={form.durationMinutes}
                    min={1}
                    unit="phút"
                    disabled={busy}
                    onChange={(v) => update('durationMinutes', v)}
                    invalid={Boolean(errors.durationMinutes)}
                  />
                </div>
                <FieldError message={errors.durationMinutes} />
              </div>
            </div>
            {session && (
              <section className="space-y-3 rounded-2xl border border-[#82aedb]/55 bg-white p-3.5">
                <div className="grid items-center gap-3 sm:grid-cols-2">
                  <button
                    type="button"
                    role="switch"
                    aria-label="Câu hỏi phụ"
                    aria-checked={followUp}
                    disabled={busy}
                    onClick={() => {
                      setFollowUp((v) => !v)
                      setErrors((v) => ({ ...v, followUp: '' }))
                    }}
                    className="flex h-10 items-center justify-between gap-3 rounded-xl border border-[#4988C4] bg-white px-3 text-sm font-bold text-[#0F2854] focus-visible:outline-2 focus-visible:outline-[#1C4D8D] disabled:opacity-60"
                  >
                    Câu hỏi phụ
                    <span
                      aria-hidden="true"
                      className={cn(
                        'relative h-6 w-11 shrink-0 rounded-full transition-colors',
                        followUp ? 'bg-[#16836a]' : 'bg-[#cbd5e1]',
                      )}
                    >
                      <span
                        className={cn(
                          'absolute top-0.5 left-0.5 size-5 rounded-full bg-white transition-transform motion-reduce:transition-none',
                          followUp && 'translate-x-5',
                        )}
                      />
                    </span>
                  </button>
                  <BlueprintSliderField
                    id="blueprint-follow-up"
                    label="Số câu phụ tối đa"
                    value={followUpCount}
                    sliderMax={5}
                    disabled={!followUp || busy}
                    onChange={(v) => {
                      setFollowUpCount(v)
                      setErrors((e) => ({ ...e, followUp: '' }))
                    }}
                    error={errors.followUp}
                  />
                </div>
              </section>
            )}
            <div className="rounded-2xl border border-[#82aedb]/55 bg-white p-3.5">
              <div className="mb-3 flex items-center justify-between gap-2">
                <p className={fieldLabelClass(Boolean(errors.distribution))}>Phân bổ độ khó</p>
                <span className="text-xs font-bold text-[#1C4D8D]">
                  {Number.isFinite(total) ? total : 0} câu
                </span>
              </div>
              <div className="mt-2 grid w-full grid-cols-3 gap-x-3 sm:gap-x-8">
                {(
                  [
                    { key: 'easyCount', label: 'Dễ' },
                    { key: 'mediumCount', label: 'Trung bình' },
                    { key: 'hardCount', label: 'Khó' },
                  ] as const
                ).map((item) => (
                  <div
                    key={item.key}
                    className="min-w-0 first:justify-self-start last:justify-self-end nth-2:justify-self-center"
                  >
                    <label className="grid min-w-0 grid-cols-[minmax(0,max-content)_auto] items-center gap-1.5">
                      <span
                        aria-hidden="true"
                        className="invisible col-start-1 row-start-1 text-sm font-bold"
                      >
                        Trung bình:
                      </span>
                      <span className="col-start-1 row-start-1 text-sm font-bold text-[#0F2854]">
                        {item.label}:
                      </span>
                      <input
                        id={`blueprint-${item.key}`}
                        type="number"
                        min={0}
                        step={1}
                        value={form[item.key]}
                        disabled={busy}
                        onChange={(e) => update(item.key, e.target.value)}
                        aria-invalid={Boolean(errors[item.key] || errors.distribution)}
                        aria-describedby={
                          errors[item.key]
                            ? `blueprint-${item.key}-error`
                            : errors.distribution
                              ? 'blueprint-distribution-error'
                              : undefined
                        }
                        className={cn(
                          ds.field,
                          'min-h-9 w-10 min-w-0 shrink px-1 text-center tabular-nums sm:w-16',
                          (errors[item.key] || errors.distribution) && fieldInvalidClass,
                        )}
                      />
                    </label>
                    <div id={`blueprint-${item.key}-error`}>
                      <FieldError message={errors[item.key]} />
                    </div>
                  </div>
                ))}
              </div>
              <div id="blueprint-distribution-error">
                <FieldError message={errors.distribution} />
              </div>
            </div>
            <div>
              <div
                className={cn(
                  'flex flex-wrap items-center justify-between gap-3 rounded-xl border bg-[#F4FBFD] p-3.5',
                  errors.clos ? 'border-[#c2415b]' : 'border-[#82aedb]/55',
                )}
              >
                <div>
                  <p className="text-sm font-bold text-[#0F2854]">Đã chọn {cloIds.length} CLO</p>
                  <p className="mt-1 text-xs text-[#5b6b84]">Giới hạn phạm vi câu hỏi của đề.</p>
                </div>
                <button
                  type="button"
                  className={ds.outlineButton}
                  onClick={() => {
                    setDraftClos(cloIds.filter((id) => activeClos.some((c) => c.id === id)))
                    setPicker(true)
                  }}
                >
                  <Layers className="size-4" />
                  Chọn CLO
                </button>
                {cloIds.length > 0 && (
                  <button
                    type="button"
                    className="text-sm font-bold text-[#1C4D8D] hover:underline"
                    aria-expanded={closExpanded}
                    aria-controls="blueprint-selected-clos"
                    onClick={() => setClosExpanded((v) => !v)}
                  >
                    {closExpanded ? 'Thu gọn' : 'Xem thêm'}
                  </button>
                )}
                {cloIds.length > 0 && closExpanded && (
                  <div id="blueprint-selected-clos" className="flex w-full flex-wrap gap-2">
                    {clos
                      .filter((clo) => cloIds.includes(clo.id))
                      .map((clo) => (
                        <span
                          key={clo.id}
                          className={cn(ds.infoChip, 'gap-1 bg-white pr-1 whitespace-nowrap')}
                        >
                          {clo.cloCode}
                          <button
                            type="button"
                            aria-label={`Bỏ ${clo.cloCode}`}
                            disabled={busy}
                            onClick={() => {
                              setCloIds((ids) => ids.filter((id) => id !== clo.id))
                              setErrors((errors) => ({ ...errors, clos: '' }))
                            }}
                            className="flex size-6 items-center justify-center rounded-full text-[#5b6b84] hover:bg-[#E7F3FB] hover:text-[#1C4D8D]"
                          >
                            <X className="size-3.5" aria-hidden="true" />
                          </button>
                        </span>
                      ))}
                  </div>
                )}
              </div>
              <FieldError message={errors.clos} />
            </div>
            <ExamReuseField
              sessionId={sessionId}
              input={{
                name: form.name,
                totalQuestions: total,
                durationMinutes: Number(form.durationMinutes),
                easyCount: Number(form.easyCount),
                mediumCount: Number(form.mediumCount),
                hardCount: Number(form.hardCount),
                cloIds,
              }}
              value={reusePercent}
              onChange={setReusePercent}
              disabled={busy}
              requiredSetCount={requiredSetCount}
            />
            {error && <ExamLoadError message={error} />}
          </form>
        )}
        {!editing && current && (
          <div className="mt-3 space-y-3">
            <ExamReuseField
              sessionId={sessionId}
              input={current}
              value={reusePercent}
              onChange={setReusePercent}
              disabled={busy || locked}
              requiredSetCount={requiredSetCount}
            />
            {error && <ExamLoadError message={error} />}
          </div>
        )}
      </FormDialog>
      {picker && (
        <FormDialog
          title="Chọn CLO"
          eyebrow="Cấu trúc đề thi"
          icon={Layers}
          size="lg"
          nested
          onClose={() => setPicker(false)}
          maxHeight="max-h-[min(90dvh,760px)]"
          bodyClassName="bg-[#F8FBFD] p-0 sm:p-0"
          headerExtra={
            <span
              className={cn(
                ds.sectionBadge,
                'shrink-0 whitespace-nowrap',
                draftClos.length === 0 && 'border-[#efc8cc] bg-[#fff2f3] text-[#a93d48]',
              )}
            >
              {draftClos.length}/{activeClos.length} CLO
            </span>
          }
          beforeBody={
            <div className="shrink-0 space-y-1 border-b border-[#82aedb]/55 bg-[#F4FBFD] p-3.5">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <p className="text-sm font-semibold text-[#0F2854]">
                  Chọn CLO để giới hạn phạm vi câu hỏi của đề.
                </p>
                <button
                  type="button"
                  className="inline-flex min-h-9 items-center gap-1.5 rounded-xl border border-[#4988C4] bg-white px-3 text-xs font-bold text-[#5b6b84] transition duration-200 hover:-translate-y-0.5 hover:bg-white disabled:cursor-not-allowed disabled:opacity-45 motion-reduce:transform-none"
                  disabled={!activeClos.length || busy}
                  onClick={() =>
                    setDraftClos(
                      activeClos.every((clo) => draftClos.includes(clo.id))
                        ? []
                        : activeClos.map((clo) => clo.id),
                    )
                  }
                >
                  {activeClos.length > 0 &&
                  activeClos.every((clo) => draftClos.includes(clo.id)) ? (
                    <X className="size-3.5" aria-hidden="true" />
                  ) : (
                    <Check className="size-3.5" aria-hidden="true" />
                  )}
                  {activeClos.length > 0 && activeClos.every((clo) => draftClos.includes(clo.id))
                    ? 'Bỏ chọn'
                    : 'Chọn tất cả'}
                </button>
              </div>
              {draftClos.length === 0 && (
                <div role="alert">
                  <FieldError message="Chọn ít nhất một CLO đang hoạt động của môn học." />
                </div>
              )}
            </div>
          }
          footer={
            <DialogFooter className="bg-white">
              <button type="button" className={ds.outlineButton} onClick={() => setPicker(false)}>
                Hủy
              </button>
              <button
                type="button"
                className={ds.primaryButton}
                disabled={!draftClos.length}
                onClick={() => {
                  setCloIds(draftClos)
                  setErrors((e) => ({ ...e, clos: '' }))
                  setPicker(false)
                }}
              >
                Xác nhận lựa chọn
              </button>
            </DialogFooter>
          }
        >
          {activeClos.length ? (
            <ul className="space-y-2 p-3.5 sm:p-3.5">
              {activeClos.map((c) => (
                <li key={c.id}>
                  <label
                    className={cn(
                      'relative flex cursor-pointer items-start gap-2.5 overflow-hidden rounded-xl border px-3.5 py-3 transition motion-reduce:transition-none',
                      draftClos.includes(c.id)
                        ? 'border-[#4988C4] bg-[#EDF7FC]'
                        : 'border-[#82aedb]/45 bg-white hover:border-[#4988C4] hover:bg-[#F8FBFD]',
                    )}
                  >
                    <input
                      type="checkbox"
                      className="mt-1 size-4 shrink-0 rounded border-[#4988C4] accent-[#1C4D8D] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]"
                      checked={draftClos.includes(c.id)}
                      onChange={(e) =>
                        setDraftClos((ids) =>
                          e.target.checked ? [...ids, c.id] : ids.filter((id) => id !== c.id),
                        )
                      }
                    />
                    <div className="min-w-0 flex-1">
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <span className="inline-flex rounded-lg border border-[#82aedb]/60 bg-white px-2 py-0.5 font-mono text-xs font-bold text-[#1C4D8D]">
                          {c.cloCode}
                        </span>
                        <span className="text-xs text-[#5b6b84]">
                          Bloom mục tiêu: {c.bloomLevel}
                        </span>
                      </div>
                      <p className="mt-1.5 text-sm leading-6 wrap-anywhere text-[#0F2854]">
                        {c.description}
                      </p>
                    </div>
                  </label>
                </li>
              ))}
            </ul>
          ) : (
            <div className="p-5">
              <EmptyState
                title="Chưa có CLO đang hoạt động"
                description="Bổ sung CLO trong chi tiết môn học trước khi cấu hình đề."
              />
            </div>
          )}
        </FormDialog>
      )}
    </>
  )
}

export function ExamAssignmentDialog({
  preparation,
  blueprint,
  offeringId,
  onClose,
  onSaved,
}: {
  preparation: ExamPreparation
  blueprint?: ExamBlueprint
  offeringId?: string
  onClose: () => void
  onSaved: () => void
}) {
  const [lecturers, setLecturers] = useState<AssignedLecturer[]>()
  const [loadError, setLoadError] = useState('')
  const [retry, setRetry] = useState(0)
  const [selection, setSelection] = useState<Record<string, string>>({})
  const [deadline, setDeadline] = useState(localDateTime(preparation.deadline))
  const [notes, setNotes] = useState('')
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const pending = useRef(false)
  useEffect(() => {
    if (!offeringId) return
    const controller = new AbortController()
    void lecturerAssignmentService
      .assignments(offeringId, controller.signal)
      .then((rows) => {
        if (!controller.signal.aborted) {
          setLecturers([
            ...new Map(
              rows.filter((row) => row.isActive).map((row) => [row.lecturerId, row]),
            ).values(),
          ])
          setLoadError('')
        }
      })
      .catch((failure) => {
        if (!controller.signal.aborted) setLoadError(preparationError(failure))
      })
    return () => controller.abort()
  }, [offeringId, retry])
  const candidates = (lecturers ?? []).filter(
    (l) =>
      !preparation.assignments.some(
        (a) => a.structureId === preparation.examStructureId && a.lecturerId === l.lecturerId,
      ),
  )
  const selected = Object.keys(selection)
  const assigned = preparation.assignments.filter(
    (a) => a.structureId === preparation.examStructureId,
  )
  const assignedCount = assigned.reduce((sum, a) => sum + a.setCount, 0)
  const selectedCount = selected.reduce((sum, id) => sum + (Number(selection[id]) || 0), 0)
  async function save(event: FormEvent) {
    event.preventDefault()
    if (pending.current) return
    const next: Record<string, string> = {}
    if (!selected.length || selected.some((id) => !candidates.some((l) => l.lecturerId === id)))
      next.lecturer = 'Vui lòng chọn giảng viên được phân công môn học.'
    for (const id of selected)
      if (!validNumber(selection[id], 1)) next[id] = 'Nhập số bộ đề lớn hơn 0.'
    if (!preparation.examStructureId) next.lecturer = 'Cấu hình đề thi trước khi phân công.'
    if (notes.length > 2000) next.notes = 'Ghi chú tối đa 2000 ký tự.'
    const due = new Date(deadline)
    if (!isValidExamDeadline(deadline, preparation.deadline))
      next.deadline = 'Hạn nộp phải ở tương lai và không sau hạn bàn giao.'
    setErrors(next)
    if (Object.keys(next).length) return
    pending.current = true
    setBusy(true)
    setError('')
    try {
      await service.assignBatch(preparation.sessionId, {
        lecturers: selected.map((lecturerId) => ({
          lecturerId,
          setCount: Number(selection[lecturerId]),
        })),
        structureId: preparation.examStructureId!,
        deadline: due.toISOString(),
        notes: notes.trim(),
      })
      toast.success('Đã phân công soạn đề.')
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
      title="Phân công soạn đề"
      eyebrow={preparation.courseCode}
      icon={UsersRound}
      size="md"
      busy={busy}
      onClose={onClose}
      footer={
        <SubmitDialogFooter
          form="exam-assignment"
          label={
            selected.length
              ? `Phân công ${selected.length} giảng viên · ${selectedCount} bộ đề`
              : 'Giao soạn đề'
          }
          busyLabel="Đang lưu…"
          busy={busy}
          disabled={
            !offeringId ||
            !candidates.length ||
            !selected.length ||
            !preparation.examStructureId ||
            Boolean(loadError)
          }
          onCancel={onClose}
        />
      }
    >
      {!offeringId ? (
        <EmptyState
          title="Môn chưa được mở trong học kỳ"
          description="Kiểm tra phân công môn học trước khi giao soạn đề."
        />
      ) : loadError ? (
        <ExamLoadError message={loadError} onRetry={() => setRetry((n) => n + 1)} />
      ) : !lecturers ? (
        <LoadingState />
      ) : !lecturers.length ? (
        <EmptyState
          title="Không có giảng viên để phân công"
          description="Các giảng viên đã được giao cấu trúc này hoặc môn học chưa có giảng viên đang hoạt động."
        />
      ) : (
        <form
          id="exam-assignment"
          noValidate
          onSubmit={(event) => void save(event)}
          className="space-y-3"
        >
          <div className="grid min-w-0 grid-cols-3 gap-3">
            {[
              {
                label: 'Cần chuẩn bị',
                value: preparation.requiredSetCount,
                tone: 'border-[#88b2de]/55 bg-[linear-gradient(145deg,#edf7fd_0%,#d4e9f8_100%)]',
                text: 'text-[#1C4D8D]',
              },
              {
                label: 'Đã giao',
                value: assignedCount,
                tone: 'border-[#a8d8c9] bg-[linear-gradient(145deg,#eefaf5_0%,#d6efe5_100%)]',
                text: 'text-[#16836a]',
              },
              {
                label: 'Còn cần giao',
                value: Math.max(0, preparation.requiredSetCount - assignedCount),
                tone:
                  assignedCount < preparation.requiredSetCount
                    ? 'border-[#efd092] bg-[linear-gradient(145deg,#fffaf0_0%,#fff0cc_100%)]'
                    : 'border-[#a8d8c9] bg-[linear-gradient(145deg,#eefaf5_0%,#d6efe5_100%)]',
                text:
                  assignedCount < preparation.requiredSetCount
                    ? 'text-[#94600e]'
                    : 'text-[#16836a]',
              },
            ].map((item) => (
              <div
                key={item.label}
                className={cn(
                  'min-w-0 rounded-xl border p-3.5 shadow-sm shadow-[#0F2854]/5',
                  item.tone,
                )}
              >
                <p className="min-h-8 text-xs font-semibold text-[#5b6b84] sm:min-h-0">
                  {item.label}
                </p>
                <p
                  className={cn(
                    'mt-1 flex flex-wrap items-baseline gap-x-1 text-2xl font-bold tabular-nums',
                    item.text,
                  )}
                >
                  {item.value} <span className="text-xs font-medium text-[#5b6b84]">bộ đề</span>
                </p>
              </div>
            ))}
          </div>
          {blueprint && (
            <p className="text-sm leading-relaxed wrap-anywhere text-[#5b6b84]">
              <strong className="text-[#0F2854]">{blueprint.name}</strong> ·{' '}
              {blueprint.totalQuestions} câu · {blueprint.durationMinutes} phút · Dễ{' '}
              {blueprint.easyCount} / TB {blueprint.mediumCount} / Khó {blueprint.hardCount}
            </p>
          )}
          <fieldset className="min-w-0">
            <legend className={fieldLabelClass(Boolean(errors.lecturer))}>
              Giảng viên được phân công môn học
            </legend>
            <p className="mt-1 text-xs text-[#5b6b84]">
              Chọn giảng viên và số bộ đề cần soạn cho từng người.
            </p>
            <div className="mt-3 space-y-2.5">
              {lecturers.map((l) => {
                const existing = assigned.find((a) => a.lecturerId === l.lecturerId)
                const checked = Object.hasOwn(selection, l.lecturerId)
                return (
                  <div
                    key={l.lecturerId}
                    className={cn(
                      'flex min-w-0 flex-wrap items-center gap-3 rounded-xl border p-3 transition-colors motion-reduce:transition-none sm:p-3.5',
                      checked ? 'border-[#82aedb]/70 bg-[#EDF7FC]' : 'border-[#82aedb]/40 bg-white',
                      existing && 'border-slate-200 bg-slate-50',
                    )}
                  >
                    <label
                      className={cn(
                        'flex min-w-0 flex-1 items-center gap-3',
                        existing || busy ? 'cursor-default' : 'cursor-pointer',
                      )}
                    >
                      <input
                        type="checkbox"
                        disabled={busy || Boolean(existing)}
                        checked={checked}
                        onChange={(e) => {
                          setSelection((old) => {
                            const next = { ...old }
                            if (e.target.checked) next[l.lecturerId] = '1'
                            else delete next[l.lecturerId]
                            return next
                          })
                          setErrors((old) => ({ ...old, lecturer: '' }))
                        }}
                        className="size-4 shrink-0 accent-[#1C4D8D]"
                      />
                      <span
                        aria-hidden="true"
                        className={cn(
                          'hidden size-10 shrink-0 items-center justify-center rounded-xl text-sm font-bold sm:flex',
                          existing
                            ? 'bg-slate-200 text-[#78879a]'
                            : checked
                              ? 'bg-[#1C4D8D] text-white'
                              : 'bg-[#E7F3FB] text-[#1C4D8D]',
                        )}
                      >
                        {l.fullName.trim().split(/\s+/).at(-1)?.charAt(0).toUpperCase()}
                      </span>
                      <span className="min-w-0">
                        <span className="block text-sm font-bold wrap-anywhere text-[#0F2854]">
                          {l.fullName}
                        </span>
                        <span className="block text-xs wrap-anywhere text-[#5b6b84]">
                          {l.email}
                        </span>
                      </span>
                    </label>
                    {existing ? (
                      <span className={cn(ds.infoChip, 'gap-1.5 whitespace-nowrap')}>
                        <span aria-hidden="true" className="size-1.5 rounded-full bg-[#4988C4]" />
                        Đã giao {existing.setCount} bộ đề
                      </span>
                    ) : (
                      <div className="shrink-0">
                        <div
                          className={cn(
                            'inline-flex h-10 items-center gap-1 rounded-xl border border-[#82aedb]/70 bg-white px-1 shadow-sm shadow-[#0F2854]/10',
                            errors[l.lecturerId] && fieldInvalidClass,
                            (!checked || busy) && 'bg-slate-100',
                          )}
                        >
                          <button
                            type="button"
                            aria-label={`Giảm số đề của ${l.fullName}`}
                            disabled={!checked || busy || Number(selection[l.lecturerId]) <= 1}
                            onClick={() => {
                              setSelection((old) => ({
                                ...old,
                                [l.lecturerId]: String(
                                  Math.max(1, (Number(old[l.lecturerId]) || 1) - 1),
                                ),
                              }))
                              setErrors((old) => ({ ...old, [l.lecturerId]: '' }))
                            }}
                            className="flex size-8 items-center justify-center rounded-lg text-[#1C4D8D] transition hover:bg-[#E7F3FB] focus-visible:outline-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-35"
                          >
                            <Minus className="size-3.5" />
                          </button>
                          <label className="flex items-center gap-1 pr-1">
                            <span className="sr-only">Số bộ đề của {l.fullName}</span>
                            <input
                              id={`assignment-count-${l.lecturerId}`}
                              aria-label={`Số bộ đề của ${l.fullName}`}
                              type="number"
                              min={1}
                              step={1}
                              value={selection[l.lecturerId] ?? ''}
                              disabled={!checked || busy}
                              onChange={(e) => {
                                setSelection((old) => ({ ...old, [l.lecturerId]: e.target.value }))
                                setErrors((old) => ({ ...old, [l.lecturerId]: '' }))
                              }}
                              aria-invalid={Boolean(errors[l.lecturerId])}
                              className="h-8 w-10 [appearance:textfield] appearance-none border-0 bg-transparent text-center text-sm font-bold text-[#0F2854] tabular-nums outline-none focus:ring-0 focus:outline-none [&::-webkit-inner-spin-button]:appearance-none [&::-webkit-outer-spin-button]:appearance-none"
                            />
                            <span className="text-xs font-semibold text-[#5b6b84]">đề</span>
                          </label>
                          <button
                            type="button"
                            aria-label={`Tăng số đề của ${l.fullName}`}
                            disabled={
                              !checked || busy || Number(selection[l.lecturerId]) >= 2147483647
                            }
                            onClick={() => {
                              setSelection((old) => ({
                                ...old,
                                [l.lecturerId]: String(
                                  Math.min(
                                    2147483647,
                                    Math.max(1, (Number(old[l.lecturerId]) || 0) + 1),
                                  ),
                                ),
                              }))
                              setErrors((old) => ({ ...old, [l.lecturerId]: '' }))
                            }}
                            className="flex size-8 items-center justify-center rounded-lg text-[#1C4D8D] transition hover:bg-[#E7F3FB] focus-visible:outline-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-35"
                          >
                            <Plus className="size-3.5" />
                          </button>
                        </div>
                        <FieldError message={errors[l.lecturerId]} />
                      </div>
                    )}
                  </div>
                )
              })}
            </div>
            <FieldError message={errors.lecturer} />
          </fieldset>
          <div role="status" className="text-sm text-[#5b6b84]">
            Đang chọn{' '}
            <strong className="text-[#1C4D8D]">
              {selected.length} giảng viên · {selectedCount} bộ đề
            </strong>
            .
            {selected.length > 0 &&
              (assignedCount + selectedCount < preparation.requiredSetCount
                ? ' Chưa đủ số đề cần chuẩn bị; có thể phân công bổ sung sau.'
                : assignedCount + selectedCount > preparation.requiredSetCount
                  ? ' Số đề vượt yêu cầu sẽ dùng để dự phòng.'
                  : ' Đã đủ số đề cần chuẩn bị.')}
          </div>
          <div>
            <div className="grid min-w-0 gap-3 sm:grid-cols-[minmax(0,3fr)_minmax(0,2fr)]">
              <DateField
                label="Ngày hạn nộp"
                value={deadline.split('T')[0]}
                invalid={Boolean(errors.deadline)}
                onChange={(value) => {
                  setDeadline(`${value}T${deadline.split('T')[1] || '00:00'}`)
                  setErrors((old) => ({ ...old, deadline: '' }))
                }}
              />
              <ClockField
                label="Giờ hạn nộp"
                value={deadline.split('T')[1] || '00:00'}
                invalid={Boolean(errors.deadline)}
                onChange={(value) => {
                  setDeadline(`${deadline.split('T')[0]}T${value}`)
                  setErrors((old) => ({ ...old, deadline: '' }))
                }}
              />
            </div>
            <FieldError message={errors.deadline} />
          </div>
          <TextareaField
            label="Ghi chú chung (không bắt buộc)"
            placeholder="Nhập yêu cầu dành cho giảng viên"
            value={notes}
            maxLength={2000}
            onChange={(e) => setNotes(e.target.value)}
            rows={2}
            className="min-h-0"
          />
          <FieldError message={errors.notes} />
          {error && <ExamLoadError message={error} />}
        </form>
      )}
    </FormDialog>
  )
}

export function ExamDeliveryDialog({
  preparation,
  papers,
  onClose,
  onSaved,
}: {
  preparation: ExamPreparation
  papers: ExamPaper[]
  onClose: () => void
  onSaved: () => void
}) {
  const eligible = papers.filter((p) => isDeliverable(p, preparation))
  const [selected, setSelected] = useState(() =>
    eligible.slice(0, preparation.requiredSetCount).map((p) => p.id),
  )
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const pending = useRef(false)
  async function send(event: FormEvent) {
    event.preventDefault()
    if (pending.current || selected.length < preparation.requiredSetCount) return
    pending.current = true
    setBusy(true)
    setError('')
    try {
      await service.handOver(preparation.sessionId, selected)
      toast.success('Đã bàn giao bộ đề cho Khảo thí.')
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
      title="Bàn giao đề thi"
      eyebrow={preparation.title}
      icon={Send}
      size="md"
      busy={busy}
      onClose={onClose}
      footer={
        <SubmitDialogFooter
          form="exam-delivery"
          label="Xác nhận bàn giao"
          busyLabel="Đang bàn giao…"
          busy={busy}
          disabled={selected.length < preparation.requiredSetCount}
          icon={Send}
          onCancel={onClose}
        />
      }
    >
      <form id="exam-delivery" onSubmit={(event) => void send(event)} className="space-y-3">
        <div className="grid grid-cols-3 gap-2">
          {[
            {
              label: 'Cần bàn giao',
              count: preparation.requiredSetCount,
              tone: 'border-[#82aedb]/55 bg-[#E7F3FB] text-[#1C4D8D]',
            },
            {
              label: 'Đủ điều kiện',
              count: eligible.length,
              tone: 'border-[#a9dfc7] bg-[#eaf8f1] text-[#16836a]',
            },
            {
              label: 'Đang chọn',
              count: selected.length,
              tone: 'border-[#cfc0ef] bg-[#f2edfc] text-[#7046ba]',
            },
          ].map((item) => (
            <div key={item.label} className={cn('rounded-xl border p-3 shadow-sm', item.tone)}>
              <p className="text-xs font-semibold">{item.label}</p>
              <p className="mt-1 text-xl font-bold tabular-nums">
                {item.count} <span className="text-xs font-medium">bộ đề</span>
              </p>
            </div>
          ))}
        </div>
        <div>
          <h3 className="text-sm font-bold text-[#0F2854]">Bộ đề đã duyệt</h3>
          <p className="mt-1 text-sm text-[#5b6b84]">
            Chọn các bộ đề theo cấu trúc hiện tại để bàn giao khảo thí.
          </p>
        </div>
        {eligible.length ? (
          <div className="max-h-72 divide-y divide-[#82aedb]/40 overflow-y-auto rounded-xl border border-[#82aedb]/55 bg-white">
            {eligible.map((p) => (
              <label
                key={p.id}
                className={cn(
                  'flex cursor-pointer items-center gap-3 p-3 text-sm font-bold text-[#0F2854] transition-colors',
                  selected.includes(p.id) ? 'bg-[#E7F3FB]' : 'hover:bg-[#F4FBFD]',
                  busy && 'cursor-wait',
                )}
              >
                <input
                  type="checkbox"
                  className="size-4 shrink-0 accent-[#1C4D8D]"
                  checked={selected.includes(p.id)}
                  disabled={busy}
                  onChange={(e) =>
                    setSelected((ids) =>
                      e.target.checked ? [...ids, p.id] : ids.filter((id) => id !== p.id),
                    )
                  }
                />
                <span className="min-w-0 flex-1 wrap-anywhere">{p.setCode}</span>
                <span className={ds.successChip}>{p.questions.length} câu</span>
              </label>
            ))}
          </div>
        ) : (
          <EmptyState title="Chưa có đề đủ điều kiện bàn giao" />
        )}
        <div className="rounded-xl border border-[#f3d19a] bg-[#fff8eb] p-3 text-sm text-[#92600c]">
          Sau khi bàn giao, nội dung bộ đề sẽ được khóa.
        </div>
        {error && <ExamLoadError message={error} />}
      </form>
    </FormDialog>
  )
}
