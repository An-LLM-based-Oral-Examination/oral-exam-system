import { useCallback, useEffect, useState } from 'react'
import { AlertTriangle, LoaderCircle, RefreshCw, RotateCcw } from 'lucide-react'
import { deadLetterQueueService } from '@/services/deadLetterQueue.service'
import type { DeadLetterQueueTask } from '@/types/deadLetterQueue.types'
import { cn } from '@/lib/utils'
import { EmptyState } from '@/components/common/EmptyState'
import { toast } from '@/components/common/toastStore'
import { BannerFact, LecturerPageBanner, SectionHeader } from '@/design/ui'
import { ds } from '@/design/system'
import { userMessage } from '@/lib/userMessage'

function errorMessage(error: unknown) {
  return userMessage(error, 'Không chạy lại được bài chấm lỗi. Vui lòng thử lại.')
}

export function DeadLetterQueuePage() {
  const [tasks, setTasks] = useState<DeadLetterQueueTask[]>([])
  const [status, setStatus] = useState('pending')
  const [loading, setLoading] = useState(true)
  const [busyId, setBusyId] = useState('')

  const load = useCallback(async () => {
    setLoading(true)
    try { setTasks(await deadLetterQueueService.list(status || undefined)) }
    catch (requestError) { toast.error(errorMessage(requestError)) }
    finally { setLoading(false) }
  }, [status])

  useEffect(() => {
    const timer = window.setTimeout(() => { void load() }, 0)
    return () => window.clearTimeout(timer)
  }, [load])

  const replay = async (task: DeadLetterQueueTask) => {
    setBusyId(task.id)
    try {
      const result = await deadLetterQueueService.replay(task.id)
      toast.success(userMessage(result.message, 'Đã chạy lại bài chấm lỗi.'))
      await load()
    } catch (requestError) { toast.error(errorMessage(requestError)) }
    finally { setBusyId('') }
  }

  return <div className={ds.page}>
    <LecturerPageBanner
      icon={AlertTriangle}
      eyebrow="Dead-Letter Queue (Zero Data Loss)"
      title="Theo dõi & Replay Hàng đợi lỗi AI"
      description="Admin giám sát & replay DLQ cho Luyện tập (MF-01) và Thi thử (MF-02); Cán bộ Khảo thí giám sát & replay DLQ cho Thi thật (MF-04)."
      extras={
        <>
          <BannerFact icon={AlertTriangle}>Cách ly task lỗi</BannerFact>
          <BannerFact icon={RotateCcw} tone="green">
            Replay chủ động
          </BannerFact>
          <BannerFact icon={RefreshCw} tone="violet">
            Zero Data Loss
          </BannerFact>
        </>
      }
    />
    <section className={ds.section}><SectionHeader icon={AlertTriangle} title="Task lỗi cần xử lý" hint="Task chấm điểm bị lỗi cần xử lý" action={<div className="flex gap-2"><select className={ds.field} value={status} onChange={(event) => setStatus(event.target.value)}><option value="pending">pending</option><option value="resolved">resolved</option><option value="abandoned">abandoned</option><option value="">Tất cả</option></select><button type="button" className={ds.outlineButton} onClick={() => void load()} disabled={loading}><RefreshCw className={cn('size-4', loading && 'animate-spin')} /> Làm mới</button></div>} />
      {loading ? <div className="flex items-center justify-center p-10"><LoaderCircle className="size-7 animate-spin text-[#1C4D8D]" /></div> : <div className="divide-y divide-[#e8edf3]">{tasks.map((task) => <article key={task.id} className="grid gap-3 p-4 lg:grid-cols-[minmax(0,1fr)_auto] lg:items-center"><div><div className="flex flex-wrap items-center gap-2"><span className={ds.infoChip}>{task.status}</span><span className="text-xs font-bold text-[#1C4D8D]">{task.taskType}</span><span className="text-xs text-[#6c7d93]">Retry {task.retryCount}</span></div><p className="mt-2 text-sm font-semibold text-[#0F2854]">{task.errorMessage}</p><p className="mt-1 text-xs text-[#6c7d93]">{new Date(task.createdAt).toLocaleString('vi-VN')}</p></div><button type="button" disabled={Boolean(busyId) || task.status !== 'pending'} onClick={() => void replay(task)} className={ds.outlineButton}><RotateCcw className="size-4" />{busyId === task.id ? 'Đang replay...' : 'Replay task'}</button></article>)}{tasks.length === 0 && <div className="p-4"><EmptyState icon={AlertTriangle} title="Không có task phù hợp" description="Thử đổi bộ lọc trạng thái hoặc làm mới danh sách." /></div>}</div>}
    </section>
  </div>
}
