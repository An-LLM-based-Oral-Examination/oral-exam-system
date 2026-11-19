import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import { BookOpen, CheckSquare, Square } from 'lucide-react'
import { EmptyState } from '@/components/common/EmptyState'
import { LoadingState } from '@/components/common/LoadingState'
import { SubmitDialogFooter } from '@/components/common/SubmitDialogFooter'
import { toast } from '@/components/common/toastStore'
import { FormDialog } from '@/design/overlays'
import { extractProblemDetails, type NormalizedProblem } from '@/lib/problemDetails'
import { cn } from '@/lib/utils'
import { AdminStateBanner } from '@/pages/admin/components/AdminStateBanner'
import {
  adminService,
  type SemesterCourseOfferingItem,
  type SemesterItem,
} from '@/services/admin.service'

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
  const [problem, setProblem] = useState<NormalizedProblem | null>(null)
  const requestIdRef = useRef(0)

  const loadCatalog = useCallback(async () => {
    const requestId = ++requestIdRef.current
    setLoading(true)
    setProblem(null)
    try {
      const catalog = await adminService.getSemesterCourseOfferings(semester.id, true)
      if (requestId !== requestIdRef.current) return
      setItems(catalog)
      setSelectedIds(new Set(catalog.filter((c) => c.isSelected).map((c) => c.courseId)))
    } catch (err: unknown) {
      if (requestId !== requestIdRef.current) return
      setProblem(extractProblemDetails(err, 'Không tải được danh mục môn mở của học kỳ.'))
    } finally {
      if (requestId === requestIdRef.current) {
        setLoading(false)
      }
    }
  }, [semester.id])

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadCatalog()
    }, 0)
    return () => {
      window.clearTimeout(timer)
      requestIdRef.current += 1
    }
  }, [loadCatalog])

  const toggleCourse = (courseId: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev)
      if (next.has(courseId)) next.delete(courseId)
      else next.add(courseId)
      return next
    })
  }

  const handleSave = async (event: FormEvent) => {
    event.preventDefault()
    if (saving || loading) return
    setSaving(true)
    setProblem(null)
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
    } catch (err: unknown) {
      const parsed = extractProblemDetails(err, 'Không đồng bộ được danh mục môn mở.')
      setProblem(parsed)
      toast.error(parsed.detail)
    } finally {
      setSaving(false)
    }
  }

  const activeCourses = items.filter((c) => c.isCourseActive)

  return (
    <FormDialog
      open
      variant="calm"
      size="lg"
      busy={saving}
      onClose={() => {
        if (!saving) onClose()
      }}
      icon={BookOpen}
      eyebrow={`${semester.name} · Đồng bộ course_offerings`}
      title={`Danh mục môn mở trong học kỳ (${semester.code})`}
      footer={
        <SubmitDialogFooter
          variant="calm"
          form="semester-offerings-form"
          onCancel={() => {
            if (!saving) onClose()
          }}
          label="Lưu danh mục môn mở"
          busyLabel="Đang lưu…"
          busy={saving}
          disabled={loading}
        />
      }
    >
      <form id="semester-offerings-form" onSubmit={handleSave} className="space-y-4">
        <AdminStateBanner
          problem={problem}
          onRetry={problem ? () => void loadCatalog() : undefined}
          onDismiss={() => setProblem(null)}
        />

        <div className="flex flex-col gap-3 rounded-xl border border-[#0F2854]/10 bg-[#F8FBFD] p-3.5 sm:flex-row sm:items-center sm:justify-between">
          <span className="text-xs font-medium text-[#0F2854] tabular-nums">
            Đang chọn mở: <strong className="font-semibold">{selectedIds.size}</strong> /{' '}
            {items.length} môn học
          </span>
          <div className="flex flex-wrap items-center gap-2">
            <button
              type="button"
              disabled={loading || saving || activeCourses.length === 0}
              onClick={() => setSelectedIds(new Set(activeCourses.map((c) => c.courseId)))}
              className="inline-flex min-h-11 items-center justify-center rounded-xl border border-[#0F2854]/15 bg-white px-3 text-xs font-medium text-[#1C4D8D] transition-colors duration-150 hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:opacity-50 motion-reduce:transition-none"
            >
              Chọn toàn bộ môn active
            </button>
            <button
              type="button"
              disabled={loading || saving || selectedIds.size === 0}
              onClick={() => setSelectedIds(new Set())}
              className="inline-flex min-h-11 items-center justify-center rounded-xl border border-[#0F2854]/15 bg-white px-3 text-xs font-medium text-[#5b6b84] transition-colors duration-150 hover:bg-[#F4FBFD] hover:text-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:opacity-50 motion-reduce:transition-none"
            >
              Bỏ chọn hết
            </button>
          </div>
        </div>

        <div className="overflow-hidden rounded-xl border border-[#0F2854]/10 bg-white">
          {loading ? (
            <LoadingState label="Đang tải danh mục môn học…" className="min-h-48" />
          ) : problem && items.length === 0 ? null : items.length === 0 ? (
            <div className="p-4">
              <EmptyState
                compact
                icon={BookOpen}
                title="Chưa có môn học nào để mở trong học kỳ này"
                description="Hãy tạo môn học ở mục Môn học, CTĐT & CLO trước khi đồng bộ danh mục mở."
              />
            </div>
          ) : (
            <div className="max-h-[42dvh] divide-y divide-[#0F2854]/8 overflow-y-auto">
              {items.map((item) => {
                const checked = selectedIds.has(item.courseId)
                const disabled = !item.isCourseActive && !checked
                return (
                  <button
                    key={item.courseId}
                    type="button"
                    disabled={disabled || saving}
                    onClick={() => toggleCourse(item.courseId)}
                    aria-pressed={checked}
                    className={cn(
                      'flex min-h-11 w-full items-center justify-between gap-3 px-4 py-3 text-left transition-colors duration-150 focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-[#1C4D8D] motion-reduce:transition-none',
                      checked ? 'bg-[#F4FBFD]' : 'bg-white hover:bg-[#F8FBFD]',
                      disabled && 'cursor-not-allowed opacity-55',
                    )}
                  >
                    <div className="flex min-w-0 items-center gap-3">
                      {checked ? (
                        <CheckSquare
                          className="size-4 shrink-0 text-[#1C4D8D]"
                          strokeWidth={1.5}
                          aria-hidden="true"
                        />
                      ) : (
                        <Square
                          className="size-4 shrink-0 text-[#5b6b84]"
                          strokeWidth={1.5}
                          aria-hidden="true"
                        />
                      )}
                      <div className="min-w-0">
                        <p className="text-sm font-medium text-[#0F2854]">
                          <span className="font-mono font-semibold text-[#1C4D8D]">{item.code}</span>{' '}
                          — {item.name}
                        </p>
                        <p className="mt-0.5 truncate text-xs text-[#5b6b84] tabular-nums">
                          {item.credits} tín chỉ
                          {item.academicProgramCode
                            ? ` · Chương trình ${item.academicProgramCode}`
                            : ''}
                        </p>
                      </div>
                    </div>
                    <span
                      className={cn(
                        'shrink-0 rounded-md border px-2 py-0.5 text-xs font-medium',
                        item.isCourseActive
                          ? 'border-[#b7e4cf] bg-[#e8f8f1] text-[#16836a]'
                          : 'border-[#0F2854]/10 bg-[#F8FBFD] text-[#5b6b84]',
                      )}
                    >
                      {item.isCourseActive ? 'Môn active' : 'Môn tạm khóa'}
                    </span>
                  </button>
                )
              })}
            </div>
          )}
        </div>
      </form>
    </FormDialog>
  )
}
