import { useEffect, useMemo, useState } from 'react'
import { CalendarDays, LoaderCircle, RefreshCw, Search, UserRoundMinus, UserRoundPlus, UsersRound, X } from 'lucide-react'
import { EmptyState } from '@/components/common/EmptyState'
import { RosterDate } from '@/components/common/RosterDate'
import { FilterDropMenu } from '@/components/common/FilterDropMenu'
import { FieldError } from '@/components/common/formField'
import { fieldLabelClass } from '@/components/common/formFieldClasses'
import { useAuth } from '@/hooks/useAuth'
import { SEARCH_DELAY_MS } from '@/hooks/useSearchInput'
import { lecturerAssignmentService as service } from '@/services/lecturerAssignment.service'
import type {
  AssignedLecturer,
  AssignmentOffering,
  LecturerDirectoryItem,
} from '@/types/lecturerAssignment.types'
import { ConfirmDialog } from '@/design/ConfirmDialog'
import { DialogFooter, FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { userMessage } from '@/lib/userMessage'

type PendingChange = { kind: 'add' | 'remove'; lecturer: LecturerDirectoryItem }

export function DepartmentLecturerAssignmentPage({
  courseId,
  initialSemesterId = '',
  onSemesterChange,
  onAssignmentsChanged,
}: {
  courseId?: string
  initialSemesterId?: string
  onSemesterChange?: (semesterId: string) => void
  onAssignmentsChanged?: () => void
} = {}) {
  const { user } = useAuth()
  const allowed = user?.role === 'DepartmentHead'
  const [offerings, setOfferings] = useState<AssignmentOffering[]>([])
  const [semesterId, setSemesterId] = useState(initialSemesterId)
  const [offeringId, setOfferingId] = useState('')
  const [assigned, setAssigned] = useState<AssignedLecturer[]>([])
  const [lookup, setLookup] = useState({ search: '', delay: SEARCH_DELAY_MS })
  const { search, delay } = lookup
  const [candidates, setCandidates] = useState<LecturerDirectoryItem[]>([])
  const [selectedLecturerId, setSelectedLecturerId] = useState('')
  const [pending, setPending] = useState<PendingChange | null>(null)
  const [addOpen, setAddOpen] = useState(false)
  const [loading, setLoading] = useState(true)
  const [loadingAssignments, setLoadingAssignments] = useState(true)
  const [searching, setSearching] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [searchError, setSearchError] = useState('')
  const [notice, setNotice] = useState('')
  const [reload, setReload] = useState(0)

  useEffect(() => {
    if (!allowed) return
    const controller = new AbortController()
    void service
      .offerings(controller.signal)
      .then((rows) => {
        if (controller.signal.aborted) return
        const enabled = rows.filter((row) => row.isOfferingEnabled && (!courseId || row.courseId === courseId))
        setOfferings(enabled)
        setSemesterId((current) =>
          enabled.some((row) => row.semesterId === current)
            ? current
            : (enabled[0]?.semesterId ?? ''),
        )
      })
      .catch((failure) => {
        if (!controller.signal.aborted)
          setError(userMessage(failure, 'Không tải được danh sách môn phụ trách.'))
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })
    return () => controller.abort()
  }, [allowed, courseId, reload])

  const semesters = useMemo(
    () => [...new Map(offerings.map((row) => [row.semesterId, row.semesterCode])).entries()],
    [offerings],
  )
  const visibleOfferings = useMemo(
    () => offerings.filter((row) => row.semesterId === semesterId),
    [offerings, semesterId],
  )
  const effectiveOfferingId = visibleOfferings.some((row) => row.courseOfferingId === offeringId)
    ? offeringId
    : (visibleOfferings[0]?.courseOfferingId ?? '')
  const offering = visibleOfferings.find((row) => row.courseOfferingId === effectiveOfferingId)

  useEffect(() => {
    if (!allowed || !effectiveOfferingId) return
    const controller = new AbortController()
    void service
      .assignments(effectiveOfferingId, controller.signal)
      .then((rows) => {
        if (!controller.signal.aborted) setAssigned(rows.filter((row) => row.isActive))
      })
      .catch((failure) => {
        if (!controller.signal.aborted)
          setError(userMessage(failure, 'Không tải được phân công hiện tại.'))
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoadingAssignments(false)
      })
    return () => controller.abort()
  }, [allowed, effectiveOfferingId, reload])

  useEffect(() => {
    if (!allowed || !effectiveOfferingId || search.trim().length < 2) return
    const controller = new AbortController()
    const timer = window.setTimeout(() => {
      void service
        .search(effectiveOfferingId, search.trim(), controller.signal)
        .then((rows) => {
          if (!controller.signal.aborted) setCandidates(rows)
        })
        .catch((failure) => {
          if (!controller.signal.aborted)
            setSearchError(userMessage(failure, 'Không tìm được giảng viên.'))
        })
        .finally(() => {
          if (!controller.signal.aborted) setSearching(false)
        })
    }, delay)
    return () => {
      window.clearTimeout(timer)
      controller.abort()
    }
  }, [allowed, delay, effectiveOfferingId, search])

  const activeIds = new Set(assigned.map((row) => row.lecturerId))
  const available = candidates.filter((candidate) => !activeIds.has(candidate.id))
  const selected = available.find((row) => row.id === selectedLecturerId)
  const disabled = saving || loadingAssignments || !!error

  function closeAddDialog() {
    setAddOpen(false)
    setLookup({ search: '', delay: SEARCH_DELAY_MS })
    setCandidates([])
    setSelectedLecturerId('')
    setSearchError('')
    setSearching(false)
  }

  function resetSelection() {
    setAssigned([])
    setLoadingAssignments(true)
    setCandidates([])
    setLookup({ search: '', delay: SEARCH_DELAY_MS })
    setSelectedLecturerId('')
    setPending(null)
    setNotice('')
    setError('')
    setSearchError('')
    setSearching(false)
  }

  function refresh() {
    resetSelection()
    setLoading(true)
    setReload((value) => value + 1)
  }

  async function confirmChange() {
    if (!pending || !effectiveOfferingId || saving) return
    const ids = assigned.map((row) => row.lecturerId)
    const next =
      pending.kind === 'add'
        ? [...ids, pending.lecturer.id]
        : ids.filter((id) => id !== pending.lecturer.id)
    setSaving(true)
    setError('')
    try {
      const result = await service.sync(effectiveOfferingId, next)
      setAssigned(result.lecturers.filter((row) => row.isActive))
      onAssignmentsChanged?.()
      setNotice(
        `${pending.kind === 'add' ? 'Đã phân công' : 'Đã gỡ phân công'} ${pending.lecturer.fullName}.`,
      )
      setPending(null)
      setSelectedLecturerId('')
    } catch (failure) {
      setError(
        userMessage(
          failure,
          'Không lưu được phân công. Vui lòng tải lại danh sách trước khi thử lại.',
        ),
      )
      setPending(null)
    } finally {
      if (pending.kind === 'add') closeAddDialog()
      setSaving(false)
    }
  }

  if (!allowed) return <p role="alert">Chỉ Trưởng bộ môn được quản lý phân công giảng viên.</p>

  return (
    <div className={courseId ? 'space-y-4' : ds.page}>
      {!courseId && <header className={cn(ds.section, ds.sectionHeader)}>
        <div className={ds.sectionTitleRow}>
          <span className={ds.sectionIconWrap}><UsersRound className={ds.sectionIcon} /></span>
          <h1 className={ds.sectionTitle}>Phân công giảng viên</h1>
        </div>
        <p className={ds.sectionHint}>
          Chọn học kỳ và môn phụ trách để thêm hoặc gỡ giảng viên đang hoạt động.
        </p>
      </header>}
      {error && (
        <div
          role="alert"
          className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-800"
        >
          {error}{' '}
          <button type="button" className={ds.outlineButton} disabled={saving} onClick={refresh}>
            Tải lại phân công
          </button>
        </div>
      )}
      {notice && (
        <p role="status" className="rounded-xl bg-emerald-50 p-3 text-sm text-emerald-800">
          {notice}
        </p>
      )}
      {loading ? (
        <p role="status" className="flex items-center justify-center gap-2 p-5 text-sm text-[#5b6b84]">
          <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" aria-hidden="true" />
          Đang tải môn phụ trách…
        </p>
      ) : offerings.length === 0 ? (
        <EmptyState icon={CalendarDays} title={courseId ? 'Môn học chưa được mở trong học kỳ nào' : 'Chưa có môn được mở thuộc phạm vi phụ trách.'} />
      ) : (
        <>
          {addOpen && !pending && (
          <FormDialog
            eyebrow="Chi tiết môn học"
            title="Thêm phân công"
            icon={UserRoundPlus}
            size="sm"
            onClose={closeAddDialog}
            footer={<DialogFooter className="bg-white sm:flex-row">
              <button type="button" className={ds.outlineButton} onClick={closeAddDialog}>Hủy</button>
              <button
                type="button"
                className={ds.primaryButton}
                disabled={disabled || !selected}
                onClick={() => selected && setPending({ kind: 'add', lecturer: selected })}
              >
                <UserRoundPlus className="size-3.5" aria-hidden="true" />
                Thêm giảng viên
              </button>
            </DialogFooter>}
          >
            <div className="space-y-3">
            <label className="block">
              <span className={fieldLabelClass(Boolean(searchError))}>Tìm giảng viên</span>
              <span className="relative mt-1.5 block">
              <input
                type="text"
                maxLength={100}
                className={cn(ds.field, 'pr-10')}
                value={search}
                disabled={disabled}
                onChange={(event) => {
                  setLookup({ search: event.target.value, delay: SEARCH_DELAY_MS })
                  setCandidates([])
                  setSelectedLecturerId('')
                  setSearchError('')
                  setSearching(event.target.value.trim().length >= 2)
                }}
                onKeyDown={(event) => {
                  if (event.key !== 'Enter' || event.nativeEvent.isComposing) return
                  event.preventDefault()
                  if (search.trim().length >= 2 && delay !== 0 && searching) {
                    setLookup({ search, delay: 0 })
                  }
                }}
                placeholder="Nhập tên hoặc email giảng viên"
              />
              {searching && (
                <span role="status" className="absolute top-1/2 right-3 -translate-y-1/2">
                  <LoaderCircle className="size-4 animate-spin" aria-hidden="true" />
                  <span className="sr-only">Đang tìm giảng viên</span>
                </span>
              )}
              {!searching && search && (
                <button
                  type="button"
                  aria-label="Xóa tìm kiếm"
                  disabled={disabled}
                  className="absolute top-1/2 right-2 grid size-7 -translate-y-1/2 place-items-center rounded-md text-[#5b6b84] hover:bg-[#E7F3FB] hover:text-[#1C4D8D]"
                  onClick={() => {
                    setLookup({ search: '', delay: SEARCH_DELAY_MS })
                    setCandidates([])
                    setSelectedLecturerId('')
                    setSearchError('')
                    setSearching(false)
                  }}
                >
                  <X className="size-4" aria-hidden="true" />
                </button>
              )}
              </span>
            </label>
            {searchError && <div role="alert"><FieldError message={searchError} /></div>}
            {!searching && available.length > 0 && (
              <div role="listbox" aria-label="Giảng viên phù hợp" className="max-h-56 space-y-1.5 overflow-y-auto">
                {available.map((row) => (
                  <button
                    key={row.id}
                    type="button"
                    role="option"
                    aria-label={`${row.fullName} — ${row.email}`}
                    aria-selected={selectedLecturerId === row.id}
                    disabled={disabled}
                    onClick={() => setSelectedLecturerId(row.id)}
                    className={cn(
                      'w-full rounded-xl border px-3 py-2.5 text-left transition',
                      selectedLecturerId === row.id
                        ? 'border-[#4988C4] bg-[#E7F3FB]'
                        : 'border-transparent bg-[#F8FBFD] hover:border-[#82aedb]/55',
                    )}
                  >
                    <span className="block text-sm font-bold text-[#0F2854]">{row.fullName}</span>
                    <span className="block text-xs [overflow-wrap:anywhere] text-[#5b6b84]">{row.email}</span>
                  </button>
                ))}
              </div>
            )}
            {search.trim().length >= 2 && !searching && !searchError && available.length === 0 && (
              <EmptyState
                compact
                icon={Search}
                title="Không có giảng viên phù hợp chưa được phân công."
                description="Thử tên hoặc email khác."
              />
            )}
            </div>
          </FormDialog>
          )}
          <section className={ds.section} aria-label="Phân công hiện tại">
            <div className={cn(ds.sectionHeader, 'flex flex-wrap items-center justify-between gap-2')}>
              <div className={ds.sectionHeading}>
                <div className={ds.sectionTitleRow}>
                  <span className={ds.sectionIconWrap}><UsersRound className={ds.sectionIcon} /></span>
                  <h2 className={ds.sectionTitle}>Danh sách giảng viên</h2>
                  {!loadingAssignments && <span className={ds.sectionBadge}>{assigned.length} giảng viên</span>}
                </div>
                <p className={ds.sectionHint}>Giảng viên được phân công theo học kỳ</p>
              </div>
              <div className="flex min-w-0 flex-wrap items-center gap-2">
                <div className="min-w-0">
                  <FilterDropMenu
                    label="Học kỳ"
                    className="w-40 max-w-full"
                    value={semesterId}
                    options={semesters.map(([value, label]) => ({ value, label }))}
                    disabled={saving}
                    onChange={(value) => {
                      resetSelection()
                      setSemesterId(value)
                      onSemesterChange?.(value)
                      setOfferingId('')
                    }}
                  />
                </div>
                {(!courseId || visibleOfferings.length > 1) && <div className="min-w-0">
                  <FilterDropMenu
                    label="Môn học"
                    className="w-40 max-w-full"
                    value={effectiveOfferingId}
                    options={visibleOfferings.map((row) => ({ value: row.courseOfferingId, label: `${row.courseCode} — ${row.courseName}` }))}
                    disabled={saving}
                    onChange={(value) => {
                      resetSelection()
                      setOfferingId(value)
                    }}
                  />
                </div>}
                <button type="button" className={ds.outlineButton} disabled={saving || loadingAssignments} onClick={refresh}>
                  <RefreshCw className="size-3.5" aria-hidden="true" />
                  Làm mới
                </button>
                <button type="button" className={ds.primaryButton} disabled={disabled} onClick={() => setAddOpen(true)}>
                  <UserRoundPlus className="size-4" aria-hidden="true" /> Thêm phân công
                </button>
              </div>
            </div>
            <div className={cn(ds.sectionBody, !loadingAssignments && assigned.length > 0 && 'py-0 sm:py-0')}>
            {loadingAssignments ? (
              <p role="status">Đang tải phân công…</p>
            ) : assigned.length === 0 ? (
              <EmptyState icon={UsersRound} title="Chưa phân công giảng viên cho môn trong học kỳ này."
                action={<button type="button" className={ds.primaryButton} disabled={disabled} onClick={() => setAddOpen(true)}>
                  <UserRoundPlus className="size-4" aria-hidden="true" /> Thêm phân công
                </button>} />
            ) : (
              <ul aria-label="Giảng viên được phân công" className="divide-y divide-[#82aedb]/55">
                {assigned.map((row) => (
                  <li
                    key={row.lecturerId}
                    className="flex flex-wrap items-center gap-x-4 gap-y-2 py-3 transition-colors hover:bg-[#F4FBFD] lg:grid lg:grid-cols-[minmax(0,1fr)_minmax(0,1.4fr)_15rem_auto]"
                  >
                    <div className="flex min-w-0 basis-full items-center gap-3">
                    <span className="grid size-9 shrink-0 place-items-center rounded-lg bg-[#E7F3FB] text-sm font-bold text-[#1C4D8D]" aria-hidden="true">
                      {row.fullName.trim().split(/\s+/).at(-1)?.slice(0, 1).toLocaleUpperCase('vi') || '?'}
                    </span>
                      <p className="text-base font-bold [overflow-wrap:anywhere] text-[#0F2854]">{row.fullName}</p>
                    </div>
                    <p className="min-w-0 text-sm [overflow-wrap:anywhere] text-[#5b6b84]">{row.email}</p>
                    <RosterDate label="Ngày phân công" value={row.assignedAt} />
                    <button
                      type="button"
                      className={cn(ds.outlineButton, 'ml-auto h-9 shrink-0 border-[#e8a8b5] bg-[#fff7f8] px-3 text-xs text-[#c2415b] hover:border-[#c2415b] hover:bg-[#ffe9ed] focus-visible:outline-[#c2415b]')}
                      disabled={disabled}
                      aria-label={`Gỡ phân công ${row.fullName}`}
                      onClick={() =>
                        setPending({
                          kind: 'remove',
                          lecturer: {
                            id: row.lecturerId,
                            fullName: row.fullName,
                            email: row.email,
                          },
                        })
                      }
                    >
                      <UserRoundMinus className="size-3.5" aria-hidden="true" /> Gỡ
                    </button>
                  </li>
                ))}
              </ul>
            )}
            </div>
          </section>
        </>
      )}
      {pending && (
        <ConfirmDialog
          title={pending.kind === 'add' ? 'Xác nhận phân công' : 'Xác nhận gỡ phân công'}
          message={`${pending.lecturer.fullName} — ${offering?.courseCode}, ${offering?.semesterCode}. ${pending.kind === 'add' ? 'Giảng viên sẽ được cấp quyền trên môn trong học kỳ này.' : 'Gỡ phân công chỉ thu hồi quyền trên môn trong học kỳ này; dữ liệu lớp và bài chấm được giữ lại.'}`}
          confirmLabel="Xác nhận"
          cancelLabel="Hủy"
          pending={saving}
          onConfirm={() => void confirmChange()}
          onCancel={() => setPending(null)}
        />
      )}
    </div>
  )
}

export default DepartmentLecturerAssignmentPage
