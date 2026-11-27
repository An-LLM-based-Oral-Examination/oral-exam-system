import { QuestionDifficultyBadge } from './QuestionDifficultyBadge'
import { useEffect, useRef, useState } from 'react'
import { BookOpen, Check } from 'lucide-react'
import { FormDialog, DialogFooter } from '@/design/overlays'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { FilterDropMenu } from '@/components/common/FilterDropMenu'
import { LoadingState } from '@/components/common/LoadingState'
import { EmptyState } from '@/components/common/EmptyState'
import { ListPagination } from '@/components/common/ListPagination'
import { questionService } from '@/services/question.service'
import { examPreparationService as service } from '@/services/examPreparation.service'
import {
  bankQuestion,
  bloomOptions,
  type PaperQuestionDraft,
  type QuestionSlot,
} from '@/lib/examPaperAuthoring'
import { preparationError, DIFFICULTY_LABELS } from '@/lib/examPreparation'
import { ExamLoadError } from './ExamPreparationShared'
import type { ExamBlueprint, AvailablePaperQuestion } from '@/types/examPreparation.types'
import type { CourseClo } from '@/types/courseClo.types'
import type { QuestionDifficulty, QuestionSummary } from '@/types/rubric.types'
export interface IncomingQuestion {
  difficulty: QuestionDifficulty
  question: PaperQuestionDraft
}
export function PaperBankDialog({
  courseId,
  sessionId,
  paperId,
  blueprint,
  clos,
  slots,
  onClose,
  onApply,
}: {
  courseId: string
  sessionId?: string
  paperId?: string
  blueprint: ExamBlueprint
  clos: CourseClo[]
  slots: QuestionSlot[]
  onClose: () => void
  onApply: (questions: IncomingQuestion[]) => void
}) {
  const [difficulty, setDifficulty] = useState('')
  const [bloom, setBloom] = useState('')
  const [cloId, setCloId] = useState('')
  const [page, setPage] = useState(1)
  const [result, setResult] = useState<QuestionSummary[]>()
  const [usage, setUsage] = useState<AvailablePaperQuestion[]>()
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [retry, setRetry] = useState(0)
  const [selected, setSelected] = useState<QuestionSummary[]>([])
  const [busy, setBusy] = useState(false)
  const pending = useRef(false)
  useEffect(() => {
    let active = true
    async function load() {
      const first = await questionService.getQuestions({
        courseId,
        usageScope: 'exam',
        approvalStatus: 'APPROVED',
        pageNumber: 1,
        pageSize: 100,
      })
      const rows = [...first.items]
      for (let next = 2; next <= first.totalPages; next += 4) {
        if (!active) return rows
        const pages = await Promise.all(
          Array.from({ length: Math.min(4, first.totalPages - next + 1) }, (_, offset) =>
            questionService.getQuestions({
              courseId,
              usageScope: 'exam',
              approvalStatus: 'APPROVED',
              pageNumber: next + offset,
              pageSize: 100,
            }),
          ),
        )
        rows.push(...pages.flatMap((p) => p.items))
      }
      return rows
    }
    void Promise.all([
      load(),
      sessionId
        ? service.availableQuestions(blueprint.id, sessionId, undefined, paperId)
        : Promise.resolve(undefined),
    ])
      .then(([data, counts]) => {
        if (active) {
          setResult(data)
          setUsage(counts)
          setError('')
          setLoading(false)
        }
      })
      .catch((failure) => {
        if (active) {
          setError(preparationError(failure))
          setLoading(false)
        }
      })
    return () => {
      active = false
    }
  }, [courseId, blueprint.id, sessionId, paperId, retry])
  function reason(q: QuestionSummary) {
    const quota = usage?.find((row) => row.questionId === q.id)
    if (
      slots.some(
        (s) =>
          s.question.originId === q.id ||
          Boolean(quota?.sourceQuestionId && s.question.originId === quota.sourceQuestionId),
      )
    )
      return 'Đã có trong đề'
    if (usage && !quota) return 'Câu hỏi không còn khả dụng trong phạm vi cấu trúc'
    if (quota && !quota.canSelect) return quota.disabledReason ?? 'Câu hỏi đã hết lượt sử dụng'
    if (q.courseId !== courseId || !clos.some((c) => c.id === q.cloId)) return 'Ngoài phạm vi CLO'
    const capacity = blueprint[`${q.difficulty}Count`]
    if (!capacity) return 'Cấu trúc không có mức độ khó này'
    if (
      selected.filter((s) => s.difficulty === q.difficulty).length >= capacity &&
      !selected.some((s) => s.id === q.id)
    )
      return 'Đã chọn đủ số câu ở mức này'
    return ''
  }
  async function apply() {
    if (pending.current) return
    pending.current = true
    setBusy(true)
    setError('')
    try {
      if (sessionId) {
        const latest = await service.availableQuestions(blueprint.id, sessionId, undefined, paperId)
        setUsage(latest)
        if (selected.some((q) => !latest.some((row) => row.questionId === q.id && row.canSelect)))
          throw new Error('Có câu hỏi đã hết lượt sử dụng. Vui lòng chọn câu khác.')
      }
      const details = await Promise.all(selected.map((q) => questionService.getQuestionById(q.id)))
      if (
        details.some(
          (q) =>
            q.courseId !== courseId ||
            !q.isActive ||
            q.approvalStatus !== 'APPROVED' ||
            !clos.some((c) => c.id === q.cloId),
        )
      )
        throw new Error('Có câu hỏi đã thay đổi hoặc không còn phù hợp. Vui lòng chọn lại.')
      onApply(details.map((q) => ({ difficulty: q.difficulty, question: bankQuestion(q) })))
    } catch (failure) {
      setError(preparationError(failure))
    } finally {
      pending.current = false
      setBusy(false)
    }
  }
  const matching = (result ?? []).filter(
    (q) =>
      (!cloId || q.cloId === cloId) &&
      (!difficulty || q.difficulty === difficulty) &&
      (!bloom || q.bloomLevel === bloom),
  )
  const pageCount = Math.max(1, Math.ceil(matching.length / 8))
  const currentPage = Math.min(page, pageCount)
  const visible = matching.slice((currentPage - 1) * 8, currentPage * 8)
  return (
    <FormDialog
      title="Chọn câu từ ngân hàng"
      eyebrow="Ngân hàng câu hỏi chính thức"
      icon={BookOpen}
      size="2xl"
      bodyClassName="pb-0 sm:pb-0"
      nested
      busy={busy}
      onClose={onClose}
      footer={
        <DialogFooter>
          <span className="mr-auto text-sm font-semibold text-[#1C4D8D]">
            Đã chọn {selected.length} câu
          </span>
          <button type="button" className={ds.outlineButton} disabled={busy} onClick={onClose}>
            Hủy
          </button>
          <button
            type="button"
            className={ds.primaryButton}
            disabled={busy || !selected.length}
            onClick={() => void apply()}
          >
            <Check className="size-4" />
            {busy ? 'Đang tải câu…' : 'Đưa vào đề'}
          </button>
        </DialogFooter>
      }
    >
      <div className="mb-3 grid min-w-0 gap-2 sm:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_minmax(0,2fr)]">
        <FilterDropMenu
          label="Độ khó"
          value={difficulty}
          options={[
            { value: '', label: 'Tất cả độ khó' },
            ...(['easy', 'medium', 'hard'] as const).map((value) => ({
              value,
              label: DIFFICULTY_LABELS[value],
            })),
          ]}
          onChange={(v) => {
            setDifficulty(v)
            setPage(1)
          }}
          className="w-full"
          menuMinWidth={0}
        />
        <FilterDropMenu
          label="Bloom"
          value={bloom}
          options={[
            { value: '', label: 'Tất cả Bloom' },
            ...bloomOptions.map((value) => ({ value, label: value })),
          ]}
          onChange={(v) => {
            setBloom(v)
            setPage(1)
          }}
          className="w-full"
          menuMinWidth={0}
        />
        <FilterDropMenu
          label="Lọc CLO"
          value={cloId}
          options={[
            { value: '', label: 'Tất cả CLO' },
            ...clos.map((c) => ({ value: c.id, label: `${c.cloCode} — ${c.description}` })),
          ]}
          wrapOptions
          onChange={setCloId}
          className="w-full"
          menuMinWidth={0}
        />
      </div>
      {error && (
        <ExamLoadError
          message={error}
          onRetry={() => {
            setRetry((n) => n + 1)
            setLoading(true)
          }}
        />
      )}
      {loading ? (
        <LoadingState />
      ) : !visible.length ? (
        <EmptyState
          className="mb-3"
          icon={BookOpen}
          title="Chưa có câu hỏi phù hợp"
          description="Thử điều chỉnh bộ lọc hoặc tạo câu hỏi mới."
        />
      ) : (
        <div className="divide-y divide-[#82aedb]/45">
          {visible.map((q) => {
            const why = reason(q)
            const checked = selected.some((s) => s.id === q.id)
            return (
              <label
                key={q.id}
                className={cn(
                  'flex min-w-0 items-start gap-3 py-4',
                  why ? 'cursor-not-allowed opacity-45' : 'cursor-pointer',
                )}
              >
                <input
                  type="checkbox"
                  aria-label={`Chọn ${q.title}`}
                  className="mt-1 size-4 shrink-0 accent-[#1C4D8D]"
                  checked={checked}
                  disabled={busy || (Boolean(why) && !checked)}
                  onChange={(e) =>
                    setSelected((s) =>
                      e.target.checked ? [...s, q] : s.filter((x) => x.id !== q.id),
                    )
                  }
                />
                <div className="min-w-0">
                  <div className="mb-1 flex flex-wrap gap-2">
                    <QuestionDifficultyBadge difficulty={q.difficulty} />
                    <span className={ds.infoChip}>
                      {clos.find((c) => c.id === q.cloId)?.cloCode ?? 'Chưa gắn CLO'}
                    </span>
                    <span className={ds.infoChip}>{q.bloomLevel}</span>
                  </div>
                  <p className="text-sm font-bold wrap-anywhere text-[#0F2854]">{q.title}</p>
                  {usage?.find((row) => row.questionId === q.id) && (
                    <p className="mt-1 text-xs text-[#6f8198]">
                      Đã dùng {usage.find((row) => row.questionId === q.id)!.usedCount}/
                      {usage.find((row) => row.questionId === q.id)!.maxUses} đề trong kỳ thi
                    </p>
                  )}
                  {why && <p className="mt-1 text-xs text-[#5b6b84]">{why}</p>}
                </div>
              </label>
            )
          })}
        </div>
      )}
      {!loading && !error && matching.length > 0 && (
        <ListPagination
          label="Phân trang ngân hàng"
          page={currentPage}
          pageCount={pageCount}
          onPageChange={(n) => {
            setPage(n)
          }}
        />
      )}
    </FormDialog>
  )
}
