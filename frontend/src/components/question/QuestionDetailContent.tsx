import { QuestionImage } from '@/components/question/QuestionImage'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'

interface QuestionDetailContentProps {
  question: {
    content: string
    imageUrl?: string | null
    imageAlt?: string | null
    imageDescription?: string | null
    sampleAnswer?: string | null
    rubric: {
      name?: string
      totalMaxScore: number
      criteria: Array<{
        id: string
        criterionName: string
        maxScore: number
        description?: string | null
      }>
    }
  }
}

export function QuestionDetailContent({ question }: QuestionDetailContentProps) {
  return (
    <>
      <section
        className={cn('rounded-2xl border border-[#82aedb]/55 bg-white p-4', ds.panelShadow)}
      >
        <p className="text-xs font-bold tracking-[0.07em] text-[#5b6b84] uppercase">
          Nội dung câu hỏi
        </p>
        <QuestionImage
          imageUrl={question.imageUrl}
          imageAlt={question.imageAlt ?? question.imageDescription}
          className="mt-3"
        />
        <p
          className={cn(
            'text-[15px] leading-relaxed font-medium whitespace-pre-wrap wrap-anywhere text-[#0F2854]',
            question.imageUrl ? 'mt-4' : 'mt-3',
          )}
        >
          {question.content}
        </p>
      </section>

      <section
        className={cn('rounded-2xl border border-[#82aedb]/55 bg-white p-4', ds.panelShadow)}
      >
        <div className="flex items-center justify-between gap-3">
          <div>
            <p className="text-xs font-bold tracking-[0.07em] text-[#5b6b84] uppercase">
              Barem Rubric
            </p>
            <h3 className="mt-1 text-sm font-bold wrap-anywhere text-[#0F2854]">{question.rubric.name}</h3>
          </div>
          <span
            className={cn(
              'shrink-0 rounded-xl px-3 py-2 text-sm font-black whitespace-nowrap',
              question.rubric.totalMaxScore === 10
                ? 'bg-[#eaf9f4] text-[#14765c]'
                : 'bg-[#fff0f1] text-[#ae3945]',
            )}
          >
            {question.rubric.totalMaxScore.toFixed(1)} / 10
          </span>
        </div>
        <div className="mt-4 space-y-3">
          {question.rubric.criteria.map((criterion, index) => {
            const ratio = Math.min(100, Math.max(0, (criterion.maxScore / 10) * 100))
            return (
              <div
                key={criterion.id}
                className="rounded-xl border border-[#82aedb]/55 bg-[#F4FBFD]/40 p-3"
              >
                <div className="flex items-start justify-between gap-3">
                  <p className="min-w-0 text-sm font-bold wrap-anywhere text-[#30445f]">
                    {index + 1}. {criterion.criterionName}
                  </p>
                  <span className="shrink-0 font-mono text-xs font-black text-[#1C4D8D]">
                    {criterion.maxScore.toFixed(1)}đ
                  </span>
                </div>
                <div className="mt-2 h-1.5 overflow-hidden rounded-full bg-[#dce6f2]">
                  <div
                    className="h-full rounded-full bg-[#4988C4]"
                    style={{ width: `${ratio}%` }}
                  />
                </div>
                {criterion.description && (
                  <p className="mt-2 text-xs leading-relaxed whitespace-pre-wrap wrap-anywhere text-[#78879a]">
                    {criterion.description}
                  </p>
                )}
              </div>
            )
          })}
        </div>
      </section>

      <section
        className={cn('rounded-2xl border border-[#82aedb]/55 bg-white p-4', ds.panelShadow)}
      >
        <p className="text-xs font-bold tracking-[0.07em] text-[#5b6b84] uppercase">
          Câu trả lời mẫu
        </p>
        <p className="mt-2 text-sm leading-relaxed whitespace-pre-wrap wrap-anywhere text-[#586a82]">
          {question.sampleAnswer || 'Chưa có câu trả lời mẫu.'}
        </p>
      </section>
    </>
  )
}
