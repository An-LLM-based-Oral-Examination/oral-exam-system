import { SearchField } from '@/components/common/SearchField'
import { ClassCapacity } from '@/components/common/ClassCapacity'
import { inClassShiftService } from '@/services/inClassShift.service'
import type { InClassShift } from '@/types/inClassShift.types'
import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { ArrowLeft, ArrowRight, BookOpen, CalendarDays, Eye, EyeOff, GraduationCap, KeyRound, LoaderCircle, Search, UserRound, UsersRound, X } from 'lucide-react'
import { EmptyState } from '@/components/common/EmptyState'
import { FieldError } from '@/components/common/formField'
import { fieldInvalidClass } from '@/components/common/formFieldClasses'
import { StatusToast } from '@/components/common/toast'
import { toast } from '@/components/common/toastStore'
import { FilterDropMenu } from '@/components/common/FilterDropMenu'
import { DialogFooter, FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'
import { scheduleUi } from '@/lib/scheduleUi'
import { cn } from '@/lib/utils'
import { userMessage } from '@/lib/userMessage'
import { classService } from '@/services/class.service'
import type { JoinableClassItem, SemesterOption, StudentClassItem } from '@/types/class.types'

function getErrorMessage(error: unknown) {
  return userMessage(error, 'Không tải được lớp học. Vui lòng thử lại.')
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
  for (let index = 0; index < code.length; index += 1) {
    hash = (hash * 31 + code.charCodeAt(index)) >>> 0
  }
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

function StudentClassCard({ item, onJoin, shifts }: {
  item: StudentClassItem | JoinableClassItem
  shifts: InClassShift[] | null
  onJoin: (item: JoinableClassItem) => void
}) {
  const joinable = 'canJoin' in item ? item : undefined
  const enrolled = !joinable || joinable.isAlreadyEnrolled
  const activeShiftCount = shifts?.filter((shift) => shift.classId === item.classId && shift.status === 'ACTIVE').length ?? 0

  return (
    <li className={cn(
      'relative flex h-full w-full flex-col overflow-hidden rounded-[22px] border border-[#82aedb]/55 transition duration-200',
      'bg-[linear-gradient(180deg,#EAF5FB_0%,#F8FBFD_22%,#FFFFFF_48%,#FFFFFF_100%)]',
      'hover:-translate-y-0.5 hover:border-[#4988C4] hover:bg-[linear-gradient(180deg,#DCEFFA_0%,#F4FBFD_24%,#F8FBFD_52%,#F8FBFD_100%)] hover:shadow-[0_14px_28px_rgba(15,40,84,0.16)]',
      ds.panelShadow,
    )}>
      <div className="pointer-events-none absolute inset-x-0 top-0 h-14 overflow-hidden rounded-t-[21px]" aria-hidden="true">
        <div className="absolute inset-x-6 top-0 h-10 bg-[radial-gradient(ellipse_at_top,rgba(73,136,196,0.18)_0%,rgba(189,232,245,0.12)_42%,transparent_72%)]" />
        <div className={ds.softCardAccentLine} />
      </div>
      <div className="relative flex h-full flex-1 flex-col p-3.5">
        <div className="flex items-center justify-between gap-2">
          <div className="flex min-w-0 flex-wrap items-center gap-1.5">
            <span className={cn('inline-flex h-6 items-center rounded-md px-2 text-xs font-bold tracking-[0.04em]', courseCodeTone(item.courseCode))}>
              {item.courseCode}
            </span>
            <span className="inline-flex h-6 items-center gap-1 rounded-full border border-[#4988C4]/55 bg-white/85 px-2 text-xs font-semibold text-[#5b6b84] backdrop-blur-[2px]">
              <GraduationCap className="size-3 shrink-0 text-[#1C4D8D]" />
              {item.semesterCode}
            </span>
          </div>
          <span className="inline-flex h-6 max-w-[42%] shrink-0 items-center gap-1 rounded-full border border-[#b7e4cf] bg-white/85 px-2 text-xs font-semibold text-[#16836a] backdrop-blur-[2px]">
            <UserRound className="size-3 shrink-0" />
            <span className="truncate">{item.lecturerName || (enrolled ? 'Giảng viên' : 'Chưa phân công')}</span>
          </span>
        </div>
        <h3 className="mt-3 truncate text-xl leading-snug font-bold text-[#0F2854]">{item.className}</h3>
        <p className="mt-1 truncate text-sm font-semibold text-[#3d5270]">{item.courseName}</p>
        {item.enrolledCount != null && (
          <ClassCapacity enrolledCount={item.enrolledCount} maxStudents={item.maxStudents} />
        )}
        {joinable?.isAlreadyEnrolled ? (
          <div className="mt-auto pt-3">
            <span className="flex min-h-10 items-center justify-center rounded-xl border border-[#b7e4cf] bg-[#e8f8f1] px-3 text-sm font-semibold text-[#16836a]">
              Đã tham gia
            </span>
          </div>
        ) : enrolled ? (
          <div className="mt-auto space-y-0.5 pt-3">
            <p className={cn('text-sm font-semibold', activeShiftCount > 0 ? 'font-bold text-[#16836a]' : 'text-[#5b6b84]')}>{activeShiftCount > 0 ? `${activeShiftCount} ca đang diễn ra`
                : shifts ? 'Chưa có ca đang diễn ra' : 'Chưa tải được lịch ca'}
            </p>
            <div className="flex items-center justify-between gap-2">
              <span className="text-xs font-semibold text-[#5b6b84]">Mở lớp</span>
              <Link
                to={`/classes/${encodeURIComponent(item.classId)}`}
                className="inline-flex h-9 items-center gap-1 rounded-xl border border-[#4988C4] bg-white px-3 text-xs font-bold text-[#1C4D8D] transition hover:border-[#1C4D8D] hover:bg-[#E7F3FB] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]"
              >
                Chi tiết <ArrowRight className="size-3.5" aria-hidden="true" />
              </Link>
            </div>
          </div>
        ) : (
          <div className="mt-auto pt-3">
            <button
              type="button"
              disabled={!joinable?.canJoin}
              onClick={() => joinable && onJoin(joinable)}
              className={cn(ds.outlineButton, 'w-full')}
            >
              <KeyRound className="size-4" />
              {joinable?.canJoin ? 'Tham gia lớp' : 'Lớp đã đầy'}
            </button>
          </div>
        )}
      </div>
    </li>
  )
}

export function StudentClassPanel({ onClassCount, renderBanner }: { onClassCount?: (count: number) => void; renderBanner: (search: ReactNode) => ReactNode }) {
  const [submittedKeyword, setSubmittedKeyword] = useState('')
  const [semesters, setSemesters] = useState<SemesterOption[]>([])
  const [semesterId, setSemesterId] = useState('')
  const [shifts, setShifts] = useState<InClassShift[] | null>(null)
  const [mine, setMine] = useState<StudentClassItem[]>([])
  const [directory, setDirectory] = useState<JoinableClassItem[]>([])
  const [loading, setLoading] = useState(true)
  const [searching, setSearching] = useState(false)
  const [searched, setSearched] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [joinTarget, setJoinTarget] = useState<JoinableClassItem | null>(null)
  const [joining, setJoining] = useState(false)
  const [passwordError, setPasswordError] = useState('')
  const [searchFailed, setSearchFailed] = useState(false)
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [keyword, setKeyword] = useState('')
  const [appliedKeyword, setAppliedKeyword] = useState('')
  const searchRef = useRef<HTMLDivElement>(null)
  const requestId = useRef(0)
  const passwordRef = useRef<HTMLInputElement>(null)
  const onClassCountRef = useRef(onClassCount)

  useEffect(() => {
    onClassCountRef.current = onClassCount
  }, [onClassCount])

  const loadMine = useCallback(async (silent = false) => {
    if (!silent) setLoading(true)
    setError('')
    try {
      const [semesterResult, myResult] = await Promise.all([
        classService.listSemesters(),
        classService.listMyClasses(),
      ])
      setSemesters(semesterResult)
      setSemesterId((current) => semesterResult.some((item) => item.id === current) ? current : pickCurrentSemester(semesterResult)?.id || '')
      setMine(myResult)
      onClassCountRef.current?.(myResult.length)
    } catch (loadError) {
      setError(getErrorMessage(loadError))
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    let active = true
    void inClassShiftService.listStudentShifts().then((items) => {
      if (active) setShifts(items)
    }).catch(() => {
      if (active) setShifts(null)
    })
    return () => { active = false }
  }, [])

  useEffect(() => {
    return scheduleUi(() => {
      void loadMine()
    })
  }, [loadMine])

  useEffect(() => {
    const trimmed = (submittedKeyword || keyword).trim()
    if (!semesterId || !trimmed) {
      requestId.current += 1
      return scheduleUi(() => {
        setAppliedKeyword(trimmed)
        setDirectory([])
        setSearched(false)
        setSearching(false)
        setSearchFailed(false)
      })
    }

    const current = ++requestId.current
    const timer = window.setTimeout(() => {
      setAppliedKeyword(trimmed)
      setSearched(true)
      setSearchFailed(false)
      setSearching(true)
      void (async () => {
        try {
          const openResult = await classService.searchClasses({
            keyword: trimmed,
            semesterId,
          })
          if (requestId.current !== current) return
          setDirectory(openResult)
          setError('')
        } catch (searchError) {
          if (requestId.current !== current) return
          setDirectory([])
          setSearchFailed(true)
          toast.error(getErrorMessage(searchError))
        } finally {
          if (requestId.current === current) setSearching(false)
        }
      })()
    }, 280)

    return () => window.clearTimeout(timer)
  }, [keyword, submittedKeyword, semesterId])

  useEffect(() => {
    if (!joinTarget) return
    if (joinTarget.requiresPassword) passwordRef.current?.focus()
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && !joining) setJoinTarget(null)
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [joinTarget, joining])

  useEffect(() => {
    if (submittedKeyword || !searched || joinTarget) return
    const onPointerDown = (event: PointerEvent) => {
      if (!searchRef.current?.contains(event.target as Node)) setSearched(false)
    }
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setSearched(false)
    }
    window.addEventListener('pointerdown', onPointerDown)
    window.addEventListener('keydown', onKeyDown)
    return () => {
      window.removeEventListener('pointerdown', onPointerDown)
      window.removeEventListener('keydown', onKeyDown)
    }
  }, [searched, joinTarget, submittedKeyword])

  const semesterOptions = useMemo(
    () => semesters.map((item) => ({ value: item.id, label: item.name || item.code })),
    [semesters],
  )
  const myClasses = mine.filter((item) => item.semesterId === semesterId)
  const openClasses = directory.filter((item) => item.semesterId === semesterId)

  const clearSearch = () => {
    requestId.current += 1
    setKeyword('')
    setSubmittedKeyword('')
    setDirectory([])
    setSearched(false)
    setSearching(false)
    setSearchFailed(false)
    setError('')
  }

  const openJoin = (item: JoinableClassItem) => {
    setJoinTarget(item)
    setPassword('')
    setShowPassword(false)
    setPasswordError('')
  }

  const confirmJoin = async () => {
    if (!joinTarget) return
    if (joinTarget.requiresPassword && password.trim().length === 0) {
      setPasswordError('Nhập mật khẩu lớp để tham gia.')
      passwordRef.current?.focus()
      return
    }
    setPasswordError('')
    setJoining(true)
    try {
      await classService.joinClass(joinTarget.classId, joinTarget.requiresPassword ? password : undefined)
      setNotice(`Đã tham gia ${joinTarget.className}. Môn này đã có trong Luyện tập và Thi thử.`)
      setJoinTarget(null)
      setPassword('')
      if (submittedKeyword) {
        setDirectory((current) => current.map((item) =>
          item.classId === joinTarget.classId ? { ...item, isAlreadyEnrolled: true, canJoin: false } : item,
        ))
      } else {
        setSearched(false)
      }
      await loadMine(true)
    } catch (joinFailure) {
      toast.error(getErrorMessage(joinFailure))
    } finally {
      setJoining(false)
    }
  }

  return (
    <>
      {renderBanner(
        <form
          className="w-full sm:w-auto"
          onSubmit={(event: FormEvent) => {
            event.preventDefault()
            const query = keyword.trim()
            if (query && semesterId) {
              setSubmittedKeyword(query)
            }
          }}
        >
          <div ref={searchRef} className="relative min-w-0 sm:w-72">
            <label className="relative block">
              <SearchField
                value={keyword}
                onChange={(event) => {
                  setSubmittedKeyword('')
                  setKeyword(event.target.value)
                }}
                placeholder="Tìm tên lớp hoặc mã môn"
                aria-expanded={!submittedKeyword && searched}
                aria-controls={submittedKeyword ? undefined : 'class-search-suggestions'}
                className={cn(ds.field, 'h-10 min-h-10 pr-10 pl-9 [&::-webkit-search-cancel-button]:appearance-none')}
              />
              {keyword && (
                <button
                  type="button"
                  aria-label="Xóa tìm kiếm"
                  onClick={clearSearch}
                  className="absolute top-1/2 right-2 flex size-7 -translate-y-1/2 items-center justify-center rounded-lg text-[#5b6b84] hover:bg-[#F4FBFD]"
                >
                  <X className="size-3.5" />
                </button>
              )}
            </label>
            {!submittedKeyword && searched && (
              <div
                id="class-search-suggestions"
                className="absolute top-[calc(100%+8px)] left-0 z-40 max-h-80 w-full overflow-y-auto rounded-2xl border border-[#4988C4] bg-white p-2 shadow-[0_16px_36px_rgba(15,40,84,0.18)]"
              >
                <div className="flex items-center justify-between gap-2 px-2 py-1.5">
                  <p className="text-xs font-bold text-[#5b6b84]">
                    {searching ? 'Đang tìm...' : `${openClasses.length} lớp cho “${appliedKeyword}”`}
                  </p>
                  <button type="button" aria-label="Đóng kết quả" onClick={() => setSearched(false)} className="flex size-7 items-center justify-center rounded-lg text-[#5b6b84] hover:bg-[#F4FBFD]">
                    <X className="size-3.5" />
                  </button>
                </div>
                {searching ? (
                  <p className="flex items-center gap-2 px-2 py-3 text-sm text-[#5b6b84]"><LoaderCircle className="size-4 animate-spin text-[#1C4D8D]" /> Đang tìm lớp...</p>
                ) : searchFailed ? (
                  <p className="px-2 py-3 text-sm text-[#5b6b84]">Không thể tải kết quả tìm kiếm. Vui lòng thử lại.</p>
                ) : openClasses.length === 0 ? (
                  <EmptyState compact icon={Search} title="Không tìm thấy lớp phù hợp" description="Thử tên lớp hoặc mã môn khác." />
                ) : (
                  <ul className="space-y-1">
                    {openClasses.map((item) => (
                        <li key={item.classId} className="rounded-xl px-2 py-2 hover:bg-[#F4FBFD]">
                          <div className="flex items-start gap-3">
                            <span className={cn('mt-0.5 inline-flex h-6 shrink-0 items-center rounded-md px-2 text-[11px] font-bold', courseCodeTone(item.courseCode))}>{item.courseCode}</span>
                            <div className="min-w-0 flex-1">
                              <p className="truncate text-sm font-bold text-[#0F2854]">{item.className}</p>
                              <p className="truncate text-xs text-[#5b6b84]">{item.courseName} · {item.semesterCode} · {item.enrolledCount}/{item.maxStudents} SV</p>
                            </div>
                            {item.isAlreadyEnrolled ? (
                              <span className="shrink-0 rounded-full border border-[#b7e4cf] bg-[#e8f8f3] px-2 py-0.5 text-[11px] font-bold text-[#16836a]">Đã tham gia</span>
                            ) : (
                              <button type="button" disabled={!item.canJoin} onClick={() => openJoin(item)} className={cn(ds.primaryButton, 'h-8 min-h-8 shrink-0 gap-1 px-2 text-xs')}>
                                <KeyRound className="size-3.5" />
                                {item.canJoin ? 'Tham gia' : 'Đầy'}
                              </button>
                            )}
                          </div>
                        </li>
                    ))}
                  </ul>
                )}
              </div>
            )}
          </div>
        </form>
      )}
      <div className="space-y-3">
      {submittedKeyword && (
        <button
          type="button"
          onClick={clearSearch}
          className="inline-flex min-h-8 w-fit items-center gap-1.5 rounded-lg text-base font-bold text-[#1C4D8D] transition hover:text-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]"
        >
          <ArrowLeft className="size-5" aria-hidden="true" />
          Về danh sách
        </button>
      )}
        <section className={cn(ds.section, 'overflow-visible')}>
          <div className={cn(ds.sectionHeader, 'relative z-20 flex flex-wrap items-center justify-between gap-3')}>
            <div className={ds.sectionHeading}>
              <div className={ds.sectionTitleRow}>
                <span className={ds.sectionIconWrap}><UsersRound className={ds.sectionIcon} /></span>
                <h2 className={ds.sectionTitle}>{submittedKeyword ? 'Kết quả tìm kiếm' : 'Lớp học của tôi'}</h2>
                {!submittedKeyword && !loading && <span className={ds.sectionBadge}>{myClasses.length} lớp</span>}
              </div>
              <p className={ds.sectionHint} aria-live="polite">
                {submittedKeyword ? (
                  <>
                    Kết quả cho “{submittedKeyword}”
                    {searched && !searching && !searchFailed && appliedKeyword === submittedKeyword && ` · ${openClasses.length} lớp`}
                  </>
                ) : 'Các lớp đã tham gia trong kỳ này'}
              </p>
            </div>
            {!submittedKeyword && (
              <FilterDropMenu
                label="Học kỳ"
                value={semesterId}
                onChange={setSemesterId}
                options={semesterOptions}
                emptyText="Chưa có học kỳ"
                className={ds.filterSemester}
                menuMinWidth={ds.filterMenuMinWidth}
                wrapOptions
              />
            )}

          </div>

          <div className={cn(ds.sectionBody, 'space-y-4 p-5')}>
            <StatusToast
              notice={notice}
              error={error && !searched ? error : ''}
              onDismiss={() => {
                setError('')
                setNotice('')
              }}
            />

            {submittedKeyword ? (
              <div id="class-search-results" className="space-y-2">
                {searching || !searched || appliedKeyword !== submittedKeyword ? (
                  <div className="flex min-h-28 items-center justify-center gap-2 text-sm text-[#5b6b84]">
                    <LoaderCircle className="size-5 animate-spin text-[#1C4D8D]" /> Đang tìm lớp...
                  </div>
                ) : searchFailed ? (
                  <p className="rounded-xl border border-[#f0c5c9] bg-[#fff5f6] px-4 py-4 text-sm text-[#a43c47]">
                    Không thể tải kết quả tìm kiếm. Vui lòng thử lại.
                  </p>
                ) : openClasses.length === 0 ? (
                  <EmptyState
                    icon={UsersRound}
                    title="Không tìm thấy lớp phù hợp"
                    description="Thử tìm theo tên lớp hoặc mã môn khác."
                  />
                ) : (
                  <ul className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
                    {openClasses.map((item) => (
                      <StudentClassCard key={item.classId} item={item} onJoin={openJoin} shifts={shifts} />
                    ))}
                  </ul>
                )}
              </div>
            ) : loading ? (
              <div className="flex min-h-28 items-center justify-center gap-2 text-sm text-[#5b6b84]">
                <LoaderCircle className="size-5 animate-spin text-[#1C4D8D]" /> Đang tải lớp...
              </div>
            ) : (
              <div className="space-y-4">
                <div>
                  {myClasses.length === 0 ? (
                    <EmptyState
                      icon={UsersRound}
                      title="Chưa có lớp trong học kỳ này"
                      description="Tìm đúng tên lớp hoặc mã môn rồi nhập mật khẩu để tham gia."
                    />
                  ) : (
                    <ul className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
                      {myClasses.map((item) => (
                        <StudentClassCard key={item.classId} item={item} onJoin={openJoin} shifts={shifts} />
                      ))}
                    </ul>
                  )}
                </div>
              </div>
            )}
          </div>
          {joinTarget && (
            <FormDialog
              titleId="join-class-title"
              eyebrow="Quản lý lớp học"
              title={joinTarget.courseName}
              icon={BookOpen}
              onClose={() => setJoinTarget(null)}
              busy={joining}
              size="sm"
              align="center"
              footer={(
                <DialogFooter>
                  <button type="button" disabled={joining} onClick={() => setJoinTarget(null)} className={ds.outlineButton}>
                    Hủy
                  </button>
                  <button type="submit" form="join-class-form" disabled={joining} className={ds.outlineButton}>
                    {joining ? <LoaderCircle className="size-4 animate-spin" /> : <KeyRound className="size-4" />}
                    Tham gia
                  </button>
                </DialogFooter>
              )}
            >
              <div className="mb-3 min-w-0">
                <p className="text-[11px] font-bold tracking-[0.04em] text-[#5b6b84] uppercase">Lớp học</p>
                <h3 className="mt-1 text-lg leading-snug font-bold wrap-anywhere text-[#0F2854]">{joinTarget.className}</h3>
                <div className="mt-2 flex flex-wrap items-center gap-2">
                  <span className={ds.infoChip}>{joinTarget.classCode}</span>
                  <span className={cn(ds.infoChip, 'bg-[#E7F3FB]')}>
                    <CalendarDays className="size-3.5" aria-hidden="true" /> Học kỳ {joinTarget.semesterCode}
                  </span>
                </div>
              </div>
              <dl className="space-y-2.5">
                <div className="flex items-center gap-3 rounded-xl border border-[#82aedb]/55 bg-white px-3 py-2.5">
                  <span className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-[#e7f8f2] text-[#16836a]"><UserRound className="size-4" /></span>
                  <div className="min-w-0">
                    <dt className="text-[11px] font-bold tracking-[0.04em] text-[#5b6b84] uppercase">Giảng viên</dt>
                    <dd className="truncate text-sm font-bold text-[#0F2854]">{joinTarget.lecturerName || 'Chưa phân công'}</dd>
                  </div>
                </div>
                <div className="flex items-center gap-3 rounded-xl border border-[#82aedb]/55 bg-white px-3 py-2.5">
                  <span className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-[#fff3e6] text-[#c26a12]"><UsersRound className="size-4" /></span>
                  <div className="min-w-0 flex-1">
                    <dt className="text-[11px] font-bold tracking-[0.04em] text-[#5b6b84] uppercase">Sĩ số</dt>
                    <dd className="text-sm font-bold text-[#0F2854]">{joinTarget.enrolledCount}/{joinTarget.maxStudents} sinh viên</dd>
                    <div className="mt-1.5 h-1.5 overflow-hidden rounded-full bg-[#e7eef6]">
                      <span
                        className="block h-full rounded-full bg-[#1C4D8D]"
                        style={{ width: `${joinTarget.maxStudents > 0 ? Math.min(100, (joinTarget.enrolledCount / joinTarget.maxStudents) * 100) : 0}%` }}
                      />
                    </div>
                  </div>
                </div>
              </dl>
              <form
                id="join-class-form"
                className="mt-3 space-y-3"
                onSubmit={(event: FormEvent) => {
                  event.preventDefault()
                  void confirmJoin()
                }}
              >
                {joinTarget.requiresPassword && (
                  <div className="block">
                    <label
                      htmlFor="student-join-class-password"
                      className={cn('text-sm font-bold', passwordError ? 'text-[#c2415b]' : 'text-[#0F2854]')}
                    >
                      Mật khẩu lớp
                    </label>
                    <div className="relative mt-1.5">
                      <KeyRound className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-[#5b6b84]" />
                      <input
                        id="student-join-class-password"
                        ref={passwordRef}
                        type={showPassword ? 'text' : 'password'}
                        value={password}
                        onChange={(event) => {
                          setPassword(event.target.value)
                          setPasswordError('')
                        }}
                        placeholder="Nhập mật khẩu được giảng viên cấp"
                        aria-invalid={Boolean(passwordError)}
                        className={cn(ds.field, 'pr-10 pl-9', passwordError && fieldInvalidClass)}
                      />
                      <button
                        type="button"
                        aria-label={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
                        aria-pressed={showPassword}
                        onClick={() => setShowPassword((visible) => !visible)}
                        className="absolute top-1/2 right-2 flex size-8 -translate-y-1/2 items-center justify-center rounded-lg text-[#5b6b84] transition hover:bg-[#E7F3FB] hover:text-[#1C4D8D] focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-[#1C4D8D]"
                      >
                        {showPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
                      </button>
                    </div>
                    <FieldError message={passwordError} />
                  </div>
                )}
              </form>
            </FormDialog>
          )}
        </section>
      </div>
    </>
  )
}
