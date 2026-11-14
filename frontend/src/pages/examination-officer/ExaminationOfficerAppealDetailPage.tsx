/* eslint-disable react-hooks/set-state-in-effect */
import { useCallback, useEffect, useState } from 'react'
import { ArrowLeft, ClipboardCheck, UserRoundCheck } from 'lucide-react'
import { Link, useParams } from 'react-router-dom'
import { appealsService } from '@/services/appeals.service'
import type { Appeal } from '@/types/appeal.types'
import { PageBanner, SectionHeader } from '@/design/ui'
import { ds } from '@/design/system'
import { toast } from '@/components/common/toastStore'
import { userMessage } from '@/lib/userMessage'

export function ExaminationOfficerAppealDetailPage() {
  const { appealId = '' } = useParams<{ appealId: string }>()
  const [appeal, setAppeal] = useState<Appeal | null>(null)
  const [lecturerId, setLecturerId] = useState('')
  const [notes, setNotes] = useState('')
  const [decision, setDecision] = useState('IN_REVIEW')
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      setAppeal(await appealsService.getById(appealId))
    } catch (loadError) {
      setError(userMessage(loadError, 'Không tải được đơn phúc khảo.'))
    } finally {
      setLoading(false)
    }
  }, [appealId])
  useEffect(() => {
    if (appealId) void load()
  }, [appealId, load])
  const review = async () => {
    if (!appeal) return
    setBusy(true)
    setError('')
    try {
      const result = await appealsService.review(appeal.id, { decision, reviewNotes: notes.trim() })
      setAppeal(result)
      toast.success('Đã ghi nhận thẩm tra phúc khảo.')
    } catch (actionError) {
      setError(userMessage(actionError, 'Không thể thẩm tra đơn.'))
    } finally {
      setBusy(false)
    }
  }
  const assign = async () => {
    if (!appeal || !lecturerId.trim()) {
      setError('Vui lòng nhập Lecturer user ID.')
      return
    }
    setBusy(true)
    setError('')
    try {
      const result = await appealsService.assignLecturer(appeal.id, {
        lecturerId: lecturerId.trim(),
      })
      setAppeal(result)
      toast.success('Đã phân công Giảng viên chấm lại.')
    } catch (actionError) {
      setError(userMessage(actionError, 'Không thể phân công Giảng viên.'))
    } finally {
      setBusy(false)
    }
  }
  return (
    <div className={ds.page}>
      <PageBanner
        icon={ClipboardCheck}
        eyebrow="CHI TIẾT PHÚC KHẢO"
        title={appeal?.studentName || 'Đơn phúc khảo'}
        description={appeal?.reason || 'Thẩm tra đơn và phân công Giảng viên chấm lại độc lập.'}
      />
      <div className="flex flex-wrap gap-2">
        <Link to="/examination-officer/appeals" className={ds.outlineButton}>
          <ArrowLeft className="size-4" /> Danh sách đơn
        </Link>
        <button
          type="button"
          onClick={() => void load()}
          disabled={loading}
          className={ds.outlineButton}
        >
          Làm mới
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
      {appeal ? (
        <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(320px,0.75fr)]">
          <section className={ds.section}>
            <SectionHeader
              icon={ClipboardCheck}
              title="Thông tin đơn"
              badge={appeal.status}
              hint="Dữ liệu trả về từ backend"
            />
            <dl className="grid gap-4 p-5 sm:grid-cols-2">
              <div>
                <dt className="text-xs font-bold text-[#5b6b84] uppercase">Sinh viên</dt>
                <dd className="mt-1 font-semibold text-[#0F2854]">
                  {appeal.studentCode} · {appeal.studentName}
                </dd>
              </div>
              <div>
                <dt className="text-xs font-bold text-[#5b6b84] uppercase">Kỳ thi</dt>
                <dd className="mt-1 font-semibold text-[#0F2854]">{appeal.sessionTitle}</dd>
              </div>
              <div>
                <dt className="text-xs font-bold text-[#5b6b84] uppercase">Điểm gốc</dt>
                <dd className="mt-1 font-semibold text-[#0F2854]">{appeal.originalScore ?? '—'}</dd>
              </div>
              <div>
                <dt className="text-xs font-bold text-[#5b6b84] uppercase">Giảng viên</dt>
                <dd className="mt-1 font-semibold text-[#0F2854]">
                  {appeal.assignedToName || appeal.assignedTo || 'Chưa phân công'}
                </dd>
              </div>
              <div className="sm:col-span-2">
                <dt className="text-xs font-bold text-[#5b6b84] uppercase">Lý do</dt>
                <dd className="mt-1 text-sm whitespace-pre-wrap text-[#0F2854]">{appeal.reason}</dd>
              </div>
            </dl>
          </section>
          <div className="grid gap-4">
            <section className={ds.section}>
              <SectionHeader
                icon={ClipboardCheck}
                title="Thẩm tra"
                hint="Ghi nhận trạng thái và nhận xét"
              />
              <div className="grid gap-3 p-5">
                <label className="grid gap-1.5 text-xs font-bold text-[#52657d]">
                  Trạng thái
                  <select
                    className={ds.field}
                    value={decision}
                    onChange={(event) => setDecision(event.target.value)}
                  >
                    <option value="IN_REVIEW">IN_REVIEW</option>
                    <option value="APPROVED">APPROVED</option>
                    <option value="REJECTED">REJECTED</option>
                  </select>
                </label>
                <label className="grid gap-1.5 text-xs font-bold text-[#52657d]">
                  Ghi chú
                  <textarea
                    className={`${ds.field} min-h-28 py-3`}
                    value={notes}
                    onChange={(event) => setNotes(event.target.value)}
                    placeholder="Nhập nhận xét thẩm tra"
                  />
                </label>
                <button
                  type="button"
                  onClick={() => void review()}
                  disabled={busy}
                  className={ds.primaryButton}
                >
                  <ClipboardCheck className="size-4" /> Ghi nhận thẩm tra
                </button>
              </div>
            </section>
            <section className={ds.section}>
              <SectionHeader
                icon={UserRoundCheck}
                title="Phân công Giảng viên"
                hint="Khảo thí trực tiếp phân công người chấm lại"
              />
              <div className="grid gap-3 p-5">
                <label className="grid gap-1.5 text-xs font-bold text-[#52657d]">
                  Lecturer user ID
                  <input
                    className={ds.field}
                    value={lecturerId}
                    onChange={(event) => setLecturerId(event.target.value)}
                    placeholder="UUID tài khoản Giảng viên"
                  />
                </label>
                <button
                  type="button"
                  onClick={() => void assign()}
                  disabled={busy}
                  className={ds.primaryButton}
                >
                  <UserRoundCheck className="size-4" /> Phân công chấm lại
                </button>
              </div>
            </section>
          </div>
        </div>
      ) : !loading ? (
        <div className={ds.section}>
          <div className="p-5">Không tìm thấy đơn phúc khảo.</div>
        </div>
      ) : null}
    </div>
  )
}
