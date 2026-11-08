import { useEffect, useState, type FormEvent, type MouseEvent } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import {
  ArrowLeft,
  ClipboardList,
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
import type { ClassDetail, ClassStudent, ClassStudentLookup } from '@/types/class.types'

type DetailTab = 'shifts' | 'students'

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
  const [addOpen, setAddOpen] = useState(false)
  const [removing, setRemoving] = useState<ClassStudent | null>(null)
  const [removingBusy, setRemovingBusy] = useState(false)

  const load = async () => {
    if (!classId) return
    setLoading(true)
    try {
      setDetail(await classService.getClassDetail(classId))
    } catch (loadError) {
      toast.error(userMessage(loadError, 'Không tải được dữ liệu lớp học. Vui lòng thử lại.'))
      setDetail(null)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    return scheduleUi(() => {
      void load()
    })
  }, [classId])

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
      <Link
        to="/lecturer/classes"
        className="inline-flex min-h-8 w-fit items-center gap-1.5 rounded-lg text-base font-bold text-[#1C4D8D] transition hover:text-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]"
      >
        <ArrowLeft className="size-5" aria-hidden="true" />
        Về danh sách
      </Link>

      <div className="space-y-6">
      {loading && !detail ? (
        <div className="flex min-h-32 items-center justify-center gap-2 text-sm text-[#5b6b84]">
          <LoaderCircle className="size-5 animate-spin text-[#1C4D8D]" />
          Đang tải lớp...
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
                <div className="inline-flex max-w-full items-center gap-2 rounded-xl border border-[#82aedb]/55 bg-white py-1.5 pr-1.5 pl-2.5">
                  <KeyRound className="size-3.5 shrink-0 text-[#1C4D8D]" aria-hidden="true" />
                  <span className="text-[11px] font-bold tracking-[0.04em] text-[#5b6b84] uppercase">
                    Mật khẩu
                  </span>
                  <JoinPasswordReveal password={detail.joinPassword} hasPassword={detail.hasPassword} />
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
  const [keyword, setKeyword] = useState('')
  const students = detail.students.filter((student) => {
    const text = `${student.fullName} ${student.studentCode} ${student.email}`.toLocaleLowerCase('vi-VN')
    return text.includes(keyword.trim().toLocaleLowerCase('vi-VN'))
  })

  return (
    <section className={cn(ds.section, 'overflow-hidden')}>
      <div className={cn(ds.sectionHeader, 'flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between')}>
        <div className={ds.sectionHeading}>
          <div className={ds.sectionTitleRow}>
            <span className={ds.sectionIconWrap}><Users className={ds.sectionIcon} /></span>
            <h2 className={ds.sectionTitle}>Sinh viên</h2>
            <span className={ds.sectionBadge}>{students.length} sinh viên</span>
          </div>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <label className="relative">
            <span className="sr-only">Tìm sinh viên</span>
            <Search className="pointer-events-none absolute top-1/2 left-3 size-3.5 -translate-y-1/2 text-[#5b6b84]" />
            <input
              value={keyword}
              onChange={(event) => setKeyword(event.target.value)}
              placeholder="Tên, mã hoặc email"
              className="h-10 w-56 rounded-full border border-[#4988C4] bg-white py-0 pr-4 pl-9 text-sm font-semibold text-[#0F2854] outline-none placeholder:font-medium placeholder:text-[#8aa0b8] focus:ring-2 focus:ring-[#1C4D8D]/15"
            />
          </label>
          <button type="button" className={cn(ds.primaryButton, 'min-h-10')} onClick={onAdd}>
            <Plus className="size-3.5" />
            Thêm sinh viên
          </button>
        </div>
      </div>
      <div className={ds.sectionBody}>
        {detail.students.length === 0 ? (
          <EmptyState icon={Users} title="Lớp chưa có sinh viên" description="Thêm sinh viên bằng mã hoặc email." />
        ) : students.length === 0 ? (
          <EmptyState icon={Search} title="Không có sinh viên khớp từ khóa" />
        ) : (
          <ul className="space-y-1.5">
            {students.map((student) => (
              <li key={student.studentId} className="flex items-center gap-2.5 rounded-xl bg-[#F8FBFD] px-2.5 py-2">
                <span className="flex size-8 shrink-0 items-center justify-center rounded-full bg-[#1C4D8D] text-xs font-black text-white">{nameInitial(student.fullName)}</span>
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-bold text-[#0F2854]">{student.fullName}</p>
                  <p className="truncate text-xs text-[#5b6b84]">{student.studentCode || 'Chưa có mã'} · {student.email}</p>
                </div>
                <span className={ds.infoChip}>{joinMethodLabel(student.joinMethod)}</span>
                <button
                  type="button"
                  className={cn(ds.outlineButton, 'min-h-9 px-3')}
                  onClick={() => onRemove(student)}
                >
                  <UserMinus className="size-3.5" />
                  Xóa
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
  const [query, setQuery] = useState('')
  const [queryError, setQueryError] = useState('')
  const [match, setMatch] = useState<ClassStudentLookup | null>(null)
  const [looking, setLooking] = useState(false)
  const [adding, setAdding] = useState(false)

  const search = async (event?: FormEvent) => {
    event?.preventDefault()
    const term = query.trim()
    if (!term) {
      setQueryError('Nhập đủ mã sinh viên hoặc email.')
      setMatch(null)
      return
    }
    setLooking(true)
    setQueryError('')
    setMatch(null)
    try {
      const found = await classService.lookupStudent(term)
      if (!found.studentId) {
        setQueryError('Không tìm thấy sinh viên với mã hoặc email này.')
        return
      }
      setMatch(found)
    } catch (searchError) {
      setQueryError(userMessage(searchError, 'Không tìm thấy sinh viên với mã hoặc email này.'))
    } finally {
      setLooking(false)
    }
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
      eyebrow="Sinh viên"
      title="Thêm sinh viên"
      subtitle="Nhập đủ mã sinh viên hoặc email."
      icon={Users}
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
      <form onSubmit={(event) => void search(event)} className="space-y-3">
        <label className="block">
          <span className={fieldLabelClass(Boolean(queryError))}>Mã hoặc email</span>
          <div className="mt-1.5 flex gap-2">
            <input
              value={query}
              onChange={(event) => {
                setQuery(event.target.value)
                setQueryError('')
                setMatch(null)
              }}
              placeholder="SE181863"
              aria-invalid={Boolean(queryError) || undefined}
              className={cn(ds.field, 'min-w-0 flex-1', queryError && fieldInvalidClass)}
            />
            <button type="submit" disabled={looking || adding} className={cn(ds.outlineButton, 'min-h-10')}>
              {looking ? <LoaderCircle className="size-4 animate-spin" /> : <Search className="size-3.5" />}
              Tìm
            </button>
          </div>
          <FieldError message={queryError} />
        </label>
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
  const secret = password?.trim() || ''
  const canReveal = Boolean(secret)

  const toggle = (event: MouseEvent) => {
    event.preventDefault()
    event.stopPropagation()
    if (!canReveal) return
    setVisible((current) => !current)
  }

  return (
    <div className="inline-flex min-w-0 items-center gap-0.5">
      <p className="max-w-[9rem] truncate font-mono text-sm font-black tracking-[0.06em] text-[#0F2854] sm:max-w-[12rem]">
        {!hasPassword && !secret ? 'Chưa đặt' : visible && secret ? secret : '••••••••'}
      </p>
      {canReveal ? (
        <button
          type="button"
          aria-label={visible ? 'Ẩn mật khẩu tham gia' : 'Hiện mật khẩu tham gia'}
          aria-pressed={visible}
          onClick={toggle}
          className="flex size-7 shrink-0 items-center justify-center rounded-lg text-[#1C4D8D] transition hover:bg-[#E7F3FB]"
        >
          {visible ? <EyeOff className="size-3.5" /> : <Eye className="size-3.5" />}
        </button>
      ) : null}
    </div>
  )
}
