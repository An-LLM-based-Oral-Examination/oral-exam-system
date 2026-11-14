/* eslint-disable react-hooks/set-state-in-effect */
import { useCallback, useEffect, useState } from 'react'
import { ArrowLeft, Download, FileBarChart2, LockKeyhole, RefreshCw } from 'lucide-react'
import { Link, useParams } from 'react-router-dom'
import { officialExamService } from '@/services/officialExam.service'
import type { AuditEvidenceItem, PublishGradesResponse } from '@/types/officialExam.types'
import { PageBanner, SectionHeader } from '@/design/ui'
import { ds } from '@/design/system'
import { EmptyState } from '@/components/common/EmptyState'
import { toast } from '@/components/common/toastStore'
import { userMessage } from '@/lib/userMessage'

export function ExaminationOfficerGradePublicationPage() {
  const { shiftId = '' } = useParams<{ shiftId: string }>()
  const [evidence, setEvidence] = useState<AuditEvidenceItem[]>([])
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [published, setPublished] = useState<PublishGradesResponse | null>(null)
  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      setEvidence(await officialExamService.getAuditEvidence(shiftId))
    } catch (loadError) {
      setError(userMessage(loadError, 'Không tải được dữ liệu hậu kiểm.'))
    } finally {
      setLoading(false)
    }
  }, [shiftId])
  useEffect(() => {
    if (shiftId) void load()
  }, [shiftId, load])
  const publish = async () => {
    if (
      !window.confirm('Công bố điểm sẽ kích hoạt khóa một chiều. Bạn có chắc chắn muốn tiếp tục?')
    )
      return
    setBusy(true)
    setError('')
    try {
      const result = await officialExamService.publishGrades(shiftId)
      setPublished(result)
      toast.success(result.message || 'Đã công bố điểm.')
      await load()
    } catch (publishError) {
      setError(userMessage(publishError, 'Không thể công bố điểm.'))
    } finally {
      setBusy(false)
    }
  }
  const download = async (kind: 'excel' | 'pdf') => {
    setBusy(true)
    try {
      const file =
        kind === 'excel'
          ? await officialExamService.exportShiftExcel(shiftId)
          : await officialExamService.exportShiftPdf(shiftId)
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
  const suspiciousCount = evidence.filter((item) => item.isSuspicious).length
  return (
    <div className={ds.page}>
      <PageBanner
        icon={FileBarChart2}
        eyebrow="CÔNG BỐ ĐIỂM"
        title="Hậu kiểm và khóa điểm"
        description="Chỉ công bố sau khi backend xác nhận điều kiện và kích hoạt One-Way Lock."
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
            {published ? 'ĐÃ CÔNG BỐ' : 'CHƯA CÔNG BỐ'}
          </strong>
        </article>
      </section>
      <section className={ds.section}>
        <SectionHeader
          icon={LockKeyhole}
          title="Cổng công bố điểm"
          hint="Thao tác này không thể hoàn tác theo luồng thông thường."
          action={
            <button
              type="button"
              onClick={() => void publish()}
              disabled={busy || loading || evidence.length === 0 || Boolean(published)}
              className={ds.primaryButton}
            >
              <LockKeyhole className="size-4" /> {published ? 'Đã công bố' : 'Công bố điểm'}
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
    </div>
  )
}
