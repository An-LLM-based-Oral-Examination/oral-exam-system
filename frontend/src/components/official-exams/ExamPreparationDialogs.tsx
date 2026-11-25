import { useEffect, useRef, useState, type FormEvent } from 'react'
import { BookOpenCheck, Check, Layers, Save, Send, Settings2, UsersRound } from 'lucide-react'
import { FormDialog, DialogFooter } from '@/design/overlays'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { FieldError } from '@/components/common/formField'
import { fieldInvalidClass, fieldLabelClass } from '@/components/common/formFieldClasses'
import { TextareaField } from '@/components/common/TextareaField'
import { SubmitDialogFooter } from '@/components/common/SubmitDialogFooter'
import { FilterDropMenu } from '@/components/common/FilterDropMenu'
import { EmptyState } from '@/components/common/EmptyState'
import { LoadingState } from '@/components/common/LoadingState'
import { toast } from '@/components/common/toastStore'
import { examPreparationService as service } from '@/services/examPreparation.service'
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
import { BlueprintFacts, ExamLoadError } from './ExamPreparationShared'

function NumberField({
  id,
  label,
  value,
  onChange,
  error,
  min = 0,
}: {
  id: string
  label: string
  value: string
  onChange: (value: string) => void
  error?: string
  min?: number
}) {
  return (
    <div className="min-w-0">
      <label htmlFor={id} className={fieldLabelClass(Boolean(error))}>
        {label}
      </label>
      <input
        id={id}
        type="number"
        inputMode="numeric"
        min={min}
        step={1}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        aria-invalid={Boolean(error)}
        aria-describedby={error ? `${id}-error` : undefined}
        className={cn(ds.field, 'mt-1.5 h-10', error && fieldInvalidClass)}
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
  current,
  clos,
  locked,
  onClose,
  onSaved,
}: {
  sessionId: string
  current?: ExamBlueprint
  clos: CourseClo[]
  locked: boolean
  onClose: () => void
  onSaved: () => void
}) {
  const [editing, setEditing] = useState(!current || current.cloIds.length === 0)
  const [form, setForm] = useState({
    name: current?.name ?? 'Cấu trúc đề thi',
    totalQuestions: String(current?.totalQuestions ?? 5),
    durationMinutes: String(current?.durationMinutes ?? 30),
    easyCount: String(current?.easyCount ?? 1),
    mediumCount: String(current?.mediumCount ?? 3),
    hardCount: String(current?.hardCount ?? 1),
  })
  const [cloIds, setCloIds] = useState<string[]>(current?.cloIds ?? [])
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
    for (const key of [
      'totalQuestions',
      'durationMinutes',
      'easyCount',
      'mediumCount',
      'hardCount',
    ] as const)
      if (!validNumber(form[key], key === 'totalQuestions' || key === 'durationMinutes' ? 1 : 0))
        next[key] = 'Nhập số nguyên hợp lệ.'
    if (total !== Number(form.totalQuestions))
      next.distribution = 'Tổng phân bổ độ khó phải bằng số câu mỗi đề.'
    if (!cloIds.length || cloIds.some((id) => !activeClos.some((c) => c.id === id)))
      next.clos = 'Chọn ít nhất một CLO đang hoạt động của môn học.'
    setErrors(next)
    if (Object.keys(next).length) return
    pending.current = true
    setBusy(true)
    setError('')
    const input: BlueprintInput = {
      name: form.name.trim(),
      totalQuestions: Number(form.totalQuestions),
      durationMinutes: Number(form.durationMinutes),
      easyCount: Number(form.easyCount),
      mediumCount: Number(form.mediumCount),
      hardCount: Number(form.hardCount),
      cloIds,
    }
    try {
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
  return (
    <>
      <FormDialog
        title={editing ? 'Cấu hình đề thi' : 'Cấu trúc đề thi'}
        eyebrow="Chuẩn bị đề thi"
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
              <button type="button" className={ds.outlineButton} onClick={onClose}>
                Đóng
              </button>
              {!locked && (
                <button type="button" className={ds.primaryButton} onClick={() => setEditing(true)}>
                  Tạo phiên bản mới
                </button>
              )}
            </DialogFooter>
          )
        }
      >
        {!editing || locked ? (
          <div className="space-y-4">
            <h3 className="font-bold text-[#0F2854]">{current?.name}</h3>
            {current && <BlueprintFacts blueprint={current} />}
            <p className={fieldLabelClass()}>Phạm vi CLO</p>
            <ul className="space-y-3">
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
          </div>
        ) : (
          <form
            id="exam-blueprint"
            noValidate
            onSubmit={(event) => void save(event)}
            className="min-w-0 space-y-4"
          >
            {current && (
              <p className="text-sm leading-relaxed text-[#5b6b84]">
                Lưu thành phiên bản mới. Các phân công đã giao giữ cấu trúc cũ; bàn giao sử dụng
                phiên bản hiện tại.
              </p>
            )}
            <div>
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
            <div className="grid min-w-0 grid-cols-2 gap-3">
              <NumberField
                id="blueprint-total"
                label="Số câu mỗi đề"
                value={form.totalQuestions}
                min={1}
                onChange={(v) => update('totalQuestions', v)}
                error={errors.totalQuestions}
              />
              <NumberField
                id="blueprint-duration"
                label="Thời gian (phút)"
                value={form.durationMinutes}
                min={1}
                onChange={(v) => update('durationMinutes', v)}
                error={errors.durationMinutes}
              />
            </div>
            <div className="rounded-xl border border-[#82aedb]/55 bg-white p-3">
              <div className="mb-3 flex items-center justify-between gap-2">
                <p className={fieldLabelClass(Boolean(errors.distribution))}>Phân bổ độ khó</p>
                <span className="text-xs font-bold text-[#1C4D8D]">
                  {Number.isFinite(total) ? total : 0}/{form.totalQuestions || 0} câu
                </span>
              </div>
              <div className="grid grid-cols-3 gap-3">
                {(
                  [
                    { key: 'easyCount', label: 'Dễ' },
                    { key: 'mediumCount', label: 'Trung bình' },
                    { key: 'hardCount', label: 'Khó' },
                  ] as const
                ).map((item) => (
                  <NumberField
                    key={item.key}
                    id={`blueprint-${item.key}`}
                    label={item.label}
                    value={form[item.key]}
                    onChange={(v) => update(item.key, v)}
                    error={errors[item.key]}
                  />
                ))}
              </div>
              <FieldError message={errors.distribution} />
            </div>
            <div
              className={cn(
                'flex flex-wrap items-center justify-between gap-3 rounded-xl border bg-[#F4FBFD] p-3',
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
            </div>
            <FieldError message={errors.clos} />
            {error && <ExamLoadError message={error} />}
          </form>
        )}
      </FormDialog>
      {picker && (
        <FormDialog
          title="Chọn CLO"
          eyebrow="Cấu trúc đề thi"
          icon={BookOpenCheck}
          size="lg"
          nested
          onClose={() => setPicker(false)}
          bodyClassName="p-0 sm:p-0"
          footer={
            <DialogFooter>
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
                <Check className="size-4" />
                Áp dụng ({draftClos.length})
              </button>
            </DialogFooter>
          }
        >
          <p className="border-b border-[#82aedb]/55 px-5 py-3 text-sm text-[#5b6b84]">
            Chọn ít nhất một CLO. Bloom mục tiêu là thông tin tham khảo.
          </p>
          {activeClos.length ? (
            <ul className="divide-y divide-[#82aedb]/45 px-5">
              {activeClos.map((c) => (
                <li key={c.id}>
                  <label
                    className={cn(
                      'flex cursor-pointer items-start gap-3 py-4',
                      draftClos.includes(c.id) && 'text-[#1C4D8D]',
                    )}
                  >
                    <input
                      type="checkbox"
                      className="mt-1 size-4 shrink-0 accent-[#1C4D8D]"
                      checked={draftClos.includes(c.id)}
                      onChange={(e) =>
                        setDraftClos((ids) =>
                          e.target.checked ? [...ids, c.id] : ids.filter((id) => id !== c.id),
                        )
                      }
                    />
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className={ds.infoChip}>{c.cloCode}</span>
                        <span className="text-xs text-[#5b6b84]">
                          Bloom mục tiêu: {c.bloomLevel}
                        </span>
                      </div>
                      <p className="mt-2 text-sm leading-relaxed wrap-anywhere">{c.description}</p>
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
  offeringId,
  onClose,
  onSaved,
}: {
  preparation: ExamPreparation
  offeringId?: string
  onClose: () => void
  onSaved: () => void
}) {
  const [lecturers, setLecturers] = useState<AssignedLecturer[]>()
  const [loadError, setLoadError] = useState('')
  const [retry, setRetry] = useState(0)
  const [lecturerId, setLecturerId] = useState('')
  const [setCount, setSetCount] = useState('1')
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
          setLecturers(rows.filter((row) => row.isActive))
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
  async function save(event: FormEvent) {
    event.preventDefault()
    if (pending.current) return
    const next: Record<string, string> = {}
    if (!candidates.some((l) => l.lecturerId === lecturerId))
      next.lecturer = 'Vui lòng chọn giảng viên được phân công môn học.'
    if (!validNumber(setCount, 1)) next.count = 'Nhập số bộ đề lớn hơn 0.'
    const due = new Date(deadline)
    if (!isValidExamDeadline(deadline, preparation.deadline))
      next.deadline = 'Hạn nộp phải ở tương lai và không sau hạn bàn giao.'
    setErrors(next)
    if (Object.keys(next).length) return
    pending.current = true
    setBusy(true)
    setError('')
    try {
      await service.assign(preparation.sessionId, {
        lecturerId,
        structureId: preparation.examStructureId!,
        setCount: Number(setCount),
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
          label="Giao soạn đề"
          busyLabel="Đang lưu…"
          busy={busy}
          disabled={!offeringId || !candidates.length || Boolean(loadError)}
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
      ) : !candidates.length ? (
        <EmptyState
          title="Không có giảng viên để phân công"
          description="Các giảng viên đã được giao cấu trúc này hoặc môn học chưa có giảng viên đang hoạt động."
        />
      ) : (
        <form
          id="exam-assignment"
          noValidate
          onSubmit={(event) => void save(event)}
          className="space-y-4"
        >
          <div>
            <p className={fieldLabelClass(Boolean(errors.lecturer))}>Giảng viên</p>
            <FilterDropMenu
              label="Chọn giảng viên"
              value={lecturerId}
              options={candidates.map((l) => ({
                value: l.lecturerId,
                label: `${l.fullName} · ${l.email}`,
              }))}
              onChange={(id) => {
                setLecturerId(id)
                setErrors((e) => ({ ...e, lecturer: '' }))
              }}
              className="mt-1.5 w-full"
              menuMinWidth={0}
              wrapOptions
              invalid={Boolean(errors.lecturer)}
            />
            <FieldError message={errors.lecturer} />
          </div>
          <NumberField
            id="assignment-count"
            label="Số bộ đề"
            min={1}
            value={setCount}
            onChange={(value) => {
              setSetCount(value)
              setErrors((e) => ({ ...e, count: '' }))
            }}
            error={errors.count}
          />
          <div>
            <label
              htmlFor="assignment-deadline"
              className={fieldLabelClass(Boolean(errors.deadline))}
            >
              Hạn nộp
            </label>
            <input
              id="assignment-deadline"
              type="datetime-local"
              className={cn(ds.field, 'mt-1.5 h-10', errors.deadline && fieldInvalidClass)}
              value={deadline}
              max={localDateTime(preparation.deadline)}
              onChange={(e) => {
                setDeadline(e.target.value)
                setErrors((old) => ({ ...old, deadline: '' }))
              }}
              aria-invalid={Boolean(errors.deadline)}
            />
            <FieldError message={errors.deadline} />
          </div>
          <TextareaField
            label="Ghi chú (không bắt buộc)"
            placeholder="Nhập yêu cầu dành cho giảng viên"
            value={notes}
            maxLength={2000}
            onChange={(e) => setNotes(e.target.value)}
            rows={3}
          />
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
      eyebrow={preparation.courseCode}
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
      <form id="exam-delivery" onSubmit={(event) => void send(event)} className="space-y-4">
        <div className="rounded-xl border border-[#82aedb]/55 bg-[#F4FBFD] p-3">
          <p className="font-bold text-[#0F2854]">
            Đã chọn {selected.length}/{preparation.requiredSetCount} đề cần bàn giao
          </p>
          <p className="mt-1 text-sm text-[#5b6b84]">
            Chỉ gồm đề đã duyệt theo cấu trúc hiện tại. Sau bàn giao, nội dung được khóa.
          </p>
        </div>
        {eligible.length ? (
          <div className="max-h-72 space-y-1 overflow-y-auto">
            {eligible.map((p) => (
              <label
                key={p.id}
                className="flex cursor-pointer items-center gap-3 border-b border-[#82aedb]/40 py-3 text-sm font-bold text-[#0F2854]"
              >
                <input
                  type="checkbox"
                  className="size-4 shrink-0 accent-[#1C4D8D]"
                  checked={selected.includes(p.id)}
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
        {error && <ExamLoadError message={error} />}
      </form>
    </FormDialog>
  )
}
