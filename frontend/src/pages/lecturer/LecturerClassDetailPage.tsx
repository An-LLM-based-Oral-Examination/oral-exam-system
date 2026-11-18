import { SectionHeader } from '@/design/ui'
import { LoadingState } from '@/components/common/LoadingState'
import { SearchField } from '@/components/common/SearchField'
import { BackLink } from '@/components/common/BackLink'
import { SEARCH_DELAY_MS, useSearchInput } from '@/hooks/useSearchInput'
import { useEffect, useState, type FormEvent, type MouseEvent } from 'react'
import { useParams, useSearchParams } from 'react-router-dom'
import {
  Check,
  ClipboardList,
  Copy,
  Eye,
  EyeOff,
  KeyRound,
  LoaderCircle,
  Lock,
  Plus,
  Search,
  Unlock,
  Users,
  UserMinus,
  UsersRound,
} from 'lucide-react'
import { EmptyState } from '@/components/common/EmptyState'
import { RosterDate } from '@/components/common/RosterDate'
import { FieldError } from '@/components/common/formField'
import { fieldInvalidClass, fieldLabelClass } from '@/components/common/formFieldClasses'
import { toast } from '@/components/common/toastStore'
import { ConfirmDialog } from '@/design/ConfirmDialog'
import { DialogFooter, FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'
import { scheduleUi } from '@/lib/scheduleUi'
import { cn } from '@/lib/utils'
import { userMessage } from '@/lib/userMessage'
import { InClassShiftHubPage } from '@/pages/lecturer/InClassShiftHubPage'
import { classService } from '@/services/class.service'
import type {
  ClassDetail,
  ClassStudent,
  ClassStudentLookup,
  ResetClassPasswordResponse,
} from '@/types/class.types'

type DetailTab = 'shifts' | 'students'

function formatClassError(error: unknown, fallback: string) {
  const message = userMessage(error, fallback)
  return message.replace(/^VALIDATION\s*[:\-–—]\s*/i, '').trim() || fallback
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

function joinMethodLabel(value: string) {
  const key = value.toLowerCase()
  if (key === 'assigned') return 'Được thêm'
  if (key === 'self' || key === 'self_enroll') return 'Tự tham gia'
  if (key === 'password') return 'Mật khẩu'
  return value || 'Tham gia'
}

function nameInitial(name: string) {
  const part = name.trim().split(/\s+/).filter(Boolean).at(-1) || '?'
  return part.slice(0, 1).toUpperCase()
}

export function LecturerClassDetailPage() {
  const { classId = '' } = useParams()
  const [searchParams, setSearchParams] = useSearchParams()
  const tab: DetailTab = searchParams.get('tab') === 'students' ? 'students' : 'shifts'
  const [detail, setDetail] = useState<ClassDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState('')
  const [addOpen, setAddOpen] = useState(false)
  const [resetOpen, setResetOpen] = useState(false)
  const [resetResult, setResetResult] = useState<ResetClassPasswordResponse | null>(null)
  const [sessionJoinPassword, setSessionJoinPassword] = useState<string | null>(null)
  const [copiedJoinCode, setCopiedJoinCode] = useState(false)
  const [removing, setRemoving] = useState<ClassStudent | null>(null)
  const [removingBusy, setRemovingBusy] = useState(false)

  const load = async (targetClassId = classId) => {
    if (!targetClassId) return
    setLoading(true)
    setLoadError('')
    try {
      const nextDetail = await classService.getClassDetail(targetClassId)
      setDetail(nextDetail)
    } catch (error) {
      const message = formatClassError(error, 'Không tải được dữ liệu lớp học. Vui lòng thử lại.')
      setLoadError(message)
      toast.error(message)
      setDetail((current) => (current?.classId === targetClassId ? current : null))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    return scheduleUi(() => {
      setDetail((current) => (current?.classId === classId ? current : null))
      setLoadError('')
      setSessionJoinPassword(null)
      setResetResult(null)
      setResetOpen(false)
      setCopiedJoinCode(false)
      void load(classId)
    })
  }, [classId])

  const handleCopyJoinCode = async (joinCode: string) => {
    const copied = await copyText(joinCode)
    if (copied) {
      setCopiedJoinCode(true)
      toast.success('Đã sao chép mã tham gia lớp.')
      return
    }
    toast.warning('Không thể tự động sao chép. Vui lòng sao chép thủ công mã tham gia.')
  }

  const selectTab = (next: DetailTab) => {
    const params = new URLSearchParams(searchParams)
    params.set('tab', next)
    setSearchParams(params, { replace: true })
  }

  const confirmRemove = async () => {
    if (!detail || !removing) return
    setRemovingBusy(true)
    try {
      await classService.dropStudent(detail.classId, removing.studentId)
      toast.success(`Đã xóa ${removing.fullName} khỏi lớp.`)
      setRemoving(null)
      await load()
    } catch (removeError) {
      toast.error(userMessage(removeError, 'Không xóa được sinh viên. Vui lòng thử lại.'))
    } finally {
      setRemovingBusy(false)
    }
  }

  return (
    <div lang="vi" className={cn(ds.page, 'pb-8')}>
      <div className="space-y-3">
      <BackLink
        to="/lecturer/classes">Về danh sách</BackLink>

      <div className="space-y-6">
      {loading && !detail ? (
        <LoadingState label="Đang tải lớp…" />
      ) : null}

      {loadError ? (
        <div
          role="alert"
          className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-[#f3c2c8] bg-[#fdecec] px-4 py-3 text-sm font-semibold text-[#c2415b]"
        >
          <span>{loadError}</span>
          <button
            type="button"
            onClick={() => void load(classId)}
            disabled={loading}
            className="inline-flex min-h-8 items-center rounded-lg border border-[#f3c2c8] bg-white px-3 text-xs font-bold text-[#c2415b] transition hover:bg-[#fff7f8] disabled:opacity-60"
          >
            Thử lại
          </button>
        </div>
      ) : null}

      {detail ? (
        <>
          <section className={cn(ds.section, 'overflow-hidden')}>
            <div className={cn(ds.sectionHeader, 'space-y-3')}>
              <div className={ds.sectionHeading}>
                <div className={ds.sectionTitleRow}>
                  <span className={ds.sectionIconWrap}>
                    <UsersRound className={ds.sectionIcon} />
                  </span>
                  <h1 className={ds.sectionTitle}>{detail.name || detail.code}</h1>
                </div>
                <p className={cn(ds.sectionHint, 'flex flex-wrap items-center gap-x-2 gap-y-1')}>
                  <span>{detail.courseName || detail.code}</span>
                  {detail.courseCode ? (
                    <>
                      <span className="text-[#82aedb]" aria-hidden="true">
                        ·
                      </span>
                      <span className="font-bold text-[#1C4D8D]">{detail.courseCode}</span>
                    </>
                  ) : null}
                  {detail.semesterCode ? (
                    <>
                      <span className="text-[#82aedb]" aria-hidden="true">
                        ·
                      </span>
                      <span className="font-bold text-[#1C4D8D]">{detail.semesterCode}</span>
                    </>
                  ) : null}
                </p>
              </div>

              <div className="flex flex-wrap items-center gap-2">
                {detail.joinCode ? (
                  <div className="inline-flex max-w-full items-center gap-1.5 rounded-xl border border-[#82aedb]/55 bg-white py-1 pr-1.5 pl-2.5">
                    <span className="text-[11px] font-bold tracking-[0.04em] text-[#5b6b84] uppercase">
                      Mã tham gia
                    </span>
                    <span className="font-mono text-sm font-black tracking-[0.06em] text-[#0F2854]">
                      {detail.joinCode}
                    </span>
                    <button
                      type="button"
                      aria-label="Sao chép mã tham gia"
                      onClick={() => void handleCopyJoinCode(detail.joinCode)}
                      className="inline-flex h-7 items-center gap-1 rounded-lg px-2 text-xs font-bold text-[#1C4D8D] transition hover:bg-[#E7F3FB]"
                    >
                      {copiedJoinCode ? <Check className="size-3.5 text-[#16836a]" /> : <Copy className="size-3.5" />}
                      <span>{copiedJoinCode ? 'Đã sao chép' : 'Sao chép'}</span>
                    </button>
                  </div>
                ) : null}
                <div className="inline-flex max-w-full items-center gap-2 rounded-xl border border-[#82aedb]/55 bg-white py-1.5 pr-1.5 pl-2.5">
                  <KeyRound className="size-3.5 shrink-0 text-[#1C4D8D]" aria-hidden="true" />
                  <span className="text-[11px] font-bold tracking-[0.04em] text-[#5b6b84] uppercase">
                    Mật khẩu
                  </span>
                  <JoinPasswordReveal
                    key={sessionJoinPassword ?? detail.joinPassword ?? 'none'}
                    password={sessionJoinPassword ?? detail.joinPassword}
                    hasPassword={detail.hasPassword}
                  />
                </div>
                <span className={ds.infoChip}>
                  <Users className="size-3.5 shrink-0" aria-hidden="true" />
                  {detail.enrolledCount}/{detail.maxStudents} SV
                </span>
                <span className={detail.isJoinEnabled ? ds.successChip : ds.infoChip}>
                  {detail.isJoinEnabled ? (
                    <Unlock className="size-3.5 shrink-0" aria-hidden="true" />
                  ) : (
                    <Lock className="size-3.5 shrink-0" aria-hidden="true" />
                  )}
                  {detail.isJoinEnabled ? 'Đang mở' : 'Đã khóa'}
                </span>
                <button
                  type="button"
                  onClick={() => {
                    setLoadError('')
                    setResetOpen(true)
                  }}
                  className={cn(ds.outlineButton, 'min-h-9 px-3 text-xs')}
                >
                  <KeyRound className="size-3.5" aria-hidden="true" />
                  Đặt lại mật khẩu lớp
                </button>
              </div>

              <div
                className="grid w-full grid-cols-2 gap-1 rounded-2xl border border-[#82aedb]/55 bg-white/80 p-1 shadow-[inset_0_1px_0_rgba(255,255,255,0.9)]"
                role="tablist"
                aria-label="Nội dung lớp"
              >
                <button
                  type="button"
                  role="tab"
                  aria-selected={tab === 'shifts'}
                  onClick={() => selectTab('shifts')}
                  className={cn(
                    'group inline-flex h-10 items-center justify-center gap-2 rounded-[14px] px-3 py-2 text-[15px] font-bold transition duration-200',
                    tab === 'shifts'
                      ? 'bg-[#1C4D8D] text-white shadow-[0_8px_18px_rgba(28,77,141,0.28)]'
                      : 'text-[#5f7088] hover:bg-[#F4FBFD] hover:text-[#0F2854]',
                  )}
                >
                  <ClipboardList
                    className={cn(
                      'size-4 shrink-0 transition',
                      tab === 'shifts' ? 'text-[#BDE8F5]' : 'text-[#7d8fa3] group-hover:text-[#1C4D8D]',
                    )}
                    aria-hidden="true"
                  />
                  <span className="truncate">Ca kiểm tra</span>
                </button>
                <button
                  type="button"
                  role="tab"
                  aria-selected={tab === 'students'}
                  onClick={() => selectTab('students')}
                  className={cn(
                    'group inline-flex h-10 items-center justify-center gap-2 rounded-[14px] px-3 py-2 text-[15px] font-bold transition duration-200',
                    tab === 'students'
                      ? 'bg-[#1C4D8D] text-white shadow-[0_8px_18px_rgba(28,77,141,0.28)]'
                      : 'text-[#5f7088] hover:bg-[#F4FBFD] hover:text-[#0F2854]',
                  )}
                >
                  <Users
                    className={cn(
                      'size-4 shrink-0 transition',
                      tab === 'students' ? 'text-[#BDE8F5]' : 'text-[#7d8fa3] group-hover:text-[#1C4D8D]',
                    )}
                    aria-hidden="true"
                  />
                  <span className="truncate">Sinh viên</span>
                </button>
              </div>
            </div>
          </section>

          {tab === 'shifts' ? (
            <InClassShiftHubPage classId={detail.classId} embedded />
          ) : (
            <StudentRoster
              detail={detail}
              onAdd={() => setAddOpen(true)}
              onRemove={setRemoving}
            />
          )}
        </>
      ) : null}
      </div>
      </div>

      {addOpen && detail ? (
        <AddStudentDialog
          classId={detail.classId}
          onClose={() => setAddOpen(false)}
          onAdded={async (name) => {
            setAddOpen(false)
            toast.success(`Đã thêm ${name} vào lớp.`)
            await load()
          }}
        />
      ) : null}

      {resetOpen && detail ? (
        <ResetClassPasswordDialog
          detail={detail}
          onClose={() => setResetOpen(false)}
          onResetSuccess={async (response) => {
            setResetOpen(false)
            setResetResult(response)
            setSessionJoinPassword(response.joinPassword)
            setDetail((current) =>
              current && current.classId === (response.classId || detail.classId)
                ? {
                    ...current,
                    joinCode: response.joinCode || current.joinCode,
                    hasPassword: response.hasPassword,
                    isJoinEnabled: response.isJoinEnabled,
                  }
                : current,
            )
            toast.success('Đã đặt lại mật khẩu lớp.')
            await load(detail.classId)
          }}
        />
      ) : null}

      {resetResult ? (
        <ResetCredentialsDialog
          result={resetResult}
          onClose={() => setResetResult(null)}
        />
      ) : null}

      {removing ? (
        <ConfirmDialog
          tone="danger"
          title="Xóa sinh viên"
          message={`Xóa ${removing.fullName} khỏi lớp. Sinh viên không vào ca mới của lớp này. Bài đã nộp vẫn nằm ở bảng điểm.`}
          confirmLabel="Xóa"
          cancelLabel="Hủy"
          pending={removingBusy}
          onConfirm={() => void confirmRemove()}
          onCancel={() => {
            if (!removingBusy) setRemoving(null)
          }}
        />
      ) : null}
    </div>
  )
}

function StudentRoster({
  detail,
  onAdd,
  onRemove,
}: {
  detail: ClassDetail
  onAdd: () => void
  onRemove: (student: ClassStudent) => void
}) {
  const keywordInput = useSearchInput()
  const keyword = keywordInput.query
  const students = detail.students.filter((student) => {
    const text = `${student.fullName} ${student.studentCode} ${student.email}`.toLocaleLowerCase('vi-VN')
    return text.includes(keyword.trim().toLocaleLowerCase('vi-VN'))
  })

  return (
    <section className={ds.section}>
      <SectionHeader
        icon={Users}
        title="Danh sách sinh viên"
        badge={`${students.length} sinh viên`}
        hint="Sinh viên đang tham gia lớp này"
        className="flex-row flex-wrap items-center justify-between"
        action={
          <div className="flex flex-wrap items-center gap-2">
          <label className={cn('relative', ds.searchControl)}>
            <span className="sr-only">Tìm sinh viên</span>
            <SearchField
              value={keywordInput.draft}
              onChange={(event) => keywordInput.change(event.target.value)}
              onKeyDown={keywordInput.onKeyDown}
              placeholder="Tên, mã hoặc email"
              className="h-10 w-full rounded-full border border-[#4988C4] bg-white py-0 pr-4 pl-9 text-sm font-semibold text-[#0F2854] shadow-[0_2px_8px_rgba(23,48,96,0.04)] outline-none placeholder:font-medium placeholder:text-[#8aa0b8] focus:ring-2 focus:ring-[#1C4D8D]/15"
            />
          </label>
          <button type="button" className={cn(ds.primaryButton, 'min-h-10')} onClick={onAdd}>
            <Plus className="size-3.5" />
            Thêm sinh viên
          </button>
        </div>
        }
      />
      <div className={cn(ds.sectionBody, students.length > 0 && 'py-0 sm:py-0')}>
        {detail.students.length === 0 ? (
          <EmptyState
            icon={Users}
            title="Lớp chưa có sinh viên"
            description="Thêm sinh viên bằng mã hoặc email."
            action={
              <button type="button" onClick={onAdd} className={ds.primaryButton}>
                <Plus className="size-3.5" aria-hidden="true" />
                Thêm sinh viên
              </button>
            }
          />
        ) : students.length === 0 ? (
          <EmptyState icon={Search} title="Không có sinh viên khớp từ khóa" />
        ) : (
          <ul aria-label="Danh sách sinh viên trong lớp" className="divide-y divide-[#82aedb]/55">
            {students.map((student) => (
              <li key={student.studentId} className="flex flex-wrap items-center gap-x-4 gap-y-2 py-3 transition-colors hover:bg-[#F4FBFD] lg:grid lg:grid-cols-[minmax(0,1fr)_minmax(0,1.4fr)_15rem_auto_auto]">
                <div className="flex min-w-0 basis-full items-center gap-3">
                  <span className="grid size-9 shrink-0 place-items-center rounded-lg bg-[#E7F3FB] text-sm font-bold text-[#1C4D8D]" aria-hidden="true">
                    {nameInitial(student.fullName)}
                  </span>
                  <div className="min-w-0 [overflow-wrap:anywhere]">
                    <p className="text-base font-bold text-[#0F2854]">{student.fullName}</p>
                    <p className="font-mono text-sm font-bold text-[#1C4D8D]">{student.studentCode || 'Chưa có mã'}</p>
                  </div>
                </div>
                <p className="min-w-0 text-sm [overflow-wrap:anywhere] text-[#5b6b84]">{student.email}</p>
                <RosterDate label="Ngày tham gia" value={student.enrolledAt} />
                <span className={cn(ds.infoChip, 'ml-auto text-sm whitespace-nowrap')}>{joinMethodLabel(student.joinMethod)}</span>
                <button
                  type="button"
                  className={cn(ds.outlineButton, 'h-9 shrink-0 border-[#e8a8b5] bg-[#fff7f8] px-3 text-xs text-[#c2415b] hover:border-[#c2415b] hover:bg-[#ffe9ed] focus-visible:outline-[#c2415b]')}
                  aria-label={`Xóa ${student.fullName}`}
                  onClick={() => onRemove(student)}
                >
                  <UserMinus className="size-3.5" aria-hidden="true" /> Xóa
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </section>
  )
}

function AddStudentDialog({
  classId,
  onClose,
  onAdded,
}: {
  classId: string
  onClose: () => void
  onAdded: (name: string) => Promise<void>
}) {
  const [lookup, setLookup] = useState({ query: '', delay: SEARCH_DELAY_MS })
  const [queryError, setQueryError] = useState('')
  const [match, setMatch] = useState<ClassStudentLookup | null>(null)
  const [notFound, setNotFound] = useState(false)
  const [looking, setLooking] = useState(false)
  const [adding, setAdding] = useState(false)

  useEffect(() => {
    const term = lookup.query.trim()
    if (!term) return

    let active = true
    const timer = window.setTimeout(async () => {
      setLooking(true)
      try {
        const found = await classService.lookupStudent(term)
        if (!active) return
        if (found.studentId) {
          setMatch(found)
        } else {
          setNotFound(true)
        }
      } catch (searchError) {
        if (!active) return
        if (typeof searchError === 'object' && searchError !== null &&
          'status' in searchError && searchError.status === 404) {
          setNotFound(true)
        } else {
          setQueryError(userMessage(searchError, 'Không tìm được sinh viên. Vui lòng thử lại.'))
        }
      } finally {
        if (active) setLooking(false)
      }
    }, lookup.delay)

    return () => {
      active = false
      window.clearTimeout(timer)
    }
  }, [lookup])

  const search = (event: FormEvent) => {
    event.preventDefault()
    if (looking || adding) return
    if (!lookup.query.trim()) {
      setQueryError('Nhập đủ mã sinh viên hoặc email.')
      return
    }
    setQueryError('')
    setMatch(null)
    setNotFound(false)
    setLookup({ query: lookup.query, delay: 0 })
  }

  const add = async () => {
    if (!match || adding) return
    setAdding(true)
    try {
      await classService.assignStudent(classId, match.studentId)
      await onAdded(match.fullName)
    } catch (addError) {
      toast.error(userMessage(addError, 'Không thêm được sinh viên. Vui lòng thử lại.'))
      setAdding(false)
    }
  }

  return (
    <FormDialog
      titleId="add-student-title"
      eyebrow="Chi tiết lớp học"
      title="Thêm sinh viên"
      icon={Users}
      size="sm"
      onClose={onClose}
      busy={adding}
      footer={(
        <DialogFooter className="bg-white sm:flex-row">
          <button type="button" onClick={onClose} disabled={adding} className={cn(ds.outlineButton, 'min-h-10')}>Hủy</button>
          <button type="button" onClick={() => void add()} disabled={!match || adding} className={cn(ds.primaryButton, 'min-h-10')}>
            {adding ? <LoaderCircle className="size-4 animate-spin" /> : <Plus className="size-3.5" />}
            Thêm vào lớp
          </button>
        </DialogFooter>
      )}
    >
      <form onSubmit={search} className="space-y-3">
        <label className="block">
          <span className={fieldLabelClass(Boolean(queryError))}>Mã hoặc email</span>
          <div className="relative mt-1.5">
            <input
              value={lookup.query}
              onChange={(event) => {
                setLookup({ query: event.target.value, delay: SEARCH_DELAY_MS })
                setQueryError('')
                setMatch(null)
                setNotFound(false)
                setLooking(false)
              }}
              disabled={adding}
              placeholder="Nhập mã sinh viên hoặc email"
              aria-invalid={Boolean(queryError) || undefined}
              className={cn(ds.field, 'w-full pr-10', queryError && fieldInvalidClass)}
            />
            {looking && (
              <span role="status" className="absolute top-1/2 right-3 -translate-y-1/2">
                <LoaderCircle className="size-4 animate-spin" aria-hidden="true" />
                <span className="sr-only">Đang tìm sinh viên</span>
              </span>
            )}
          </div>
          <FieldError message={queryError} />
        </label>
        {notFound && !looking && (
          <EmptyState compact icon={Search} title="Không tìm thấy sinh viên" description="Thử mã sinh viên hoặc email khác." />
        )}
        {match ? (
          <div className="rounded-xl bg-[#F8FBFD] px-3 py-2.5">
            <p className="text-sm font-bold text-[#0F2854]">{match.fullName}</p>
            <p className="text-xs text-[#5b6b84]">{match.studentCode || 'Chưa có mã'} · {match.email}</p>
          </div>
        ) : null}
      </form>
    </FormDialog>
  )
}

function JoinPasswordReveal({
  password,
  hasPassword,
}: {
  password: string | null | undefined
  hasPassword: boolean
}) {
  const [visible, setVisible] = useState(false)
  const [copied, setCopied] = useState(false)
  const secret = password?.trim() || ''
  const canReveal = Boolean(secret)

  const toggle = (event: MouseEvent) => {
    event.preventDefault()
    event.stopPropagation()
    if (!canReveal) return
    setVisible((current) => !current)
  }

  const handleCopy = async (event: MouseEvent) => {
    event.preventDefault()
    event.stopPropagation()
    if (!secret) return
    const ok = await copyText(secret)
    if (ok) {
      setCopied(true)
      toast.success('Đã sao chép mật khẩu tham gia.')
      return
    }
    toast.warning('Không thể tự động sao chép mật khẩu.')
  }

  return (
    <div className="inline-flex min-w-0 items-center gap-0.5">
      <p className="max-w-[9rem] truncate font-mono text-sm font-black tracking-[0.06em] text-[#0F2854] sm:max-w-[12rem]">
        {!hasPassword && !secret ? 'Chưa đặt' : visible && secret ? secret : '••••••••'}
      </p>
      {canReveal ? (
        <>
          <button
            type="button"
            aria-label={visible ? 'Ẩn mật khẩu tham gia' : 'Hiện mật khẩu tham gia'}
            aria-pressed={visible}
            onClick={toggle}
            className="flex size-7 shrink-0 items-center justify-center rounded-lg text-[#1C4D8D] transition hover:bg-[#E7F3FB]"
          >
            {visible ? <EyeOff className="size-3.5" /> : <Eye className="size-3.5" />}
          </button>
          <button
            type="button"
            aria-label="Sao chép mật khẩu tham gia"
            onClick={(event) => void handleCopy(event)}
            className="flex size-7 shrink-0 items-center justify-center rounded-lg text-[#1C4D8D] transition hover:bg-[#E7F3FB]"
          >
            {copied ? <Check className="size-3.5 text-[#16836a]" /> : <Copy className="size-3.5" />}
          </button>
        </>
      ) : null}
    </div>
  )
}

function ResetClassPasswordDialog({
  detail,
  onClose,
  onResetSuccess,
}: {
  detail: ClassDetail
  onClose: () => void
  onResetSuccess: (response: ResetClassPasswordResponse) => Promise<void>
}) {
  const [newPassword, setNewPassword] = useState('')
  const [newJoinCode, setNewJoinCode] = useState('')
  const [isJoinEnabled, setIsJoinEnabled] = useState(detail.isJoinEnabled)
  const [fieldErrors, setFieldErrors] = useState<{ newPassword?: string; newJoinCode?: string }>({})
  const [submitError, setSubmitError] = useState('')
  const [busy, setBusy] = useState(false)

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    if (busy) return

    const trimmedPassword = newPassword.trim()
    const trimmedCode = newJoinCode.trim()
    const nextErrors: { newPassword?: string; newJoinCode?: string } = {}
    if (trimmedPassword.length < 8) {
      nextErrors.newPassword = 'Mật khẩu mới cần ít nhất 8 ký tự.'
    } else if (trimmedPassword.length > 128) {
      nextErrors.newPassword = 'Mật khẩu mới tối đa 128 ký tự.'
    }
    if (trimmedCode && (trimmedCode.length < 4 || trimmedCode.length > 32)) {
      nextErrors.newJoinCode = 'Mã tham gia mới cần từ 4 đến 32 ký tự.'
    }

    if (Object.keys(nextErrors).length > 0) {
      setFieldErrors(nextErrors)
      return
    }

    setBusy(true)
    setFieldErrors({})
    setSubmitError('')
    try {
      const response = await classService.resetPassword(detail.classId, {
        newPassword: trimmedPassword,
        newJoinCode: trimmedCode || undefined,
        isJoinEnabled,
      })
      await onResetSuccess(response)
    } catch (resetError) {
      const message = formatClassError(
        resetError,
        'Không đặt lại được mật khẩu lớp. Vui lòng thử lại.',
      )
      setSubmitError(message)
      toast.error(message)
      setBusy(false)
    }
  }

  return (
    <FormDialog
      titleId="reset-class-password-title"
      eyebrow="Bảo mật lớp học"
      title="Đặt lại mật khẩu lớp"
      icon={KeyRound}
      onClose={onClose}
      busy={busy}
      footer={(
        <DialogFooter className="bg-white sm:flex-row">
          <button type="button" onClick={onClose} disabled={busy} className={cn(ds.outlineButton, 'min-h-10')}>
            Hủy
          </button>
          <button type="submit" form="reset-class-password-form" disabled={busy} className={cn(ds.primaryButton, 'min-h-10')}>
            {busy ? <LoaderCircle className="size-4 animate-spin" /> : <KeyRound className="size-3.5" />}
            Lưu mật khẩu mới
          </button>
        </DialogFooter>
      )}
    >
      <form id="reset-class-password-form" onSubmit={(event) => void submit(event)} className="space-y-3" noValidate>
        {submitError ? (
          <div
            role="alert"
            className="rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-3 py-2.5 text-xs font-semibold text-[#c2415b]"
          >
            {submitError}
          </div>
        ) : null}

        <label className="block">
          <span className={fieldLabelClass(Boolean(fieldErrors.newPassword))}>Mật khẩu mới</span>
          <input
            value={newPassword}
            onChange={(event) => {
              setNewPassword(event.target.value)
              setSubmitError('')
              setFieldErrors((current) => ({ ...current, newPassword: undefined }))
            }}
            placeholder="Ít nhất 8 ký tự"
            aria-invalid={Boolean(fieldErrors.newPassword) || undefined}
            className={cn(ds.field, 'mt-1.5', fieldErrors.newPassword && fieldInvalidClass)}
          />
          <FieldError message={fieldErrors.newPassword} />
        </label>

        <label className="block">
          <span className={fieldLabelClass(Boolean(fieldErrors.newJoinCode))}>Mã tham gia mới (tùy chọn)</span>
          <input
            value={newJoinCode}
            onChange={(event) => {
              setNewJoinCode(event.target.value)
              setSubmitError('')
              setFieldErrors((current) => ({ ...current, newJoinCode: undefined }))
            }}
            placeholder={detail.joinCode || 'Để trống để giữ nguyên mã hiện tại'}
            aria-invalid={Boolean(fieldErrors.newJoinCode) || undefined}
            className={cn(ds.field, 'mt-1.5', fieldErrors.newJoinCode && fieldInvalidClass)}
          />
          <p className="mt-1 text-xs text-[#5b6b84]">Để trống để giữ nguyên mã tham gia hiện tại.</p>
          <FieldError message={fieldErrors.newJoinCode} />
        </label>

        <label className="flex items-center gap-2.5 rounded-xl border border-[#82aedb]/45 bg-[#F8FBFD] px-3 py-2.5">
          <input
            type="checkbox"
            checked={isJoinEnabled}
            onChange={(event) => setIsJoinEnabled(event.target.checked)}
            className="size-4 rounded border-[#4988C4] text-[#1C4D8D] accent-[#1C4D8D]"
          />
          <span className="text-sm font-semibold text-[#0F2854]">Cho phép sinh viên tự tham gia lớp</span>
        </label>
      </form>
    </FormDialog>
  )
}

function ResetCredentialsDialog({
  result,
  onClose,
}: {
  result: ResetClassPasswordResponse
  onClose: () => void
}) {
  const [copiedField, setCopiedField] = useState<'joinCode' | 'joinPassword' | null>(null)

  const handleCopy = async (field: 'joinCode' | 'joinPassword', value: string | null) => {
    if (!value) return
    const ok = await copyText(value)
    if (ok) {
      setCopiedField(field)
      toast.success(field === 'joinCode' ? 'Đã sao chép mã tham gia.' : 'Đã sao chép mật khẩu mới.')
      return
    }
    toast.warning('Không thể tự động sao chép. Vui lòng sao chép thủ công.')
  }

  return (
    <FormDialog
      titleId="reset-class-credentials-title"
      eyebrow="Đã cập nhật mật khẩu lớp"
      title={result.name || result.code}
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
      <div className="space-y-3">
        <div className="rounded-xl border border-[#ecd9ab] bg-[#fff9ec] px-3.5 py-2.5 text-xs font-semibold text-[#8a5e17]">
          Mật khẩu mới chỉ hiển thị một lần ngay sau khi đặt lại. Hãy sao chép để gửi cho sinh viên.
        </div>
        <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-[#82aedb]/45 bg-[#F8FBFD] px-3.5 py-2.5">
          <div>
            <p className="text-[11px] font-bold tracking-[0.06em] text-[#5b6b84] uppercase">Mã tham gia</p>
            <p className="mt-0.5 font-mono text-base font-black tracking-[0.08em] text-[#0F2854]">
              {result.joinCode}
            </p>
          </div>
          {result.joinCode ? (
            <button
              type="button"
              onClick={() => void handleCopy('joinCode', result.joinCode)}
              className={cn(ds.outlineButton, 'min-h-9 px-3 text-xs')}
            >
              {copiedField === 'joinCode' ? <Check className="size-3.5 text-[#16836a]" /> : <Copy className="size-3.5" />}
              {copiedField === 'joinCode' ? 'Đã sao chép' : 'Sao chép mã'}
            </button>
          ) : null}
        </div>
        <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-[#82aedb]/45 bg-[#F8FBFD] px-3.5 py-2.5">
          <div>
            <p className="text-[11px] font-bold tracking-[0.06em] text-[#5b6b84] uppercase">Mật khẩu mới</p>
            <p className="mt-0.5 font-mono text-base font-black tracking-[0.06em] text-[#0F2854]">
              {result.joinPassword || 'Đã cập nhật mật khẩu'}
            </p>
          </div>
          {result.joinPassword ? (
            <button
              type="button"
              onClick={() => void handleCopy('joinPassword', result.joinPassword)}
              className={cn(ds.outlineButton, 'min-h-9 px-3 text-xs')}
            >
              {copiedField === 'joinPassword' ? <Check className="size-3.5 text-[#16836a]" /> : <Copy className="size-3.5" />}
              {copiedField === 'joinPassword' ? 'Đã sao chép' : 'Sao chép mật khẩu'}
            </button>
          ) : null}
        </div>
        <p className="text-xs font-semibold text-[#5b6b84]">
          Trạng thái tự tham gia: <span className="text-[#0F2854]">{result.isJoinEnabled ? 'Đang mở' : 'Đã khóa'}</span>
        </p>
      </div>
    </FormDialog>
  )
}
