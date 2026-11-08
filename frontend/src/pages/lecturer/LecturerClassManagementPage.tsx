import { useEffect, useMemo, useState, type FormEvent, type MouseEvent } from 'react'
import { ArrowRight, CalendarDays, Eye, EyeOff, GraduationCap, KeyRound, LoaderCircle, Lock, Plus, Search, Unlock, Users, UsersRound } from 'lucide-react'
import { EmptyState } from '@/components/common/EmptyState'
import { FieldError, fieldInvalidClass, fieldLabelClass } from '@/components/common/formField'
import { StatusToast } from '@/components/common/toast'
import { toast } from '@/components/common/toastStore'
import { FilterDropMenu } from '@/components/common/FilterDropMenu'
import { DialogFooter, FormDialog } from '@/design/overlays'
import { BannerFact, LecturerPageBanner } from '@/design/ui'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { classService } from '@/services/class.service'
import type { ClassDetail, LecturerCourseOffering, ManagedClass, SemesterOption } from '@/types/class.types'

function getErrorMessage(error: unknown) {
  if (typeof error === 'object' && error !== null && 'detail' in error) {
    const detail = (error as { detail?: unknown }).detail
    if (typeof detail === 'string' && detail.trim()) return detail
  }
  if (error instanceof Error && error.message) return error.message
  return 'Không thể tải dữ liệu lớp học.'
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
  const [detail, setDetail] = useState<ClassDetail | null>(null)
  const [detailLoading, setDetailLoading] = useState(false)

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
    const timer = window.setTimeout(() => void load(''), 0)
    return () => window.clearTimeout(timer)
    // eslint-disable-next-line react-hooks/exhaustive-deps
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

  const openDetail = async (classId: string) => {
    setDetail(null)
    setDetailLoading(true)
    try {
      setDetail(await classService.getClassDetail(classId))
    } catch (loadError) {
      toast.error(getErrorMessage(loadError))
      setDetail(null)
    } finally {
      setDetailLoading(false)
    }
  }

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
                  <LecturerClassCard item={item} onOpen={(classId) => void openDetail(classId)} />
                </li>
              ))}
            </ul>
          )}
        </div>
      </section>

      {(detailLoading || detail) && (
        <ClassDetailDialog
          detail={detail}
          loading={detailLoading}
          onClose={() => {
            setDetail(null)
            setDetailLoading(false)
          }}
        />
      )}

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

function formatWhen(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  return date.toLocaleString('vi-VN', { hour12: false })
}

function joinMethodLabel(value: string) {
  const key = value.toLowerCase()
  if (key === 'assigned') return 'Được thêm'
  if (key === 'self' || key === 'self_enroll') return 'Tự tham gia'
  if (key === 'password') return 'Mật khẩu'
  return value || 'Tham gia'
}

function shiftStatusLabel(value: string) {
  const labels: Record<string, string> = {
    SCHEDULED: 'Chờ kích hoạt',
    ACTIVE: 'Đang diễn ra',
    IN_PROGRESS: 'Đang diễn ra',
    FINALIZED: 'Đã chốt điểm',
    CLOSED: 'Đã kết thúc',
    ENDED: 'Đã kết thúc',
  }
  return labels[value] || value || 'Chưa rõ'
}

function nameInitial(name: string) {
  const part = name.trim().split(/\s+/).filter(Boolean).at(-1) || '?'
  return part.slice(0, 1).toUpperCase()
}

function joinSecretLabel(password: string | null | undefined, hasPassword: boolean) {
  if (password?.trim()) return password.trim()
  if (hasPassword) return null
  return ''
}

function JoinPasswordReveal({
  password,
  hasPassword,
}: {
  password: string | null | undefined
  hasPassword: boolean
}) {
  const [visible, setVisible] = useState(false)
  const secret = joinSecretLabel(password, hasPassword)
  const canReveal = Boolean(secret)

  const toggle = (event: MouseEvent) => {
    event.preventDefault()
    event.stopPropagation()
    if (!canReveal) return
    setVisible((current) => !current)
  }

  return (
    <div className="flex min-w-0 items-center gap-1.5">
      <p className="min-w-0 flex-1 truncate font-mono text-sm font-black tracking-[0.06em] text-[#0F2854]">
        {!hasPassword && !secret
          ? 'Chưa đặt'
          : visible && secret
            ? secret
            : '••••••••'}
      </p>
      {canReveal && (
        <button
          type="button"
          aria-label={visible ? 'Ẩn mật khẩu tham gia' : 'Hiện mật khẩu tham gia'}
          aria-pressed={visible}
          onClick={toggle}
          className="flex size-8 shrink-0 items-center justify-center rounded-xl border border-[#82aedb]/55 bg-white text-[#1C4D8D] shadow-[0_1px_0_#fff_inset] transition hover:border-[#1C4D8D] hover:bg-[#E7F3FB]"
        >
          {visible ? <EyeOff className="size-3.5" /> : <Eye className="size-3.5" />}
        </button>
      )}
    </div>
  )
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
          <span
            className={cn(
              'inline-flex h-7 shrink-0 items-center gap-1 rounded-full border px-2.5 text-[11px] font-bold',
              item.isJoinEnabled
                ? 'border-[#b7e4cf] bg-[#e8f8f1] text-[#16836a]'
                : 'border-[#d5dde8] bg-[#F8FBFD] text-[#5b6b84]',
            )}
          >
            {item.isJoinEnabled ? (
              <Unlock className="size-3 shrink-0" aria-hidden="true" />
            ) : (
              <Lock className="size-3 shrink-0" aria-hidden="true" />
            )}
            {item.isJoinEnabled ? 'Đang mở' : 'Đã khóa'}
          </span>
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

        <div className="mt-3 rounded-2xl border border-[#82aedb]/45 bg-[#F8FBFD] px-3 py-2.5">
          <div className="flex items-center justify-between gap-2">
            <p className="inline-flex items-center gap-1.5 text-[11px] font-bold tracking-[0.05em] text-[#5b6b84] uppercase">
              <KeyRound className="size-3.5 text-[#1C4D8D]" aria-hidden="true" />
              Mật khẩu tham gia
            </p>
          </div>
          <div className="mt-1.5">
            <JoinPasswordReveal password={item.joinPassword} hasPassword={item.hasPassword} />
          </div>
        </div>

        <div className="mt-auto flex items-center justify-between gap-2 pt-3.5">
          <span className="text-xs font-semibold text-[#5b6b84]">Nhấn để xem sinh viên & ca thi</span>
          <span className="inline-flex h-9 items-center gap-1 rounded-xl border border-[#4988C4] bg-white px-3 text-xs font-bold text-[#1C4D8D] transition group-hover:border-[#1C4D8D] group-hover:bg-[#E7F3FB]">
            Chi tiết
            <ArrowRight className="size-3.5 transition group-hover:translate-x-0.5" aria-hidden="true" />
          </span>
        </div>
      </div>
    </article>
  )
}

function ClassDetailDialog({
  detail,
  loading,
  onClose,
}: {
  detail: ClassDetail | null
  loading: boolean
  onClose: () => void
}) {
  const [keyword, setKeyword] = useState('')
  const students = (detail?.students ?? []).filter((student) => {
    const text = `${student.fullName} ${student.studentCode} ${student.email}`.toLowerCase()
    return text.includes(keyword.trim().toLowerCase())
  })

  return (
    <FormDialog
      titleId="class-detail-title"
      eyebrow={detail ? `${detail.courseCode} · ${detail.semesterCode}` : 'Lớp học phần'}
      title={detail?.name || 'Đang tải lớp'}
      subtitle={detail?.courseName}
      icon={GraduationCap}
      onClose={onClose}
      size="lg"
    >
      {loading && <p className="flex items-center gap-2 text-sm font-semibold text-[#5b6b84]"><LoaderCircle className="size-4 animate-spin text-[#1C4D8D]" /> Đang tải thông tin lớp...</p>}
      {detail && (
        <div className="space-y-3">
          <div className="grid grid-cols-1 gap-2 sm:grid-cols-3">
            <div className={cn('min-w-0 rounded-2xl border border-[#82aedb]/55 bg-white px-3.5 py-3', ds.panelShadow)}>
              <p className="inline-flex items-center gap-1.5 text-[11px] font-bold tracking-[0.04em] text-[#5b6b84] uppercase">
                <span className={ds.sectionIconWrap}>
                  <KeyRound className="size-3.5" aria-hidden="true" />
                </span>
                Mật khẩu tham gia
              </p>
              <div className="mt-2.5">
                <JoinPasswordReveal password={detail.joinPassword} hasPassword={detail.hasPassword} />
              </div>
            </div>

            <div className={cn('min-w-0 rounded-2xl border border-[#82aedb]/55 bg-white px-3.5 py-3', ds.panelShadow)}>
              <p className="inline-flex items-center gap-1.5 text-[11px] font-bold tracking-[0.04em] text-[#5b6b84] uppercase">
                <span className={ds.sectionIconWrap}>
                  <Users className="size-3.5" aria-hidden="true" />
                </span>
                Sĩ số
              </p>
              <div className="mt-2.5">
                <p className="text-sm font-black text-[#0F2854]">
                  {detail.enrolledCount}
                  <span className="font-semibold text-[#5b6b84]">/{detail.maxStudents}</span>
                </p>
                <div className="mt-2 h-1.5 overflow-hidden rounded-full bg-[#E7F3FB] ring-1 ring-[#82aedb]/30">
                  <div
                    className="h-full rounded-full bg-[linear-gradient(90deg,#4988C4,#1C4D8D)]"
                    style={{
                      width: `${Math.max(
                        detail.maxStudents > 0
                          ? Math.min(100, (detail.enrolledCount / detail.maxStudents) * 100)
                          : 0,
                        detail.enrolledCount > 0 ? 8 : 0,
                      )}%`,
                    }}
                  />
                </div>
              </div>
            </div>

            <div className={cn('min-w-0 rounded-2xl border border-[#82aedb]/55 bg-white px-3.5 py-3', ds.panelShadow)}>
              <p className="inline-flex items-center gap-1.5 text-[11px] font-bold tracking-[0.04em] text-[#5b6b84] uppercase">
                <span className={ds.sectionIconWrap}>
                  {detail.isJoinEnabled ? (
                    <Unlock className="size-3.5" aria-hidden="true" />
                  ) : (
                    <Lock className="size-3.5" aria-hidden="true" />
                  )}
                </span>
                Tham gia
              </p>
              <div className="mt-2.5">
                <span
                  className={cn(
                    'inline-flex h-8 items-center gap-1.5 rounded-full border px-3 text-xs font-bold',
                    detail.isJoinEnabled
                      ? 'border-[#b7e4cf] bg-[#e8f8f1] text-[#16836a]'
                      : 'border-[#d5dde8] bg-[#F8FBFD] text-[#5b6b84]',
                  )}
                >
                  {detail.isJoinEnabled ? (
                    <Unlock className="size-3.5 shrink-0" aria-hidden="true" />
                  ) : (
                    <Lock className="size-3.5 shrink-0" aria-hidden="true" />
                  )}
                  {detail.isJoinEnabled ? 'Đang mở' : 'Đã khóa'}
                </span>
              </div>
            </div>
          </div>

          <section className={cn('rounded-2xl border border-[#82aedb]/55 bg-white p-3', ds.panelShadow)}>
            <div className="flex items-center justify-between gap-2">
              <h3 className="inline-flex items-center gap-2 text-sm font-bold text-[#0F2854]">
                <span className={ds.sectionIconWrap}><Users className="size-3.5" aria-hidden="true" /></span>
                Sinh viên · {detail.students.length}
              </h3>
              <label className="relative">
                <Search className="pointer-events-none absolute top-1/2 left-2.5 size-3.5 -translate-y-1/2 text-[#5b6b84]" />
                <input value={keyword} onChange={(event) => setKeyword(event.target.value)} placeholder="Tìm tên, mã, email" className="h-8 w-40 rounded-full border border-[#4988C4] bg-[#F8FBFD] pr-2.5 pl-7 text-xs font-semibold text-[#0F2854] outline-none focus:ring-2 focus:ring-[#4988C4]/30 sm:w-48" />
              </label>
            </div>
            {students.length === 0 ? (
              <EmptyState compact icon={Users} title="Chưa có sinh viên khớp bộ lọc" className="mt-2" />
            ) : (
              <ul className="mt-2 space-y-1.5">
                {students.map((student) => (
                  <li key={student.studentId} className="flex items-center gap-2.5 rounded-xl bg-[#F8FBFD] px-2.5 py-1.5">
                    <span className="flex size-8 shrink-0 items-center justify-center rounded-full bg-[#1C4D8D] text-xs font-black text-white">{nameInitial(student.fullName)}</span>
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm font-bold text-[#0F2854]">{student.fullName}</p>
                      <p className="truncate text-xs text-[#5b6b84]">{student.studentCode || 'Chưa có mã'} · {student.email}</p>
                    </div>
                    <span className={ds.infoChip}>{joinMethodLabel(student.joinMethod)}</span>
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section className={cn('rounded-2xl border border-[#82aedb]/55 bg-white p-3', ds.panelShadow)}>
            <h3 className="inline-flex items-center gap-2 text-sm font-bold text-[#0F2854]">
              <span className={ds.sectionIconWrap}><CalendarDays className="size-3.5" aria-hidden="true" /></span>
              Ca kiểm tra · {detail.shifts.length}
            </h3>
            {detail.shifts.length === 0 ? (
              <EmptyState compact icon={CalendarDays} title="Lớp chưa có ca kiểm tra" className="mt-2" />
            ) : (
              <ul className="mt-2 space-y-1.5">
                {detail.shifts.map((shift) => (
                  <li key={shift.shiftId} className="rounded-xl bg-[#F8FBFD] px-2.5 py-2">
                    <div className="flex items-center justify-between gap-2">
                      <p className="truncate text-sm font-bold text-[#0F2854]">{shift.shiftName}</p>
                      <span className={shift.status === 'ACTIVE' || shift.status === 'IN_PROGRESS' ? ds.successChip : ds.infoChip}>
                        {shiftStatusLabel(shift.status)}{shift.isLocked ? ' · Đã khóa' : ''}
                      </span>
                    </div>
                    <p className="mt-0.5 text-xs font-semibold text-[#5b6b84]">{formatWhen(shift.startTime)} – {formatWhen(shift.endTime)} · {shift.participantsCount} lượt vào · {shift.submissionsCount} bài nộp</p>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </div>
      )}
    </FormDialog>
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
