import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react'
import { CalendarPlus, ClipboardList, LoaderCircle, Plus, RefreshCw, Ticket, UsersRound } from 'lucide-react'
import { useAuth } from '@/hooks/useAuth'
import { courseService } from '@/services/course.service'
import { officialExamService } from '@/services/officialExam.service'
import { CLO_BLOOM_LEVELS, type CloBloomLevel } from '@/types/courseClo.types'
import type { ExamStructure } from '@/types/officialExam.types'
import { cn } from '@/lib/utils'
import { EmptyState } from '@/components/common/EmptyState'
import { toast } from '@/components/common/toastStore'
import { BannerFact, LecturerPageBanner, SectionHeader } from '@/design/ui'
import { ds } from '@/design/system'
import { userMessage } from '@/lib/userMessage'
import { officialManagementRoles } from '@/lib/task09Permissions'

interface ExamSession {
  id: string
  courseId?: string
  examStructureId?: string
  title: string
  examDate: string
  hasFollowUp: boolean
  maxFollowUpQuestions: number
  examInputMode: string
  transcriptBufferSeconds: number
  status: string
}

interface ExamShift {
  id: string
  sessionId: string
  shiftName: string
  roomLab: string
  startTime: string
  endTime: string
  proctorUserId?: string | null
  status: string
}

const service = officialExamService as typeof officialExamService & {
  getSessions: (params?: Record<string, string>) => Promise<ExamSession[]>
  createSession: (payload: Record<string, unknown>) => Promise<ExamSession>
  getShifts: (params?: Record<string, string>) => Promise<ExamShift[]>
  createShift: (payload: Record<string, unknown>) => Promise<ExamShift>
  assignTicket: (payload: Record<string, unknown>) => Promise<unknown>
  getExamStructures: (params?: { courseId?: string; isActive?: boolean }) => Promise<ExamStructure[]>
  createExamStructure: (payload: Record<string, unknown>) => Promise<ExamStructure>
  updateExamStructure: (id: string, payload: Record<string, unknown>) => Promise<ExamStructure>
}

const emptySession = {
  courseId: '',
  examStructureId: '',
  title: '',
  examDate: '',
  hasFollowUp: false,
  maxFollowUpQuestions: 2,
  examInputMode: 'VoiceWithTranscriptEdit',
  transcriptBufferSeconds: 60,
}

const emptyShift = {
  sessionId: '',
  shiftName: '',
  roomLab: '',
  startTime: '',
  endTime: '',
  proctorUserId: '',
}

const emptyStructure = {
  courseId: '',
  name: '',
  totalQuestions: 5,
  durationMinutes: 30,
  isActive: true,
}

const BLOOM_LABELS: Record<CloBloomLevel, string> = {
  Remember: 'Remember (Nhớ)',
  Understand: 'Understand (Hiểu)',
  Apply: 'Apply (Áp dụng)',
  Analyze: 'Analyze (Phân tích)',
  Evaluate: 'Evaluate (Đánh giá)',
  Create: 'Create (Sáng tạo)',
}

function createEmptyBloomCounts(): Record<CloBloomLevel, number> {
  return {
    Remember: 0,
    Understand: 0,
    Apply: 0,
    Analyze: 0,
    Evaluate: 0,
    Create: 0,
  }
}

function formatOfficialExamError(error: unknown, fallback: string) {
  const message = userMessage(error, fallback)
  return message.replace(/^VALIDATION\s*[:\-–—]\s*/i, '').trim() || fallback
}

function parseBloomDistribution(raw?: string | null): Array<[string, number]> {
  if (!raw || typeof raw !== 'string' || !raw.trim()) return []
  try {
    const parsed: unknown = JSON.parse(raw)
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) return []
    return Object.entries(parsed as Record<string, unknown>)
      .filter(([, value]) => typeof value === 'number' && Number.isFinite(value) && value > 0)
      .map(([key, value]) => [key, value as number])
  } catch {
    return []
  }
}

export function OfficialExamManagementPage() {
  const { user } = useAuth()
  const canOperate = user?.role === 'ExaminationOfficer'
  const canManageStructures = user?.role === 'DepartmentHead'
  const canView = Boolean(user && officialManagementRoles.includes(user.role))
  const [sessions, setSessions] = useState<ExamSession[]>([])
  const [shifts, setShifts] = useState<ExamShift[]>([])
  const [structures, setStructures] = useState<ExamStructure[]>([])
  const [assignedCourses, setAssignedCourses] = useState<Array<{ id: string; code: string; name: string }>>([])
  const [sessionForm, setSessionForm] = useState(emptySession)
  const [shiftForm, setShiftForm] = useState(emptyShift)
  const [ticketForm, setTicketForm] = useState({ shiftId: '', studentId: '', seatNumber: 1, ipAddress: '' })
  const [structureForm, setStructureForm] = useState(emptyStructure)
  const [bloomCounts, setBloomCounts] = useState<Record<CloBloomLevel, number>>(createEmptyBloomCounts)
  const [structureError, setStructureError] = useState('')
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)

  const selectedSession = useMemo(
    () => sessions.find((session) => session.id === shiftForm.sessionId),
    [sessions, shiftForm.sessionId],
  )

  const bloomSum = useMemo(
    () => CLO_BLOOM_LEVELS.reduce((sum, level) => sum + (bloomCounts[level] || 0), 0),
    [bloomCounts],
  )

  const loadData = useCallback(async () => {
    if (!canView) return
    setLoading(true)
    try {
      const structureRequest = canManageStructures
        ? Promise.all([
          service.getExamStructures({ isActive: true }),
          service.getExamStructures({ isActive: false }),
        ]).then((results) => [...new Map(results.flat().map((item) => [item.id, item])).values()])
        : service.getExamStructures({ isActive: true })
      const sessionsRequest = canOperate ? service.getSessions() : Promise.resolve([] as ExamSession[])
      const shiftsRequest = canOperate ? service.getShifts() : Promise.resolve([] as ExamShift[])
      const coursesRequest = canManageStructures
        ? courseService.getLecturerCourses()
        : Promise.resolve([] as Array<{ id: string; code: string; name: string }>)

      const [sessionResult, shiftResult, structureResult, courseResult] = await Promise.all([
        sessionsRequest,
        shiftsRequest,
        structureRequest,
        coursesRequest,
      ])
      setSessions(Array.isArray(sessionResult) ? sessionResult : [])
      setShifts(Array.isArray(shiftResult) ? shiftResult : [])
      setStructures(Array.isArray(structureResult) ? structureResult : [])
      const normalizedCourses = Array.isArray(courseResult) ? courseResult : []
      setAssignedCourses(normalizedCourses)
      if (canManageStructures && normalizedCourses.length > 0) {
        setStructureForm((current) =>
          current.courseId && normalizedCourses.some((item) => item.id === current.courseId)
            ? current
            : { ...current, courseId: normalizedCourses[0].id },
        )
      }
    } catch (loadError) {
      toast.error(formatOfficialExamError(loadError, 'Không tải được dữ liệu kỳ thi. Vui lòng thử lại.'))
    } finally {
      setLoading(false)
    }
  }, [canView, canOperate, canManageStructures])

  useEffect(() => {
    const timer = window.setTimeout(() => { void loadData() }, 0)
    return () => window.clearTimeout(timer)
  }, [loadData])

  const submitSession = async (event: FormEvent) => {
    event.preventDefault()
    if (!canOperate) return
    setBusy(true)
    try {
      await service.createSession({
        ...sessionForm,
        courseId: sessionForm.courseId,
        examStructureId: sessionForm.examStructureId,
        actorUserId: user?.id,
        hasFollowUp: false,
        maxFollowUpQuestions: 2,
        transcriptBufferSeconds: Number(sessionForm.transcriptBufferSeconds),
      })
      setSessionForm(emptySession)
      toast.success('Đã tạo kỳ thi chính thức.')
      await loadData()
    } catch (submitError) { toast.error(formatOfficialExamError(submitError, 'Không tạo được kỳ thi. Vui lòng thử lại.')) } finally { setBusy(false) }
  }

  const submitShift = async (event: FormEvent) => {
    event.preventDefault()
    if (!canOperate) return
    setBusy(true)
    try {
      await service.createShift({
        ...shiftForm,
        startTime: new Date(shiftForm.startTime).toISOString(),
        endTime: new Date(shiftForm.endTime).toISOString(),
      })
      setShiftForm(emptyShift)
      toast.success('Đã mở ca thi và phân công giám thị.')
      await loadData()
    } catch (submitError) { toast.error(formatOfficialExamError(submitError, 'Không mở được ca thi. Vui lòng thử lại.')) } finally { setBusy(false) }
  }

  const submitTicket = async (event: FormEvent) => {
    event.preventDefault()
    if (!canOperate) return
    setBusy(true)
    try {
      await service.assignTicket({ ...ticketForm, seatNumber: Number(ticketForm.seatNumber) })
      setTicketForm((current) => ({ ...current, studentId: '', ipAddress: '', seatNumber: current.seatNumber + 1 }))
      toast.success('Đã phân vé thi và số máy cho sinh viên.')
    } catch (submitError) { toast.error(formatOfficialExamError(submitError, 'Không phân được vé thi. Vui lòng thử lại.')) } finally { setBusy(false) }
  }

  const submitStructure = async (event: FormEvent) => {
    event.preventDefault()
    if (!canManageStructures) return

    const totalQuestions = Number(structureForm.totalQuestions)
    const durationMinutes = Number(structureForm.durationMinutes)
    if (!structureForm.courseId) {
      setStructureError('Vui lòng chọn môn học phụ trách.')
      return
    }
    if (!structureForm.name.trim()) {
      setStructureError('Vui lòng nhập tên cấu trúc đề thi.')
      return
    }
    if (!Number.isInteger(totalQuestions) || totalQuestions < 1 || totalQuestions > 20) {
      setStructureError('Tổng số câu hỏi phải là số nguyên từ 1 đến 20.')
      return
    }
    if (!Number.isInteger(durationMinutes) || durationMinutes < 5 || durationMinutes > 180) {
      setStructureError('Thời lượng thi phải từ 5 đến 180 phút.')
      return
    }
    const invalidBloom = CLO_BLOOM_LEVELS.some((level) => {
      const count = bloomCounts[level]
      return !Number.isInteger(count) || count < 0
    })
    if (invalidBloom) {
      setStructureError('Số câu ở mỗi bậc Bloom phải là số nguyên không âm.')
      return
    }
    if (bloomSum > totalQuestions) {
      setStructureError('Tổng số câu phân bổ theo bậc Bloom không được vượt quá tổng số câu hỏi.')
      return
    }

    const bloomMap: Record<string, number> = {}
    for (const level of CLO_BLOOM_LEVELS) {
      const count = bloomCounts[level]
      if (count > 0) bloomMap[level] = count
    }

    setStructureError('')
    setBusy(true)
    try {
      await service.createExamStructure({
        courseId: structureForm.courseId,
        name: structureForm.name.trim(),
        totalQuestions,
        durationMinutes,
        bloomDistribution: JSON.stringify(bloomMap),
        isActive: true,
      })
      setStructureForm((current) => ({
        ...emptyStructure,
        courseId: current.courseId || assignedCourses[0]?.id || '',
      }))
      setBloomCounts(createEmptyBloomCounts())
      toast.success('Đã tạo cấu trúc đề thi.')
      await loadData()
    } catch (submitError) {
      const msg = formatOfficialExamError(submitError, 'Không tạo được cấu trúc đề thi. Vui lòng thử lại.')
      setStructureError(msg)
      toast.error(msg)
    } finally {
      setBusy(false)
    }
  }

  const toggleStructure = async (structure: ExamStructure) => {
    if (!canManageStructures) return
    setBusy(true)
    try {
      await service.updateExamStructure(structure.id, {
        name: structure.name,
        totalQuestions: structure.totalQuestions,
        durationMinutes: structure.durationMinutes,
        bloomDistribution: structure.bloomDistribution,
        isActive: !structure.isActive,
      })
      toast.success(`Đã ${structure.isActive ? 'tắt' : 'bật'} cấu trúc đề thi ${structure.name}.`)
      await loadData()
    } catch (updateError) { toast.error(formatOfficialExamError(updateError, 'Không cập nhật được cấu trúc đề thi. Vui lòng thử lại.')) } finally { setBusy(false) }
  }

  if (!canView) return <div role="alert">Chỉ Khảo thí và Trưởng bộ môn được truy cập phân hệ này.</div>

  return (
    <div className={ds.page}>
      <LecturerPageBanner
        icon={ClipboardList}
        eyebrow="Kỳ thi chính thức"
        title="Kỳ thi, ca thi và vé thi"
        description="Tạo kỳ thi, mở ca phòng Lab, phân công giám thị và cấp vé thi theo số máy."
        extras={
          <>
            <BannerFact icon={CalendarPlus}>Kỳ thi</BannerFact>
            <BannerFact icon={UsersRound} tone="green">
              Ca phòng Lab
            </BannerFact>
            <BannerFact icon={Ticket} tone="violet">
              Vé theo số máy
            </BannerFact>
          </>
        }
      />

      {canOperate && <section className="grid gap-4 xl:grid-cols-3">
        <form onSubmit={submitSession} className={ds.section}>
          <SectionHeader icon={CalendarPlus} title="Tạo kỳ thi" hint="Cấu hình kỳ thi chính thức" />
          <div className="space-y-3 p-4">
            <input required className={ds.field} placeholder="Course ID" value={sessionForm.courseId} onChange={(e) => setSessionForm({ ...sessionForm, courseId: e.target.value })} />
            {structures.length > 0 ? <select required className={ds.field} value={sessionForm.examStructureId} onChange={(e) => setSessionForm({ ...sessionForm, examStructureId: e.target.value })}><option value="">Chọn cấu trúc đề thi</option>{structures.filter((structure) => structure.isActive).map((structure) => <option key={structure.id} value={structure.id}>{structure.name} · {structure.courseCode}</option>)}</select> : <input required className={ds.field} placeholder="Exam Structure ID" value={sessionForm.examStructureId} onChange={(e) => setSessionForm({ ...sessionForm, examStructureId: e.target.value })} />}
            <input required className={ds.field} placeholder="Tên kỳ thi" value={sessionForm.title} onChange={(e) => setSessionForm({ ...sessionForm, title: e.target.value })} />
            <input required type="date" className={ds.field} value={sessionForm.examDate} onChange={(e) => setSessionForm({ ...sessionForm, examDate: e.target.value })} />
            <input required type="number" min="10" max="300" className={ds.field} placeholder="Buffer (s)" value={sessionForm.transcriptBufferSeconds} onChange={(e) => setSessionForm({ ...sessionForm, transcriptBufferSeconds: Number(e.target.value) })} />
            <p className="text-xs text-[#52657d]">Follow-up mặc định tắt, tối đa 2 câu. Chỉ Trưởng bộ môn phụ trách được thay đổi cấu hình học thuật.</p>
            <select className={ds.field} value={sessionForm.examInputMode} onChange={(e) => setSessionForm({ ...sessionForm, examInputMode: e.target.value })}><option value="VoiceOnly">VoiceOnly</option><option value="VoiceWithTranscriptEdit">VoiceWithTranscriptEdit</option></select>
            <button disabled={busy} className={ds.primaryButton}><Plus className="size-4" /> Tạo kỳ thi</button>
          </div>
        </form>

        <form onSubmit={submitShift} className={ds.section}>
          <SectionHeader icon={CalendarPlus} title="Mở ca thi" hint="Gắn phòng Lab và giám thị" />
          <div className="space-y-3 p-4">
            <select required className={ds.field} value={shiftForm.sessionId} onChange={(e) => setShiftForm({ ...shiftForm, sessionId: e.target.value })}><option value="">Chọn kỳ thi</option>{sessions.map((session) => <option key={session.id} value={session.id}>{session.title || session.id}</option>)}</select>
            <input required className={ds.field} placeholder="Tên ca" value={shiftForm.shiftName} onChange={(e) => setShiftForm({ ...shiftForm, shiftName: e.target.value })} />
            <input required className={ds.field} placeholder="Phòng Lab" value={shiftForm.roomLab} onChange={(e) => setShiftForm({ ...shiftForm, roomLab: e.target.value })} />
            <input required type="datetime-local" className={ds.field} value={shiftForm.startTime} onChange={(e) => setShiftForm({ ...shiftForm, startTime: e.target.value })} />
            <input required type="datetime-local" className={ds.field} value={shiftForm.endTime} onChange={(e) => setShiftForm({ ...shiftForm, endTime: e.target.value })} />
            <input required className={ds.field} placeholder="Proctor user ID" value={shiftForm.proctorUserId} onChange={(e) => setShiftForm({ ...shiftForm, proctorUserId: e.target.value })} />
            <button disabled={busy || !selectedSession} className={ds.primaryButton}><Plus className="size-4" /> Mở ca thi</button>
          </div>
        </form>

        <form onSubmit={submitTicket} className={ds.section}>
          <SectionHeader icon={Ticket} title="Cấp vé thi" hint="Gán sinh viên vào ghế Lab" />
          <div className="space-y-3 p-4">
            <select required className={ds.field} value={ticketForm.shiftId} onChange={(e) => setTicketForm({ ...ticketForm, shiftId: e.target.value })}><option value="">Chọn ca thi</option>{shifts.map((shift) => <option key={shift.id} value={shift.id}>{shift.shiftName} · {shift.roomLab}</option>)}</select>
            <input required className={ds.field} placeholder="Student ID" value={ticketForm.studentId} onChange={(e) => setTicketForm({ ...ticketForm, studentId: e.target.value })} />
            <div className="grid grid-cols-2 gap-2"><input required type="number" min="1" max="40" className={ds.field} placeholder="Seat" value={ticketForm.seatNumber} onChange={(e) => setTicketForm({ ...ticketForm, seatNumber: Number(e.target.value) })} /><input className={ds.field} placeholder="IP address" value={ticketForm.ipAddress} onChange={(e) => setTicketForm({ ...ticketForm, ipAddress: e.target.value })} /></div>
            <button disabled={busy} className={ds.primaryButton}><Ticket className="size-4" /> Cấp vé thi</button>
          </div>
        </form>
      </section>}

      <section className="mt-4 grid gap-4 lg:grid-cols-[minmax(0,360px)_minmax(0,1fr)]">
        {canManageStructures && <form onSubmit={submitStructure} className={ds.section}>
          <SectionHeader icon={ClipboardList} title="Cấu trúc đề thi" hint="Cấu hình số câu và bậc Bloom" />
          <div className="space-y-3 p-4">
            {structureError ? (
              <div
                role="alert"
                className="rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-3 py-2.5 text-xs font-semibold text-[#c2415b]"
              >
                {structureError}
              </div>
            ) : null}
            <label className="block">
              <span className="mb-1 block text-xs font-bold text-[#0F2854]">Môn học phụ trách</span>
              <select
                required
                aria-label="Môn học"
                className={ds.field}
                value={structureForm.courseId}
                onChange={(e) => {
                  setStructureError('')
                  setStructureForm({ ...structureForm, courseId: e.target.value })
                }}
              >
                <option value="">
                  {assignedCourses.length === 0 ? 'Chưa có môn được phân công' : 'Chọn môn học phụ trách'}
                </option>
                {assignedCourses.map((course) => (
                  <option key={course.id} value={course.id}>
                    {course.code} · {course.name}
                  </option>
                ))}
              </select>
            </label>
            <label className="block">
              <span className="mb-1 block text-xs font-bold text-[#0F2854]">Tên cấu trúc</span>
              <input
                required
                className={ds.field}
                placeholder="Tên cấu trúc"
                value={structureForm.name}
                onChange={(e) => {
                  setStructureError('')
                  setStructureForm({ ...structureForm, name: e.target.value })
                }}
              />
            </label>
            <div className="grid grid-cols-2 gap-2">
              <label className="block">
                <span className="mb-1 block text-xs font-bold text-[#0F2854]">Số câu (1–20)</span>
                <input
                  required
                  type="number"
                  min={1}
                  max={20}
                  step={1}
                  className={ds.field}
                  placeholder="Số câu"
                  value={structureForm.totalQuestions}
                  onChange={(e) => {
                    setStructureError('')
                    setStructureForm({ ...structureForm, totalQuestions: Number(e.target.value) })
                  }}
                />
              </label>
              <label className="block">
                <span className="mb-1 block text-xs font-bold text-[#0F2854]">Phút (5–180)</span>
                <input
                  required
                  type="number"
                  min={5}
                  max={180}
                  step={1}
                  className={ds.field}
                  placeholder="Phút"
                  value={structureForm.durationMinutes}
                  onChange={(e) => {
                    setStructureError('')
                    setStructureForm({ ...structureForm, durationMinutes: Number(e.target.value) })
                  }}
                />
              </label>
            </div>
            <div className="space-y-2 rounded-xl border border-[#82aedb]/45 bg-[#F8FBFD] p-3">
              <div className="flex items-center justify-between gap-2">
                <span className="text-xs font-bold text-[#0F2854]">Phân bổ theo bậc Bloom</span>
                <span
                  className={cn(
                    'text-xs font-bold tabular-nums',
                    bloomSum > structureForm.totalQuestions ? 'text-[#c2415b]' : 'text-[#1C4D8D]',
                  )}
                >
                  Phân bổ Bloom: {bloomSum} / {structureForm.totalQuestions} câu
                </span>
              </div>
              <div className="grid grid-cols-2 gap-2">
                {CLO_BLOOM_LEVELS.map((level) => (
                  <label key={level} className="block">
                    <span className="mb-1 block text-[11px] font-semibold text-[#5b6b84]">
                      {BLOOM_LABELS[level]}
                    </span>
                    <input
                      type="number"
                      min={0}
                      step={1}
                      aria-label={`Bloom ${level}`}
                      className={cn(ds.field, 'min-h-9 py-1.5 text-xs')}
                      value={bloomCounts[level]}
                      onChange={(e) => {
                        setStructureError('')
                        const parsed = Math.max(0, Math.floor(Number(e.target.value) || 0))
                        setBloomCounts((current) => ({ ...current, [level]: parsed }))
                      }}
                    />
                  </label>
                ))}
              </div>
            </div>
            <button disabled={busy} className={ds.primaryButton}><Plus className="size-4" /> Tạo cấu trúc</button>
          </div>
        </form>}
        <section className={ds.section}>
          <SectionHeader
            icon={ClipboardList}
            title="Danh sách cấu trúc"
            hint="Bật/tắt cấu trúc đề đang có"
            action={
              <button type="button" className={ds.outlineButton} onClick={() => void loadData()} disabled={loading}>
                <RefreshCw className={cn('size-4', loading && 'animate-spin')} /> Làm mới
              </button>
            }
          />
          <div className="grid gap-3 p-4 md:grid-cols-2">
            {structures.map((structure) => {
              const bloomEntries = parseBloomDistribution(structure.bloomDistribution)
              return (
                <article key={structure.id} className="rounded-xl border border-[#82aedb]/55 p-4">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <h3 className="font-bold text-[#0F2854]">{structure.name}</h3>
                      <p className="mt-1 text-xs text-[#687a91]">{structure.courseCode} · {structure.totalQuestions} câu · {structure.durationMinutes} phút</p>
                    </div>
                    <span className={ds.infoChip}>{structure.isActive ? 'ACTIVE' : 'INACTIVE'}</span>
                  </div>
                  <div className="mt-2.5 flex flex-wrap gap-1.5">
                    {bloomEntries.length > 0 ? (
                      bloomEntries.map(([level, count]) => (
                        <span
                          key={level}
                          className="rounded-full border border-[#82aedb]/45 bg-[#F4FBFD] px-2 py-0.5 text-[11px] font-semibold text-[#1C4D8D]"
                        >
                          {level}: {count}
                        </span>
                      ))
                    ) : (
                      <span className="text-xs text-[#687a91]">Phân bổ linh hoạt</span>
                    )}
                  </div>
                  {canManageStructures && <button type="button" disabled={busy} onClick={() => void toggleStructure(structure)} className="mt-3 rounded-lg border border-[#82aedb]/55 px-3 py-2 text-xs font-bold text-[#1C4D8D]">{structure.isActive ? 'Tắt cấu trúc' : 'Bật cấu trúc'}</button>}
                </article>
              )
            })}
            {structures.length === 0 && <EmptyState compact icon={ClipboardList} title="Chưa có cấu trúc đề thi" description="Cấu trúc đề thi do Trưởng bộ môn phụ trách quản lý." />}
          </div>
        </section>
      </section>

      {canOperate && <section className={ds.section}>
        <SectionHeader
          icon={UsersRound}
          title="Danh sách đã cấu hình"
          hint="Theo dõi kỳ thi và ca thi đã cấu hình"
          action={
            <button type="button" className={ds.outlineButton} onClick={() => void loadData()} disabled={loading}>
              <RefreshCw className={cn('size-4', loading && 'animate-spin')} /> Làm mới
            </button>
          }
        />
        {loading ? <div className="flex items-center justify-center p-10"><LoaderCircle className="size-7 animate-spin text-[#1C4D8D]" /></div> : <div className="grid gap-3 p-4 md:grid-cols-2">{sessions.map((session) => <article key={session.id} className="rounded-xl border border-[#82aedb]/55 p-4"><div className="flex justify-between gap-3"><div><h3 className="font-bold text-[#0F2854]">{session.title || 'Kỳ thi chính thức'}</h3><p className="mt-1 text-xs text-[#687a91]">{session.examDate} · {session.examInputMode}</p></div><span className={ds.infoChip}>{session.status}</span></div><p className="mt-3 text-xs text-[#5b6b84]">Follow-up: {session.hasFollowUp ? `${session.maxFollowUpQuestions} câu` : 'Không'} · Buffer: {session.transcriptBufferSeconds}s</p></article>)}{sessions.length === 0 && <EmptyState compact icon={CalendarPlus} title="Chưa có kỳ thi" description="Tạo kỳ thi ở form phía trên để bắt đầu cấu hình." />}</div>}
      </section>}
    </div>
  )
}
