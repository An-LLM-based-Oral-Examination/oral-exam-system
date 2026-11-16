/* eslint-disable react-hooks/set-state-in-effect */
import { useCallback, useEffect, useMemo, useState } from 'react'
import { ArrowLeft, RefreshCw, UploadCloud } from 'lucide-react'
import { Link, useParams } from 'react-router-dom'
import { officialExamService } from '@/services/officialExam.service'
import type { ExamSet, ExamShift, OfficialExamSession } from '@/types/officialExam.types'
import { PageBanner } from '@/design/ui'
import { ds } from '@/design/system'
import { userMessage } from '@/lib/userMessage'
import { getShiftCapacityMetrics } from '@/lib/shiftCapacity'
import { BulkTicketImportPanel } from './BulkTicketImportPanel'

function formatDateTime(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value

  return new Intl.DateTimeFormat('vi-VN', {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(date)
}

export function ExaminationOfficerRosterImportPage() {
  const { shiftId = '' } = useParams<{ shiftId: string }>()
  const [shift, setShift] = useState<ExamShift | null>(null)
  const [session, setSession] = useState<OfficialExamSession | null>(null)
  const [handedOverSets, setHandedOverSets] = useState<ExamSet[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')

  const loadShift = useCallback(async () => {
    if (!shiftId) {
      setIsLoading(false)
      setError('Không tìm thấy mã ca thi.')
      return
    }

    setIsLoading(true)
    setError('')
    try {
      const [shifts, sessions] = await Promise.all([
        officialExamService.getShifts(),
        officialExamService.getSessions(),
      ])
      const currentShift = shifts.find((item) => item.id === shiftId) ?? null
      const currentSession = currentShift
        ? (sessions.find((item) => item.id === currentShift.sessionId) ?? null)
        : null
      let loadedSets: ExamSet[] = []
      if (currentSession && typeof officialExamService.getExamSets === 'function') {
        try {
          loadedSets = await officialExamService.getExamSets({
            courseId: currentSession.courseId,
            sessionId: currentSession.id,
            status: 'HANDED_OVER',
          })
        } catch {
          loadedSets = []
        }
      }
      setShift(currentShift)
      setSession(currentSession)
      setHandedOverSets(loadedSets)
      if (!currentShift) setError('Không tìm thấy ca thi được chọn.')
    } catch (loadError) {
      setError(userMessage(loadError, 'Không tải được thông tin ca thi.'))
    } finally {
      setIsLoading(false)
    }
  }, [shiftId])

  useEffect(() => {
    void loadShift()
  }, [loadShift])

  const shiftDescription = useMemo(() => {
    if (!shift) return isLoading ? 'Đang tải thông tin ca thi' : 'Chưa có thông tin ca thi'

    return [
      session?.title,
      shift.roomLab ? `Phòng ${shift.roomLab}` : null,
      `${formatDateTime(shift.startTime)} – ${formatDateTime(shift.endTime)}`,
    ]
      .filter(Boolean)
      .join(' · ')
  }, [isLoading, session, shift])

  return (
    <div className={ds.page}>
      <PageBanner
        icon={UploadCloud}
        eyebrow="MF-04 · IMPORT DSSV"
        title="Import danh sách và phát vé hàng loạt"
        description={shiftDescription}
      />

      <div className="flex flex-wrap gap-2">
        <Link to="/examination-officer/tickets/assign" className={ds.outlineButton}>
          <ArrowLeft className="size-4" /> Quay lại cấp vé
        </Link>
        <button
          type="button"
          onClick={() => void loadShift()}
          disabled={isLoading}
          className={ds.outlineButton}
        >
          <RefreshCw className={isLoading ? 'size-4 animate-spin' : 'size-4'} /> Làm mới ca thi
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

      {shift ? (
        <>
          <section className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            {(() => {
              const capacity = getShiftCapacityMetrics(shift)
              return [
                ['Sức chứa tối đa', capacity.maxSeats ?? '—'],
                ['Số vé đã phát', capacity.assignedTicketsCount],
                ['Số chỗ còn lại', capacity.remainingSeats ?? '—'],
                ['Bộ đề HANDED_OVER', handedOverSets.length],
              ].map(([label, value]) => (
                <div
                  key={String(label)}
                  className="rounded-2xl border border-[#d9e8f2] bg-white px-4 py-3"
                >
                  <span className="block text-xs font-bold tracking-[0.06em] text-[#5b6b84] uppercase">
                    {label}
                  </span>
                  <strong className="mt-1 block text-2xl font-black text-[#0F2854]">{value}</strong>
                </div>
              ))
            })()}
          </section>
          <BulkTicketImportPanel
            key={shift.id}
            shiftId={shift.id}
            shift={shift}
            availableHandedOverSetCount={handedOverSets.length}
            onRefreshShift={loadShift}
          />
        </>
      ) : null}
    </div>
  )
}
