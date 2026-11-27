import { SubmitDialogFooter } from '@/components/common/SubmitDialogFooter'
import { TextareaField } from '@/components/common/TextareaField'
import { useEffect, useState, type FormEvent } from 'react'
import { BookOpenCheck, Pencil, Plus, RefreshCw, Save, Trash2 } from 'lucide-react'
import { EmptyState } from '@/components/common/EmptyState'
import { FilterDropMenu } from '@/components/common/FilterDropMenu'
import { fieldInvalidClass, fieldLabelClass } from '@/components/common/formFieldClasses'
import { FieldError } from '@/components/common/formField'
import { ConfirmDialog } from '@/design/ConfirmDialog'
import { FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { courseCloService } from '@/services/courseClo.service'
import { CLO_BLOOM_LEVELS, type CloBloomLevel, type CourseClo, type ManagedCloCourse, type UpdateCourseCloInput } from '@/types/courseClo.types'
import { userMessage } from '@/lib/userMessage'

const control = ds.field
const button = ds.outlineButton
const createdDateFormatter = new Intl.DateTimeFormat('vi-VN', {
  day: '2-digit', month: '2-digit', year: 'numeric', timeZone: 'Asia/Ho_Chi_Minh',
})
const freshForm = (orderIndex = 1): UpdateCourseCloInput => ({ cloCode: '', description: '', bloomLevel: 'Understand', orderIndex, isActive: true })

export default function DepartmentCourseClosPage({
  managedCourse,
  onClosChanged,
}: { managedCourse?: ManagedCloCourse; onClosChanged?: () => void } = {}) {
  const [courses, setCourses] = useState<ManagedCloCourse[]>([])
  const [selectedCourseId, setCourseId] = useState('')
  const courseId = managedCourse?.id ?? selectedCourseId
  const [clos, setClos] = useState<CourseClo[]>([])
  const [loading, setLoading] = useState(true)
  const [coursesLoaded, setCoursesLoaded] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [revision, setRevision] = useState(0)
  const [editing, setEditing] = useState<CourseClo | 'new' | null>(null)
  const [deleting, setDeleting] = useState<CourseClo | null>(null)
  const [form, setForm] = useState(freshForm())
  const [fieldErrors, setFieldErrors] = useState<{ cloCode?: string; description?: string; bloomLevel?: string }>({})

  function updateForm<Key extends keyof UpdateCourseCloInput>(key: Key, value: UpdateCourseCloInput[Key]) {
    setForm(current => ({ ...current, [key]: value }))
    setFieldErrors(current => ({ ...current, [key]: undefined }))
    setError('')
  }

  useEffect(() => {
    if (managedCourse) return
    let active = true
    courseCloService.getManagedCourses().then(items => {
      if (!active) return
      setCourses(items)
      setCourseId(items[0]?.id ?? '')
      setCoursesLoaded(true)
      if (!items.length) setLoading(false)
    }).catch(failure => {
      if (active) { setError(userMessage(failure, 'Không tải được các môn phụ trách.')); setLoading(false) }
    })
    return () => { active = false }
  }, [managedCourse])

  useEffect(() => {
    if (!courseId) return
    let active = true
    courseCloService.list(courseId).then(items => { if (active) setClos(items) })
      .catch(failure => { if (active) setError(userMessage(failure, 'Không tải được danh sách CLO.')) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [courseId, revision])

  function prepareLoad() {
    setLoading(true)
    setClos([])
    setEditing(null)
    setDeleting(null)
    setError('')
    setNotice('')
  }

  function startEdit(clo?: CourseClo) {
    setFieldErrors({})
    setEditing(clo ?? 'new')
    setForm(clo ? {
      cloCode: clo.cloCode, description: clo.description, bloomLevel: clo.bloomLevel,
      orderIndex: clo.orderIndex, isActive: clo.isActive,
    } : freshForm(Math.max(0, ...clos.map(item => item.orderIndex)) + 1))
    setError('')
    setNotice('')
  }

  async function save(event: FormEvent) {
    event.preventDefault()
    if (!editing || !courseId || busy) return
    const input = { ...form, cloCode: form.cloCode.trim().toUpperCase(), description: form.description.trim() }
    const errors: typeof fieldErrors = {}
    if (!input.cloCode) errors.cloCode = 'Vui lòng nhập mã CLO.'
    else if (input.cloCode.length > 30) errors.cloCode = 'Mã CLO không được vượt quá 30 ký tự.'
    if (!input.description) errors.description = 'Vui lòng nhập mô tả chuẩn đầu ra.'
    else if (input.description.length > 1000) errors.description = 'Mô tả không được vượt quá 1000 ký tự.'
    if (!CLO_BLOOM_LEVELS.includes(input.bloomLevel)) errors.bloomLevel = 'Vui lòng chọn Bloom mục tiêu hợp lệ.'
    if (clos.some(item => item.id !== (editing === 'new' ? '' : editing.id)
      && item.cloCode.trim().toUpperCase() === input.cloCode)) {
      errors.cloCode = 'Mã CLO đã tồn tại trong môn học.'
    }
    setFieldErrors(errors)
    setError('')
    if (Object.keys(errors).length > 0) return
    setBusy(true)
    setError('')
    try {
      const saved = editing === 'new'
        ? await courseCloService.create(courseId, { cloCode: input.cloCode, description: input.description, bloomLevel: input.bloomLevel, orderIndex: input.orderIndex })
        : await courseCloService.update(courseId, editing.id, input)
      setClos(current => [...current.filter(item => item.id !== saved.id), saved].sort((a, b) => a.orderIndex - b.orderIndex || a.cloCode.localeCompare(b.cloCode)))
      onClosChanged?.()
      setEditing(null)
      setNotice('Đã lưu chuẩn đầu ra.')
    } catch (failure) { setError(userMessage(failure, 'Không lưu được CLO. Vui lòng thử lại.')) }
    finally { setBusy(false) }
  }

  async function remove() {
    if (!deleting || busy) return
    setBusy(true)
    setError('')
    try {
      await courseCloService.remove(courseId, deleting.id)
      setClos(current => current.filter(item => item.id !== deleting.id))
      onClosChanged?.()
      setDeleting(null)
      setNotice('Đã xóa chuẩn đầu ra.')
    } catch (failure) {
      setDeleting(null)
      const baseMessage = userMessage(failure, 'Không xóa được CLO. Vui lòng kiểm tra các tham chiếu.')
      const isConflict = typeof failure === 'object' && failure !== null && 'status' in failure && failure.status === 409
      const needsDeactivateHint = (isConflict || /tham chiếu|sử dụng/i.test(baseMessage)) && !/ngừng sử dụng|is_active|isActive|tạm ngừng|vô hiệu/i.test(baseMessage)
      setError(needsDeactivateHint ? `${baseMessage} Hãy chọn Sửa và chuyển sang trạng thái Ngừng sử dụng thay vì xóa.` : baseMessage)
    }
    finally { setBusy(false) }
  }

  return <div className={managedCourse ? 'space-y-4' : ds.page}>
    {!managedCourse && <header className={cn(ds.section, ds.sectionHeader)}>
      <div className={ds.sectionTitleRow}>
        <span className={ds.sectionIconWrap}><BookOpenCheck className={ds.sectionIcon} /></span>
        <h1 className={ds.sectionTitle}>Chuẩn đầu ra môn học (CLO)</h1>
      </div>
      <p className={ds.sectionHint}>Quản lý các chuẩn đầu ra được câu hỏi và cấu trúc đề tham chiếu.</p>
    </header>}
    {error && !editing && <p role="alert" className="rounded-lg bg-red-50 p-3 text-sm text-red-800">{error}</p>}
    {notice && <p role="status" className="rounded-lg bg-green-50 p-3 text-sm text-green-800">{notice}</p>}
    <section className={ds.section}>
      <div className={cn(ds.sectionHeader, 'flex flex-wrap items-center justify-between gap-3')}>
        <div className={ds.sectionHeading}>
          <div className={ds.sectionTitleRow}>
            <span className={ds.sectionIconWrap}><BookOpenCheck className={ds.sectionIcon} /></span>
            <h2 className={ds.sectionTitle}>Danh sách CLO</h2>
            {!loading && <span className={ds.sectionBadge}>{clos.length} CLO</span>}
          </div>
          <p className={ds.sectionHint}>Chuẩn đầu ra của môn học</p>
        </div>
        {(managedCourse || courses.length > 0) && <div className="flex min-w-0 flex-wrap items-center gap-2">
      {!managedCourse && (
        <div className={ds.filterCourse}>
          <FilterDropMenu
            label="Môn học phụ trách"
            menuMinWidth={ds.filterMenuMinWidth}
            value={courseId}
            disabled={busy}
            options={courses.map((course) => ({ value: course.id, label: `${course.code} — ${course.name}` }))}
            onChange={(value) => {
              if (value === courseId) return
              prepareLoad()
              setCourseId(value)
            }}
            wrapOptions
          />
        </div>
      )}
      <button type="button" className={button} disabled={loading || busy} onClick={() => { prepareLoad(); setRevision(value => value + 1) }}>
        <RefreshCw className="size-3.5" aria-hidden="true" />
        Làm mới
      </button>
      <button className={ds.primaryButton} disabled={loading || busy} onClick={() => startEdit()}>
        <Plus className="size-4" aria-hidden="true" /> Thêm CLO
      </button>
        </div>}
      </div>
      <div className={cn(ds.sectionBody, !loading && clos.length > 0 && 'py-0 sm:py-0')}>
    {loading ? <p role="status">Đang tải CLO...</p> : coursesLoaded && !courses.length
      ? <EmptyState icon={BookOpenCheck} title="Bạn chưa được phân công phụ trách môn học nào." />
      : courseId && (clos.length === 0
        ? <EmptyState icon={BookOpenCheck} title="Môn học chưa có CLO" description="Thêm chuẩn đầu ra đầu tiên cho môn học."
            action={<button type="button" className={ds.primaryButton} disabled={busy} onClick={() => startEdit()}>
              <Plus className="size-4" aria-hidden="true" /> Thêm CLO
            </button>} />
        : <ul aria-label="Danh sách CLO" className="divide-y divide-[#82aedb]/55">
            {clos.map((clo) => {
              const createdAt = new Date(clo.createdAt)

              return (
              <li key={clo.id} className="flex flex-wrap items-center gap-3 py-4 transition-colors hover:bg-[#F4FBFD] lg:grid lg:grid-cols-[minmax(0,1fr)_140px_auto]">
                <div className="min-w-0 basis-full [overflow-wrap:anywhere] lg:pr-12">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="inline-flex rounded-lg border border-[#82aedb]/65 bg-[#E7F3FB] px-2.5 py-1 font-mono text-sm font-bold text-[#1C4D8D]">{clo.cloCode}</span>
                    <span className={cn('rounded-full px-2 py-1 text-xs font-bold whitespace-nowrap', clo.isActive ? 'bg-[#eaf9f4] text-[#17745b]' : 'bg-[#f1f3f6] text-[#6f7c8e]')}>
                      {clo.isActive ? 'Hoạt động' : 'Ngừng sử dụng'}
                    </span>
                  </div>
                  <p className="mt-2 text-sm leading-snug font-bold whitespace-pre-wrap text-[#0F2854]">{clo.description}</p>
                  {Number.isFinite(createdAt.getTime()) && (
                    <p className="mt-1 text-xs text-[#8794a6]">
                      Tạo ngày <time dateTime={createdAt.toISOString()}>{createdDateFormatter.format(createdAt)}</time>
                    </p>
                  )}
                </div>
                <div className="flex items-center gap-2 lg:block">
                  <span className="text-xs font-semibold text-[#8995a6] lg:block">Bloom mục tiêu</span>
                  <span className="mt-0.5 inline-flex rounded-lg bg-[#4988C4]/16 px-2 py-1 text-xs font-bold text-[#1C4D8D]">{clo.bloomLevel}</span>
                </div>
                <div className="ml-auto flex shrink-0 items-center gap-2 whitespace-nowrap">
                  <button type="button" className={cn(button, 'h-9 px-3 text-xs')} aria-label={`Sửa ${clo.cloCode}`} disabled={busy} onClick={() => startEdit(clo)}>
                    <Pencil className="size-3.5" aria-hidden="true" /> Sửa
                  </button>
                  <button
                    type="button"
                    className={cn(button, 'h-9 border-[#e8a8b5] bg-[#fff7f8] px-3 text-xs text-[#c2415b] hover:border-[#c2415b] hover:bg-[#ffe9ed]')}
                    aria-label={`Xóa ${clo.cloCode}`}
                    disabled={busy}
                    onClick={() => { setDeleting(clo); setError(''); setNotice('') }}
                  >
                    <Trash2 className="size-3.5" aria-hidden="true" /> Xóa
                  </button>
                </div>
              </li>
              )
            })}
          </ul>)}
      </div>
    </section>
    {editing && <FormDialog
      eyebrow="Chi tiết môn học"
      title={editing === 'new' ? 'Thêm chuẩn đầu ra' : `Sửa ${editing.cloCode}`}
      icon={BookOpenCheck}
      size="sm"
      busy={busy}
      onClose={() => setEditing(null)}
      footer={<SubmitDialogFooter
              form="department-clo-form"
              label="Lưu CLO"
              busyLabel="Đang lưu…"
              busy={busy}
              icon={Save}
              onCancel={() => setEditing(null)}
            />}
    >
      <form id="department-clo-form" noValidate onSubmit={event => void save(event)} className="grid gap-3 sm:grid-cols-2">
        {error && <p role="alert" className="rounded-lg bg-red-50 p-3 text-sm text-red-800 sm:col-span-2">{error}</p>}
        <label className="block min-w-0">
          <span className={fieldLabelClass(Boolean(fieldErrors.cloCode))}>Mã CLO</span>
          <input className={cn(control, 'mt-1.5', fieldErrors.cloCode && fieldInvalidClass)} aria-invalid={Boolean(fieldErrors.cloCode)} aria-describedby={fieldErrors.cloCode ? 'clo-code-error' : undefined} placeholder="Ví dụ: CLO1" value={form.cloCode} maxLength={30} required disabled={busy} onChange={event => updateForm('cloCode', event.target.value)} />
          {fieldErrors.cloCode && <div id="clo-code-error" role="alert"><FieldError message={fieldErrors.cloCode} /></div>}
        </label>
        <div className="min-w-0">
          <span className={fieldLabelClass(Boolean(fieldErrors.bloomLevel))}>Bloom mục tiêu</span>
          <FilterDropMenu
            label="Bloom mục tiêu"
            className="mt-1.5"
            disabled={busy}
            invalid={Boolean(fieldErrors.bloomLevel)}
            value={form.bloomLevel}
            menuMinWidth={0}
            options={CLO_BLOOM_LEVELS.map((level) => ({ value: level, label: level }))}
            onChange={(value) => updateForm('bloomLevel', value as CloBloomLevel)}
          />
          <p className="mt-1.5 text-xs leading-relaxed text-[#5b6b84]">Mức tư duy mà sinh viên cần đạt</p>
          {fieldErrors.bloomLevel && <div role="alert"><FieldError message={fieldErrors.bloomLevel} /></div>}
        </div>
        <TextareaField label="Tên / mô tả chuẩn đầu ra" error={fieldErrors.description} wrapperClassName="sm:col-span-2"    placeholder="Nhập kiến thức hoặc năng lực sinh viên cần đạt" rows={3} maxLength={1000} required disabled={busy} value={form.description} onChange={event => updateForm('description', event.target.value)} />

        {editing !== 'new' && (
          <button
            type="button"
            role="switch"
            aria-label="Hoạt động"
            aria-checked={form.isActive}
            disabled={busy}
            onClick={() => setForm({ ...form, isActive: !form.isActive })}
            className="flex w-full items-center justify-between gap-3 rounded-xl border border-[#82aedb]/45 bg-white px-3 py-3 text-left transition hover:border-[#4988C4] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:opacity-50 sm:col-span-2"
          >
            <span>
              <span className="block text-sm font-bold text-[#0F2854]">Hoạt động</span>
              <span className="mt-0.5 block text-xs leading-relaxed text-[#5b6b84]">Cho phép chọn CLO khi tạo câu hỏi.</span>
            </span>
            <span aria-hidden="true" className={cn('relative h-6 w-11 shrink-0 rounded-full transition-colors', form.isActive ? 'bg-[#16836a]' : 'bg-[#cbd5e1]')}>
              <span className={cn('absolute top-0.5 left-0.5 size-5 rounded-full bg-white shadow-sm transition-transform motion-reduce:transition-none', form.isActive && 'translate-x-5')} />
            </span>
          </button>
        )}
      </form>
    </FormDialog>}
    {deleting && <ConfirmDialog
      tone="danger"
      title={`Xác nhận xóa ${deleting.cloCode}`}
      message="CLO đang được câu hỏi hoặc cấu trúc đề sử dụng sẽ được giữ lại."
      confirmLabel="Xác nhận xóa"
      cancelLabel="Hủy"
      pending={busy}
      onConfirm={() => void remove()}
      onCancel={() => setDeleting(null)}
    />}
  </div>
}
