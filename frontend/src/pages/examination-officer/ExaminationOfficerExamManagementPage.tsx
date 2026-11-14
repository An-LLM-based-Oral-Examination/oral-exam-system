/* eslint-disable react-hooks/set-state-in-effect */
import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { ArrowRight, CalendarDays, ClipboardList, Plus, RefreshCw } from 'lucide-react'
import { Link } from 'react-router-dom'
import { officialExamService } from '@/services/officialExam.service'
import type { ExamShift, OfficialExamSession } from '@/types/officialExam.types'
import { PageBanner, SectionHeader } from '@/design/ui'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { EmptyState } from '@/components/common/EmptyState'
import { toast } from '@/components/common/toastStore'
import { userMessage } from '@/lib/userMessage'

interface SessionFormState {
  courseId: string
  examStructureId: string
  title: string
  examDate: string
  hasFollowUp: boolean
  maxFollowUpQuestions: number
  examInputMode: string
  transcriptBufferSeconds: number
}
interface ShiftFormState {
  sessionId: string
  shiftName: string
  roomLab: string
  startTime: string
  endTime: string
  proctorUserId: string
}
const initialSession: SessionFormState = {
  courseId: '',
  examStructureId: '',
  title: '',
  examDate: '',
  hasFollowUp: true,
  maxFollowUpQuestions: 2,
  examInputMode: 'VoiceWithTranscriptEdit',
  transcriptBufferSeconds: 60,
}
const initialShift: ShiftFormState = {
  sessionId: '',
  shiftName: '',
  roomLab: '',
  startTime: '',
  endTime: '',
  proctorUserId: '',
}
export function ExaminationOfficerExamManagementPage() {
  const [sessions, setSessions] = useState<OfficialExamSession[]>([])
  const [shifts, setShifts] = useState<ExamShift[]>([])
  const [sessionForm, setSessionForm] = useState(initialSession)
  const [shiftForm, setShiftForm] = useState(initialShift)
  const [isLoading, setIsLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const selectedSession = useMemo(
    () => sessions.find((session) => session.id === shiftForm.sessionId),
    [sessions, shiftForm.sessionId],
  )

  const load = async () => {
    setIsLoading(true)
    setError('')
    try {
      const [sessionResult, shiftResult] = await Promise.all([
        officialExamService.getSessions(),
        officialExamService.getShifts(),
      ])
      setSessions(sessionResult)
      setShifts(shiftResult)
    } catch (loadError) {
      setError(userMessage(loadError, 'Không tải được dữ liệu kỳ thi.'))
      toast.error(userMessage(loadError, 'Không tải được dữ liệu kỳ thi.'))
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    void load()
  }, [])

  const submitSession = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setBusy(true)
    setError('')
    try {
      await officialExamService.createSession({ ...sessionForm })
      setSessionForm(initialSession)
      toast.success('Đã tạo kỳ thi chính thức.')
      await load()
    } catch (submitError) {
      setError(userMessage(submitError, 'Không tạo được kỳ thi.'))
      toast.error(userMessage(submitError, 'Không tạo được kỳ thi.'))
    } finally {
      setBusy(false)
    }
  }

  const submitShift = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setBusy(true)
    setError('')
    try {
      await officialExamService.createShift(shiftForm)
      setShiftForm(initialShift)
      toast.success('Đã tạo ca thi.')
      await load()
    } catch (submitError) {
      setError(userMessage(submitError, 'Không tạo được ca thi.'))
      toast.error(userMessage(submitError, 'Không tạo được ca thi.'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className={ds.page}>
      <PageBanner
        icon={CalendarDays}
        title="Kỳ thi chính thức"
        description="Tạo kỳ thi, mở ca và theo dõi cấu hình thi thật."
      />
      {error ? (
        <div
          role="alert"
          className="rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-4 py-3 text-sm font-semibold text-[#a43d4b]"
        >
          {error}
        </div>
      ) : null}
      <section className="grid gap-4 xl:grid-cols-2">
        <form onSubmit={submitSession} className={ds.section}>
          <SectionHeader
            icon={CalendarDays}
            title="Tạo kỳ thi"
            hint="Thông tin session được backend hỗ trợ"
          />
          <div className="grid gap-3 p-5">
            <label className="grid gap-1.5 text-xs font-bold text-[#52657d]">
              Course ID
              <input
                required
                className={ds.field}
                value={sessionForm.courseId}
                onChange={(event) =>
                  setSessionForm({ ...sessionForm, courseId: event.target.value })
                }
              />
            </label>
            <label className="grid gap-1.5 text-xs font-bold text-[#52657d]">
              Exam Structure ID
              <input
                required
                className={ds.field}
                value={sessionForm.examStructureId}
                onChange={(event) =>
                  setSessionForm({ ...sessionForm, examStructureId: event.target.value })
                }
              />
            </label>
            <label className="grid gap-1.5 text-xs font-bold text-[#52657d]">
              Tên kỳ thi
              <input
                required
                className={ds.field}
                value={sessionForm.title}
                onChange={(event) => setSessionForm({ ...sessionForm, title: event.target.value })}
              />
            </label>
            <label className="grid gap-1.5 text-xs font-bold text-[#52657d]">
              Ngày thi
              <input
                required
                type="date"
                className={ds.field}
                value={sessionForm.examDate}
                onChange={(event) =>
                  setSessionForm({ ...sessionForm, examDate: event.target.value })
                }
              />
            </label>
            <label className="grid gap-1.5 text-xs font-bold text-[#52657d]">
              Buffer transcript (giây)
              <input
                required
                type="number"
                min="10"
                max="300"
                className={ds.field}
                value={sessionForm.transcriptBufferSeconds}
                onChange={(event) =>
                  setSessionForm({
                    ...sessionForm,
                    transcriptBufferSeconds: Number(event.target.value),
                  })
                }
              />
            </label>
            <div className="rounded-xl border border-[#f3d19a] bg-[#fff8ed] px-3 py-2 text-xs font-semibold text-[#8a5511]">
              Follow-up được giữ theo cấu hình backend/Trưởng bộ môn; Khảo thí không có quyền thay
              đổi trên portal này.
            </div>
            <button type="submit" disabled={busy} className={ds.primaryButton}>
              <Plus className="size-4" /> Tạo kỳ thi
            </button>
          </div>
        </form>
        <form onSubmit={submitShift} className={ds.section}>
          <SectionHeader icon={CalendarDays} title="Tạo ca thi" hint="Gắn phòng Lab và Giám thị" />
          <div className="grid gap-3 p-5">
            <label className="grid gap-1.5 text-xs font-bold text-[#52657d]">
              Kỳ thi
              <select
                required
                className={ds.field}
                value={shiftForm.sessionId}
                onChange={(event) => setShiftForm({ ...shiftForm, sessionId: event.target.value })}
              >
                <option value="">Chọn kỳ thi</option>
                {sessions.map((session) => (
                  <option key={session.id} value={session.id}>
                    {session.title || session.id}
                  </option>
                ))}
              </select>
            </label>
            <label className="grid gap-1.5 text-xs font-bold text-[#52657d]">
              Tên ca
              <input
                required
                className={ds.field}
                value={shiftForm.shiftName}
                onChange={(event) => setShiftForm({ ...shiftForm, shiftName: event.target.value })}
              />
            </label>
            <label className="grid gap-1.5 text-xs font-bold text-[#52657d]">
              Phòng Lab
              <input
                required
                className={ds.field}
                value={shiftForm.roomLab}
                onChange={(event) => setShiftForm({ ...shiftForm, roomLab: event.target.value })}
              />
            </label>
            <div className="grid gap-3">
              <label className="grid gap-1.5 text-xs font-bold text-[#52657d]">
                Bắt đầu
                <input
                  required
                  type="datetime-local"
                  className={ds.field}
                  value={shiftForm.startTime}
                  onChange={(event) =>
                    setShiftForm({ ...shiftForm, startTime: event.target.value })
                  }
                />
              </label>
              <label className="grid gap-1.5 text-xs font-bold text-[#52657d]">
                Kết thúc
                <input
                  required
                  type="datetime-local"
                  className={ds.field}
                  value={shiftForm.endTime}
                  onChange={(event) => setShiftForm({ ...shiftForm, endTime: event.target.value })}
                />
              </label>
            </div>
            <label className="grid gap-1.5 text-xs font-bold text-[#52657d]">
              Giám thị user ID
              <input
                required
                className={ds.field}
                value={shiftForm.proctorUserId}
                onChange={(event) =>
                  setShiftForm({ ...shiftForm, proctorUserId: event.target.value })
                }
              />
            </label>
            <button type="submit" disabled={busy || !selectedSession} className={ds.primaryButton}>
              <Plus className="size-4" /> Tạo ca thi
            </button>
          </div>
        </form>
      </section>
      <section className={ds.section}>
        <SectionHeader
          icon={ClipboardList}
          title="Dữ liệu đã cấu hình"
          hint="Kỳ thi và ca thi đọc từ backend"
          action={
            <button
              type="button"
              onClick={() => void load()}
              disabled={isLoading}
              className={ds.outlineButton}
            >
              <RefreshCw className={cn('size-4', isLoading && 'animate-spin')} /> Làm mới
            </button>
          }
        />
        <div className="grid gap-4 p-5 lg:grid-cols-2">
          <div>
            <h3 className="mb-3 text-sm font-bold text-[#0F2854]">Kỳ thi ({sessions.length})</h3>
            {sessions.length === 0 && !isLoading ? (
              <EmptyState
                compact
                icon={CalendarDays}
                title="Chưa có kỳ thi"
                description="Tạo kỳ thi bằng form phía trên."
              />
            ) : (
              <div className="grid gap-2">
                {sessions.map((session) => (
                  <article
                    key={session.id}
                    className="rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] px-3 py-3"
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div>
                        <strong className="block text-sm text-[#0F2854]">
                          {session.title || session.id}
                        </strong>
                        <span className="mt-1 block text-xs text-[#5b6b84]">
                          {session.examDate} · {session.examInputMode}
                        </span>
                      </div>
                      <div className="flex shrink-0 items-center gap-2">
                        <span className={ds.infoChip}>{session.status}</span>
                        <Link
                          to={`/examination-officer/exams/${session.id}`}
                          className={ds.outlineButton}
                        >
                          Chi tiết <ArrowRight className="size-3.5" />
                        </Link>
                      </div>
                    </div>
                  </article>
                ))}
              </div>
            )}
          </div>
          <div>
            <h3 className="mb-3 text-sm font-bold text-[#0F2854]">Ca thi ({shifts.length})</h3>
            {shifts.length === 0 && !isLoading ? (
              <EmptyState
                compact
                icon={CalendarDays}
                title="Chưa có ca thi"
                description="Tạo ca thi sau khi có kỳ thi."
              />
            ) : (
              <div className="grid gap-2">
                {shifts.map((shift) => (
                  <article
                    key={shift.id}
                    className="rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] px-3 py-3"
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div>
                        <strong className="block text-sm text-[#0F2854]">
                          {shift.shiftName || shift.id}
                        </strong>
                        <span className="mt-1 block text-xs text-[#5b6b84]">
                          {shift.roomLab} · {shift.startTime}
                        </span>
                      </div>
                      <span className={ds.infoChip}>{shift.status}</span>
                    </div>
                  </article>
                ))}
              </div>
            )}
          </div>
        </div>
      </section>
    </div>
  )
}
