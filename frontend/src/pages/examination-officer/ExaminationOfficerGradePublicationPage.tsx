import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  AlertTriangle,
  ArrowLeft,
  Download,
  FileBarChart2,
  LockKeyhole,
  RefreshCw,
  ShieldCheck,
} from 'lucide-react'
import { Link, useParams } from 'react-router-dom'
import { officialExamService } from '@/services/officialExam.service'
import type {
  AuditEvidenceItem,
  ExamShift,
  PublishGradesResponse,
} from '@/types/officialExam.types'
import { PageBanner, SectionHeader } from '@/design/ui'
import { ConfirmDialog } from '@/design/ConfirmDialog'
import { ds } from '@/design/system'
import { EmptyState } from '@/components/common/EmptyState'
import { toast } from '@/components/common/toastStore'
import { userMessage } from '@/lib/userMessage'
import {
  formatEoTimeRange,
  getShiftStatusMeta,
} from '@/components/examination-officer/eoFormatters'

const REVIEWED_TICKET_STATUSES = new Set(['AUDITED', 'PUBLISHED'])

export function ExaminationOfficerGradePublicationPage() {
  const { shiftId = '' } = useParams<{ shiftId: string }>()
  const [shift, setShift] = useState<ExamShift | null>(null)
  const [evidence, setEvidence] = useState<AuditEvidenceItem[]>([])
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [published, setPublished] = useState<PublishGradesResponse | null>(null)
  const [confirmPublishOpen, setConfirmPublishOpen] = useState(false)
  const [certificateHash, setCertificateHash] = useState<string | null>(null)

  const load = useCallback(async () => {
    if (!shiftId) {
      setLoading(false)
      return
    }
    setLoading(true)
    setError('')
    const [evidenceResult, shiftsResult] = await Promise.allSettled([
      officialExamService.getAuditEvidence(shiftId),
      typeof officialExamService.getShifts === 'function'
        ? officialExamService.getShifts()
        : Promise.resolve([] as ExamShift[]),
    ])

    if (evidenceResult.status === 'fulfilled') {
      setEvidence(evidenceResult.value)
    } else {
      setError(userMessage(evidenceResult.reason, 'Không tải được dữ liệu hậu kiểm.'))
    }

    if (shiftsResult.status === 'fulfilled') {
      const matched = shiftsResult.value.find((item) => item.id === shiftId) ?? null
      setShift(matched)
      if (matched?.certificateHash) {
        setCertificateHash(matched.certificateHash)
      }
    }

    setLoading(false)
  }, [shiftId])

  useEffect(() => {
    const timer = window.setTimeout(() => {
      if (shiftId) void load()
    }, 0)
    return () => window.clearTimeout(timer)
  }, [shiftId, load])

  const executePublish = async () => {
    if (busy || !shiftId) return
    setBusy(true)
    setError('')
    try {
      const result = await officialExamService.publishGrades(shiftId)
      setPublished(result)
      setConfirmPublishOpen(false)
      toast.success(result.message || 'Đã công bố điểm.')
      await load()
    } catch (publishError) {
      const message = userMessage(publishError, 'Không thể công bố điểm.')
      setError(message)
      toast.error(message)
      setConfirmPublishOpen(false)
    } finally {
      setBusy(false)
    }
  }

  const download = async (kind: 'excel' | 'pdf') => {
    setBusy(true)
    setError('')
    try {
      const file =
        kind === 'excel'
          ? await officialExamService.exportShiftExcel(shiftId)
          : await officialExamService.exportShiftPdf(shiftId)
      if (file.certificateHash) {
        setCertificateHash(file.certificateHash)
      }
      const objectUrl = URL.createObjectURL(file.blob)
      const anchor = document.createElement('a')
      anchor.href = objectUrl
      anchor.download = file.fileName
      anchor.style.display = 'none'
      document.body.appendChild(anchor)
      anchor.click()
      anchor.remove()
      URL.revokeObjectURL(objectUrl)
      toast.success(`Đã tải báo cáo ${kind.toUpperCase()}.`)
    } catch (downloadError) {
      setError(userMessage(downloadError, 'Không tải được báo cáo.'))
    } finally {
      setBusy(false)
    }
  }

  const suspiciousCount = useMemo(
    () => evidence.filter((item) => item.isSuspicious).length,
    [evidence],
  )

  const unresolvedSuspiciousCount = useMemo(
    () =>
      evidence.filter(
        (item) =>
          item.isSuspicious &&
          !item.isLocked &&
          !REVIEWED_TICKET_STATUSES.has(item.status.toUpperCase()),
      ).length,
    [evidence],
  )

  const ungradedCount = useMemo(
    () => evidence.filter((item) => item.aiScore === null && item.finalScore === null).length,
    [evidence],
  )

  const isAlreadyPublished = useMemo(() => {
    if (published) return true
    if (shift?.isGradesPublished || Boolean(shift?.gradesPublishedAt)) return true
    if (evidence.length === 0) return false
    return (
      evidence.every((item) => item.isLocked) ||
      evidence.some((item) => item.status.toUpperCase() === 'PUBLISHED')
    )
  }, [evidence, published, shift])

  const publishedTimestamp = published?.publishedAt ?? shift?.gradesPublishedAt ?? null
  const publishBlockedReasons = useMemo(() => {
    const reasons: string[] = []
    const shiftStatus = shift?.status?.trim().toLowerCase()
    if (shiftStatus === 'scheduled' || shiftStatus === 'cancelled') {
      reasons.push(
        shiftStatus === 'cancelled'
          ? 'Ca thi đã bị hủy nên không thể công bố điểm.'
          : 'Ca thi chưa kết thúc (trạng thái Đã lên lịch).',
      )
    }
    if (ungradedCount > 0) {
      reasons.push(`Còn ${ungradedCount} bài thi chưa hoàn tất chấm AI.`)
    }
    if (unresolvedSuspiciousCount > 0) {
      reasons.push(`Còn ${unresolvedSuspiciousCount} bài nghi ngờ chưa được Giám khảo hậu kiểm (AUDITED).`)
    }
    return reasons
  }, [shift?.status, ungradedCount, unresolvedSuspiciousCount])

  const canPublish =
    !busy &&
    !loading &&
    !isAlreadyPublished &&
    evidence.length > 0 &&
    publishBlockedReasons.length === 0

  return (
    <div className={ds.page}>
      <PageBanner
        icon={FileBarChart2}
        eyebrow="CÔNG BỐ ĐIỂM"
        title={shift ? `Hậu kiểm & khóa điểm · ${shift.shiftName}` : 'Hậu kiểm và khóa điểm'}
        description={
          shift
            ? `Phòng ${shift.roomLab} · ${formatEoTimeRange(shift.startTime, shift.endTime)} · ${getShiftStatusMeta(shift.status).label}`
            : 'Chỉ công bố sau khi hoàn tất chấm AI & hậu kiểm. Thao tác kích hoạt One-Way Lock và đồng bộ trạng thái bền vững qua máy chủ.'
        }
      />
      <div className="flex flex-wrap gap-2">
        <Link to="/examination-officer/grades" className={ds.outlineButton}>
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
        <button
          type="button"
          onClick={() => void download('excel')}
          disabled={busy}
          className={ds.outlineButton}
        >
          <Download className="size-4" /> Excel
        </button>
        <button
          type="button"
          onClick={() => void download('pdf')}
          disabled={busy}
          className={ds.outlineButton}
        >
          <Download className="size-4" /> PDF
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
      {certificateHash ? (
        <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-[#b7e4cf] bg-[#effaf6] px-4 py-3 text-xs font-semibold text-[#176f58]">
          <div className="flex flex-wrap items-center gap-2">
            <ShieldCheck className="size-4 shrink-0" aria-hidden="true" />
            <span>Mã chứng thực PKI (X-Certificate-Hash):</span>
            <code className="font-mono break-all text-[#0F2854]">{certificateHash}</code>
          </div>
          <Link
            to={`/verify/grades/${encodeURIComponent(certificateHash)}`}
            target="_blank"
            rel="noreferrer"
            className="rounded-lg border border-[#b7e4cf] bg-white px-3 py-1.5 text-xs font-bold text-[#176f58] hover:bg-[#f0fdf4]"
          >
            Xác thực chứng thư QR →
          </Link>
        </div>
      ) : null}
      <section className="grid gap-3 sm:grid-cols-3">
        <article className="rounded-2xl border border-[#d9e8f2] bg-white px-4 py-4">
          <p className="text-xs font-bold text-[#5b6b84] uppercase">Bằng chứng</p>
          <strong className="mt-1 block text-2xl font-black text-[#0F2854]">
            {evidence.length}
          </strong>
        </article>
        <article className="rounded-2xl border border-[#f3d19a] bg-[#fff8ed] px-4 py-4">
          <p className="text-xs font-bold text-[#8a5511] uppercase">Nghi ngờ</p>
          <strong className="mt-1 block text-2xl font-black text-[#8a5511]">
            {suspiciousCount}
          </strong>
        </article>
        <article className="rounded-2xl border border-[#b7e4cf] bg-[#effaf6] px-4 py-4">
          <p className="text-xs font-bold text-[#16836a] uppercase">Trạng thái</p>
          <strong className="mt-1 block text-sm font-black text-[#16836a]">
            {isAlreadyPublished ? 'ĐÃ CÔNG BỐ' : 'CHƯA CÔNG BỐ'}
          </strong>
          {isAlreadyPublished && publishedTimestamp ? (
            <span className="mt-1 block text-[11px] font-medium text-[#3b7a68]">
              {new Date(publishedTimestamp).toLocaleString('vi-VN')}
            </span>
          ) : null}
        </article>
      </section>

      {!isAlreadyPublished && publishBlockedReasons.length > 0 ? (
        <div
          role="status"
          className="flex items-start gap-2.5 rounded-xl border border-[#f3d19a] bg-[#fff8ed] px-4 py-3 text-xs font-semibold text-[#8a5511]"
        >
          <AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
          <div className="space-y-1">
            <p className="font-bold">Chưa đủ điều kiện công bố điểm:</p>
            <ul className="list-disc pl-4">
              {publishBlockedReasons.map((reason) => (
                <li key={reason}>{reason}</li>
              ))}
            </ul>
          </div>
        </div>
      ) : null}

      <section className={ds.section}>
        <SectionHeader
          icon={LockKeyhole}
          title="Cổng công bố điểm"
          hint={
            isAlreadyPublished
              ? 'Điểm đã khóa một chiều (One-Way Lock). Chỉ được điều chỉnh qua quy trình Phúc khảo 5 bước.'
              : 'Thao tác này không thể hoàn tác theo luồng thông thường.'
          }
          action={
            <button
              type="button"
              onClick={() => setConfirmPublishOpen(true)}
              disabled={!canPublish}
              className={ds.primaryButton}
            >
              <LockKeyhole className="size-4" />{' '}
              {isAlreadyPublished ? 'Đã công bố' : 'Công bố điểm'}
            </button>
          }
        />
        <div className="overflow-x-auto">
          <table className="w-full min-w-[760px] text-left text-sm">
            <thead className="bg-[#F4FBFD] text-xs font-bold text-[#5b6b84] uppercase">
              <tr>
                <th className="px-5 py-3">Sinh viên</th>
                <th className="px-5 py-3">Điểm AI</th>
                <th className="px-5 py-3">Điểm cuối</th>
                <th className="px-5 py-3">Khóa</th>
                <th className="px-5 py-3">Kiểm tra</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#d9e8f2]">
              {evidence.map((item) => (
                <tr key={`${item.ticketId}-${item.questionId ?? 'summary'}`}>
                  <td className="px-5 py-3 font-semibold text-[#0F2854]">
                    {item.studentCode} · {item.fullName}
                  </td>
                  <td className="px-5 py-3 text-[#5b6b84]">{item.aiScore ?? '—'}</td>
                  <td className="px-5 py-3 text-[#5b6b84]">{item.finalScore ?? '—'}</td>
                  <td className="px-5 py-3">
                    {item.isLocked ? (
                      <span className={ds.successChip}>LOCKED</span>
                    ) : (
                      <span className={ds.infoChip}>Mở</span>
                    )}
                  </td>
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
              icon={LockKeyhole}
              title="Chưa có dữ liệu hậu kiểm"
              description="Chưa thể công bố khi backend chưa trả về bằng chứng."
            />
          </div>
        ) : null}
      </section>

      {confirmPublishOpen ? (
        <ConfirmDialog
          variant="calm"
          tone="danger"
          eyebrow="ONE-WAY LOCK · KHÔNG THỂ HOÀN TÁC"
          title="Xác nhận công bố điểm toàn ca thi?"
          message={`Hệ thống sẽ công bố điểm cho ${evidence.length} bằng chứng bài thi và kích hoạt khóa một chiều (One-Way Lock). Mọi điều chỉnh về sau chỉ được thực hiện qua quy trình Phúc khảo 5 bước.`}
          confirmLabel="Xác nhận công bố điểm"
          cancelLabel="Hủy"
          pending={busy}
          onConfirm={() => void executePublish()}
          onCancel={() => !busy && setConfirmPublishOpen(false)}
        />
      ) : null}
    </div>
  )
}
