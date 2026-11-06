import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import {
  AlertCircle,
  Check,
  CheckCircle2,
  ChevronRight,
  ClipboardList,
  Clock3,
  LoaderCircle,
  Lock,
  Plus,
  Presentation,
  RefreshCw,
  Search,
  UsersRound,
  X,
} from 'lucide-react'
import { FilterDropMenu } from '@/components/common/FilterDropMenu'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { inClassShiftService } from '@/services/inClassShift.service'
import { InClassShiftProjectorDialog } from '@/pages/lecturer/InClassShiftProjectorDialog'
import type {
  CreateInClassShiftRequest,
  InClassShift,
  InClassShiftStatus,
  LecturerClassOption,
} from '@/types/inClassShift.types'

type StatusFilter = 'ALL' | InClassShiftStatus

const STATUS_FILTERS: Array<{
  value: StatusFilter
  label: string
  countIdleClass: string
  dotClass: string
}> = [
  {
    value: 'ALL',
    label: 'Tất cả',
    countIdleClass: 'bg-[#e8eef6] text-[#1C4D8D]',
    dotClass: 'bg-[#1C4D8D]',
  },
  {
    value: 'ACTIVE',
    label: 'Đang mở',
    countIdleClass: 'bg-[#e8f7f1] text-[#197158]',
    dotClass: 'bg-[#1a9a72]',
  },
  {
    value: 'CLOSED',
    label: 'Đã kết thúc',
    countIdleClass: 'bg-[#fff4df] text-[#8d6118]',
    dotClass: 'bg-[#d4a017]',
  },
  {
    value: 'FINALIZED',
    label: 'Đã chốt',
    countIdleClass: 'bg-[#eef1f5] text-[#52647d]',
    dotClass: 'bg-[#7a8aa0]',
  },
]

function getErrorMessage(error: unknown) {
  if (typeof error === 'object' && error !== null && 'detail' in error) {
    const detail = (error as { detail?: unknown }).detail
    if (typeof detail === 'string' && detail.trim()) return detail
  }
  if (error instanceof Error && error.message) return error.message
  return 'Không thể tải dữ liệu ca thi trên lớp.'
}

function statusChip(status: InClassShiftStatus) {
  if (status === 'ACTIVE') {
    return (
      <span className={ds.successChip}>
        <span className="size-1.5 rounded-full bg-[#16836a]" />
        Đang mở
      </span>
    )
  }
  if (status === 'FINALIZED') {
    return (
      <span className={cn(ds.infoChip, 'border-[#c5d0e0] bg-[#f3f6fa] text-[#5b6b84]')}>
        <Lock className="size-3" />
        Đã chốt
      </span>
    )
  }
  return <span className={ds.infoChip}>Đã kết thúc</span>
}

function CreateInClassShiftDialog({
  open,
  classes,
  onClose,
  onCreated,
}: {
  open: boolean
  classes: LecturerClassOption[]
  onClose: () => void
  onCreated: (shift: InClassShift) => void
}) {
  const [step, setStep] = useState<1 | 2>(1)
  const [classId, setClassId] = useState('')
  const [title, setTitle] = useState('')
  const [durationMinutes, setDurationMinutes] = useState(20)
  const [questionCount, setQuestionCount] = useState(3)
  const [hasFollowUp, setHasFollowUp] = useState(true)
  const [showAdvanced, setShowAdvanced] = useState(false)
  const [passwordRotationSeconds, setPasswordRotationSeconds] = useState(300)
  const [gracePeriodSeconds, setGracePeriodSeconds] = useState(60)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    if (!open) return
    setStep(1)
    setClassId(classes[0]?.id ?? '')
    setTitle('')
    setDurationMinutes(20)
    setQuestionCount(3)
    setHasFollowUp(true)
    setShowAdvanced(false)
    setPasswordRotationSeconds(300)
    setGracePeriodSeconds(60)
    setError('')
    setBusy(false)
  }, [open, classes])

  useEffect(() => {
    if (!open) return
    const previous = document.body.style.overflow
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && !busy) onClose()
    }
    document.body.style.overflow = 'hidden'
    window.addEventListener('keydown', onKey)
    return () => {
      document.body.style.overflow = previous
      window.removeEventListener('keydown', onKey)
    }
  }, [open, onClose, busy])

  if (!open) return null

  const selectedClass = classes.find((item) => item.id === classId) ?? null
  const classOptions = classes.map((item) => ({
    value: item.id,
    label: `${item.name} · ${item.courseCode}`,
  }))

  const canStep1 = Boolean(classId) && title.trim().length >= 4
  const canSubmit =
    canStep1 &&
    durationMinutes >= 5 &&
    durationMinutes <= 120 &&
    questionCount >= 1 &&
    questionCount <= 5

  const goNext = () => {
    if (!canStep1 || busy) return
    setError('')
    setStep(2)
  }

  const goBack = () => {
    if (busy) return
    setError('')
    setStep(1)
  }

  const createShift = async () => {
    if (step !== 2 || !canSubmit || busy) return
    setBusy(true)
    setError('')
    const payload: CreateInClassShiftRequest = {
      classId,
      title: title.trim(),
      durationMinutes,
      questionCount,
      hasFollowUp,
      passwordRotationSeconds,
      gracePeriodSeconds,
    }
    try {
      const shift = await inClassShiftService.createShift(payload)
      onCreated(shift)
    } catch (createError) {
      setError(getErrorMessage(createError))
      setBusy(false)
    }
  }

  const onFormSubmit = (event: FormEvent) => {
    event.preventDefault()
    // Enter trên bước 1 chỉ chuyển bước — không tạo ca / không mở OTP.
    if (step === 1) {
      goNext()
      return
    }
    void createShift()
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-end justify-center bg-[#0F2854]/50 p-0 backdrop-blur-[3px] sm:items-center sm:p-5"
      role="presentation"
      onClick={(event) => {
        if (event.target === event.currentTarget && !busy) onClose()
      }}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="create-inclass-title"
        className="flex max-h-[min(94dvh,680px)] w-full max-w-xl flex-col overflow-hidden rounded-t-3xl border border-[#4988C4]/45 bg-white shadow-[0_28px_80px_rgba(15,40,84,0.28)] sm:rounded-3xl"
      >
        <header className="shrink-0 border-b border-[#4988C4]/40 bg-[linear-gradient(180deg,#D7F1F8_0%,#F4FBFD_100%)] px-5 py-4">
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <p className="text-[11px] font-bold tracking-[0.08em] text-[#1C4D8D] uppercase">
                MF-05 · Điểm thành phần
              </p>
              <h2
                id="create-inclass-title"
                className="mt-1 text-xl font-bold tracking-[-0.02em] text-[#0F2854]"
              >
                Tạo ca thi trên lớp
              </h2>
              <p className="mt-1 text-sm text-[#5b6b84]">
                {step === 1 ? 'Chọn lớp và đặt tên ca' : 'Thiết lập cấu trúc đề của ca'}
              </p>
            </div>
            <button
              type="button"
              aria-label="Đóng"
              disabled={busy}
              onClick={onClose}
              className="flex size-9 shrink-0 items-center justify-center rounded-xl border border-[#4988C4]/45 bg-white text-[#5b6b84] transition hover:text-[#0F2854] disabled:opacity-45"
            >
              <X className="size-4" />
            </button>
          </div>

          <nav className="mt-4 px-2" aria-label="Tiến độ tạo ca">
            <ol className="relative flex w-full items-start justify-between">
              <span
                aria-hidden="true"
                className="absolute top-3.5 right-[22%] left-[22%] h-0.5 rounded-full bg-[#c5d6ea]"
              />
              <span
                aria-hidden="true"
                className={cn(
                  'absolute top-3.5 left-[22%] h-0.5 rounded-full bg-[#1C4D8D] transition-all duration-300',
                  step >= 2 ? 'w-[56%]' : 'w-0',
                )}
              />
              {(
                [
                  { id: 1 as const, title: 'Lớp & tên ca' },
                  { id: 2 as const, title: 'Cấu trúc đề' },
                ] as const
              ).map((item) => {
                const visual =
                  step > item.id ? 'done' : step === item.id ? 'current' : 'upcoming'
                return (
                  <li
                    key={item.id}
                    className="relative z-[1] flex w-[38%] flex-col items-center"
                  >
                    <button
                      type="button"
                      disabled={busy || (item.id === 2 && !canStep1)}
                      onClick={() => {
                        if (item.id === 1) goBack()
                        else goNext()
                      }}
                      aria-current={visual === 'current' ? 'step' : undefined}
                      className={cn(
                        'flex size-7 items-center justify-center rounded-full text-xs font-black transition focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed',
                        visual === 'done' && 'bg-[#1C4D8D] text-white',
                        visual === 'current' &&
                          'bg-[#1C4D8D] text-white ring-[3px] ring-[#1C4D8D]/22',
                        visual === 'upcoming' &&
                          'border-2 border-[#4988C4] bg-white text-[#1C4D8D]',
                      )}
                    >
                      {visual === 'done' ? (
                        <Check className="size-3.5 stroke-[3]" aria-hidden="true" />
                      ) : (
                        item.id
                      )}
                    </button>
                    <p
                      className={cn(
                        'mt-2 text-center text-xs font-bold',
                        visual === 'current' ? 'text-[#1C4D8D]' : 'text-[#5b6b84]',
                      )}
                    >
                      {item.title}
                    </p>
                  </li>
                )
              })}
            </ol>
          </nav>
        </header>

        <form onSubmit={onFormSubmit} className="flex min-h-0 flex-1 flex-col">
          <div className="min-h-0 flex-1 space-y-4 overflow-y-auto bg-[#F8FBFD] p-5">
            {step === 1 ? (
              <section className="space-y-4 rounded-2xl border border-[#4988C4]/40 bg-white p-4">
                <label className="block">
                  <span className="text-xs font-bold tracking-[0.06em] text-[#5b6b84] uppercase">
                    Lớp học phần
                  </span>
                  <FilterDropMenu
                    label="Chọn lớp"
                    value={classId}
                    onChange={setClassId}
                    options={classOptions}
                    className="mt-1.5 w-full"
                  />
                  <p className="mt-2 text-xs leading-relaxed text-[#5b6b84]">
                    Chỉ lớp bạn phụ trách. Sinh viên cần đã tham gia lớp trước khi nhập OTP.
                  </p>
                </label>
                <label className="block">
                  <span className="text-xs font-bold tracking-[0.06em] text-[#5b6b84] uppercase">
                    Tiêu đề ca
                  </span>
                  <input
                    value={title}
                    onChange={(event) => setTitle(event.target.value)}
                    maxLength={160}
                    placeholder="Ví dụ: Progress Test 1 — Clean Architecture"
                    className={cn(ds.field, 'mt-1.5')}
                    autoFocus
                  />
                  <p className="mt-1.5 text-[11px] text-[#8aa0b8]">Tối thiểu 4 ký tự</p>
                </label>
              </section>
            ) : (
              <>
                <div className="rounded-2xl border border-[#4988C4]/35 bg-white px-3.5 py-3">
                  <p className="text-[11px] font-bold tracking-[0.06em] text-[#5b6b84] uppercase">
                    Ca đang tạo
                  </p>
                  <p className="mt-1 truncate text-sm font-bold text-[#0F2854]">
                    {title.trim() || '—'}
                  </p>
                  <p className="mt-0.5 text-xs font-semibold text-[#1C4D8D]">
                    {selectedClass
                      ? `${selectedClass.name} · ${selectedClass.courseCode}`
                      : 'Chưa chọn lớp'}
                  </p>
                </div>

                <section className="space-y-4 rounded-2xl border border-[#4988C4]/40 bg-white p-4">
                  <div className="grid gap-4 sm:grid-cols-2">
                    <label className="block">
                      <span className="flex items-center justify-between text-xs font-bold text-[#5b6b84] uppercase">
                        Thời lượng
                        <span className="font-mono normal-case text-[#1C4D8D]">
                          {durationMinutes} phút
                        </span>
                      </span>
                      <input
                        type="range"
                        min={5}
                        max={60}
                        step={5}
                        value={durationMinutes}
                        onChange={(event) => setDurationMinutes(Number(event.target.value))}
                        className="mt-3 w-full accent-[#1C4D8D]"
                      />
                    </label>
                    <label className="block">
                      <span className="flex items-center justify-between text-xs font-bold text-[#5b6b84] uppercase">
                        Số câu hỏi
                        <span className="font-mono normal-case text-[#1C4D8D]">{questionCount}</span>
                      </span>
                      <input
                        type="range"
                        min={1}
                        max={5}
                        step={1}
                        value={questionCount}
                        onChange={(event) => setQuestionCount(Number(event.target.value))}
                        className="mt-3 w-full accent-[#1C4D8D]"
                      />
                    </label>
                  </div>
                  <p className="rounded-xl bg-[#F4FBFD] px-3 py-2 text-xs leading-relaxed text-[#5b6b84]">
                    Hệ thống bốc câu đã duyệt từ ngân hàng luyện tập theo số lượng.
                  </p>
                  <div className="flex items-center justify-between gap-3 rounded-xl border border-[#4988C4]/40 px-3 py-3">
                    <span>
                      <span className="block text-sm font-bold text-[#0F2854]">Câu hỏi phụ</span>
                      <span className="text-xs text-[#5b6b84]">Follow-up sau câu chính</span>
                    </span>
                    <button
                      type="button"
                      role="switch"
                      aria-checked={hasFollowUp}
                      onClick={() => setHasFollowUp((value) => !value)}
                      className={cn(
                        'relative h-7 w-12 rounded-full transition',
                        hasFollowUp ? 'bg-[#1C4D8D]' : 'bg-[#c5d0e0]',
                      )}
                    >
                      <span
                        className={cn(
                          'absolute top-0.5 size-6 rounded-full bg-white shadow transition',
                          hasFollowUp ? 'left-[22px]' : 'left-0.5',
                        )}
                      />
                    </button>
                  </div>
                </section>

                <section className="rounded-2xl border border-[#4988C4]/40 bg-white p-4">
                  <button
                    type="button"
                    onClick={() => setShowAdvanced((value) => !value)}
                    className="flex w-full items-center justify-between text-sm font-bold text-[#1C4D8D]"
                  >
                    Tùy chọn OTP nâng cao
                    <ChevronRight
                      className={cn('size-4 transition', showAdvanced && 'rotate-90')}
                    />
                  </button>
                  {showAdvanced && (
                    <div className="mt-3 grid gap-3 sm:grid-cols-2">
                      <label className="block">
                        <span className="text-xs font-bold text-[#5b6b84] uppercase">
                          Chu kỳ xoay (giây)
                        </span>
                        <input
                          type="number"
                          min={60}
                          max={600}
                          step={30}
                          value={passwordRotationSeconds}
                          onChange={(event) =>
                            setPasswordRotationSeconds(Number(event.target.value) || 300)
                          }
                          className={cn(ds.field, 'mt-1.5')}
                        />
                      </label>
                      <label className="block">
                        <span className="text-xs font-bold text-[#5b6b84] uppercase">
                          Grace period (giây)
                        </span>
                        <input
                          type="number"
                          min={0}
                          max={120}
                          step={10}
                          value={gracePeriodSeconds}
                          onChange={(event) =>
                            setGracePeriodSeconds(Number(event.target.value) || 60)
                          }
                          className={cn(ds.field, 'mt-1.5')}
                        />
                      </label>
                    </div>
                  )}
                </section>
              </>
            )}

            {error && (
              <p className="flex items-start gap-2 text-sm font-semibold text-[#a43c47]" role="alert">
                <AlertCircle className="mt-0.5 size-4 shrink-0" />
                {error}
              </p>
            )}
          </div>

          <footer className="shrink-0 border-t border-[#4988C4]/40 bg-white px-5 py-3">
            <div className="flex flex-col-reverse gap-2 sm:flex-row sm:items-center sm:justify-between">
              <button
                type="button"
                disabled={busy}
                onClick={onClose}
                className={cn(ds.outlineButton, 'min-h-10')}
              >
                Hủy
              </button>
              <div className="flex flex-col-reverse gap-2 sm:flex-row">
                {step === 2 && (
                  <button
                    type="button"
                    disabled={busy}
                    onClick={goBack}
                    className={cn(ds.outlineButton, 'min-h-10')}
                  >
                    Quay lại
                  </button>
                )}
                {step === 1 ? (
                  <button
                    type="button"
                    disabled={!canStep1 || busy}
                    onClick={goNext}
                    className={cn(ds.primaryButton, 'min-h-10')}
                  >
                    Tiếp tục
                    <ChevronRight className="size-4" />
                  </button>
                ) : (
                  <button
                    type="button"
                    disabled={!canSubmit || busy}
                    onClick={() => void createShift()}
                    className={cn(ds.primaryButton, 'min-h-10')}
                  >
                    {busy ? (
                      <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" />
                    ) : (
                      <Plus className="size-3.5" />
                    )}
                    {busy ? 'Đang mở ca...' : 'Mở ca'}
                  </button>
                )}
              </div>
            </div>
          </footer>
        </form>
      </div>
    </div>
  )
}

export function InClassShiftHubPage() {
  const [classes, setClasses] = useState<LecturerClassOption[]>([])
  const [shifts, setShifts] = useState<InClassShift[]>([])
  const [classFilter, setClassFilter] = useState('ALL')
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('ALL')
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [createOpen, setCreateOpen] = useState(false)
  const [projectorShiftId, setProjectorShiftId] = useState<string | null>(null)
  const [reloadKey, setReloadKey] = useState(0)

  useEffect(() => {
    let cancelled = false
    const load = async () => {
      setLoading(true)
      setError('')
      try {
        const [classResult, shiftResult] = await Promise.all([
          inClassShiftService.getLecturerClasses(),
          inClassShiftService.listShifts(),
        ])
        if (cancelled) return
        setClasses(classResult)
        setShifts(shiftResult)
      } catch (loadError) {
        if (!cancelled) setError(getErrorMessage(loadError))
      } finally {
        if (!cancelled) setLoading(false)
      }
    }
    void load()
    return () => {
      cancelled = true
    }
  }, [reloadKey])

  const classOptions = useMemo(
    () => [
      { value: 'ALL', label: 'Tất cả lớp' },
      ...classes.map((item) => ({ value: item.id, label: `${item.name} · ${item.courseCode}` })),
    ],
    [classes],
  )

  const classScopedShifts = useMemo(() => {
    if (classFilter === 'ALL') return shifts
    return shifts.filter((shift) => shift.classId === classFilter)
  }, [shifts, classFilter])

  const statusCounts = useMemo(() => {
    const counts: Record<StatusFilter, number> = {
      ALL: classScopedShifts.length,
      ACTIVE: 0,
      CLOSED: 0,
      FINALIZED: 0,
    }
    for (const shift of classScopedShifts) {
      counts[shift.status] += 1
    }
    return counts
  }, [classScopedShifts])

  const filtered = useMemo(() => {
    const query = search.trim().toLocaleLowerCase('vi-VN')
    return classScopedShifts.filter((shift) => {
      if (statusFilter !== 'ALL' && shift.status !== statusFilter) return false
      if (!query) return true
      return (
        shift.title.toLocaleLowerCase('vi-VN').includes(query) ||
        shift.className.toLocaleLowerCase('vi-VN').includes(query) ||
        shift.courseCode.toLocaleLowerCase('vi-VN').includes(query)
      )
    })
  }, [classScopedShifts, statusFilter, search])

  return (
    <div lang="vi" className={cn(ds.page, 'pb-8')}>
      <section className={ds.banner}>
        <div className="pointer-events-none absolute inset-0 [background-image:radial-gradient(rgba(255,255,255,0.3)_0.8px,transparent_0.8px)] [background-size:22px_22px] opacity-35" />
        <div className="relative z-10 max-w-2xl p-6 sm:p-8">
          <div className="inline-flex items-center gap-2 rounded-full border border-white/25 bg-white/14 px-3 py-1 text-xs font-bold tracking-[0.06em] text-[#BDE8F5] uppercase backdrop-blur-sm">
            <UsersRound className="size-3.5" />
            MF-05 · Ca thi trên lớp
          </div>
          <h1 className="mt-3 text-2xl font-bold tracking-[-0.03em] text-white sm:text-3xl">
            Điểm thành phần vấn đáp
          </h1>
          <p className="mt-2 text-sm leading-relaxed text-white/85">
            Tạo ca Progress Test / Quiz, chiếu mã OTP xoay 5 phút, hậu kiểm và chốt điểm One-Way
            Lock. Không lưu audio lên R2.
          </p>
        </div>
      </section>

      {(error || notice) && (
        <div
          className={cn(
            'flex items-center justify-between gap-3 rounded-2xl border px-4 py-3 text-sm',
            error
              ? 'border-[#f3c8cf] bg-[#fff5f6] text-[#b33d52]'
              : 'border-[#bcebdc] bg-[#effbf6] text-[#16785f]',
          )}
          role={error ? 'alert' : 'status'}
        >
          <span className="flex items-center gap-2 font-medium">
            {error ? (
              <AlertCircle className="size-4 shrink-0" />
            ) : (
              <CheckCircle2 className="size-4 shrink-0" />
            )}
            {error || notice}
          </span>
          <button
            type="button"
            onClick={() => {
              setError('')
              setNotice('')
            }}
            className="text-xs font-bold underline-offset-2 hover:underline"
          >
            Đóng
          </button>
        </div>
      )}

      <section className={cn(ds.section, 'overflow-hidden')}>
        <div className={ds.sectionHeader}>
          <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
            <div>
              <div className={ds.sectionTitleRow}>
                <span className={ds.sectionIconWrap}>
                  <ClipboardList className={ds.sectionIcon} />
                </span>
                <h2 className={ds.sectionTitle}>Danh sách ca</h2>
                <span className="rounded-full bg-white px-2.5 py-0.5 text-xs font-bold text-[#1C4D8D] shadow-2xs">
                  {loading ? '…' : `${filtered.length} ca`}
                </span>
              </div>
              <p className={ds.sectionHint}>
                Lọc theo lớp, trạng thái ca và tìm theo tiêu đề
              </p>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <FilterDropMenu
                label="Lớp học phần"
                value={classFilter}
                onChange={setClassFilter}
                options={classOptions}
                className="w-56 shrink-0"
              />
              <div className="relative shrink-0">
                <Search className="pointer-events-none absolute top-1/2 left-3.5 size-3.5 -translate-y-1/2 text-[#5b6b84]" />
                <input
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                  placeholder="Tìm theo tên ca..."
                  className="h-10 w-56 rounded-full border border-[#4988C4]/45 bg-white py-0 pr-4 pl-9 text-sm font-semibold text-[#0F2854] shadow-[0_2px_8px_rgba(23,48,96,0.04)] outline-none placeholder:font-medium placeholder:text-[#8aa0b8] focus:border-[#4988C4]/45 focus:ring-2 focus:ring-[#1C4D8D]/15"
                />
              </div>
              <button
                type="button"
                onClick={() => setReloadKey((key) => key + 1)}
                disabled={loading}
                className={ds.outlineButton}
              >
                <RefreshCw className={cn('size-3.5', loading && 'animate-spin')} />
                Làm mới
              </button>
              <button
                type="button"
                onClick={() => setCreateOpen(true)}
                className={cn(ds.primaryButton, 'min-h-10')}
              >
                <Plus className="size-3.5" />
                Tạo ca thi
              </button>
            </div>
          </div>
        </div>

        <div className="bg-white">
          <div className="px-4 pt-3 sm:px-6">
            <div
              className="flex flex-wrap items-end gap-0 border-b-2 border-[#1C4D8D]"
              role="tablist"
              aria-label="Lọc trạng thái ca"
            >
              {STATUS_FILTERS.map((tab) => {
                const count = statusCounts[tab.value]
                const active = statusFilter === tab.value
                return (
                  <button
                    key={tab.value}
                    type="button"
                    role="tab"
                    aria-selected={active}
                    onClick={() => setStatusFilter(tab.value)}
                    className={cn(
                      'relative inline-flex items-center gap-1.5 border px-3.5 py-2.5 text-[14px] font-bold whitespace-nowrap transition sm:px-4',
                      active
                        ? '-mb-0.5 rounded-t-xl border-[#1C4D8D] border-b-white bg-[linear-gradient(165deg,#BDE8F5_0%,#EAF6FB_42%,#ffffff_100%)] text-[#1C4D8D]'
                        : 'mb-0 border-transparent text-[#6f7f93] hover:bg-[#F4FBFD] hover:text-[#1C4D8D]',
                    )}
                  >
                    <span
                      className={cn(
                        'size-1.5 rounded-full',
                        active ? 'bg-[#1C4D8D]' : tab.dotClass,
                      )}
                      aria-hidden="true"
                    />
                    <span>{tab.label}</span>
                    <span
                      className={cn(
                        'rounded-full px-1.5 py-0.5 text-[12px] font-bold tabular-nums',
                        active ? 'bg-[#1C4D8D]/12 text-[#1C4D8D]' : tab.countIdleClass,
                      )}
                    >
                      {count}
                    </span>
                  </button>
                )
              })}
            </div>
          </div>
        </div>

        <div className={cn(ds.sectionBody, 'border-t-0 pt-4')}>
          {loading ? (
            <div className="flex min-h-40 flex-col items-center justify-center gap-3 text-sm text-[#5b6b84]">
              <LoaderCircle className="size-7 animate-spin text-[#1C4D8D] motion-reduce:animate-none" />
              Đang tải danh sách ca...
            </div>
          ) : filtered.length === 0 ? (
            <div className="rounded-2xl border border-dashed border-[#4988C4]/50 bg-[#F8FBFD] px-6 py-12 text-center">
              <UsersRound className="mx-auto size-10 text-[#4988C4]" />
              <h3 className="mt-3 text-lg font-bold text-[#0F2854]">Chưa có ca phù hợp</h3>
              <p className="mx-auto mt-2 max-w-md text-sm text-[#5b6b84]">
                Tạo Progress Test / Quiz cho lớp đang dạy để sinh viên vào bằng mã OTP chiếu trên
                lớp.
              </p>
              <button
                type="button"
                onClick={() => setCreateOpen(true)}
                className={cn(ds.primaryButton, 'mt-5')}
              >
                <Plus className="size-4" />
                Tạo ca thi
              </button>
            </div>
          ) : (
            <ul className="divide-y divide-[#4988C4]/25 overflow-hidden rounded-2xl border border-[#4988C4]/40">
              {filtered.map((shift) => (
                <li
                  key={shift.id}
                  className="flex flex-col gap-3 bg-white px-4 py-4 transition hover:bg-[#F8FBFD] sm:flex-row sm:items-center sm:justify-between sm:px-5"
                >
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      {statusChip(shift.status)}
                      <span className="rounded-full border border-[#4988C4]/35 bg-[#F4FBFD] px-2.5 py-0.5 text-[11px] font-bold text-[#1C4D8D]">
                        {shift.className} · {shift.courseCode}
                      </span>
                    </div>
                    <h3 className="mt-2 truncate text-base font-bold text-[#0F2854]">
                      {shift.title}
                    </h3>
                    <p className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs font-semibold text-[#5b6b84]">
                      <span className="inline-flex items-center gap-1">
                        <Clock3 className="size-3.5" />
                        {shift.durationMinutes} phút · {shift.questionCount} câu
                        {shift.hasFollowUp ? ' · có follow-up' : ''}
                      </span>
                      <span>
                        {shift.joinedCount} vào lớp · {shift.submittedCount} đã nộp
                      </span>
                    </p>
                  </div>
                  <div className="flex flex-wrap items-center gap-2 sm:shrink-0">
                    {shift.status === 'ACTIVE' && (
                      <button
                        type="button"
                        onClick={() => setProjectorShiftId(shift.id)}
                        className={cn(ds.primaryButton, 'min-h-10')}
                      >
                        <Presentation className="size-3.5" />
                        Chiếu mã
                      </button>
                    )}
                    <Link
                      to={`/in-class-shifts/${shift.id}/results`}
                      className={cn(ds.outlineButton, 'min-h-10')}
                    >
                      {shift.status === 'FINALIZED' ? 'Xem (khóa)' : 'Bảng điểm'}
                      <ChevronRight className="size-3.5" />
                    </Link>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </div>
      </section>

      <CreateInClassShiftDialog
        open={createOpen}
        classes={classes}
        onClose={() => setCreateOpen(false)}
        onCreated={(shift) => {
          setCreateOpen(false)
          setNotice(`Đã mở ca “${shift.title}”. Bấm Chiếu mã trên dòng ca để hiện OTP.`)
          setShifts((current) => [shift, ...current.filter((item) => item.id !== shift.id)])
        }}
      />

      <InClassShiftProjectorDialog
        open={Boolean(projectorShiftId)}
        shiftId={projectorShiftId}
        onClose={() => setProjectorShiftId(null)}
      />
    </div>
  )
}
