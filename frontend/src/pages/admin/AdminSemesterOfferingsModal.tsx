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
import { adminUi } from '@/pages/admin/components/adminUi'
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
      eyebrow={semester.name}
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
      <form id="semester-offerings-form" onSubmit={handleSave} className="space-y-3">
        <AdminStateBanner
          problem={problem}
          onRetry={problem ? () => void loadCatalog() : undefined}
          onDismiss={() => setProblem(null)}
        />

        <div className="flex flex-col gap-3 rounded-xl border border-slate-300 bg-[#f4f8fc] p-3.5 sm:flex-row sm:items-center sm:justify-between">
          <span className="text-sm font-medium text-[#0F2854] tabular-nums">
            Đang chọn mở: <strong className="font-bold text-[#1C4D8D]">{selectedIds.size}</strong> /{' '}
            {items.length} môn học
          </span>
          <div className="flex flex-wrap items-center gap-2">
            <button
              type="button"
              disabled={loading || saving || activeCourses.length === 0}
              onClick={() => setSelectedIds(new Set(activeCourses.map((c) => c.courseId)))}
              className={cn(adminUi.btnRowPrimary, 'min-h-11 md:min-h-9')}
            >
              Chọn toàn bộ môn active
            </button>
            <button
              type="button"
              disabled={loading || saving || selectedIds.size === 0}
              onClick={() => setSelectedIds(new Set())}
              className={cn(adminUi.btnRowNeutral, 'min-h-11 md:min-h-9')}
            >
              Bỏ chọn hết
            </button>
          </div>
        </div>

        <div className="overflow-hidden rounded-xl border border-slate-300 bg-white">
          {loading ? (
            <LoadingState label="Đang tải danh mục môn học…" className="min-h-48" />
          ) : problem && items.length === 0 ? null : items.length === 0 ? (
            <div className="p-4">
              <EmptyState
                compact
                icon={BookOpen}
                title="Chưa có môn học nào để mở trong học kỳ này"
                description="Hãy tạo môn học ở mục Môn học & CLO trước khi đồng bộ danh mục mở."
              />
            </div>
          ) : (
            <div className="max-h-[42dvh] divide-y divide-slate-200 overflow-y-auto">
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
                      'flex min-h-12 w-full items-center justify-between gap-3 px-4 py-3 text-left transition-colors duration-150 focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-[#1C4D8D] motion-reduce:transition-none',
                      checked ? 'bg-[#edf5fd]' : 'bg-white hover:bg-[#f6fafe]',
                      disabled && 'cursor-not-allowed opacity-55',
                    )}
                  >
                    <div className="flex min-w-0 items-center gap-3">
                      {checked ? (
                        <CheckSquare
                          className="size-4 shrink-0 text-[#1C4D8D]"
                          strokeWidth={2}
                          aria-hidden="true"
                        />
                      ) : (
                        <Square
                          className="size-4 shrink-0 text-slate-400"
                          strokeWidth={1.75}
                          aria-hidden="true"
                        />
                      )}
                      <div className="min-w-0">
                        <p className="text-sm font-semibold text-[#0F2854]">
                          <span className="font-mono font-bold text-[#1C4D8D]">{item.code}</span>{' '}
                          — {item.name}
                        </p>
                        <p className="mt-0.5 truncate text-xs font-medium text-slate-600 tabular-nums">
                          {item.credits} tín chỉ
                          {item.academicProgramCode
                            ? ` · Chương trình ${item.academicProgramCode}`
                            : ''}
                        </p>
                      </div>
                    </div>
                    <span
                      className={cn(
                        'shrink-0',
                        item.isCourseActive ? adminUi.badgeSuccess : adminUi.badgeNeutral,
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
