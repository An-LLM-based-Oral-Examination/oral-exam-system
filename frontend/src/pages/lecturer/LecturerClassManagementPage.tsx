import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { ArrowRight, CalendarDays, GraduationCap, KeyRound, LoaderCircle, Plus, Users, UsersRound } from 'lucide-react'
import { EmptyState } from '@/components/common/EmptyState'
import { FieldError } from '@/components/common/formField'
import { fieldInvalidClass, fieldLabelClass } from '@/components/common/formFieldClasses'
import { StatusToast } from '@/components/common/toast'
import { toast } from '@/components/common/toastStore'
import { FilterDropMenu } from '@/components/common/FilterDropMenu'
import { DialogFooter, FormDialog } from '@/design/overlays'
import { BannerFact, LecturerPageBanner } from '@/design/ui'
import { ds } from '@/design/system'
import { scheduleUi } from '@/lib/scheduleUi'
import { cn } from '@/lib/utils'
import { userMessage } from '@/lib/userMessage'
import { classService } from '@/services/class.service'
import type { LecturerCourseOffering, ManagedClass, SemesterOption } from '@/types/class.types'

function getErrorMessage(error: unknown) {
  return userMessage(error, 'Không tải được dữ liệu lớp học. Vui lòng thử lại.')
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
  const [courseCode, setCourseCode] = useState('ALL')
  const navigate = useNavigate()

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
  const visibleClasses = classes.filter((item) => courseCode === 'ALL' || item.courseCode === courseCode)
  const semesterLabel = semesterCatalog.find((item) => item.id === semesterId)?.name || classes[0]?.semesterCode || 'hiện tại'

  return (
    <div lang="vi" className={ds.page}>
      <LecturerPageBanner
        icon={GraduationCap}
        eyebrow={`Học kỳ ${semesterLabel}`}
        title="Lớp của tôi"
        description="Tạo lớp cho môn đã được phân công. Sinh viên tham gia bằng mật khẩu lớp."
        extras={
          <>
            <BannerFact icon={KeyRound}>Mật khẩu lớp</BannerFact>
            <BannerFact icon={Users} tone="green">
              Sinh viên tự tham gia
            </BannerFact>
            <BannerFact icon={UsersRound} tone="violet">
              Theo dõi sĩ số
            </BannerFact>
          </>
        }
      />

      <StatusToast
        notice={notice}
        error={error}
        onDismiss={() => {
          setError('')
          setNotice('')
        }}
      />

      <section className={ds.section}>
        <div className={cn(ds.sectionHeader, 'flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between')}>
            <div className={ds.sectionHeading}>
              <div className={ds.sectionTitleRow}>
                <span className={ds.sectionIconWrap}>
                  <UsersRound className={ds.sectionIcon} />
                </span>
                <h2 className={ds.sectionTitle}>Các lớp của tôi</h2>
                <span className={ds.sectionBadge}>{visibleClasses.length} lớp</span>
              </div>
              <p className={ds.sectionHint}>Quản lý lớp theo học kỳ và môn được phân công</p>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <FilterDropMenu
                label="Học kỳ"
                value={semesterId}
                onChange={(value) => {
                  setSemesterId(value)
                  setCourseCode('ALL')
                  void load(value)
                }}
                options={semesters}
                emptyText="Chưa có học kỳ"
                className="w-44"
              />
              <FilterDropMenu
                label="Môn"
                value={courseOptions.some((item) => item.value === courseCode) ? courseCode : 'ALL'}
                onChange={setCourseCode}
                options={courseOptions}
                emptyText="Chưa có môn trong kỳ này"
                className="w-64"
              />
              <button type="button" className={cn(ds.primaryButton, 'min-h-10')} onClick={() => setCreateOpen(true)}>
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
              title="Chưa có lớp trong bộ lọc này"
              description="Đổi học kỳ/môn hoặc tạo lớp mới cho môn được phân công."
              action={
                <button type="button" className={ds.primaryButton} onClick={() => setCreateOpen(true)}>
                  <Plus className="size-4" />
                  Tạo lớp
                </button>
              }
            />
          ) : (
            <ul className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
              {visibleClasses.map((item) => (
                <li key={item.id}>
                  <LecturerClassCard item={item} onOpen={(classId) => navigate(`/lecturer/classes/${classId}`)} />
                </li>
              ))}
            </ul>
          )}
        </div>
      </section>

      {createOpen && (
        <CreateClassDialog
          offerings={visibleOfferings.length > 0 ? visibleOfferings : offerings}
          onClose={() => setCreateOpen(false)}
          onCreated={(name) => {
            setCreateOpen(false)
            setNotice(`Đã tạo lớp “${name}”.`)
            void load(semesterId)
          }}
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
  const seatsLeft = Math.max(0, item.maxStudents - item.enrolledCount)
  const fillRatio = item.maxStudents > 0 ? Math.min(1, item.enrolledCount / item.maxStudents) : 0
  const nearlyFull = fillRatio >= 0.85

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

        <div className="mt-4 space-y-1.5">
          <div className="flex items-center justify-between gap-2 text-xs font-bold">
            <span className="inline-flex items-center gap-1 text-[#5b6b84]">
              <Users className="size-3.5 text-[#1C4D8D]" aria-hidden="true" />
              Sĩ số
            </span>
            <span className={cn(nearlyFull ? 'text-[#c26a12]' : 'text-[#0F2854]')}>
              {item.enrolledCount}
              <span className="font-semibold text-[#5b6b84]">/{item.maxStudents}</span>
              <span className="ml-1.5 font-semibold text-[#5b6b84]">· còn {seatsLeft}</span>
            </span>
          </div>
          <div className="h-2 overflow-hidden rounded-full bg-[#E7F3FB] ring-1 ring-[#82aedb]/30">
            <div
              className={cn(
                'h-full rounded-full transition-[width] duration-300',
                nearlyFull
                  ? 'bg-[linear-gradient(90deg,#e3a23a,#c26a12)]'
                  : 'bg-[linear-gradient(90deg,#4988C4,#1C4D8D)]',
              )}
              style={{ width: `${Math.max(fillRatio * 100, item.enrolledCount > 0 ? 8 : 0)}%` }}
            />
          </div>
        </div>

        <p className={cn('mt-3 text-sm font-bold', item.activeShiftCount > 0 ? 'text-[#16836a]' : 'text-[#5b6b84]')}>
          {classShiftLine(item)}
        </p>

        <div className="mt-auto flex items-center justify-between gap-2 pt-3.5">
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
  onCreated: (name: string) => void
}) {
  const [offeringId, setOfferingId] = useState(offerings[0]?.courseOfferingId ?? '')
  const [code, setCode] = useState('')
  const [name, setName] = useState('')
  const [password, setPassword] = useState('')
  const [maxStudents, setMaxStudents] = useState(35)
  const [busy, setBusy] = useState(false)
  const [fieldErrors, setFieldErrors] = useState<CreateClassFieldErrors>({})

  const clearFieldError = (key: keyof CreateClassFieldErrors) => {
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
    setFieldErrors({})
    try {
      await classService.createClass({
        courseOfferingId: offeringId,
        code: code.trim(),
        name: name.trim(),
        password: password.trim(),
        maxStudents,
      })
      onCreated(name.trim())
    } catch (createError) {
      toast.error(getErrorMessage(createError))
      setBusy(false)
    }
  }

  return (
    <FormDialog
      titleId="create-class-title"
      eyebrow="Lớp học phần"
      title="Tạo lớp mới"
      subtitle="Sinh viên tham gia bằng mật khẩu lớp."
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
