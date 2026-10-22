import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { CalendarPlus, ClipboardList, LoaderCircle, Plus, RefreshCw, Ticket, UsersRound } from 'lucide-react'
import { useAuth } from '@/hooks/useAuth'
import { officialExamService } from '@/services/officialExam.service'
import type { ExamStructure } from '@/types/officialExam.types'
import { cn } from '@/lib/utils'
import { ds } from '@/design/system'

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

function getErrorMessage(error: unknown) {
  if (typeof error === 'object' && error !== null && 'detail' in error) {
    const detail = (error as { detail?: unknown }).detail
    if (typeof detail === 'string' && detail.trim()) return detail
  }
  return 'Không thể hoàn tất yêu cầu. Vui lòng thử lại.'
}

const emptySession = {
  courseId: '',
  examStructureId: '',
  title: '',
  examDate: '',
  hasFollowUp: true,
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
  bloomDistribution: '{}',
  isActive: true,
}

export function OfficialExamManagementPage() {
  const { user } = useAuth()
  const [sessions, setSessions] = useState<ExamSession[]>([])
  const [shifts, setShifts] = useState<ExamShift[]>([])
  const [structures, setStructures] = useState<ExamStructure[]>([])
  const [sessionForm, setSessionForm] = useState(emptySession)
  const [shiftForm, setShiftForm] = useState(emptyShift)
  const [ticketForm, setTicketForm] = useState({ shiftId: '', studentId: '', seatNumber: 1, ipAddress: '' })
  const [structureForm, setStructureForm] = useState(emptyStructure)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')

  const selectedSession = useMemo(
    () => sessions.find((session) => session.id === shiftForm.sessionId),
    [sessions, shiftForm.sessionId],
  )

  const loadData = async () => {
    setLoading(true)
    setError('')
    try {
      const [sessionResult, shiftResult, structureResult] = await Promise.all([
        service.getSessions(),
        service.getShifts(),
        service.getExamStructures({ isActive: false }),
      ])
      setSessions(Array.isArray(sessionResult) ? sessionResult : [])
      setShifts(Array.isArray(shiftResult) ? shiftResult : [])
      setStructures(Array.isArray(structureResult) ? structureResult : [])
    } catch (loadError) {
      setError(getErrorMessage(loadError))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    const timer = window.setTimeout(() => { void loadData() }, 0)
    return () => window.clearTimeout(timer)
  }, [])

  const submitSession = async (event: FormEvent) => {
    event.preventDefault()
    setBusy(true); setError(''); setNotice('')
    try {
      await service.createSession({
        ...sessionForm,
        courseId: sessionForm.courseId,
        examStructureId: sessionForm.examStructureId,
        actorUserId: user?.id,
        maxFollowUpQuestions: Number(sessionForm.maxFollowUpQuestions),
        transcriptBufferSeconds: Number(sessionForm.transcriptBufferSeconds),
      })
      setSessionForm(emptySession)
      setNotice('Đã tạo kỳ thi chính thức.')
      await loadData()
    } catch (submitError) { setError(getErrorMessage(submitError)) } finally { setBusy(false) }
  }

  const submitShift = async (event: FormEvent) => {
    event.preventDefault()
    setBusy(true); setError(''); setNotice('')
    try {
      await service.createShift({
        ...shiftForm,
        startTime: new Date(shiftForm.startTime).toISOString(),
        endTime: new Date(shiftForm.endTime).toISOString(),
      })
      setShiftForm(emptyShift)
      setNotice('Đã mở ca thi và phân công giám thị.')
      await loadData()
    } catch (submitError) { setError(getErrorMessage(submitError)) } finally { setBusy(false) }
  }

  const submitTicket = async (event: FormEvent) => {
    event.preventDefault()
    setBusy(true); setError(''); setNotice('')
    try {
      await service.assignTicket({ ...ticketForm, seatNumber: Number(ticketForm.seatNumber) })
      setTicketForm((current) => ({ ...current, studentId: '', ipAddress: '', seatNumber: current.seatNumber + 1 }))
      setNotice('Đã phân vé thi và số máy cho sinh viên.')
    } catch (submitError) { setError(getErrorMessage(submitError)) } finally { setBusy(false) }
  }

  const submitStructure = async (event: FormEvent) => {
    event.preventDefault()
    setBusy(true); setError(''); setNotice('')
    try {
      await service.createExamStructure({
        ...structureForm,
        totalQuestions: Number(structureForm.totalQuestions),
        durationMinutes: Number(structureForm.durationMinutes),
      })
      setStructureForm(emptyStructure)
      setNotice('ÄÃ£ táº¡o cáº¥u trÃºc Ä‘á» thi.')
      await loadData()
    } catch (submitError) { setError(getErrorMessage(submitError)) } finally { setBusy(false) }
  }

  const toggleStructure = async (structure: ExamStructure) => {
    setBusy(true); setError(''); setNotice('')
    try {
      await service.updateExamStructure(structure.id, {
        name: structure.name,
        totalQuestions: structure.totalQuestions,
        durationMinutes: structure.durationMinutes,
        bloomDistribution: structure.bloomDistribution,
        isActive: !structure.isActive,
      })
      setNotice(`ÄÃ£ ${structure.isActive ? 'táº¯t' : 'báº­t'} cáº¥u trÃºc Ä‘á» thi ${structure.name}.`)
      await loadData()
    } catch (updateError) { setError(getErrorMessage(updateError)) } finally { setBusy(false) }
  }

  return (
    <div className={ds.page}>
      <section className={cn(ds.banner, 'p-5 sm:p-6')}>
        <p className={ds.bannerChip}><ClipboardList className="size-4" /> MF-04 · Quản lý kỳ thi chính thức</p>
        <h1 className={ds.bannerTitle}>Kỳ thi, ca thi & vé thi</h1>
        <p className={ds.bannerText}>Tạo kỳ thi, mở ca phòng Lab, phân công giám thị và cấp vé thi theo số máy.</p>
      </section>

      {(error || notice) && <div role={error ? 'alert' : 'status'} className={cn('rounded-xl border px-4 py-3 text-sm font-semibold', error ? 'border-[#f0c5c9] bg-[#fff5f6] text-[#a43c47]' : 'border-[#b7e4cf] bg-[#effaf6] text-[#176f58]')}>{error || notice}</div>}

      <section className="grid gap-4 xl:grid-cols-3">
        <form onSubmit={submitSession} className={ds.section}>
          <div className={ds.sectionHeader}><h2 className={ds.sectionTitleRow}><CalendarPlus className={ds.sectionIcon} /> <span className="text-sm font-bold text-[#0F2854]">Tạo kỳ thi</span></h2><p className={ds.sectionHint}>Cấu hình follow-up và buffer theo MF-04.</p></div>
          <div className="space-y-3 p-4">
            <input required className={ds.field} placeholder="Course ID" value={sessionForm.courseId} onChange={(e) => setSessionForm({ ...sessionForm, courseId: e.target.value })} />
            {structures.length > 0 ? <select required className={ds.field} value={sessionForm.examStructureId} onChange={(e) => setSessionForm({ ...sessionForm, examStructureId: e.target.value })}><option value="">Chá»n cáº¥u trÃºc Ä‘á» thi</option>{structures.filter((structure) => structure.isActive).map((structure) => <option key={structure.id} value={structure.id}>{structure.name} Â· {structure.courseCode}</option>)}</select> : <input required className={ds.field} placeholder="Exam Structure ID" value={sessionForm.examStructureId} onChange={(e) => setSessionForm({ ...sessionForm, examStructureId: e.target.value })} />}
            <input required className={ds.field} placeholder="Tên kỳ thi" value={sessionForm.title} onChange={(e) => setSessionForm({ ...sessionForm, title: e.target.value })} />
            <input required type="date" className={ds.field} value={sessionForm.examDate} onChange={(e) => setSessionForm({ ...sessionForm, examDate: e.target.value })} />
            <div className="grid grid-cols-2 gap-2"><input required type="number" min="1" max="5" className={ds.field} placeholder="Follow-up" value={sessionForm.maxFollowUpQuestions} onChange={(e) => setSessionForm({ ...sessionForm, maxFollowUpQuestions: Number(e.target.value) })} /><input required type="number" min="10" max="300" className={ds.field} placeholder="Buffer (s)" value={sessionForm.transcriptBufferSeconds} onChange={(e) => setSessionForm({ ...sessionForm, transcriptBufferSeconds: Number(e.target.value) })} /></div>
            <label className="flex items-center gap-2 text-xs font-semibold text-[#52657d]"><input type="checkbox" checked={sessionForm.hasFollowUp} onChange={(e) => setSessionForm({ ...sessionForm, hasFollowUp: e.target.checked })} /> Cho phép follow-up</label>
            <select className={ds.field} value={sessionForm.examInputMode} onChange={(e) => setSessionForm({ ...sessionForm, examInputMode: e.target.value })}><option value="VoiceOnly">VoiceOnly</option><option value="VoiceWithTranscriptEdit">VoiceWithTranscriptEdit</option></select>
            <button disabled={busy} className={ds.primaryButton}><Plus className="size-4" /> Tạo kỳ thi</button>
          </div>
        </form>

        <form onSubmit={submitShift} className={ds.section}>
          <div className={ds.sectionHeader}><h2 className={ds.sectionTitleRow}><CalendarPlus className={ds.sectionIcon} /> <span className="text-sm font-bold text-[#0F2854]">Mở ca thi</span></h2><p className={ds.sectionHint}>Gắn phòng Lab và giám thị.</p></div>
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
          <div className={ds.sectionHeader}><h2 className={ds.sectionTitleRow}><Ticket className={ds.sectionIcon} /> <span className="text-sm font-bold text-[#0F2854]">Cấp vé thi</span></h2><p className={ds.sectionHint}>Gán sinh viên vào ghế số 1–40.</p></div>
          <div className="space-y-3 p-4">
            <select required className={ds.field} value={ticketForm.shiftId} onChange={(e) => setTicketForm({ ...ticketForm, shiftId: e.target.value })}><option value="">Chọn ca thi</option>{shifts.map((shift) => <option key={shift.id} value={shift.id}>{shift.shiftName} · {shift.roomLab}</option>)}</select>
            <input required className={ds.field} placeholder="Student ID" value={ticketForm.studentId} onChange={(e) => setTicketForm({ ...ticketForm, studentId: e.target.value })} />
            <div className="grid grid-cols-2 gap-2"><input required type="number" min="1" max="40" className={ds.field} placeholder="Seat" value={ticketForm.seatNumber} onChange={(e) => setTicketForm({ ...ticketForm, seatNumber: Number(e.target.value) })} /><input className={ds.field} placeholder="IP address" value={ticketForm.ipAddress} onChange={(e) => setTicketForm({ ...ticketForm, ipAddress: e.target.value })} /></div>
            <button disabled={busy} className={ds.primaryButton}><Ticket className="size-4" /> Cấp vé thi</button>
          </div>
        </form>
      </section>

      <section className="mt-4 grid gap-4 lg:grid-cols-[minmax(0,340px)_minmax(0,1fr)]">
        <form onSubmit={submitStructure} className={ds.section}>
          <div className={ds.sectionHeader}><h2 className={ds.sectionTitleRow}><ClipboardList className={ds.sectionIcon} /> <span className="text-sm font-bold text-[#0F2854]">Cáº¥u trÃºc Ä‘á» thi</span></h2><p className={ds.sectionHint}>Cáº¥u hÃ¬nh Bloom vÃ  sá»‘ cÃ¢u cho MF-04.</p></div>
          <div className="space-y-3 p-4">
            <input required className={ds.field} placeholder="Course ID" value={structureForm.courseId} onChange={(e) => setStructureForm({ ...structureForm, courseId: e.target.value })} />
            <input required className={ds.field} placeholder="TÃªn cáº¥u trÃºc" value={structureForm.name} onChange={(e) => setStructureForm({ ...structureForm, name: e.target.value })} />
            <div className="grid grid-cols-2 gap-2"><input required type="number" min="1" className={ds.field} placeholder="Sá»‘ cÃ¢u" value={structureForm.totalQuestions} onChange={(e) => setStructureForm({ ...structureForm, totalQuestions: Number(e.target.value) })} /><input required type="number" min="1" className={ds.field} placeholder="PhÃºt" value={structureForm.durationMinutes} onChange={(e) => setStructureForm({ ...structureForm, durationMinutes: Number(e.target.value) })} /></div>
            <input className={ds.field} placeholder='Bloom JSON, vÃ­ dá»¥ {"{Remember:2}"}' value={structureForm.bloomDistribution} onChange={(e) => setStructureForm({ ...structureForm, bloomDistribution: e.target.value })} />
            <button disabled={busy} className={ds.primaryButton}><Plus className="size-4" /> Táº¡o cáº¥u trÃºc</button>
          </div>
        </form>
        <section className={ds.section}>
          <div className={ds.sectionHeader}><h2 className={ds.sectionTitleRow}><ClipboardList className={ds.sectionIcon} /> <span className="text-sm font-bold text-[#0F2854]">Danh sÃ¡ch cáº¥u trÃºc</span></h2><p className={ds.sectionHint}>Báº­t/táº¯t cáº¥u trÃºc Ä‘á» thi Ä‘ang cÃ³.</p></div>
          <div className="grid gap-3 p-4 md:grid-cols-2">{structures.map((structure) => <article key={structure.id} className="rounded-xl border border-[#dce5ef] p-4"><div className="flex items-start justify-between gap-3"><div><h3 className="font-bold text-[#0F2854]">{structure.name}</h3><p className="mt-1 text-xs text-[#687a91]">{structure.courseCode} Â· {structure.totalQuestions} cÃ¢u Â· {structure.durationMinutes} phÃºt</p></div><span className={ds.infoChip}>{structure.isActive ? 'ACTIVE' : 'INACTIVE'}</span></div><button type="button" disabled={busy} onClick={() => void toggleStructure(structure)} className="mt-3 rounded-lg border border-[#cbd8e7] px-3 py-2 text-xs font-bold text-[#1C4D8D]">{structure.isActive ? 'Táº¯t cáº¥u trÃºc' : 'Báº­t cáº¥u trÃºc'}</button></article>)}{structures.length === 0 && <p className="p-4 text-sm text-[#687a91]">ChÆ°a cÃ³ cáº¥u trÃºc Ä‘á» thi.</p>}</div>
        </section>
      </section>

      <section className={ds.section}>
        <div className={cn(ds.sectionHeader, 'flex items-center justify-between')}><div><h2 className={ds.sectionTitleRow}><UsersRound className={ds.sectionIcon} /> <span className="text-sm font-bold text-[#0F2854]">Danh sách đã cấu hình</span></h2><p className={ds.sectionHint}>Theo dõi trạng thái kỳ thi và ca thi từ backend.</p></div><button type="button" className={ds.outlineButton} onClick={() => void loadData()} disabled={loading}><RefreshCw className={cn('size-4', loading && 'animate-spin')} /> Làm mới</button></div>
        {loading ? <div className="flex items-center justify-center p-10"><LoaderCircle className="size-7 animate-spin text-[#1C4D8D]" /></div> : <div className="grid gap-3 p-4 md:grid-cols-2">{sessions.map((session) => <article key={session.id} className="rounded-xl border border-[#dce5ef] p-4"><div className="flex justify-between gap-3"><div><h3 className="font-bold text-[#0F2854]">{session.title || 'Kỳ thi chính thức'}</h3><p className="mt-1 text-xs text-[#687a91]">{session.examDate} · {session.examInputMode}</p></div><span className={ds.infoChip}>{session.status}</span></div><p className="mt-3 text-xs text-[#5b6b84]">Follow-up: {session.hasFollowUp ? `${session.maxFollowUpQuestions} câu` : 'Không'} · Buffer: {session.transcriptBufferSeconds}s</p></article>)}{sessions.length === 0 && <p className="p-4 text-sm text-[#687a91]">Chưa có kỳ thi.</p>}</div>}
      </section>
    </div>
  )
}

