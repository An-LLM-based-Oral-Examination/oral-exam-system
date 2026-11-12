import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { BookOpen, CalendarDays, GraduationCap, KeyRound, LoaderCircle, Mic2, Search, TimerReset, UserRound, UsersRound, X } from 'lucide-react'
import { EmptyState } from '@/components/common/EmptyState'
import { FieldError } from '@/components/common/formField'
import { fieldInvalidClass } from '@/components/common/formFieldClasses'
import { StatusToast } from '@/components/common/toast'
import { toast } from '@/components/common/toastStore'
import { FilterDropMenu } from '@/components/common/FilterDropMenu'
import { DialogFooter, FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'
import { studentCardShellClass } from '@/design/studentCardTones'
import { StudentColorCardAccent } from '@/design/ui'
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

export function StudentClassPanel({ onClassCount }: { onClassCount?: (count: number) => void }) {
  const [semesters, setSemesters] = useState<SemesterOption[]>([])
  const [semesterId, setSemesterId] = useState('')
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
  const [keyword, setKeyword] = useState('')
  const [appliedKeyword, setAppliedKeyword] = useState('')
  const searchRef = useRef<HTMLDivElement>(null)
  const requestId = useRef(0)
  const passwordRef = useRef<HTMLInputElement>(null)

  const loadMine = async (silent = false) => {
    if (!silent) setLoading(true)
    setError('')
    try {
      const [semesterResult, myResult] = await Promise.all([
        classService.listSemesters(),
        classService.listMyClasses(),
      ])
      setSemesters(semesterResult)
      setSemesterId((current) => current || pickCurrentSemester(semesterResult)?.id || '')
      setMine(myResult)
      onClassCount?.(myResult.length)
    } catch (loadError) {
      setError(getErrorMessage(loadError))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    return scheduleUi(() => {
      void loadMine()
    })
  }, [])

  useEffect(() => {
    const trimmed = keyword.trim()
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
  }, [keyword, semesterId])

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
    if (!searched || joinTarget) return
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
  }, [searched, joinTarget])

  const semesterOptions = useMemo(
    () => semesters.map((item) => ({ value: item.id, label: item.name || item.code })),
    [semesters],
  )
  const myClasses = mine.filter((item) => item.semesterId === semesterId)
  const openClasses = directory.filter((item) => item.semesterId === semesterId)

  const clearSearch = () => {
    requestId.current += 1
    setKeyword('')
    setDirectory([])
    setSearched(false)
    setSearching(false)
    setSearchFailed(false)
    setError('')
  }

  const openJoin = (item: JoinableClassItem) => {
    setJoinTarget(item)
    setPassword('')
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
      setSearched(false)
      await loadMine(true)
    } catch (joinFailure) {
      toast.error(getErrorMessage(joinFailure))
    } finally {
      setJoining(false)
    }
  }

  return (
    <section className={cn(ds.section, 'overflow-visible')}>
      <div className={cn(ds.sectionHeader, 'relative z-20 flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between')}>
        <div className={ds.sectionHeading}>
          <div className={ds.sectionTitleRow}>
            <span className={ds.sectionIconWrap}><UsersRound className={ds.sectionIcon} /></span>
            <h2 className={ds.sectionTitle}>Lớp học của tôi</h2>
          </div>
          <p className={ds.sectionHint}>Các lớp đã tham gia trong kỳ này</p>
        </div>
        <form
          className="flex flex-col gap-2 sm:flex-row sm:items-center"
          onSubmit={(event: FormEvent) => event.preventDefault()}
        >
          <div ref={searchRef} className="relative min-w-0 sm:w-72">
            <label className="relative block">
              <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-[#5b6b84]" />
              <input
                value={keyword}
                onChange={(event) => setKeyword(event.target.value)}
                placeholder="Tìm tên lớp hoặc mã môn"
                aria-expanded={searched}
                aria-controls="class-search-results"
                className={cn(ds.field, 'h-10 min-h-10 pr-10 pl-9')}
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
            {searched && (
              <div
                id="class-search-results"
                className="absolute top-[calc(100%+8px)] left-0 z-40 max-h-80 w-full min-w-[min(26rem,calc(100vw-2rem))] overflow-y-auto rounded-2xl border border-[#4988C4] bg-white p-2 shadow-[0_16px_36px_rgba(15,40,84,0.18)]"
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
                  <p className="px-2 py-3 text-sm text-[#5b6b84]">Không có lớp khớp. Thử tên lớp hoặc mã môn khác.</p>
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
                              <button type="button" disabled={!item.canJoin} onClick={() => openJoin(item)} className={cn(ds.primaryButton, 'min-h-8 shrink-0 px-2.5 text-xs')}>
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
          <FilterDropMenu
            label="Học kỳ"
            value={semesterId}
            onChange={setSemesterId}
            options={semesterOptions}
            emptyText="Chưa có học kỳ"
            className="w-40"
          />
        </form>
      </div>

      <div className={cn(ds.sectionBody, 'space-y-4')}>
        <StatusToast
          notice={notice}
          error={error && !searched ? error : ''}
          onDismiss={() => {
            setError('')
            setNotice('')
          }}
        />

        {loading ? (
          <div className="flex min-h-28 items-center justify-center gap-2 text-sm text-[#5b6b84]">
            <LoaderCircle className="size-5 animate-spin text-[#1C4D8D]" /> Đang tải lớp...
          </div>
        ) : (
          <div className="space-y-4">
            <div>
              <p className="mb-3 text-xs font-bold tracking-[0.06em] text-[#1C4D8D] uppercase">Đã tham gia · {myClasses.length}</p>
              {myClasses.length === 0 ? (
                <EmptyState
                  icon={UsersRound}
                  title="Chưa có lớp trong học kỳ này"
                  description="Tìm đúng tên lớp hoặc mã môn rồi nhập mật khẩu để tham gia."
                />
              ) : (
                <ul className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
                  {myClasses.map((item) => (
                    <li key={item.classId} className={studentCardShellClass(item.courseCode)}>
                      <StudentColorCardAccent seed={item.courseCode} />
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
                            <span className="truncate">{item.lecturerName || 'Giảng viên'}</span>
                          </span>
                        </div>
                        <h3 className="mt-3 truncate text-xl leading-snug font-bold text-[#0F2854]">{item.className}</h3>
                        <p className="mt-1 truncate text-sm font-semibold text-[#3d5270]">{item.courseName}</p>
                        <div className="mt-auto grid grid-cols-2 gap-2 pt-3">
                          <Link
                            to={`/practice?course=${encodeURIComponent(item.courseCode)}`}
                            className={ds.outlineButton}
                          >
                            <Mic2 className="size-3.5" /> Luyện tập
                          </Link>
                          <Link
                            to={`/mock-exam?course=${encodeURIComponent(item.courseCode)}`}
                            className={ds.outlineButton}
                          >
                            <TimerReset className="size-3.5" /> Thi thử
                          </Link>
                        </div>
                      </div>
                    </li>
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
          eyebrow={`${joinTarget.courseCode} · ${joinTarget.semesterCode}`}
          title={joinTarget.courseName}
          subtitle={joinTarget.className}
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
              <button type="submit" form="join-class-form" disabled={joining} className={ds.primaryButton}>
                {joining ? <LoaderCircle className="size-4 animate-spin" /> : <KeyRound className="size-4" />}
                Tham gia
              </button>
            </DialogFooter>
          )}
        >
          <dl className="space-y-2.5">
            <div className="flex items-center gap-3 rounded-xl border border-[#82aedb]/55 bg-white px-3 py-2.5">
              <span className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-[#e7f1fb] text-[#1C4D8D]"><CalendarDays className="size-4" /></span>
              <div className="min-w-0">
                <dt className="text-[11px] font-bold tracking-[0.04em] text-[#5b6b84] uppercase">Học kỳ</dt>
                <dd className="text-sm font-bold text-[#0F2854]">{joinTarget.semesterCode}</dd>
              </div>
            </div>
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
            className="mt-4 space-y-3"
            onSubmit={(event: FormEvent) => {
              event.preventDefault()
              void confirmJoin()
            }}
          >
            {joinTarget.requiresPassword && (
              <label className="block">
                <span className={cn('text-sm font-bold', passwordError ? 'text-[#c2415b]' : 'text-[#0F2854]')}>Mật khẩu lớp</span>
                <span className="relative mt-1.5 block">
                  <KeyRound className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-[#5b6b84]" />
                  <input
                    ref={passwordRef}
                    type="password"
                    value={password}
                    onChange={(event) => {
                      setPassword(event.target.value)
                      setPasswordError('')
                    }}
                    placeholder="Nhập mật khẩu được giảng viên cấp"
                    aria-invalid={Boolean(passwordError)}
                    className={cn(ds.field, 'pl-9', passwordError && fieldInvalidClass)}
                  />
                </span>
                <FieldError message={passwordError} />
              </label>
            )}
          </form>
        </FormDialog>
      )}
    </section>
  )
}
