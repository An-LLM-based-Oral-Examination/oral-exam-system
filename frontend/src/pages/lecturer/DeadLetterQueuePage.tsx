import { useCallback, useEffect, useState } from 'react'
import { AlertTriangle, LoaderCircle, RefreshCw, RotateCcw } from 'lucide-react'
import { deadLetterQueueService } from '@/services/deadLetterQueue.service'
import type { DeadLetterQueueTask } from '@/types/deadLetterQueue.types'
import { cn } from '@/lib/utils'
import { ds } from '@/design/system'

function errorMessage(error: unknown) {
  if (typeof error === 'object' && error !== null && 'detail' in error) {
    const detail = (error as { detail?: unknown }).detail
    if (typeof detail === 'string' && detail.trim()) return detail
  }
  return 'KhÃ´ng thá»ƒ hoÃ n táº¥t thao tÃ¡c DLQ.'
}

export function DeadLetterQueuePage() {
  const [tasks, setTasks] = useState<DeadLetterQueueTask[]>([])
  const [status, setStatus] = useState('pending')
  const [loading, setLoading] = useState(true)
  const [busyId, setBusyId] = useState('')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try { setTasks(await deadLetterQueueService.list(status || undefined)) }
    catch (requestError) { setError(errorMessage(requestError)) }
    finally { setLoading(false) }
  }, [status])

  useEffect(() => {
    const timer = window.setTimeout(() => { void load() }, 0)
    return () => window.clearTimeout(timer)
  }, [load])

  const replay = async (task: DeadLetterQueueTask) => {
    setBusyId(task.id); setError(''); setNotice('')
    try {
      const result = await deadLetterQueueService.replay(task.id)
      setNotice(result.message)
      await load()
    } catch (requestError) { setError(errorMessage(requestError)) }
    finally { setBusyId('') }
  }

  return <div className={ds.page}>
    <section className={cn(ds.banner, 'p-5 sm:p-6')}><p className={ds.bannerChip}><AlertTriangle className="size-4" /> MF-04 Â· Dead-Letter Queue</p><h1 className={ds.bannerTitle}>Theo dÃµi task chấm AI lỗi</h1><p className={ds.bannerText}>Trưởng bộ môn hoặc Admin có thể xem và replay task lỗi để bảo toàn dữ liệu chấm thi.</p></section>
    {(error || notice) && <div role={error ? 'alert' : 'status'} className={cn('rounded-xl border px-4 py-3 text-sm font-semibold', error ? 'border-[#f0c5c9] bg-[#fff5f6] text-[#a43d4b]' : 'border-[#b7e4cf] bg-[#effaf6] text-[#176f58]')}>{error || notice}</div>}
    <section className={ds.section}><div className={cn(ds.sectionHeader, 'flex flex-wrap items-center justify-between gap-3')}><div><h2 className={ds.sectionTitleRow}><AlertTriangle className={ds.sectionIcon} /> <span className="text-sm font-bold text-[#0F2854]">Task lỗi cần xử lý</span></h2><p className={ds.sectionHint}>Lọc theo trạng thái pending, resolved hoặc abandoned.</p></div><div className="flex gap-2"><select className={ds.field} value={status} onChange={(event) => setStatus(event.target.value)}><option value="pending">pending</option><option value="resolved">resolved</option><option value="abandoned">abandoned</option><option value="">Tất cả</option></select><button type="button" className={ds.outlineButton} onClick={() => void load()} disabled={loading}><RefreshCw className={cn('size-4', loading && 'animate-spin')} /> Làm mới</button></div></div>
      {loading ? <div className="flex items-center justify-center p-10"><LoaderCircle className="size-7 animate-spin text-[#1C4D8D]" /></div> : <div className="divide-y divide-[#e8edf3]">{tasks.map((task) => <article key={task.id} className="grid gap-3 p-4 lg:grid-cols-[minmax(0,1fr)_auto] lg:items-center"><div><div className="flex flex-wrap items-center gap-2"><span className={ds.infoChip}>{task.status}</span><span className="text-xs font-bold text-[#1C4D8D]">{task.taskType}</span><span className="text-xs text-[#6c7d93]">Retry {task.retryCount}</span></div><p className="mt-2 text-sm font-semibold text-[#0F2854]">{task.errorMessage}</p><p className="mt-1 text-xs text-[#6c7d93]">{new Date(task.createdAt).toLocaleString('vi-VN')}</p></div><button type="button" disabled={Boolean(busyId) || task.status !== 'pending'} onClick={() => void replay(task)} className={ds.outlineButton}><RotateCcw className="size-4" />{busyId === task.id ? 'Đang replay...' : 'Replay task'}</button></article>)}{tasks.length === 0 && <p className="p-8 text-center text-sm text-[#6c7d93]">Không có task phù hợp.</p>}</div>}
    </section>
  </div>
}
