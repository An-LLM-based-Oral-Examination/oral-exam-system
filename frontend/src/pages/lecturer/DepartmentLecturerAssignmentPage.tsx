import { useEffect, useMemo, useState } from 'react'
import { useAuth } from '@/hooks/useAuth'
import { lecturerAssignmentService as service } from '@/services/lecturerAssignment.service'
import type {
  AssignedLecturer,
  AssignmentOffering,
  LecturerDirectoryItem,
} from '@/types/lecturerAssignment.types'
import { ConfirmDialog } from '@/design/ConfirmDialog'
import { ds } from '@/design/system'
import { userMessage } from '@/lib/userMessage'

type PendingChange = { kind: 'add' | 'remove'; lecturer: LecturerDirectoryItem }

export function DepartmentLecturerAssignmentPage() {
  const { user } = useAuth()
  const allowed = user?.role === 'DepartmentHead'
  const [offerings, setOfferings] = useState<AssignmentOffering[]>([])
  const [semesterId, setSemesterId] = useState('')
  const [offeringId, setOfferingId] = useState('')
  const [assigned, setAssigned] = useState<AssignedLecturer[]>([])
  const [search, setSearch] = useState('')
  const [candidates, setCandidates] = useState<LecturerDirectoryItem[]>([])
  const [selectedLecturerId, setSelectedLecturerId] = useState('')
  const [pending, setPending] = useState<PendingChange | null>(null)
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
        const enabled = rows.filter((row) => row.isOfferingEnabled)
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
  }, [allowed, reload])

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
    }, 250)
    return () => {
      window.clearTimeout(timer)
      controller.abort()
    }
  }, [allowed, effectiveOfferingId, search])

  const activeIds = new Set(assigned.map((row) => row.lecturerId))
  const available = candidates.filter((candidate) => !activeIds.has(candidate.id))
  const selected = available.find((row) => row.id === selectedLecturerId)
  const disabled = saving || loadingAssignments || !!error

  function resetSelection() {
    setAssigned([])
    setLoadingAssignments(true)
    setCandidates([])
    setSearch('')
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
      setSaving(false)
    }
  }

  if (!allowed) return <p role="alert">Chỉ Trưởng bộ môn được quản lý phân công giảng viên.</p>

  return (
    <div className={ds.page}>
      <header className={`${ds.section} p-5`}>
        <h1 className={ds.sectionTitle}>Phân công giảng viên</h1>
        <p className={ds.sectionHint}>
          Chọn học kỳ và môn phụ trách để thêm hoặc gỡ giảng viên đang hoạt động.
        </p>
      </header>
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
        <p role="status">Đang tải môn phụ trách…</p>
      ) : offerings.length === 0 ? (
        <p>
          Chưa có môn được mở thuộc phạm vi phụ trách. Liên hệ quản trị viên để kiểm tra phân công
          môn.
        </p>
      ) : (
        <>
          <section className={`${ds.section} grid gap-4 p-5 sm:grid-cols-2`}>
            <label>
              Học kỳ
              <select
                className={ds.field}
                value={semesterId}
                disabled={saving}
                onChange={(event) => {
                  resetSelection()
                  setSemesterId(event.target.value)
                  setOfferingId('')
                }}
              >
                {semesters.map(([id, code]) => (
                  <option key={id} value={id}>
                    {code}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Môn học
              <select
                className={ds.field}
                value={effectiveOfferingId}
                disabled={saving}
                onChange={(event) => {
                  resetSelection()
                  setOfferingId(event.target.value)
                }}
              >
                {visibleOfferings.map((row) => (
                  <option key={row.courseOfferingId} value={row.courseOfferingId}>
                    {row.courseCode} — {row.courseName}
                  </option>
                ))}
              </select>
            </label>
          </section>
          <section className={`${ds.section} space-y-4 p-5`} aria-label="Thêm phân công">
            <label className="block">
              Tìm giảng viên theo tên hoặc email
              <input
                type="search"
                maxLength={100}
                className={ds.field}
                value={search}
                disabled={disabled}
                onChange={(event) => {
                  setSearch(event.target.value)
                  setCandidates([])
                  setSelectedLecturerId('')
                  setSearchError('')
                  setSearching(event.target.value.trim().length >= 2)
                }}
                placeholder="Nhập ít nhất 2 ký tự"
              />
            </label>
            {searchError && <p role="alert">{searchError}</p>}
            <label className="block">
              Giảng viên đang hoạt động
              <select
                className={ds.field}
                value={selectedLecturerId}
                disabled={disabled || searching}
                onChange={(event) => setSelectedLecturerId(event.target.value)}
              >
                <option value="">{searching ? 'Đang tìm…' : 'Chọn giảng viên'}</option>
                {available.map((row) => (
                  <option key={row.id} value={row.id}>
                    {row.fullName} — {row.email}
                  </option>
                ))}
              </select>
            </label>
            {search.trim().length >= 2 && !searching && !searchError && available.length === 0 && (
              <p>Không có giảng viên phù hợp chưa được phân công. Thử tên hoặc email khác.</p>
            )}
            <button
              type="button"
              className={ds.primaryButton}
              disabled={disabled || !selected}
              onClick={() => selected && setPending({ kind: 'add', lecturer: selected })}
            >
              Thêm giảng viên
            </button>
          </section>
          <section className={`${ds.section} p-5`} aria-label="Phân công hiện tại">
            <h2 className={ds.sectionTitle}>Giảng viên được phân công — {offering?.courseCode}</h2>
            {loadingAssignments ? (
              <p role="status">Đang tải phân công…</p>
            ) : assigned.length === 0 ? (
              <p>Chưa phân công giảng viên cho môn trong học kỳ này.</p>
            ) : (
              <ul className="mt-4 space-y-3">
                {assigned.map((row) => (
                  <li
                    key={row.lecturerId}
                    className="flex flex-wrap items-center justify-between gap-3 rounded-xl border p-3"
                  >
                    <div>
                      <p className="font-semibold">{row.fullName}</p>
                      <p className="text-sm text-slate-600">{row.email}</p>
                    </div>
                    <button
                      type="button"
                      className={ds.outlineButton}
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
                      Gỡ phân công
                    </button>
                  </li>
                ))}
              </ul>
            )}
            <button
              type="button"
              className={`${ds.outlineButton} mt-4`}
              disabled={saving || loadingAssignments}
              onClick={refresh}
            >
              Làm mới danh sách
            </button>
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
