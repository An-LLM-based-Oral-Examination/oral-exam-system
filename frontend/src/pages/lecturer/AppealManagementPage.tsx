import { useEffect, useState } from 'react'
import { ClipboardCheck, Gavel, LoaderCircle, RefreshCw, UserRoundCog } from 'lucide-react'
import { appealsService } from '@/services/appeals.service'
import { useAuth } from '@/hooks/useAuth'
import type { Appeal } from '@/types/appeal.types'
import { cn } from '@/lib/utils'
import { EmptyState } from '@/components/common/EmptyState'
import { FieldError, fieldInvalidClass, fieldLabelClass } from '@/components/common/formField'
import { BannerFact, LecturerPageBanner, SectionHeader } from '@/design/ui'
import { ds } from '@/design/system'

function getErrorMessage(error: unknown) {
  if (typeof error === 'object' && error !== null && 'detail' in error) {
    const detail = (error as { detail?: unknown }).detail
    if (typeof detail === 'string' && detail.trim()) return detail
  }
  return 'Không thể hoàn tất yêu cầu. Vui lòng thử lại.'
}

export function AppealManagementPage() {
  const { user } = useAuth()
  const [appeals, setAppeals] = useState<Appeal[]>([])
  const [selected, setSelected] = useState<Appeal | null>(null)
  const [lecturerId, setLecturerId] = useState('')
  const [decision, setDecision] = useState('APPROVED')
  const [score, setScore] = useState('')
  const [notes, setNotes] = useState('')
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [scoreError, setScoreError] = useState('')
  const [notesError, setNotesError] = useState('')
  const [lecturerIdError, setLecturerIdError] = useState('')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')

  const resetFieldErrors = () => {
    setScoreError('')
    setNotesError('')
    setLecturerIdError('')
  }

  /** Trả về true nếu hợp lệ; đồng thời highlight từng field lỗi. */
  const validateReviewFields = (scoreRequired: boolean) => {
    let nextScoreError = ''
    const scoreText = score.trim()
    if (!scoreText) {
      if (scoreRequired) nextScoreError = 'Vui lòng nhập điểm.'
    } else {
      const value = Number(scoreText)
      if (!Number.isFinite(value) || value < 0 || value > 10) nextScoreError = 'Điểm phải nằm trong khoảng 0 – 10.'
    }
    const nextNotesError = notes.trim().length < 10 ? 'Ghi chú cần ít nhất 10 ký tự.' : ''
    setScoreError(nextScoreError)
    setNotesError(nextNotesError)
    return !nextScoreError && !nextNotesError
  }

  const loadAppeals = async () => {
    setLoading(true); setError('')
    try { const result = await appealsService.list(); setAppeals(Array.isArray(result) ? result : []); setSelected((current) => current ? (result.find((item) => item.id === current.id) ?? current) : result[0] ?? null) } catch (loadError) { setError(getErrorMessage(loadError)) } finally { setLoading(false) }
  }

  useEffect(() => {
    const timer = window.setTimeout(() => { void loadAppeals() }, 0)
    return () => window.clearTimeout(timer)
  }, [])

  const runAction = async (action: () => Promise<unknown>, successMessage: string) => {
    setBusy(true); setError(''); setNotice('')
    try { await action(); setNotice(successMessage); await loadAppeals() } catch (actionError) { setError(getErrorMessage(actionError)) } finally { setBusy(false) }
  }

  const review = () => {
    if (!selected) return
    setLecturerIdError('')
    if (!validateReviewFields(false)) return
    void runAction(() => appealsService.review(selected.id, { decision, proposedScore: score.trim() ? Number(score) : undefined, reviewNotes: notes.trim() }), 'Đã ghi nhận quyết định phúc khảo.')
  }
  const assign = () => {
    if (!selected) return
    resetFieldErrors()
    if (!lecturerId.trim()) {
      setLecturerIdError('Vui lòng nhập Lecturer user ID.')
      return
    }
    void runAction(() => appealsService.assignLecturer(selected.id, { lecturerId: lecturerId.trim() }), 'Đã giao giảng viên chấm lại.')
  }
  const regrade = () => {
    if (!selected) return
    setLecturerIdError('')
    if (!validateReviewFields(true)) return
    void runAction(() => appealsService.regrade(selected.id, { newScore: Number(score), reviewNotes: notes.trim() }), 'Đã lưu điểm chấm lại.')
  }

  return (
    <div className={ds.page}>
      <LecturerPageBanner
        icon={ClipboardCheck}
        eyebrow="Phúc khảo"
        title="Phúc khảo điểm"
        description="Trưởng bộ môn duyệt và phân công. Giảng viên được giao gửi điểm chấm lại, và điểm đã chốt không sửa lại."
        extras={
          <>
            <BannerFact icon={Gavel}>Duyệt đơn</BannerFact>
            <BannerFact icon={UserRoundCog} tone="green">
              Phân công giảng viên
            </BannerFact>
            <BannerFact icon={ClipboardCheck} tone="violet">
              Chấm lại một lần
            </BannerFact>
          </>
        }
      />
      {(error || notice) && <div role={error ? 'alert' : 'status'} className={cn('rounded-xl border px-4 py-3 text-sm font-semibold', error ? 'border-[#f0c5c9] bg-[#fff5f6] text-[#a43c47]' : 'border-[#b7e4cf] bg-[#effaf6] text-[#176f58]')}>{error || notice}</div>}
      <section className="grid gap-4 lg:grid-cols-[minmax(280px,0.8fr)_minmax(0,1.4fr)]">
        <div className={ds.section}><SectionHeader icon={Gavel} title="Danh sách đơn" badge={`${appeals.length} đơn`} hint="Theo dõi và xử lý đơn phúc khảo" action={<button type="button" className={ds.outlineButton} onClick={() => void loadAppeals()}><RefreshCw className="size-4" /> Làm mới</button>} />{loading ? <div className="flex justify-center p-10"><LoaderCircle className="size-7 animate-spin text-[#1C4D8D]" /></div> : <div className="max-h-[640px] space-y-2 overflow-y-auto p-3">{appeals.map((appeal) => <button key={appeal.id} type="button" onClick={() => { setSelected(appeal); setScore(appeal.proposedScore?.toString() ?? appeal.originalScore?.toString() ?? ''); setNotes(appeal.reviewNotes ?? ''); resetFieldErrors() }} className={cn('w-full rounded-xl border p-3 text-left transition', selected?.id === appeal.id ? 'border-[#1C4D8D] bg-[#F4FBFD]' : 'border-[#82aedb]/55 bg-white hover:border-[#82aedb]/55')}><div className="flex items-center justify-between gap-2"><span className="font-mono text-xs font-bold text-[#1C4D8D]">{appeal.studentCode || '—'}</span><span className={ds.infoChip}>{appeal.status}</span></div><p className="mt-2 text-sm font-bold text-[#0F2854]">{appeal.studentName || 'Sinh viên'}</p><p className="mt-1 truncate text-xs text-[#687a91]">{appeal.sessionTitle || appeal.reason}</p></button>)}{appeals.length === 0 && <EmptyState compact icon={Gavel} title="Chưa có đơn phúc khảo" description="Đơn mới sẽ xuất hiện tại đây khi sinh viên gửi." />}</div>}</div>
        {selected ? <div className="space-y-4"><section className={ds.section}><SectionHeader icon={Gavel} title={`Chi tiết đơn #${selected.id.slice(0, 8)}`} hint={`${selected.studentName} · ${selected.studentCode}`} /><div className="space-y-4 p-5"><div className="rounded-xl border border-[#82aedb]/55 bg-[#f8fbfd] p-4 text-sm leading-relaxed text-[#52657d]"><p><strong>Lý do:</strong> {selected.reason}</p><p className="mt-2"><strong>Điểm gốc:</strong> {selected.originalScore ?? '—'} · <strong>Trạng thái:</strong> {selected.status}</p>{selected.assignedToName && <p className="mt-2"><strong>Giảng viên:</strong> {selected.assignedToName}</p>}</div><div className="grid gap-3 sm:grid-cols-2"><label className="text-xs font-bold text-[#63758e]">Quyết định<select className={cn(ds.field, 'mt-1')} value={decision} onChange={(event) => setDecision(event.target.value)}><option value="APPROVED">APPROVED</option><option value="REJECTED">REJECTED</option></select></label><label className={fieldLabelClass(Boolean(scoreError))}>Điểm đề xuất<input type="number" min="0" max="10" step="0.1" className={cn(ds.field, 'mt-1', scoreError && fieldInvalidClass)} value={score} onChange={(event) => { setScore(event.target.value); if (scoreError) setScoreError('') }} aria-invalid={Boolean(scoreError)} /><FieldError message={scoreError} /></label></div><div><textarea className={cn(ds.field, 'min-h-24 py-2', notesError && fieldInvalidClass)} placeholder="Ghi chú xử lý / giải trình (tối thiểu 10 ký tự)" value={notes} onChange={(event) => { setNotes(event.target.value); if (notesError) setNotesError('') }} aria-label="Ghi chú xử lý" aria-invalid={Boolean(notesError)} /><FieldError message={notesError} /></div><div className="flex flex-wrap gap-2">{(user?.role === 'DepartmentHead' || user?.role === 'Admin') && <button type="button" disabled={busy} onClick={review} className={ds.primaryButton}><Gavel className="size-4" /> Duyệt quyết định</button>}{user?.role === 'DepartmentHead' && <><div className="max-w-[220px]"><input className={cn(ds.field, lecturerIdError && fieldInvalidClass)} placeholder="Lecturer user ID" value={lecturerId} onChange={(event) => { setLecturerId(event.target.value); if (lecturerIdError) setLecturerIdError('') }} aria-label="Lecturer user ID" aria-invalid={Boolean(lecturerIdError)} /><FieldError message={lecturerIdError} /></div><button type="button" disabled={busy} onClick={assign} className={ds.outlineButton}><UserRoundCog className="size-4" /> Giao chấm lại</button></>}{user?.role === 'Instructor' && <button type="button" disabled={busy} onClick={regrade} className={ds.primaryButton}><Gavel className="size-4" /> Gửi điểm chấm lại</button>}</div></div></section></div> : <EmptyState className="min-h-[320px] justify-center" icon={Gavel} title="Chưa chọn đơn" description="Chọn một đơn phúc khảo ở danh sách bên trái để xử lý." />}
      </section>
    </div>
  )
}

