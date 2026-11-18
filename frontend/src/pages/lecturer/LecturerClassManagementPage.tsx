import { ClassCapacity } from '@/components/common/ClassCapacity'
import { CompactPagination } from '@/components/common/CompactPagination'
import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { ArrowRight, CalendarDays, Check, Copy, KeyRound, LoaderCircle, Plus, Search, UsersRound } from 'lucide-react'
import { EmptyState } from '@/components/common/EmptyState'
import { FieldError } from '@/components/common/formField'
import { fieldInvalidClass, fieldLabelClass } from '@/components/common/formFieldClasses'
import { StatusToast } from '@/components/common/toast'
import { toast } from '@/components/common/toastStore'
import { FilterDropMenu } from '@/components/common/FilterDropMenu'
import { DialogFooter, FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'
import { scheduleUi } from '@/lib/scheduleUi'
import { cn } from '@/lib/utils'
import { userMessage } from '@/lib/userMessage'
import { classService } from '@/services/class.service'
import type { CreateClassResponse, LecturerCourseOffering, ManagedClass, SemesterOption } from '@/types/class.types'

function getErrorMessage(error: unknown) {
  const message = userMessage(error, 'Không tải được dữ liệu lớp học. Vui lòng thử lại.')
  return message.replace(/^VALIDATION\s*[:\-–—]\s*/i, '').trim() || 'Không tải được dữ liệu lớp học. Vui lòng thử lại.'
}

async function copyText(value: string): Promise<boolean> {
  if (!value) return false
  try {
    if (typeof navigator !== 'undefined' && navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(value)
      return true
    }
  } catch {
    // Fallback sang execCommand khi Clipboard API bị hạn chế
  }
  if (typeof document === 'undefined' || !document.body) return false
  const textarea = document.createElement('textarea')
  textarea.value = value
  textarea.setAttribute('readonly', '')
  textarea.style.position = 'fixed'
  textarea.style.opacity = '0'
  try {
    document.body.appendChild(textarea)
    textarea.select()
    return typeof document.execCommand === 'function' ? document.execCommand('copy') : false
  } catch {
    return false
  } finally {
    textarea.value = ''
    textarea.remove()
  }
}

const COURSE_CODE_TONES = [
  'border border-[#4988C4]/35 bg-[#4988C4]/16 text-[#1C4D8D]',
  'border border-[#16836a]/30 bg-[#e8f8f3] text-[#16836a]',
  'border border-[#c26a12]/30 bg-[#fff4e8] text-[#c26a12]',
  'border border-[#6d4cc4]/30 bg-[#f3eefe] text-[#6d4cc4]',
  'border border-[#0e7490]/30 bg-[#e7f6fb] text-[#0e7490]',
  'border border-[#c2415b]/30 bg-[#fdecef] text-[#c2415b]',
  'border border-[#3d7a2a]/30 bg-[#eef8e8] text-[#3d7a2a]',
  'border border-[#be185d]/30 bg-[#fff0f6] text-[#be185d]',
]

function courseToneIndex(code: string) {
  let hash = 0
  for (let index = 0; index < code.length; index += 1) hash = (hash * 31 + code.charCodeAt(index)) >>> 0
  return hash
}

function courseCodeTone(code: string) {
  return COURSE_CODE_TONES[courseToneIndex(code) % COURSE_CODE_TONES.length]
}

function dateKey(value: string) {
  const [year, month, day] = value.slice(0, 10).split('-').map(Number)
  if (!year || !month || !day) return null
  return year * 10000 + month * 100 + day
}

function pickCurrentSemester(semesters: SemesterOption[]) {
  const today = new Date()
  const todayKey = today.getFullYear() * 10000 + (today.getMonth() + 1) * 100 + today.getDate()
  const covering = semesters.filter((item) => {
    const start = dateKey(item.startDate)
    const end = dateKey(item.endDate)
    return start !== null && end !== null && todayKey >= start && todayKey <= end
  })
  return covering.find((item) => item.isActive) ?? covering[0] ?? semesters.find((item) => item.isActive) ?? semesters[0]
}

export function LecturerClassManagementPage() {
  const [offerings, setOfferings] = useState<LecturerCourseOffering[]>([])
  const [semesterCatalog, setSemesterCatalog] = useState<SemesterOption[]>([])
  const [classes, setClasses] = useState<ManagedClass[]>([])
  const [semesterId, setSemesterId] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [createOpen, setCreateOpen] = useState(false)
  const [createdClass, setCreatedClass] = useState<CreateClassResponse | null>(null)
  const [courseCode, setCourseCode] = useState('ALL')
  const [classSearch, setClassSearch] = useState('')
  const [appliedClassSearch, setAppliedClassSearch] = useState('')
  const [classPage, setClassPage] = useState(1)
  const [classPageSize, setClassPageSize] = useState(() =>
    window.matchMedia('(min-width: 80rem)').matches ? 6 : window.matchMedia('(min-width: 40rem)').matches ? 4 : 2,
  )
  const navigate = useNavigate()

  useEffect(() => {
    const wide = window.matchMedia('(min-width: 80rem)')
    const medium = window.matchMedia('(min-width: 40rem)')
    const update = () => {
      setClassPageSize(wide.matches ? 6 : medium.matches ? 4 : 2)
      setClassPage(1)
    }
    wide.addEventListener('change', update)
    medium.addEventListener('change', update)
    return () => {
      wide.removeEventListener('change', update)
      medium.removeEventListener('change', update)
    }
  }, [])

  useEffect(() => {
    const timeout = window.setTimeout(() => {
      setAppliedClassSearch(classSearch)
      setClassPage(1)
    }, 500)
    return () => window.clearTimeout(timeout)
  }, [classSearch])

  const load = async (selectedSemester = semesterId) => {
    setLoading(true)
    setError('')
    try {
      const [offeringResult, semesterResult] = await Promise.all([
        classService.listMyOfferings(),
        classService.listSemesters(),
      ])
      const assignedIds = new Set(offeringResult.map((item) => item.semesterId).filter(Boolean))
      const assignedSemesters = semesterResult.filter((item) => assignedIds.has(item.id))
      const current = pickCurrentSemester(assignedSemesters.length > 0 ? assignedSemesters : semesterResult)
      const nextId = selectedSemester || current?.id || assignedSemesters[0]?.id || semesterResult[0]?.id || ''
      const classResult = nextId ? await classService.listLecturerClasses(nextId) : []
      setOfferings(offeringResult)
      setSemesterCatalog(semesterResult)
      setSemesterId(nextId)
      setClasses(classResult)
    } catch (loadError) {
      setError(getErrorMessage(loadError))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    return scheduleUi(() => {
      void load('')
    })
  }, [])

  const semesters = useMemo(() => {
    const assigned = new Map<string, string>()
    for (const item of offerings) {
      if (item.semesterId) assigned.set(item.semesterId, item.semesterCode)
    }
    return [...assigned.entries()].map(([id, code]) => ({
      value: id,
      label: semesterCatalog.find((item) => item.id === id)?.name || code,
    }))
  }, [offerings, semesterCatalog])

  const visibleOfferings = offerings.filter((item) => item.semesterId === semesterId)
  const courseOptions = useMemo(() => {
    const map = new Map<string, string>()
    for (const item of visibleOfferings) {
      if (item.courseCode) map.set(item.courseCode, item.courseName || item.courseCode)
    }
    if (map.size === 0) return []
    return [
      { value: 'ALL', label: 'Tất cả môn' },
      ...[...map.entries()].map(([code, name]) => ({ value: code, label: `${code} · ${name}` })),
    ]
  }, [visibleOfferings])
  const searchTerm = appliedClassSearch.trim().toLocaleLowerCase('vi-VN')
  const visibleClasses = classes.filter((item) => {
    const matchesCourse = courseCode === 'ALL' || item.courseCode === courseCode
    const matchesSearch = !searchTerm || [item.name, item.code].some(
      (value) => value?.toLocaleLowerCase('vi-VN').includes(searchTerm),
    )
    return matchesCourse && matchesSearch
  })
  const classPageCount = Math.max(1, Math.ceil(visibleClasses.length / classPageSize))
  const currentClassPage = Math.min(classPage, classPageCount)
  const classStart = (currentClassPage - 1) * classPageSize
  const pageClasses = visibleClasses.slice(classStart, classStart + classPageSize)

  return (
    <div lang="vi" className={ds.page}>
      <StatusToast
        notice={notice}
        error={error}
        onDismiss={() => {
          setError('')
          setNotice('')
        }}
      />

      <section className={ds.section}>
        <div className={cn(ds.sectionHeader, 'flex flex-col gap-3')}>
            <div className={ds.sectionHeading}>
              <div className={ds.sectionTitleRow}>
                <span className={ds.sectionIconWrap}>
                  <UsersRound className={ds.sectionIcon} />
                </span>
                <h2 className={ds.sectionTitle}>Lớp học của tôi</h2>
                <span className={ds.sectionBadge}>{visibleClasses.length} lớp</span>
              </div>
              <p className={ds.sectionHint}>Quản lý lớp theo học kỳ và môn được phân công</p>
            </div>
            <div className="flex flex-wrap items-center gap-2 lg:flex-nowrap">
              <FilterDropMenu
                label="Học kỳ"
                value={semesterId}
                onChange={(value) => {
                  setSemesterId(value)
                  setClassPage(1)
                  setCourseCode('ALL')
                  void load(value)
                }}
                options={semesters}
                emptyText="Chưa có học kỳ"
                className="w-40 max-w-full"
                menuMinWidth={192}
              />
              <FilterDropMenu
                label="Môn"
                value={courseOptions.some((item) => item.value === courseCode) ? courseCode : 'ALL'}
                onChange={(value) => {
                  setCourseCode(value)
                  setClassPage(1)
                }}
                options={courseOptions}
                emptyText="Chưa có môn trong kỳ này"
                className="w-64 max-w-full"
                menuMinWidth={352}
              />
              <div className="relative w-full sm:w-fit">
                <Search className="pointer-events-none absolute inset-y-0 left-4 my-auto size-4 text-[#1C4D8D]" aria-hidden="true" />
                <input
                  type="search"
                  aria-label="Tìm theo tên lớp hoặc mã lớp"
                  placeholder="Tìm theo tên lớp hoặc mã lớp"
                  value={classSearch}
                  onChange={(event) => setClassSearch(event.target.value)}
                  onKeyDown={(event) => {
                    if (event.key === 'Enter' && !event.nativeEvent.isComposing) {
                      event.preventDefault()
                      setAppliedClassSearch(classSearch)
                      setClassPage(1)
                    }
                  }}
                  className={cn(ds.field, 'h-10 min-h-10 rounded-full pr-3 pl-10 shadow-[0_2px_8px_rgba(23,48,96,0.04)] transition sm:w-[264px]')}
                />
              </div>
              <button type="button" className={cn(ds.primaryButton, 'min-h-10 shrink-0')} onClick={() => setCreateOpen(true)}>
                <Plus className="size-3.5" />
                Tạo lớp
              </button>
            </div>
        </div>
        <div className={ds.sectionBody}>
          {loading ? (
            <div className="flex min-h-32 items-center justify-center gap-2 text-sm text-[#5b6b84]">
              <LoaderCircle className="size-5 animate-spin text-[#1C4D8D]" />
              Đang tải lớp...
            </div>
          ) : visibleClasses.length === 0 ? (
            <EmptyState
              icon={UsersRound}
              title={searchTerm ? 'Không tìm thấy lớp phù hợp' : 'Chưa có lớp trong bộ lọc này'}
              description={searchTerm ? 'Thử tên lớp hoặc mã lớp khác.' : 'Đổi học kỳ/môn hoặc tạo lớp mới cho môn được phân công.'}
              action={searchTerm ? undefined : (
                <button type="button" className={ds.primaryButton} onClick={() => setCreateOpen(true)}>
                  <Plus className="size-4" />
                  Tạo lớp
                </button>
              )}
            />
          ) : (
            <div className="space-y-4">
              <ul className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
                {pageClasses.map((item) => (
                  <li key={item.id}>
                    <LecturerClassCard item={item} onOpen={(classId) => navigate(`/lecturer/classes/${classId}`)} />
                  </li>
                ))}
              </ul>
              <CompactPagination
                label="Phân trang lớp học của tôi"
                page={currentClassPage}
                pageCount={classPageCount}
                onPageChange={setClassPage}
              />
            </div>
          )}
        </div>
      </section>

      {createOpen && (
        <CreateClassDialog
          offerings={visibleOfferings.length > 0 ? visibleOfferings : offerings}
          onClose={() => setCreateOpen(false)}
          onCreated={(created) => {
            setCreateOpen(false)
            setCreatedClass(created)
            setNotice(`Đã tạo lớp “${created.name}”.`)
            void load(semesterId)
          }}
        />
      )}

      {createdClass && (
        <CreatedClassCredentialsDialog
          created={createdClass}
          onClose={() => setCreatedClass(null)}
        />
      )}
    </div>
  )
}

function classShiftLine(item: ManagedClass) {
  if (item.activeShiftCount > 0) return `${item.activeShiftCount} ca đang diễn ra`
  if (item.shiftCount > 0) return 'Chưa có ca đang diễn ra'
  return 'Chưa có ca'
}

function LecturerClassCard({
  item,
  onOpen,
}: {
  item: ManagedClass
  onOpen: (classId: string) => void
}) {

  return (
    <article
      role="button"
      tabIndex={0}
      onClick={() => onOpen(item.id)}
      onKeyDown={(event) => {
        if (event.key === 'Enter' || event.key === ' ') {
          event.preventDefault()
          onOpen(item.id)
        }
      }}
      className={cn(
        'group relative flex h-full w-full cursor-pointer flex-col overflow-hidden rounded-[22px] border border-[#82aedb]/55 text-left transition duration-200',
        'bg-[linear-gradient(180deg,#EAF5FB_0%,#F8FBFD_22%,#FFFFFF_48%,#FFFFFF_100%)]',
        'hover:-translate-y-0.5 hover:border-[#4988C4] hover:bg-[linear-gradient(180deg,#DCEFFA_0%,#F4FBFD_24%,#F8FBFD_52%,#F8FBFD_100%)] hover:shadow-[0_14px_28px_rgba(15,40,84,0.16)] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]',
        ds.panelShadow,
      )}
    >
      <div
        className="pointer-events-none absolute inset-x-0 top-0 h-14 overflow-hidden rounded-t-[21px]"
        aria-hidden="true"
      >
        <div className="absolute inset-x-6 top-0 h-10 bg-[radial-gradient(ellipse_at_top,rgba(73,136,196,0.18)_0%,rgba(189,232,245,0.12)_42%,transparent_72%)]" />
        <div className={ds.softCardAccentLine} />
      </div>
      <div className="relative flex flex-1 flex-col p-4">
        <div className="flex items-start justify-between gap-2">
          <div className="flex min-w-0 flex-wrap items-center gap-1.5">
            <span
              className={cn(
                'inline-flex h-7 items-center rounded-lg px-2.5 text-xs font-black tracking-[0.05em]',
                courseCodeTone(item.courseCode),
              )}
            >
              {item.courseCode}
            </span>
            <span className="inline-flex h-7 items-center gap-1 rounded-full border border-[#4988C4]/45 bg-[#F4FBFD] px-2.5 text-xs font-bold text-[#1C4D8D]">
              <CalendarDays className="size-3 shrink-0" aria-hidden="true" />
              {item.semesterCode}
            </span>
          </div>
        </div>

        <p className="mt-3 text-[11px] font-bold tracking-[0.08em] text-[#5b6b84] uppercase">
          {item.code || 'Mã lớp'}
        </p>
        <h3 className="mt-1 line-clamp-2 text-[1.35rem] leading-snug font-bold tracking-[-0.03em] text-[#0F2854]">
          {item.name}
        </h3>
        <p className="mt-1 line-clamp-1 text-sm font-semibold text-[#3d5270]">{item.courseName}</p>

        <ClassCapacity enrolledCount={item.enrolledCount} maxStudents={item.maxStudents} />

        <p className={cn('mt-3 text-sm font-bold', item.activeShiftCount > 0 ? 'text-[#16836a]' : 'text-[#5b6b84]')}>
          {classShiftLine(item)}
        </p>

        <div className="mt-auto flex items-center justify-between gap-2 pt-0.5">
          <span className="text-xs font-semibold text-[#5b6b84]">Mở lớp</span>
          <span className="inline-flex h-9 items-center gap-1 rounded-xl border border-[#4988C4] bg-white px-3 text-xs font-bold text-[#1C4D8D] transition group-hover:border-[#1C4D8D] group-hover:bg-[#E7F3FB]">
            Chi tiết
            <ArrowRight className="size-3.5 transition group-hover:translate-x-0.5" aria-hidden="true" />
          </span>
        </div>
      </div>
    </article>
  )
}


type CreateClassFieldErrors = {
  offeringId?: string
  code?: string
  name?: string
  password?: string
  maxStudents?: string
}

function CreateClassDialog({
  offerings,
  onClose,
  onCreated,
}: {
  offerings: LecturerCourseOffering[]
  onClose: () => void
  onCreated: (created: CreateClassResponse) => void
}) {
  const [offeringId, setOfferingId] = useState(offerings[0]?.courseOfferingId ?? '')
  const [code, setCode] = useState('')
  const [name, setName] = useState('')
  const [password, setPassword] = useState('')
  const [maxStudents, setMaxStudents] = useState(35)
  const [busy, setBusy] = useState(false)
  const [submitError, setSubmitError] = useState('')
  const [fieldErrors, setFieldErrors] = useState<CreateClassFieldErrors>({})

  const clearFieldError = (key: keyof CreateClassFieldErrors) => {
    setSubmitError('')
    setFieldErrors((current) => {
      if (!current[key]) return current
      const next = { ...current }
      delete next[key]
      return next
    })
  }

  const validate = (): CreateClassFieldErrors => {
    const next: CreateClassFieldErrors = {}
    if (!offeringId) next.offeringId = 'Chọn môn được phân công.'
    if (code.trim().length < 2) next.code = 'Mã lớp cần ít nhất 2 ký tự.'
    if (name.trim().length < 2) next.name = 'Tên lớp cần ít nhất 2 ký tự.'
    if (password.trim().length < 8) next.password = 'Mật khẩu cần ít nhất 8 ký tự.'
    if (!Number.isFinite(maxStudents) || maxStudents < 1 || maxStudents > 200) {
      next.maxStudents = 'Sĩ số từ 1 đến 200.'
    }
    return next
  }

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    if (busy) return

    const nextErrors = validate()
    if (Object.keys(nextErrors).length > 0) {
      setFieldErrors(nextErrors)
      return
    }

    setBusy(true)
    setSubmitError('')
    setFieldErrors({})
    try {
      const created = await classService.createClass({
        courseOfferingId: offeringId,
        code: code.trim(),
        name: name.trim(),
        password: password.trim(),
        maxStudents,
      })
      onCreated(created)
    } catch (createError) {
      const message = getErrorMessage(createError)
      setSubmitError(message)
      toast.error(message)
      setBusy(false)
    }
  }

  return (
    <FormDialog
      titleId="create-class-title"
      eyebrow="Quản lý lớp học"
      title="Tạo lớp mới"
      icon={Plus}
      onClose={onClose}
      busy={busy}
      footer={(
        <DialogFooter className="bg-white sm:flex-row">
          <button type="button" onClick={onClose} disabled={busy} className={cn(ds.outlineButton, 'min-h-10')}>Hủy</button>
          <button type="submit" form="create-class-form" disabled={busy || offerings.length === 0} className={cn(ds.primaryButton, 'min-h-10')}>
            {busy ? <LoaderCircle className="size-4 animate-spin" /> : <Plus className="size-3.5" />}
            Tạo lớp
          </button>
        </DialogFooter>
      )}
    >
      <form id="create-class-form" onSubmit={(event) => void submit(event)} className="space-y-3" noValidate>
        {submitError ? (
          <div
            role="alert"
            className="rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-3 py-2.5 text-xs font-semibold text-[#c2415b]"
          >
            {submitError}
          </div>
        ) : null}
        <div className="block">
          <span className={fieldLabelClass(Boolean(fieldErrors.offeringId))}>Môn được phân công</span>
          <FilterDropMenu
            label="Chọn môn được phân công"
            value={offeringId}
            onChange={(value) => {
              setOfferingId(value)
              clearFieldError('offeringId')
            }}
            options={offerings.map((item) => ({
              value: item.courseOfferingId,
              label: `${item.courseCode} · ${item.courseName}`,
            }))}
            emptyText="Chưa có môn được phân công"
            invalid={Boolean(fieldErrors.offeringId)}
            className="mt-1.5 w-full"
          />
          <FieldError message={fieldErrors.offeringId} />
        </div>
        <div className="grid gap-3 sm:grid-cols-2">
          <label className="block">
            <span className={fieldLabelClass(Boolean(fieldErrors.code))}>Mã lớp</span>
            <input
              value={code}
              onChange={(event) => {
                setCode(event.target.value)
                clearFieldError('code')
              }}
              maxLength={50}
              aria-invalid={Boolean(fieldErrors.code) || undefined}
              className={cn(ds.field, 'mt-1.5', fieldErrors.code && fieldInvalidClass)}
              placeholder="SE1802-CSD"
            />
            <FieldError message={fieldErrors.code} />
          </label>
          <label className="block">
            <span className={fieldLabelClass(Boolean(fieldErrors.name))}>Tên lớp</span>
            <input
              value={name}
              onChange={(event) => {
                setName(event.target.value)
                clearFieldError('name')
              }}
              maxLength={150}
              aria-invalid={Boolean(fieldErrors.name) || undefined}
              className={cn(ds.field, 'mt-1.5', fieldErrors.name && fieldInvalidClass)}
              placeholder="Lớp SE1802-CSD"
            />
            <FieldError message={fieldErrors.name} />
          </label>
        </div>
        <div className="grid gap-3 sm:grid-cols-[1fr_8.5rem]">
          <label className="block">
            <span className={fieldLabelClass(Boolean(fieldErrors.password))}>Mật khẩu tham gia</span>
            <input
              value={password}
              onChange={(event) => {
                setPassword(event.target.value)
                clearFieldError('password')
              }}
              aria-invalid={Boolean(fieldErrors.password) || undefined}
              className={cn(ds.field, 'mt-1.5', fieldErrors.password && fieldInvalidClass)}
              placeholder="Ít nhất 8 ký tự"
            />
            <FieldError message={fieldErrors.password} />
          </label>
          <label className="block">
            <span className={fieldLabelClass(Boolean(fieldErrors.maxStudents))}>Sĩ số tối đa</span>
            <input
              type="number"
              min={1}
              max={200}
              value={maxStudents}
              onChange={(event) => {
                setMaxStudents(Number(event.target.value) || 0)
                clearFieldError('maxStudents')
              }}
              aria-invalid={Boolean(fieldErrors.maxStudents) || undefined}
              className={cn(ds.field, 'mt-1.5', fieldErrors.maxStudents && fieldInvalidClass)}
            />
            <FieldError message={fieldErrors.maxStudents} />
          </label>
        </div>
      </form>
    </FormDialog>
  )
}

function CreatedClassCredentialsDialog({
  created,
  onClose,
}: {
  created: CreateClassResponse
  onClose: () => void
}) {
  const [copiedField, setCopiedField] = useState<'joinCode' | 'joinPassword' | null>(null)

  const handleCopy = async (field: 'joinCode' | 'joinPassword', value: string | null) => {
    if (!value) return
    const copied = await copyText(value)
    if (copied) {
      setCopiedField(field)
      toast.success(field === 'joinCode' ? 'Đã sao chép mã tham gia.' : 'Đã sao chép mật khẩu tham gia.')
      return
    }
    toast.warning('Không thể tự động sao chép. Vui lòng bôi đen và sao chép thủ công.')
  }

  return (
    <FormDialog
      titleId="created-class-credentials-title"
      eyebrow="Thông tin tham gia lớp học"
      title={created.name || created.code}
      icon={KeyRound}
      onClose={onClose}
      footer={(
        <DialogFooter className="bg-white sm:flex-row">
          <button type="button" onClick={onClose} className={cn(ds.primaryButton, 'min-h-10')}>
            Đã lưu thông tin
          </button>
        </DialogFooter>
      )}
    >
      <div className="space-y-4">
        <div className="rounded-xl border border-[#ecd9ab] bg-[#fff9ec] px-3.5 py-2.5 text-xs font-semibold text-[#8a5e17]">
          Mật khẩu tham gia chỉ hiển thị một lần sau khi tạo lớp. Hãy sao chép và lưu lại trước khi đóng hộp thoại này.
        </div>

        <div className="grid gap-3 rounded-2xl border border-[#82aedb]/55 bg-[#F8FBFD] p-4">
          <div className="flex items-center justify-between gap-3">
            <div>
              <p className="text-[11px] font-bold tracking-[0.06em] text-[#5b6b84] uppercase">Mã lớp / Tên lớp</p>
              <p className="mt-0.5 text-sm font-bold text-[#0F2854]">
                {created.code} · {created.name}
              </p>
            </div>
          </div>

          <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-[#82aedb]/45 bg-white px-3.5 py-2.5">
            <div className="min-w-0">
              <p className="text-[11px] font-bold tracking-[0.06em] text-[#5b6b84] uppercase">Mã tham gia (Join Code)</p>
              <p className="mt-0.5 font-mono text-base font-black tracking-[0.08em] text-[#0F2854]">
                {created.joinCode || 'Chưa cấp'}
              </p>
            </div>
            {created.joinCode ? (
              <button
                type="button"
                onClick={() => void handleCopy('joinCode', created.joinCode)}
                className={cn(ds.outlineButton, 'min-h-9 px-3 text-xs')}
              >
                {copiedField === 'joinCode' ? <Check className="size-3.5 text-[#16836a]" /> : <Copy className="size-3.5" />}
                {copiedField === 'joinCode' ? 'Đã sao chép' : 'Sao chép mã'}
              </button>
            ) : null}
          </div>

          <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-[#82aedb]/45 bg-white px-3.5 py-2.5">
            <div className="min-w-0">
              <p className="text-[11px] font-bold tracking-[0.06em] text-[#5b6b84] uppercase">Mật khẩu tham gia</p>
              <p className="mt-0.5 font-mono text-base font-black tracking-[0.06em] text-[#0F2854]">
                {created.joinPassword || 'Đã lưu mật khẩu vừa nhập'}
              </p>
            </div>
            {created.joinPassword ? (
              <button
                type="button"
                onClick={() => void handleCopy('joinPassword', created.joinPassword)}
                className={cn(ds.outlineButton, 'min-h-9 px-3 text-xs')}
              >
                {copiedField === 'joinPassword' ? <Check className="size-3.5 text-[#16836a]" /> : <Copy className="size-3.5" />}
                {copiedField === 'joinPassword' ? 'Đã sao chép' : 'Sao chép mật khẩu'}
              </button>
            ) : null}
          </div>
        </div>
      </div>
    </FormDialog>
  )
}
