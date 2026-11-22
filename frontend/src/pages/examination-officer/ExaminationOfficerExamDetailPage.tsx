import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import {
  ArrowLeft,
  Building2,
  CalendarDays,
  CheckCircle2,
  KeyRound,
  Laptop,
  LoaderCircle,
  RefreshCw,
  ScanSearch,
  Shuffle,
  TicketCheck,
  Trash2,
} from 'lucide-react'
import { officialExamService } from '@/services/officialExam.service'
import { proctorService } from '@/services/proctor.service'
import type { ShiftStaffCandidate } from '@/services/course.service'
import type { ProctorSeatItem } from '@/types/proctor.types'
import type {
  ExamAttemptType,
  ExamSet,
  ExamShift,
  OfficialExamEnrollmentEstimate,
  OfficialExamSession,
  OfficialShiftStatusValue,
} from '@/types/officialExam.types'
import { PageBanner, SectionHeader } from '@/design/ui'
import { ConfirmDialog } from '@/design/ConfirmDialog'
import { DialogFooter, FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'
import { EmptyState } from '@/components/common/EmptyState'
import { userMessage } from '@/lib/userMessage'
import { toast } from '@/components/common/toastStore'
import {
  DEFAULT_SHIFT_CAPACITY,
  MAX_SHIFT_CAPACITY,
  MIN_SHIFT_CAPACITY,
  getShiftCapacityMetrics,
  validateShiftCapacityInput,
} from '@/lib/shiftCapacity'
import { CourseAttemptTabs } from '@/components/examination-officer/CourseAttemptTabs'
import { RoomEstimateBanner } from '@/components/examination-officer/RoomEstimateBanner'
import { CandidateRosterImportPanel } from '@/components/examination-officer/CandidateRosterImportPanel'
import { BatchShiftCreatorModal } from '@/components/examination-officer/BatchShiftCreatorModal'
import { AutoAllocateWizardDialog } from '@/components/examination-officer/AutoAllocateWizardDialog'
import {
  UnlockDeviceDialog,
  type UnlockDeviceTarget,
} from '@/components/examination-officer/UnlockDeviceDialog'
import {
  resolveShiftStaffPools,
  toDateTimeLocalValue,
} from '@/components/examination-officer/shiftStaffHelpers'

const NON_UNLOCKABLE_STATUSES = new Set([
  'SUBMITTED',
  'AI_GRADED',
  'AUDITED',
  'PUBLISHED',
  'LOCKED',
])

const SHIFT_STATUS_OPTIONS: Array<{ value: OfficialShiftStatusValue; label: string }> = [
  { value: 'scheduled', label: 'scheduled (Đã lên lịch)' },
  { value: 'in_progress', label: 'in_progress (Đang diễn ra)' },
  { value: 'completed', label: 'completed (Đã kết thúc)' },
  { value: 'cancelled', label: 'cancelled (Đã hủy)' },
]

export function ExaminationOfficerExamDetailPage() {
  const { sessionId = '' } = useParams<{ sessionId: string }>()
  const navigate = useNavigate()
  const [allSessions, setAllSessions] = useState<OfficialExamSession[]>([])
  const [session, setSession] = useState<OfficialExamSession | null>(null)
  const [shifts, setShifts] = useState<ExamShift[]>([])
  const [examSets, setExamSets] = useState<ExamSet[]>([])
  const [estimate, setEstimate] = useState<OfficialExamEnrollmentEstimate | null>(null)
  const [proctors, setProctors] = useState<ShiftStaffCandidate[]>([])
  const [examiners, setExaminers] = useState<ShiftStaffCandidate[]>([])
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)
  const [creatingAttempt, setCreatingAttempt] = useState(false)

  const [showBatchShiftModal, setShowBatchShiftModal] = useState(false)
  const [showAutoAllocateModal, setShowAutoAllocateModal] = useState(false)

  const [editingShift, setEditingShift] = useState<ExamShift | null>(null)
  const [editMaxSeats, setEditMaxSeats] = useState(String(DEFAULT_SHIFT_CAPACITY))
  const [editShiftName, setEditShiftName] = useState('')
  const [editRoomLab, setEditRoomLab] = useState('')
  const [editStartTime, setEditStartTime] = useState('')
  const [editEndTime, setEditEndTime] = useState('')
  const [editBusy, setEditBusy] = useState(false)
  const [editError, setEditError] = useState('')

  const [confirmPublishShift, setConfirmPublishShift] = useState<ExamShift | null>(null)
  const [publishBusy, setPublishBusy] = useState(false)
  const [confirmDeleteShift, setConfirmDeleteShift] = useState<ExamShift | null>(null)
  const [deleteBusy, setDeleteBusy] = useState(false)
  const [distributingShiftId, setDistributingShiftId] = useState<string | null>(null)
  const [statusShift, setStatusShift] = useState<ExamShift | null>(null)
  const [nextShiftStatus, setNextShiftStatus] = useState<OfficialShiftStatusValue>('in_progress')
  const [statusBusy, setStatusBusy] = useState(false)
  const [devicesShift, setDevicesShift] = useState<ExamShift | null>(null)
  const [deviceSeats, setDeviceSeats] = useState<ProctorSeatItem[]>([])
  const [loadingDevices, setLoadingDevices] = useState(false)
  const [unlockTarget, setUnlockTarget] = useState<UnlockDeviceTarget | null>(null)

  const openDevicesDialog = async (shift: ExamShift) => {
    setDevicesShift(shift)
    setDeviceSeats([])
    if (typeof proctorService.getShiftAttendance !== 'function') return
    setLoadingDevices(true)
    try {
      const monitor = await proctorService.getShiftAttendance(shift.id)
      setDeviceSeats(monitor.seats.filter((seat) => seat.isOccupied && Boolean(seat.ticketId)))
    } catch (loadError) {
      toast.error(userMessage(loadError, 'Không tải được danh sách thiết bị BYOD của ca thi.'))
    } finally {
      setLoadingDevices(false)
    }
  }

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const estimatePromise =
        typeof officialExamService.getEnrollmentEstimate === 'function'
          ? officialExamService.getEnrollmentEstimate(sessionId).catch(() => null)
          : Promise.resolve(null)

      const [sessions, shiftResult, estimateResult] = await Promise.all([
        officialExamService.getSessions(),
        officialExamService.getShifts({ sessionId }),
        estimatePromise,
      ])
      const currentSession = sessions.find((item) => item.id === sessionId) ?? null
      setAllSessions(sessions)
      setSession(currentSession)
      setShifts(shiftResult)
      setEstimate(estimateResult)
      setExamSets(
        currentSession
          ? await officialExamService.getExamSets({
              courseId: currentSession.courseId,
              sessionId,
              status: 'HANDED_OVER',
            })
          : [],
      )
      if (currentSession?.courseId) {
        const pools = await resolveShiftStaffPools(
          currentSession.courseId,
          currentSession.semesterId ?? '',
          shiftResult,
        )
        setProctors(pools.proctors)
        setExaminers(pools.examiners)
      }
    } catch (loadError) {
      setError(userMessage(loadError, 'Không tải được chi tiết kỳ thi.'))
    } finally {
      setLoading(false)
    }
  }, [sessionId])

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void load()
    }, 0)
    return () => window.clearTimeout(timer)
  }, [load])

  const siblingAttempts = useMemo(() => {
    if (!session) return { fe: null, re: null }
    const sameCourse = allSessions.filter(
      (item) =>
        item.courseId === session.courseId &&
        (!session.semesterId || !item.semesterId || item.semesterId === session.semesterId),
    )
    const fe =
      sameCourse.find((item) => (item.examAttemptType ?? 'FE') === 'FE') ??
      ((session.examAttemptType ?? 'FE') === 'FE' ? session : null)
    const re =
      sameCourse.find((item) => item.examAttemptType === 'RE') ??
      (session.examAttemptType === 'RE' ? session : null)
    return { fe, re }
  }, [allSessions, session])

  const handleCreateSiblingAttempt = async (attemptType: ExamAttemptType) => {
    if (!session || creatingAttempt) return
    setCreatingAttempt(true)
    setError('')
    try {
      let createdId = ''
      if (
        session.semesterId &&
        typeof officialExamService.batchCreateSessions === 'function'
      ) {
        const batchResult = await officialExamService.batchCreateSessions(session.semesterId, {
          examDate: session.examDate.slice(0, 10),
          examAttemptType: attemptType,
          examInputMode: session.examInputMode,
          transcriptBufferSeconds: session.transcriptBufferSeconds,
          skipExisting: false,
          courses: [{ courseId: session.courseId, examAttemptType: attemptType }],
        })
        createdId = batchResult.sessions[0]?.id ?? ''
      } else {
        const created = await officialExamService.createSession({
          courseId: session.courseId,
          semesterId: session.semesterId ?? null,
          examAttemptType: attemptType,
          examStructureId: session.examStructureId || null,
          title: `${session.title.replace(/\s*\((FE|RE)\)\s*$/i, '')} (${attemptType})`,
          examDate: session.examDate.slice(0, 10),
          hasFollowUp: session.hasFollowUp,
          maxFollowUpQuestions: session.maxFollowUpQuestions,
          examInputMode: session.examInputMode,
          transcriptBufferSeconds: session.transcriptBufferSeconds,
        })
        createdId = created.id
      }
      toast.success(`Đã mở đợt thi ${attemptType} độc lập cho môn học.`)
      if (createdId) {
        navigate(`/examination-officer/exams/${createdId}`)
      } else {
        await load()
      }
    } catch (createError) {
      const message = userMessage(createError, `Không thể mở đợt thi ${attemptType}.`)
      setError(message)
      toast.error(message)
    } finally {
      setCreatingAttempt(false)
    }
  }

  const executePublishShift = async () => {
    if (!confirmPublishShift || publishBusy) return
    const shiftId = confirmPublishShift.id
    setPublishBusy(true)
    setError('')
    try {
      const result = await officialExamService.publishShift(shiftId)
      setShifts((current) =>
        current.map((shift) =>
          shift.id === shiftId ? { ...shift, isExamPublished: true } : shift,
        ),
      )
      setConfirmPublishShift(null)
      toast.success(result.message || 'Đã xuất bản đề cho ca thi.')
    } catch (publishError) {
      const message = userMessage(publishError, 'Không thể xuất bản đề cho ca thi.')
      setError(message)
      toast.error(message)
      setConfirmPublishShift(null)
    } finally {
      setPublishBusy(false)
    }
  }

  const executeDistributeTickets = async (shift: ExamShift) => {
    if (distributingShiftId || typeof officialExamService.distributeShiftTickets !== 'function') {
      return
    }
    setDistributingShiftId(shift.id)
    setError('')
    try {
      const result = await officialExamService.distributeShiftTickets(shift.id)
      toast.success(
        result.message || `Đã phát bộ đề không trùng cho ${result.totalDistributed} thí sinh.`,
      )
      await load()
    } catch (distributeError) {
      const message = userMessage(distributeError, 'Không thể phát bộ đề cho ca thi.')
      setError(message)
      toast.error(message)
    } finally {
      setDistributingShiftId(null)
    }
  }

  const executeUpdateShiftStatus = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (
      !statusShift ||
      statusBusy ||
      typeof officialExamService.updateShiftStatus !== 'function'
    ) {
      return
    }
    setStatusBusy(true)
    setError('')
    try {
      const updated = await officialExamService.updateShiftStatus(statusShift.id, nextShiftStatus)
      setShifts((current) =>
        current.map((item) => (item.id === updated.id ? { ...item, status: updated.status } : item)),
      )
      setStatusShift(null)
      toast.success(`Đã chuyển trạng thái ca thi sang ${updated.status}.`)
    } catch (statusError) {
      const message = userMessage(statusError, 'Không thể cập nhật trạng thái ca thi.')
      setError(message)
      toast.error(message)
    } finally {
      setStatusBusy(false)
    }
  }

  const executeDeleteShift = async () => {
    if (
      !confirmDeleteShift ||
      deleteBusy ||
      typeof officialExamService.deleteShift !== 'function'
    ) {
      return
    }
    setDeleteBusy(true)
    setError('')
    try {
      await officialExamService.deleteShift(confirmDeleteShift.id)
      setConfirmDeleteShift(null)
      toast.success('Đã xóa ca thi chưa phân bổ vé.')
      await load()
    } catch (deleteError) {
      const message = userMessage(deleteError, 'Không thể xóa ca thi.')
      setError(message)
      toast.error(message)
      setConfirmDeleteShift(null)
    } finally {
      setDeleteBusy(false)
    }
  }

  const openEditShift = (shift: ExamShift) => {
    setEditingShift(shift)
    setEditShiftName(shift.shiftName || '')
    setEditRoomLab(shift.roomLab || '')
    setEditStartTime(toDateTimeLocalValue(shift.startTime))
    setEditEndTime(toDateTimeLocalValue(shift.endTime))
    setEditMaxSeats(String(shift.maxSeats ?? DEFAULT_SHIFT_CAPACITY))
    setEditError('')
  }

  const submitEditShift = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!editingShift) return
    const capacityCheck = validateShiftCapacityInput(editMaxSeats, {
      defaultWhenEmpty: editingShift.maxSeats ?? DEFAULT_SHIFT_CAPACITY,
    })
    if (capacityCheck.error || capacityCheck.value === null) {
      setEditError(capacityCheck.error)
      toast.error(capacityCheck.error)
      return
    }
    setEditBusy(true)
    setEditError('')
    try {
      await officialExamService.updateShift(editingShift.id, {
        shiftName: editShiftName.trim() || editingShift.shiftName,
        roomLab: editRoomLab.trim() || editingShift.roomLab,
        startTime: new Date(editStartTime || editingShift.startTime).toISOString(),
        endTime: new Date(editEndTime || editingShift.endTime).toISOString(),
        proctorUserId: editingShift.proctorUserId ?? '',
        examinerUserId: editingShift.examinerUserId ?? '',
        maxSeats: capacityCheck.value,
      })
      toast.success('Đã cập nhật ca thi thành công.')
      setEditingShift(null)
      await load()
    } catch (updateError) {
      const message = userMessage(updateError, 'Không cập nhật được ca thi.')
      setEditError(message)
      toast.error(message)
    } finally {
      setEditBusy(false)
    }
  }

  const editCapacityValidation = validateShiftCapacityInput(editMaxSeats, {
    defaultWhenEmpty: editingShift?.maxSeats ?? DEFAULT_SHIFT_CAPACITY,
  })

  const sessionTitle = useMemo(
    () => session?.title || sessionId || 'Kỳ thi chính thức',
    [session, sessionId],
  )
  const activeAttemptType: ExamAttemptType = session?.examAttemptType === 'RE' ? 'RE' : 'FE'

  return (
    <div className={ds.page}>
      <PageBanner
        icon={CalendarDays}
        eyebrow={`CHI TIẾT KỲ THI · ĐỢT ${activeAttemptType}`}
        title={sessionTitle}
        description="Vận hành trọn gói Bước 2–6: Import DSSV, ước tính chuẩn 24 SV/phòng, tạo ca hàng loạt, chia phòng CSPRNG cân bằng ca cuối, phát đề & xuất bản."
      />

      <div className="flex flex-wrap gap-2">
        <Link to="/examination-officer/exams" className={ds.outlineButton}>
          <ArrowLeft className="size-4" /> Danh sách kỳ thi
        </Link>
        <button
          type="button"
          onClick={() => void load()}
          disabled={loading}
          className={ds.outlineButton}
        >
          <RefreshCw className={loading ? 'size-4 animate-spin' : 'size-4'} /> Làm mới
        </button>
      </div>

      {error ? (
        <div
          role="alert"
          className="rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-4 py-3 text-sm font-semibold text-[#a43d4b]"
        >
          {error}
        </div>
      ) : null}

      {session ? (
        <CourseAttemptTabs
          activeSession={session}
          feSession={siblingAttempts.fe}
          reSession={siblingAttempts.re}
          creatingAttempt={creatingAttempt}
          onCreateAttempt={(attempt) => void handleCreateSiblingAttempt(attempt)}
        />
      ) : null}

      <RoomEstimateBanner estimate={estimate} loading={loading} />

      {sessionId && typeof officialExamService.getSessionCandidates === 'function' ? (
        <CandidateRosterImportPanel
          sessionId={sessionId}
          examAttemptType={activeAttemptType}
          onRosterChanged={load}
        />
      ) : null}

      <section className={ds.section}>
        <SectionHeader
          icon={CalendarDays}
          title="Các ca thi & Phòng Lab (Bước 4–5)"
          badge={shifts.length}
          hint={
            session
              ? `${session.examDate} · ${session.status} · Follow-up: ${session.hasFollowUp ? `Bật (${session.maxFollowUpQuestions} câu)` : 'Tắt'}`
              : 'Đang tải thông tin kỳ thi'
          }
          action={
            <div className="flex flex-wrap gap-2">
              {typeof officialExamService.batchOpenShifts === 'function' ? (
                <button
                  type="button"
                  onClick={() => setShowBatchShiftModal(true)}
                  className={ds.outlineButton}
                >
                  <Building2 className="size-4" /> Tạo ca hàng loạt (K phòng)
                </button>
              ) : null}
              {typeof officialExamService.autoAllocateStudents === 'function' ? (
                <button
                  type="button"
                  onClick={() => setShowAutoAllocateModal(true)}
                  disabled={shifts.length === 0}
                  className={ds.primaryButton}
                >
                  <Shuffle className="size-4" /> Chia phòng ngẫu nhiên (CSPRNG)
                </button>
              ) : null}
            </div>
          }
        />
        <div className="grid gap-3 p-5 md:grid-cols-2">
          {shifts.map((shift) => {
            const capacity = getShiftCapacityMetrics(shift)
            const isPublished = Boolean(shift.isExamPublished)
            const canDeleteShift =
              typeof officialExamService.deleteShift === 'function' &&
              capacity.assignedTicketsCount === 0 &&
              !isPublished
            return (
              <article
                key={shift.id}
                className="rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] p-4"
              >
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <h3 className="font-bold text-[#0F2854]">{shift.shiftName || shift.id}</h3>
                    <p className="mt-1 text-xs text-[#5b6b84]">
                      {shift.roomLab} · {shift.startTime}
                    </p>
                    <p className="mt-1 text-xs text-[#5b6b84]">
                      Giám thị: {shift.proctorName || shift.proctorUserId || 'Chưa phân công'} · Giám
                      khảo: {shift.examinerName || shift.examinerUserId || 'Chưa phân công'}
                    </p>
                    <p className="mt-1.5 text-xs font-semibold text-[#245997]">
                      Sức chứa tối đa: {capacity.maxSeats ?? '—'} · Số vé đã phát:{' '}
                      {capacity.assignedTicketsCount} · Số chỗ còn lại:{' '}
                      {capacity.remainingSeats ?? '—'}
                    </p>
                  </div>
                  <div className="flex flex-col items-end gap-1">
                    <span className={ds.infoChip}>{shift.status}</span>
                    {isPublished ? <span className={ds.successChip}>Đề đã mở</span> : null}
                  </div>
                </div>
                <div className="mt-4 flex flex-wrap gap-2">
                  <Link to={`/examination-officer/monitor/${shift.id}`} className={ds.outlineButton}>
                    <ScanSearch className="size-4" /> Giám sát
                  </Link>
                  <button
                    type="button"
                    onClick={() => void openDevicesDialog(shift)}
                    className={ds.outlineButton}
                  >
                    <Laptop className="size-4" /> Thiết bị BYOD
                  </button>
                  <Link to={`/examination-officer/grades/${shift.id}`} className={ds.outlineButton}>
                    <TicketCheck className="size-4" /> Điểm
                  </Link>
                  <Link
                    to={`/examination-officer/shifts/${shift.id}/tickets/import`}
                    className={ds.outlineButton}
                  >
                    <TicketCheck className="size-4" /> Import DSSV
                  </Link>
                  {typeof officialExamService.distributeShiftTickets === 'function' ? (
                    <button
                      type="button"
                      disabled={distributingShiftId === shift.id || capacity.assignedTicketsCount === 0}
                      onClick={() => void executeDistributeTickets(shift)}
                      className={ds.outlineButton}
                    >
                      {distributingShiftId === shift.id ? (
                        <LoaderCircle className="size-4 animate-spin" />
                      ) : (
                        <Shuffle className="size-4" />
                      )}
                      Phát bộ đề
                    </button>
                  ) : null}
                  {typeof officialExamService.updateShiftStatus === 'function' ? (
                    <button
                      type="button"
                      onClick={() => {
                        setStatusShift(shift)
                        setNextShiftStatus(
                          shift.status.toLowerCase() === 'scheduled' ? 'in_progress' : 'completed',
                        )
                      }}
                      className={ds.outlineButton}
                    >
                      Đổi trạng thái
                    </button>
                  ) : null}
                  <button
                    type="button"
                    onClick={() => openEditShift(shift)}
                    className={ds.outlineButton}
                  >
                    Cập nhật ca
                  </button>
                  <button
                    type="button"
                    onClick={() => setConfirmPublishShift(shift)}
                    disabled={isPublished || publishBusy}
                    className={ds.primaryButton}
                  >
                    <CheckCircle2 className="size-4" />{' '}
                    {isPublished ? 'Đã xuất bản' : 'Xuất bản đề'}
                  </button>
                  {canDeleteShift ? (
                    <button
                      type="button"
                      onClick={() => setConfirmDeleteShift(shift)}
                      className="inline-flex min-h-10 items-center gap-1.5 rounded-xl border border-[#f3c2c8] bg-white px-3 text-xs font-bold text-[#a43d4b] hover:bg-[#fdecec]"
                    >
                      <Trash2 className="size-3.5" /> Xóa ca
                    </button>
                  ) : null}
                </div>
              </article>
            )
          })}
          {!loading && shifts.length === 0 ? (
            <EmptyState
              compact
              icon={CalendarDays}
              title="Chưa có ca thi"
              description="Kỳ thi này chưa có ca thi được cấu hình. Nhấn “Tạo ca hàng loạt (K phòng)” để mở phòng Lab."
            />
          ) : null}
        </div>
      </section>

      <section className={ds.section}>
        <SectionHeader
          icon={TicketCheck}
          title="Bộ đề đã bàn giao"
          badge={examSets.length}
          hint="Chỉ hiển thị bộ đề HANDED_OVER trả về từ backend."
        />
        <div className="grid gap-3 p-5 md:grid-cols-2 xl:grid-cols-3">
          {examSets.map((examSet) => (
            <article
              key={examSet.id}
              className="rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] p-4"
            >
              <div className="flex items-center justify-between gap-3">
                <strong className="text-[#0F2854]">{examSet.setCode}</strong>
                <span className={ds.successChip}>{examSet.status}</span>
              </div>
              <p className="mt-2 text-xs text-[#5b6b84]">
                {examSet.questions.length} câu ·{' '}
                {examSet.handedOverAt ? `Bàn giao ${examSet.handedOverAt}` : 'Đã bàn giao'}
              </p>
            </article>
          ))}
          {!loading && examSets.length === 0 ? (
            <EmptyState
              compact
              icon={TicketCheck}
              title="Chưa có bộ đề bàn giao"
              description="Backend chưa trả về bộ đề HANDED_OVER cho kỳ thi này."
            />
          ) : null}
        </div>
      </section>

      {showBatchShiftModal ? (
        <BatchShiftCreatorModal
          open
          sessionId={sessionId}
          defaultExamDate={session?.examDate}
          recommendedRooms={estimate?.recommendedAdditionalRooms || estimate?.recommendedTotalRooms || 1}
          proctors={proctors}
          examiners={examiners}
          onClose={() => setShowBatchShiftModal(false)}
          onCreated={load}
        />
      ) : null}

      {showAutoAllocateModal ? (
        <AutoAllocateWizardDialog
          open
          sessionId={sessionId}
          shifts={shifts}
          estimate={estimate}
          onClose={() => setShowAutoAllocateModal(false)}
          onAllocated={load}
        />
      ) : null}

      {confirmPublishShift ? (
        <ConfirmDialog
          variant="calm"
          tone="warning"
          eyebrow="XUẤT BẢN ĐỀ CA THI"
          title="Xuất bản đề cho ca thi này?"
          message={`Sau khi xuất bản đề cho ${confirmPublishShift.shiftName || confirmPublishShift.id} (${confirmPublishShift.roomLab}), Kiosk có thể tải đề thi khi ca thi chuyển sang trạng thái đang diễn ra.`}
          confirmLabel="Xác nhận xuất bản"
          cancelLabel="Hủy"
          pending={publishBusy}
          onConfirm={() => void executePublishShift()}
          onCancel={() => !publishBusy && setConfirmPublishShift(null)}
        />
      ) : null}

      {confirmDeleteShift ? (
        <ConfirmDialog
          variant="calm"
          tone="danger"
          eyebrow="XÓA CA THI TRỐNG"
          title="Xóa ca thi chưa phân bổ vé?"
          message={`Bạn chắc chắn muốn xóa ${confirmDeleteShift.shiftName || confirmDeleteShift.id} (${confirmDeleteShift.roomLab})? Thao tác không thể hoàn tác.`}
          confirmLabel="Xác nhận xóa ca"
          cancelLabel="Hủy"
          pending={deleteBusy}
          onConfirm={() => void executeDeleteShift()}
          onCancel={() => !deleteBusy && setConfirmDeleteShift(null)}
        />
      ) : null}

      {statusShift ? (
        <FormDialog
          open
          align="center"
          size="sm"
          variant="calm"
          icon={CalendarDays}
          eyebrow="VÒNG ĐỜI CA THI MF-04"
          title="Chuyển trạng thái ca thi"
          busy={statusBusy}
          onClose={() => !statusBusy && setStatusShift(null)}
          footer={
            <DialogFooter className="justify-end bg-white">
              <button
                type="button"
                className={ds.outlineButton}
                onClick={() => setStatusShift(null)}
                disabled={statusBusy}
              >
                Hủy
              </button>
              <button
                type="submit"
                form="detail-shift-status-form"
                disabled={statusBusy}
                className={ds.primaryButton}
              >
                {statusBusy ? <LoaderCircle className="size-4 animate-spin" /> : null}
                Cập nhật trạng thái
              </button>
            </DialogFooter>
          }
        >
          <form
            id="detail-shift-status-form"
            onSubmit={executeUpdateShiftStatus}
            className="grid gap-3"
          >
            <p className="text-xs text-[#5b6b84]">
              Ca thi: <strong className="text-[#0F2854]">{statusShift.shiftName}</strong> (
              {statusShift.roomLab}) · Hiện tại: <strong>{statusShift.status}</strong>
            </p>
            <label className="grid gap-1.5 text-xs font-bold text-[#52657d]" htmlFor="next-shift-status">
              Trạng thái mới *
              <select
                id="next-shift-status"
                className={ds.field}
                value={nextShiftStatus}
                onChange={(event) =>
                  setNextShiftStatus(event.target.value as OfficialShiftStatusValue)
                }
              >
                {SHIFT_STATUS_OPTIONS.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </label>
          </form>
        </FormDialog>
      ) : null}

      {editingShift ? (
        <FormDialog
          open
          align="center"
          size="md"
          eyebrow="CẤU HÌNH CA THI MF-04"
          title="Cập nhật ca thi"
          icon={CalendarDays}
          busy={editBusy}
          onClose={() => !editBusy && setEditingShift(null)}
          footer={
            <DialogFooter className="justify-end bg-white">
              <button
                type="button"
                className={ds.outlineButton}
                onClick={() => setEditingShift(null)}
                disabled={editBusy}
              >
                Hủy
              </button>
              <button
                type="submit"
                form="detail-update-shift-form"
                disabled={editBusy}
                className={ds.primaryButton}
              >
                {editBusy ? <LoaderCircle className="size-4 animate-spin" /> : null}
                {editBusy ? 'Đang lưu…' : 'Lưu cập nhật ca thi'}
              </button>
            </DialogFooter>
          }
        >
          <form id="detail-update-shift-form" onSubmit={submitEditShift} className="grid gap-4">
            {editError ? (
              <div
                role="alert"
                className="rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-4 py-3 text-sm font-semibold text-[#a43d4b]"
              >
                {editError}
              </div>
            ) : null}
            <label
              className="grid gap-1.5 text-xs font-bold text-[#52657d]"
              htmlFor="detail-edit-shift-name"
            >
              Tên ca *
              <input
                id="detail-edit-shift-name"
                required
                className={ds.field}
                value={editShiftName}
                onChange={(event) => setEditShiftName(event.target.value)}
              />
            </label>
            <label
              className="grid gap-1.5 text-xs font-bold text-[#52657d]"
              htmlFor="detail-edit-shift-room"
            >
              Phòng Lab *
              <input
                id="detail-edit-shift-room"
                required
                className={ds.field}
                value={editRoomLab}
                onChange={(event) => setEditRoomLab(event.target.value)}
              />
            </label>
            <label
              className="grid gap-1.5 text-xs font-bold text-[#52657d]"
              htmlFor="detail-edit-max-seats"
            >
              Sức chứa tối đa (maxSeats) *
              <input
                id="detail-edit-max-seats"
                type="number"
                min={MIN_SHIFT_CAPACITY}
                max={MAX_SHIFT_CAPACITY}
                step={1}
                className={ds.field}
                value={editMaxSeats}
                onChange={(event) => setEditMaxSeats(event.target.value)}
              />
              {editMaxSeats.trim() !== '' && editCapacityValidation.error ? (
                <span role="alert" className="text-[11px] font-semibold text-[#a43d4b]">
                  {editCapacityValidation.error}
                </span>
              ) : null}
            </label>
          </form>
        </FormDialog>
      ) : null}
      {devicesShift ? (
        <FormDialog
          open
          align="center"
          size="lg"
          variant="calm"
          icon={Laptop}
          eyebrow="QUẢN LÝ THIẾT BỊ BYOD · KHẢO THÍ"
          title={`Thiết bị BYOD · ${devicesShift.shiftName} (${devicesShift.roomLab})`}
          onClose={() => setDevicesShift(null)}
          footer={
            <DialogFooter variant="calm">
              <button
                type="button"
                className={ds.outlineButton}
                onClick={() => void openDevicesDialog(devicesShift)}
                disabled={loadingDevices}
              >
                <RefreshCw className={loadingDevices ? 'size-4 animate-spin' : 'size-4'} /> Làm mới
              </button>
              <button
                type="button"
                className={ds.primaryButton}
                onClick={() => setDevicesShift(null)}
              >
                Đóng
              </button>
            </DialogFooter>
          }
        >
          {loadingDevices ? (
            <div className="flex items-center justify-center gap-2 py-8 text-xs font-semibold text-[#5b6b84]">
              <LoaderCircle className="size-5 animate-spin text-[#1C4D8D]" />
              Đang tải danh sách thiết bị đang gắn trong ca…
            </div>
          ) : deviceSeats.length === 0 ? (
            <EmptyState
              compact
              icon={Laptop}
              title="Chưa có thí sinh gắn thiết bị"
              description="Ca thi hiện chưa có vé thi được cấp hoặc chưa có thí sinh check-in BYOD."
            />
          ) : (
            <div className="max-h-96 space-y-2 overflow-y-auto">
              {deviceSeats.map((seat) => {
                const canUnlock =
                  Boolean(seat.ticketId && seat.deviceId) &&
                  !seat.isSuspended &&
                  !NON_UNLOCKABLE_STATUSES.has((seat.status || '').toUpperCase())
                return (
                  <div
                    key={seat.ticketId ?? seat.seatNumber}
                    className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] px-3.5 py-2.5 text-xs"
                  >
                    <div className="min-w-0">
                      <strong className="text-[#0F2854]">
                        Ghế #{String(seat.seatNumber).padStart(2, '0')} · {seat.studentCode}
                        {seat.fullName ? ` — ${seat.fullName}` : ''}
                      </strong>
                      <p className="mt-0.5 font-mono text-[11px] text-[#5b6b84]">
                        Thiết bị: {seat.deviceId || 'Chưa check-in'} · Trạng thái:{' '}
                        {seat.status || 'SCHEDULED'} · Mở khóa: {seat.deviceUnlockCount ?? 0} lần
                      </p>
                    </div>
                    <button
                      type="button"
                      disabled={!canUnlock}
                      onClick={() => {
                        if (!seat.ticketId) return
                        setUnlockTarget({
                          ticketId: seat.ticketId,
                          studentCode: seat.studentCode || seat.studentId || 'Thí sinh',
                          fullName: seat.fullName,
                          seatNumber: seat.seatNumber,
                          deviceId: seat.deviceId,
                          deviceUnlockCount: seat.deviceUnlockCount,
                        })
                      }}
                      className={ds.outlineButton}
                    >
                      <KeyRound className="size-3.5" /> Mở khóa máy
                    </button>
                  </div>
                )
              })}
            </div>
          )}
        </FormDialog>
      ) : null}

      <UnlockDeviceDialog
        open={Boolean(unlockTarget)}
        target={unlockTarget}
        onClose={() => setUnlockTarget(null)}
        onUnlocked={async () => {
          if (devicesShift) await openDevicesDialog(devicesShift)
          await load()
        }}
      />
    </div>
  )
}
