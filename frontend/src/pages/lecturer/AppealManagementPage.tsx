import { useEffect, useState } from 'react'
import { ClipboardCheck, Gavel, LoaderCircle, RefreshCw, UserRoundCog } from 'lucide-react'
import { appealsService } from '@/services/appeals.service'
import { useAuth } from '@/hooks/useAuth'
import type { Appeal } from '@/types/appeal.types'
import { cn } from '@/lib/utils'
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
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')

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

  const review = () => selected && void runAction(() => appealsService.review(selected.id, { decision, proposedScore: score ? Number(score) : undefined, reviewNotes: notes.trim() }), 'Đã ghi nhận quyết định phúc khảo.')
  const assign = () => selected && lecturerId.trim() && void runAction(() => appealsService.assignLecturer(selected.id, { lecturerId: lecturerId.trim() }), 'Đã giao giảng viên chấm lại.')
  const regrade = () => selected && score && notes.trim() && void runAction(() => appealsService.regrade(selected.id, { newScore: Number(score), reviewNotes: notes.trim() }), 'Đã lưu điểm chấm lại.')

  return (
    <div className={ds.page}>
      <section className={cn(ds.banner, 'p-5 sm:p-6')}><p className={ds.bannerChip}><ClipboardCheck className="size-4" /> MF-04 · Phúc khảo nội bộ</p><h1 className={ds.bannerTitle}>Tiếp nhận & xử lý phúc khảo</h1><p className={ds.bannerText}>Trưởng bộ môn duyệt và phân công; giảng viên được giao gửi điểm chấm lại theo đúng One-Way Lock.</p></section>
      {(error || notice) && <div role={error ? 'alert' : 'status'} className={cn('rounded-xl border px-4 py-3 text-sm font-semibold', error ? 'border-[#f0c5c9] bg-[#fff5f6] text-[#a43c47]' : 'border-[#b7e4cf] bg-[#effaf6] text-[#176f58]')}>{error || notice}</div>}
      <section className="grid gap-4 lg:grid-cols-[minmax(280px,0.8fr)_minmax(0,1.4fr)]">
        <div className={ds.section}><div className={cn(ds.sectionHeader, 'flex items-center justify-between')}><div><h2 className={ds.sectionTitleRow}><Gavel className={ds.sectionIcon} /> <span className="text-sm font-bold text-[#0F2854]">Danh sách đơn</span></h2><p className={ds.sectionHint}>{appeals.length} đơn phúc khảo</p></div><button type="button" className={ds.outlineButton} onClick={() => void loadAppeals()}><RefreshCw className="size-4" /> Làm mới</button></div>{loading ? <div className="flex justify-center p-10"><LoaderCircle className="size-7 animate-spin text-[#1C4D8D]" /></div> : <div className="max-h-[640px] space-y-2 overflow-y-auto p-3">{appeals.map((appeal) => <button key={appeal.id} type="button" onClick={() => { setSelected(appeal); setScore(appeal.proposedScore?.toString() ?? appeal.originalScore?.toString() ?? ''); setNotes(appeal.reviewNotes ?? '') }} className={cn('w-full rounded-xl border p-3 text-left transition', selected?.id === appeal.id ? 'border-[#1C4D8D] bg-[#F4FBFD]' : 'border-[#dce5ef] bg-white hover:border-[#4988C4]')}><div className="flex items-center justify-between gap-2"><span className="font-mono text-xs font-bold text-[#1C4D8D]">{appeal.studentCode || '—'}</span><span className={ds.infoChip}>{appeal.status}</span></div><p className="mt-2 text-sm font-bold text-[#0F2854]">{appeal.studentName || 'Sinh viên'}</p><p className="mt-1 truncate text-xs text-[#687a91]">{appeal.sessionTitle || appeal.reason}</p></button>)}{appeals.length === 0 && <p className="p-5 text-center text-sm text-[#687a91]">Chưa có đơn phúc khảo.</p>}</div>}</div>
        {selected ? <div className="space-y-4"><section className={ds.section}><div className={ds.sectionHeader}><h2 className={ds.sectionTitleRow}><Gavel className={ds.sectionIcon} /> <span className="text-sm font-bold text-[#0F2854]">Chi tiết đơn #{selected.id.slice(0, 8)}</span></h2><p className={ds.sectionHint}>{selected.studentName} · {selected.studentCode}</p></div><div className="space-y-4 p-5"><div className="rounded-xl border border-[#dce5ef] bg-[#f8fbfd] p-4 text-sm leading-relaxed text-[#52657d]"><p><strong>Lý do:</strong> {selected.reason}</p><p className="mt-2"><strong>Điểm gốc:</strong> {selected.originalScore ?? '—'} · <strong>Trạng thái:</strong> {selected.status}</p>{selected.assignedToName && <p className="mt-2"><strong>Giảng viên:</strong> {selected.assignedToName}</p>}</div><div className="grid gap-3 sm:grid-cols-2"><label className="text-xs font-bold text-[#63758e]">Quyết định<select className={cn(ds.field, 'mt-1')} value={decision} onChange={(event) => setDecision(event.target.value)}><option value="APPROVED">APPROVED</option><option value="REJECTED">REJECTED</option></select></label><label className="text-xs font-bold text-[#63758e]">Điểm đề xuất<input type="number" min="0" max="10" step="0.1" className={cn(ds.field, 'mt-1')} value={score} onChange={(event) => setScore(event.target.value)} /></label></div><textarea className={cn(ds.field, 'min-h-24 py-2')} placeholder="Ghi chú xử lý / giải trình (tối thiểu 10 ký tự)" value={notes} onChange={(event) => setNotes(event.target.value)} /><div className="flex flex-wrap gap-2">{(user?.role === 'DepartmentHead' || user?.role === 'Admin') && <button type="button" disabled={busy} onClick={review} className={ds.primaryButton}><Gavel className="size-4" /> Duyệt quyết định</button>}{user?.role === 'DepartmentHead' && <><input className={cn(ds.field, 'max-w-[220px]')} placeholder="Lecturer user ID" value={lecturerId} onChange={(event) => setLecturerId(event.target.value)} /><button type="button" disabled={busy || !lecturerId.trim()} onClick={assign} className={ds.outlineButton}><UserRoundCog className="size-4" /> Giao chấm lại</button></>}{user?.role === 'Instructor' && <button type="button" disabled={busy || !score || notes.trim().length < 10} onClick={regrade} className={ds.primaryButton}><Gavel className="size-4" /> Gửi điểm chấm lại</button>}</div></div></section></div> : <section className="flex min-h-[320px] items-center justify-center rounded-2xl border border-dashed border-[#4988C4]/45 bg-white text-sm text-[#687a91]">Chọn một đơn phúc khảo để xử lý.</section>}
      </section>
    </div>
  )
}

