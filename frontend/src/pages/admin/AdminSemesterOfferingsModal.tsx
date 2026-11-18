import { useCallback, useEffect, useState } from 'react'
import { BookOpen, CheckSquare, LoaderCircle, RefreshCw, Square, X } from 'lucide-react'
import {
  adminService,
  type SemesterCourseOfferingItem,
  type SemesterItem,
} from '@/services/admin.service'
import { toast } from '@/components/common/toastStore'
import { userMessage } from '@/lib/userMessage'
import { cn } from '@/lib/utils'

interface AdminSemesterOfferingsModalProps {
  semester: SemesterItem
  onClose: () => void
  onSaved: () => void
}

export function AdminSemesterOfferingsModal({
  semester,
  onClose,
  onSaved,
}: AdminSemesterOfferingsModalProps) {
  const [items, setItems] = useState<SemesterCourseOfferingItem[]>([])
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set())
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')

  const loadCatalog = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const catalog = await adminService.getSemesterCourseOfferings(semester.id, true)
      setItems(catalog)
      setSelectedIds(new Set(catalog.filter((c) => c.isSelected).map((c) => c.courseId)))
    } catch (err) {
      setError(userMessage(err, 'Không tải được danh mục môn mở của học kỳ.'))
    } finally {
      setLoading(false)
    }
  }, [semester.id])

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadCatalog()
    }, 0)
    return () => window.clearTimeout(timer)
  }, [loadCatalog])

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && !saving) {
        onClose()
      }
    }
    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [onClose, saving])

  const toggleCourse = (courseId: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev)
      if (next.has(courseId)) next.delete(courseId)
      else next.add(courseId)
      return next
    })
  }

  const handleSave = async (event: React.FormEvent) => {
    event.preventDefault()
    if (saving || loading) return
    setSaving(true)
    setError('')
    try {
      const result = await adminService.syncSemesterCourseOfferings(
        semester.id,
        Array.from(selectedIds),
      )
      toast.success(
        `Đã đồng bộ môn mở học kỳ [${semester.code}]: bật ${result.enabledCount + result.createdCount}, tắt ${result.disabledCount}.`,
      )
      onSaved()
      onClose()
    } catch (err) {
      const msg = userMessage(err, 'Không đồng bộ được danh mục môn mở.')
      setError(msg)
      toast.error(msg)
    } finally {
      setSaving(false)
    }
  }

  const activeCourses = items.filter((c) => c.isCourseActive)

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={`Môn mở trong kỳ ${semester.code}`}
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm"
    >
      <div className="flex max-h-[88vh] w-full max-w-2xl flex-col rounded-3xl border border-[#82aedb]/50 bg-white p-6 shadow-2xl">
        <div className="flex items-center justify-between border-b border-[#82aedb]/30 pb-3">
          <div className="flex items-center gap-2">
            <BookOpen className="size-5 text-[#1C4D8D]" />
            <div>
              <h3 className="text-base font-bold text-[#0F2854]">
                Danh Mục Môn Mở Trong Học Kỳ ({semester.code})
              </h3>
              <p className="text-[11px] text-[#63738a]">
                {semester.name} · Đồng bộ bảng <code className="font-mono">course_offerings</code>
              </p>
            </div>
          </div>
          <button
            type="button"
            aria-label="Đóng"
            onClick={onClose}
            className="text-[#94a3b8] transition hover:text-[#0F2854]"
          >
            <X className="size-5" />
          </button>
        </div>

        {error && (
          <div
            role="alert"
            className="mt-3 flex items-center justify-between gap-3 rounded-xl border border-[#f0c5c9] bg-[#fff5f6] px-3.5 py-2.5 text-xs font-semibold text-[#a43d4b]"
          >
            <span>{error}</span>
            <button
              type="button"
              onClick={() => void loadCatalog()}
              className="inline-flex shrink-0 items-center gap-1 rounded-lg border border-[#f0c5c9] bg-white px-2.5 py-1 text-[11px] font-bold text-[#a43d4b]"
            >
              <RefreshCw className="size-3" /> Thử lại
            </button>
          </div>
        )}

        <form onSubmit={handleSave} className="mt-4 flex min-h-0 flex-1 flex-col gap-3 text-xs">
          <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl bg-[#F4FBFD] px-3 py-2">
            <span className="font-semibold text-[#0F2854]">
              Đang chọn mở: <strong>{selectedIds.size}</strong> / {items.length} môn học
            </span>
            <div className="flex items-center gap-2">
              <button
                type="button"
                disabled={loading || saving || activeCourses.length === 0}
                onClick={() => setSelectedIds(new Set(activeCourses.map((c) => c.courseId)))}
                className="rounded-lg border border-[#82aedb]/50 bg-white px-2.5 py-1 text-[11px] font-bold text-[#1C4D8D] hover:bg-[#edf5fc] disabled:opacity-50"
              >
                Chọn toàn bộ môn active
              </button>
              <button
                type="button"
                disabled={loading || saving || selectedIds.size === 0}
                onClick={() => setSelectedIds(new Set())}
                className="rounded-lg border border-[#82aedb]/50 bg-white px-2.5 py-1 text-[11px] font-bold text-[#64748b] hover:bg-[#f1f5f9] disabled:opacity-50"
              >
                Bỏ chọn hết
              </button>
            </div>
          </div>

          <div className="min-h-0 flex-1 overflow-y-auto rounded-2xl border border-[#82aedb]/40 divide-y divide-[#82aedb]/25">
            {loading ? (
              <div className="py-12 text-center text-[#63738a]">
                <LoaderCircle className="mx-auto size-6 animate-spin text-[#1C4D8D]" />
                <span className="mt-2 block">Đang tải danh mục môn học...</span>
              </div>
            ) : items.length === 0 ? (
              <div className="py-10 text-center text-[#63738a]">
                Chưa có môn học nào để mở trong học kỳ này. Hãy tạo môn học ở tab Môn học trước.
              </div>
            ) : (
              items.map((item) => {
                const checked = selectedIds.has(item.courseId)
                return (
                  <button
                    key={item.courseId}
                    type="button"
                    disabled={!item.isCourseActive && !checked}
                    onClick={() => toggleCourse(item.courseId)}
                    className={cn(
                      'flex w-full items-center justify-between gap-3 px-4 py-3 text-left transition',
                      checked ? 'bg-[#F4FBFD]' : 'bg-white hover:bg-slate-50',
                      !item.isCourseActive && !checked && 'cursor-not-allowed opacity-55',
                    )}
                  >
                    <div className="flex min-w-0 items-center gap-3">
                      {checked ? (
                        <CheckSquare className="size-4 shrink-0 text-[#1C4D8D]" />
                      ) : (
                        <Square className="size-4 shrink-0 text-[#94a3b8]" />
                      )}
                      <div className="min-w-0">
                        <p className="font-bold text-[#0F2854]">
                          <span className="font-mono text-[#1C4D8D]">{item.code}</span> — {item.name}
                        </p>
                        <p className="truncate text-[11px] text-[#63738a]">
                          {item.credits} tín chỉ
                          {item.academicProgramCode ? ` · Chương trình ${item.academicProgramCode}` : ''}
                        </p>
                      </div>
                    </div>
                    <span
                      className={cn(
                        'shrink-0 rounded-full px-2 py-0.5 text-[10px] font-bold',
                        item.isCourseActive
                          ? 'bg-[#ecfdf5] text-[#059669]'
                          : 'bg-[#f1f5f9] text-[#64748b]',
                      )}
                    >
                      {item.isCourseActive ? 'Môn active' : 'Môn tạm khóa'}
                    </span>
                  </button>
                )
              })
            )}
          </div>

          <div className="flex items-center justify-end gap-2 pt-2">
            <button
              type="button"
              onClick={onClose}
              className="rounded-xl border border-[#82aedb]/50 px-4 py-2 font-bold text-[#64748b] transition hover:bg-[#f1f5f9]"
            >
              Đóng
            </button>
            <button
              type="submit"
              disabled={loading || saving}
              className="inline-flex items-center gap-1.5 rounded-xl bg-gradient-to-r from-[#1C4D8D] to-[#4988C4] px-4 py-2 font-bold text-white shadow-md transition hover:brightness-110 disabled:opacity-50"
            >
              {saving && <LoaderCircle className="size-3.5 animate-spin" />}
              Lưu danh mục môn mở
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
