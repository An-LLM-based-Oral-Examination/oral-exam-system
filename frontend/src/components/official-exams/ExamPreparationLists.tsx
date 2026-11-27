import { useState } from 'react'
import { Eye } from 'lucide-react'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { preparationDate } from '@/lib/examPreparation'
import { DividedList } from '@/components/common/DividedList'
import { ProgressBar } from '@/components/common/ProgressBar'
import type { ExamAuthoringAssignment, ExamPaper } from '@/types/examPreparation.types'
import { ExamStatusBadge } from './ExamPreparationShared'
export function ExamPaperRows({
  papers,
  structureId,
  selectedId,
  authorName,
  onSelect,
  onEdit,
  selection,
}: {
  selection?: { ids: string[]; eligibleIds: string[]; onToggle: (id: string) => void }
  onEdit?: (id: string) => void
  papers: ExamPaper[]
  structureId: string | null
  selectedId?: string
  authorName: (p: ExamPaper) => string
  onSelect: (id: string) => void
}) {
  return (
    <DividedList as="div" className="mt-2 px-4 sm:px-5">
      {papers.map((p) => (
        <div
          key={p.id}
          role="button"
          tabIndex={0}
          aria-label={`Xem bộ đề ${p.setCode}`}
          onClick={() => onSelect(p.id)}
          onKeyDown={(e) => {
            if (e.target === e.currentTarget && (e.key === 'Enter' || e.key === ' ')) {
              e.preventDefault()
              onSelect(p.id)
            }
          }}
          className={cn(
            'grid min-w-0 cursor-pointer items-center gap-4 border-l-4 border-l-transparent py-4 pr-1 pl-1 focus-visible:outline-2 focus-visible:outline-[#1C4D8D] md:grid-cols-[minmax(0,1fr)_130px_75px_auto]',
            selectedId === p.id && 'border-l-[#1C4D8D] bg-[#E7F3FB]',
          )}
        >
          <div className="flex min-w-0 items-center gap-3">
            {selection && (
              <input
                type="checkbox"
                aria-label={`Chọn bộ đề ${p.setCode}`}
                checked={selection.ids.includes(p.id)}
                disabled={!selection.eligibleIds.includes(p.id)}
                onClick={(e) => e.stopPropagation()}
                onChange={() => selection.onToggle(p.id)}
                className="size-4 shrink-0 accent-[#1C4D8D]"
              />
            )}
            <div className="min-w-0">
              <div className="flex flex-wrap items-center gap-2">
                <strong className="text-base wrap-anywhere text-[#0F2854]">{p.setCode}</strong>
                <ExamStatusBadge status={p.status} />
                {p.structureId !== structureId && (
                  <span className={ds.dangerChip}>Cấu trúc cũ</span>
                )}
                {!p.assignmentId && <span className={ds.dangerChip}>Chưa gắn phân công</span>}
              </div>
              <p className="mt-1.5 text-sm font-medium wrap-anywhere text-[#5b6b84]">
                {authorName(p)}
              </p>
            </div>
          </div>
          <div>
            <p className="text-xs text-[#78879a]">
              {p.submittedAt ? 'Thời gian gửi' : 'Tạo bản nháp'}
            </p>
            <p className="mt-1 text-sm font-semibold text-[#0F2854]">
              {preparationDate(p.submittedAt ?? p.generatedAt)}
            </p>
          </div>
          <div>
            <p className="text-xs text-[#78879a]">Số câu</p>
            <p className="mt-1 text-sm font-bold text-[#0F2854]">{p.questions.length} câu</p>
          </div>
          <div className="flex flex-wrap justify-end gap-2">
            {onEdit && ['DRAFT', 'NEEDS_REVISION'].includes(p.status) && (
              <button
                type="button"
                className={ds.outlineButton}
                onClick={(e) => {
                  e.stopPropagation()
                  onEdit(p.id)
                }}
              >
                Chỉnh sửa
              </button>
            )}
            <button
              type="button"
              className={cn(ds.outlineButton, 'justify-self-end')}
              onClick={(e) => {
                e.stopPropagation()
                onSelect(p.id)
              }}
            >
              <Eye className="size-4" />
              Xem đề
            </button>
          </div>
        </div>
      ))}
    </DividedList>
  )
}
export function ExamAssignmentRows({
  assignments,
  structureId,
}: {
  assignments: ExamAuthoringAssignment[]
  structureId: string | null
}) {
  const [now] = useState(Date.now)
  return (
    <DividedList as="div" className="px-4 sm:px-5">
      {assignments.map((a) => (
        <div
          key={a.id}
          className="grid min-w-0 items-center gap-4 py-4 sm:grid-cols-2 lg:grid-cols-[minmax(0,1fr)_145px_100px_145px_minmax(130px,0.7fr)]"
        >
          <div className="min-w-0">
            <p className="text-base font-bold wrap-anywhere text-[#0F2854]">{a.lecturerName}</p>
            {a.notes && (
              <p className="mt-1 text-sm wrap-anywhere whitespace-pre-wrap text-[#5b6b84]">
                {a.notes}
              </p>
            )}
            {a.structureId !== structureId && (
              <span className={cn(ds.dangerChip, 'mt-2')}>Cấu trúc cũ</span>
            )}
          </div>
          <div>
            <p className="text-xs text-[#78879a]">Thời gian phân công</p>
            <p className="mt-1 text-sm font-semibold text-[#0F2854]">
              {preparationDate(a.assignedAt)}
            </p>
          </div>
          <div>
            <p className="text-xs text-[#78879a]">Được giao</p>
            <p className="mt-1 text-sm font-bold text-[#0F2854]">{a.setCount} bộ đề</p>
          </div>
          <div>
            <p className="text-xs text-[#78879a]">Hạn nộp</p>
            <p
              className={cn(
                'mt-1 text-sm font-semibold',
                a.approved + a.handedOver < a.setCount && Date.parse(a.deadline) < now
                  ? 'text-[#c2415b]'
                  : 'text-[#0F2854]',
              )}
            >
              {preparationDate(a.deadline)}
            </p>
          </div>
          <div>
            <ProgressBar
              value={a.approved + a.handedOver}
              max={a.setCount}
              aria-label={`Tiến độ ${a.lecturerName}`}
            />
            <p className="mt-2 text-xs font-semibold text-[#5b6b84]">
              {a.approved + a.handedOver}/{a.setCount} đã duyệt · {a.pending} chờ duyệt
            </p>
            <p className="mt-1 text-xs text-[#78879a]">
              {a.draft} nháp · {a.needsRevision} cần sửa · {a.rejected} từ chối
            </p>
          </div>
        </div>
      ))}
    </DividedList>
  )
}
