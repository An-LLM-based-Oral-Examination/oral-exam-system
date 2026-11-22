import { Link } from 'react-router-dom'
import { PlusCircle } from 'lucide-react'
import type { ExamAttemptType, OfficialExamSession } from '@/types/officialExam.types'
import { cn } from '@/lib/utils'
import { ds } from '@/design/system'

interface CourseAttemptTabsProps {
  activeSession: OfficialExamSession
  feSession: OfficialExamSession | null
  reSession: OfficialExamSession | null
  onCreateAttempt?: (attemptType: ExamAttemptType) => void
  creatingAttempt?: boolean
}

export function CourseAttemptTabs({
  activeSession,
  feSession,
  reSession,
  onCreateAttempt,
  creatingAttempt = false,
}: CourseAttemptTabsProps) {
  const activeAttempt: ExamAttemptType = activeSession.examAttemptType === 'RE' ? 'RE' : 'FE'

  return (
    <div
      role="tablist"
      aria-label="Chuyển đổi đợt thi FE và RE"
      className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-[#d9e8f2] bg-white p-3"
    >
      <div className="flex flex-wrap items-center gap-2">
        {feSession ? (
          <Link
            role="tab"
            aria-selected={activeAttempt === 'FE'}
            to={`/examination-officer/exams/${feSession.id}`}
            className={cn(
              'inline-flex min-h-10 items-center gap-2 rounded-xl border px-4 text-xs font-bold transition',
              activeAttempt === 'FE'
                ? 'border-[#1C4D8D] bg-[#1C4D8D] text-white'
                : 'border-[#d9e8f2] bg-[#F8FBFD] text-[#1C4D8D] hover:bg-[#e8f4fc]',
            )}
          >
            <span>FE · Thi lần 1</span>
            {typeof feSession.candidateCount === 'number' && (
              <span className="rounded-full bg-white/20 px-2 py-0.5 font-mono text-[11px]">
                {feSession.candidateCount} SV
              </span>
            )}
          </Link>
        ) : onCreateAttempt ? (
          <button
            type="button"
            disabled={creatingAttempt}
            onClick={() => onCreateAttempt('FE')}
            className={ds.outlineButton}
          >
            <PlusCircle className="size-4" /> Mở đợt Thi lần 1 (FE)
          </button>
        ) : null}

        {reSession ? (
          <Link
            role="tab"
            aria-selected={activeAttempt === 'RE'}
            to={`/examination-officer/exams/${reSession.id}`}
            className={cn(
              'inline-flex min-h-10 items-center gap-2 rounded-xl border px-4 text-xs font-bold transition',
              activeAttempt === 'RE'
                ? 'border-[#1C4D8D] bg-[#1C4D8D] text-white'
                : 'border-[#d9e8f2] bg-[#F8FBFD] text-[#1C4D8D] hover:bg-[#e8f4fc]',
            )}
          >
            <span>RE · Thi lại</span>
            {typeof reSession.candidateCount === 'number' && (
              <span className="rounded-full bg-white/20 px-2 py-0.5 font-mono text-[11px]">
                {reSession.candidateCount} SV
              </span>
            )}
          </Link>
        ) : onCreateAttempt ? (
          <button
            type="button"
            disabled={creatingAttempt}
            onClick={() => onCreateAttempt('RE')}
            className={ds.outlineButton}
          >
            <PlusCircle className="size-4" /> Mở đợt Thi lại (RE)
          </button>
        ) : null}
      </div>

      <p className="text-xs font-medium text-[#5b6b84]">
        Hai đợt <strong>FE</strong> và <strong>RE</strong> vận hành DSSV, ca thi, công bố điểm và
        phúc khảo hoàn toàn độc lập.
      </p>
    </div>
  )
}
