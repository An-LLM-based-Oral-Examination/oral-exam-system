/* eslint-disable react-hooks/set-state-in-effect */
import { useCallback, useEffect, useState } from 'react'
import { ArrowLeft, RefreshCw, ScanSearch, ShieldAlert } from 'lucide-react'
import { Link, useParams } from 'react-router-dom'
import { proctorService } from '@/services/proctor.service'
import { officialExamService } from '@/services/officialExam.service'
import type { ShiftAttendanceMonitor } from '@/types/proctor.types'
import type { AuditEvidenceItem, ExamAntiCheatLog } from '@/types/officialExam.types'
import { PageBanner, SectionHeader } from '@/design/ui'
import { ds } from '@/design/system'
import { EmptyState } from '@/components/common/EmptyState'
import { userMessage } from '@/lib/userMessage'

export function ExaminationOfficerShiftMonitorPage() {
  const { shiftId = '' } = useParams<{ shiftId: string }>()
  const [attendance, setAttendance] = useState<ShiftAttendanceMonitor | null>(null)
  const [evidence, setEvidence] = useState<AuditEvidenceItem[]>([])
  const [antiCheatLogs, setAntiCheatLogs] = useState<ExamAntiCheatLog[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    const [attendanceResult, evidenceResult, antiCheatResult] = await Promise.allSettled([
      proctorService.getShiftAttendance(shiftId),
      officialExamService.getAuditEvidence(shiftId),
      officialExamService.getAntiCheatLogs(shiftId),
    ])
    if (attendanceResult.status === 'fulfilled') setAttendance(attendanceResult.value)
    if (evidenceResult.status === 'fulfilled') setEvidence(evidenceResult.value)
    if (antiCheatResult.status === 'fulfilled') setAntiCheatLogs(antiCheatResult.value)
    if (
      attendanceResult.status === 'rejected' &&
      evidenceResult.status === 'rejected' &&
      antiCheatResult.status === 'rejected'
    )
      setError(userMessage(attendanceResult.reason, 'Không tải được dữ liệu giám sát.'))
    setLoading(false)
  }, [shiftId])
  useEffect(() => {
    if (shiftId) void load()
  }, [shiftId, load])
  const seats = attendance?.seats ?? []
  return (
    <div className={ds.page}>
      <PageBanner
        icon={ScanSearch}
        eyebrow="GIÁM SÁT CA THI"
        title={attendance?.shiftName || 'Giám sát MF-04'}
        description={
          attendance
            ? `${attendance.roomLab} · ${attendance.shiftStatus}`
            : 'Điểm danh, trạng thái vé và bằng chứng hậu kiểm của ca thi.'
        }
      />
      <div className="flex flex-wrap gap-2">
        <Link to="/examination-officer/monitor" className={ds.outlineButton}>
          <ArrowLeft className="size-4" /> Danh sách ca
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
      <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
        {[
          ['Tổng vé', attendance?.assignedTicketsCount ?? 0],
          ['Đã điểm danh', attendance?.checkedInCount ?? 0],
          ['Đang thi', attendance?.inProgressCount ?? 0],
          ['Đã nộp', attendance?.submittedCount ?? 0],
          ['Đình chỉ', attendance?.suspendedCount ?? 0],
        ].map(([label, value]) => (
          <article
            key={String(label)}
            className="rounded-2xl border border-[#82aedb]/55 bg-white px-4 py-4 shadow-[0_8px_18px_rgba(15,40,84,0.1)]"
          >
            <p className="text-[11px] font-bold tracking-[0.06em] text-[#5b6b84] uppercase">
              {label}
            </p>
            <strong className="mt-1 block text-2xl font-black text-[#0F2854]">{value}</strong>
          </article>
        ))}
      </section>
      <section className={ds.section}>
        <SectionHeader
          icon={ShieldAlert}
          title="Nhật ký chống gian lận"
          badge={antiCheatLogs.length}
          hint="Dữ liệu chỉ đọc từ API MF-04"
        />
        <div className="overflow-x-auto">
          <table className="w-full min-w-[760px] text-left text-sm">
            <thead className="bg-[#F4FBFD] text-xs font-bold text-[#5b6b84] uppercase">
              <tr>
                <th className="px-5 py-3">Sinh viên</th>
                <th className="px-5 py-3">Sự kiện</th>
                <th className="px-5 py-3">Mức độ</th>
                <th className="px-5 py-3">Chi tiết</th>
                <th className="px-5 py-3">Thời điểm</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#d9e8f2]">
              {antiCheatLogs.map((log) => (
                <tr key={log.id}>
                  <td className="px-5 py-3 font-semibold text-[#0F2854]">
                    {log.studentCode} · {log.studentName}
                  </td>
                  <td className="px-5 py-3 text-[#5b6b84]">{log.eventType}</td>
                  <td className="px-5 py-3">
                    <span
                      className={
                        log.severity.toUpperCase() === 'HIGH' ? ds.dangerChip : ds.infoChip
                      }
                    >
                      {log.severity}
                    </span>
                  </td>
                  <td
                    className="max-w-[320px] truncate px-5 py-3 text-[#5b6b84]"
                    title={log.details ?? ''}
                  >
                    {log.details || '—'}
                  </td>
                  <td className="px-5 py-3 whitespace-nowrap text-[#5b6b84]">
                    {new Date(log.occurredAt).toLocaleString('vi-VN')}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {!loading && antiCheatLogs.length === 0 ? (
          <div className="p-5">
            <EmptyState
              compact
              icon={ShieldAlert}
              title="Chưa có log vi phạm"
              description="Ca thi chưa có sự kiện chống gian lận được backend trả về."
            />
          </div>
        ) : null}
      </section>
      <section className={ds.section}>
        <SectionHeader
          icon={ShieldAlert}
          title="Sơ đồ ghế"
          badge={seats.length}
          hint="Dữ liệu điểm danh từ API Proctor"
        />
        <div className="grid gap-2 p-5 sm:grid-cols-2 lg:grid-cols-4 xl:grid-cols-5">
          {seats.map((seat) => (
            <article
              key={seat.seatNumber}
              className={`rounded-xl border p-3 ${seat.isSuspended ? 'border-[#f3c2c8] bg-[#fff4f5]' : seat.isCheckedIn ? 'border-[#b7e4cf] bg-[#effaf6]' : 'border-[#d9e8f2] bg-[#F8FBFD]'}`}
            >
              <div className="flex items-center justify-between">
                <strong className="text-lg text-[#0F2854]">#{seat.seatNumber}</strong>
                <span className="text-xs font-bold text-[#5b6b84]">
                  {seat.status || 'SCHEDULED'}
                </span>
              </div>
              <p className="mt-2 truncate text-xs font-semibold text-[#0F2854]">
                {seat.studentCode || seat.studentId || 'Chưa cấp vé'}
              </p>
              <p className="mt-1 truncate text-xs text-[#5b6b84]">{seat.fullName || '—'}</p>
              <p className="mt-2 truncate text-[11px] text-[#5b6b84]">
                IP: {seat.ipAddress || 'Chưa niêm phong'}
              </p>
            </article>
          ))}
          {!loading && seats.length === 0 ? (
            <EmptyState
              compact
              icon={ScanSearch}
              title="Chưa có dữ liệu ghế"
              description="Ca thi chưa có dữ liệu điểm danh."
            />
          ) : null}
        </div>
      </section>
      <section className={ds.section}>
        <SectionHeader
          icon={ShieldAlert}
          title="Bằng chứng hậu kiểm"
          badge={evidence.length}
          hint="Evidence Panel chỉ đọc cho Khảo thí"
        />
        <div className="overflow-x-auto">
          <table className="w-full min-w-[760px] text-left text-sm">
            <thead className="bg-[#F4FBFD] text-xs font-bold text-[#5b6b84] uppercase">
              <tr>
                <th className="px-5 py-3">Sinh viên</th>
                <th className="px-5 py-3">Ghế</th>
                <th className="px-5 py-3">AI score</th>
                <th className="px-5 py-3">Confidence</th>
                <th className="px-5 py-3">Trạng thái</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#d9e8f2]">
              {evidence.map((item) => (
                <tr key={`${item.ticketId}-${item.questionId ?? 'summary'}`}>
                  <td className="px-5 py-3 font-semibold text-[#0F2854]">
                    {item.studentCode} · {item.fullName}
                  </td>
                  <td className="px-5 py-3 text-[#5b6b84]">{item.seatNumber}</td>
                  <td className="px-5 py-3 text-[#5b6b84]">{item.aiScore ?? '—'}</td>
                  <td className="px-5 py-3 text-[#5b6b84]">{item.confidenceScore ?? '—'}</td>
                  <td className="px-5 py-3">
                    {item.isSuspicious ? (
                      <span className={ds.dangerChip}>Nghi ngờ</span>
                    ) : (
                      <span className={ds.successChip}>Bình thường</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {!loading && evidence.length === 0 ? (
          <div className="p-5">
            <EmptyState
              compact
              icon={ShieldAlert}
              title="Chưa có bằng chứng"
              description="Backend chưa trả về evidence cho ca thi này."
            />
          </div>
        ) : null}
      </section>
    </div>
  )
}
