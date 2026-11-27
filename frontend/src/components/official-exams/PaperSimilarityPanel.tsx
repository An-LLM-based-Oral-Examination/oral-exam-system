import { CircleCheck, Files } from 'lucide-react'
import { cn } from '@/lib/utils'
import { similarityTone } from '@/lib/examPreparation'
import type { PaperSimilarityReport } from '@/types/examPreparation.types'

export function PaperSimilarityPanel({
  report,
  questionId,
}: {
  report: PaperSimilarityReport
  questionId?: string
}) {
  const high = report.similarityPercent >= 70
  const match = report.questions.find((q) => q.questionId === questionId)?.matches[0]
  return (
    <section
      aria-label="Tương đồng bộ đề"
      className={cn('min-w-0 rounded-xl border p-3', similarityTone(report.similarityPercent))}
    >
      <div className="flex items-center justify-between gap-3">
        <div className="flex min-w-0 items-center gap-2 text-sm font-bold">
          {high ? (
            <Files className="size-4 shrink-0" />
          ) : (
            <CircleCheck className="size-4 shrink-0" />
          )}
          Tương đồng bộ đề
        </div>
        <span className="shrink-0 rounded-lg bg-white/80 px-3 py-1 text-lg font-bold tabular-nums">
          {report.similarityPercent}%
        </span>
      </div>
      {!report.comparedPaper ? (
        <p className="mt-1 text-xs">Chưa có bộ đề đã duyệt phù hợp để đối chiếu.</p>
      ) : high ? (
        <>
          <p className="mt-2 text-sm font-semibold wrap-anywhere">
            {report.similarityPercent >= 85 ? 'Tương đồng rất cao' : 'Tương đồng cao'} ·{' '}
            {report.comparedPaper.setCode}
          </p>
          <p className="mt-1 text-xs wrap-anywhere">
            {report.comparedPaper.sessionTitle} · {report.comparedPaper.semesterCode}
          </p>
          <details
            key={`${report.comparedPaper.examSetId}-${questionId}`}
            className="mt-3 border-t border-current/15 pt-2"
          >
            <summary className="cursor-pointer text-sm font-bold">Xem câu đối chiếu</summary>
            {match ? (
              <div className="mt-2 min-w-0 rounded-xl border border-current/15 bg-white/80 p-3">
                <p className="text-xs font-bold">
                  Câu {match.orderIndex} · {match.percent}%
                  {match.sameSource ? ' · Trùng nguồn' : ''}
                </p>
                <p className="mt-1 text-sm font-bold wrap-anywhere text-[#0F2854]">{match.title}</p>
                <p className="mt-1 text-sm leading-relaxed wrap-anywhere whitespace-pre-wrap text-[#5b6b84]">
                  {match.content}
                </p>
              </div>
            ) : (
              <p className="mt-2 text-sm">
                Câu này không có câu ghép tương ứng trong đề đối chiếu.
              </p>
            )}
          </details>
          <p className="mt-2 text-xs">
            Rà soát trước khi duyệt. Cảnh báo không tự động chặn duyệt.
          </p>
        </>
      ) : null}
    </section>
  )
}
