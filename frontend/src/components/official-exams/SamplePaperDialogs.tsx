import { useEffect, useMemo, useRef, useState } from 'react'
import { BookOpenCheck, Check, FileCheck2, Trash2 } from 'lucide-react'
import { FormDialog, DialogFooter } from '@/design/overlays'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { FilterDropMenu } from '@/components/common/FilterDropMenu'
import { SearchField } from '@/components/common/SearchField'
import { LoadingState } from '@/components/common/LoadingState'
import { EmptyState } from '@/components/common/EmptyState'
import { QuestionDetailContent } from '@/components/question/QuestionDetailContent'
import { toast } from '@/components/common/toastStore'
import { questionService } from '@/services/question.service'
import { examPreparationService as service } from '@/services/examPreparation.service'
import { DIFFICULTY_LABELS, PAPER_LABELS, preparationDate, preparationError } from '@/lib/examPreparation'
import type { CourseClo } from '@/types/courseClo.types'
import type { ExamPaper, PaperQuestion } from '@/types/examPreparation.types'
import type { QuestionSummary } from '@/types/rubric.types'
import { ExamLoadError } from './ExamPreparationShared'
import { QuestionDifficultyBadge } from './QuestionDifficultyBadge'

const difficultyFilterOptions = [
  { value: '', label: 'Tất cả độ khó' },
  { value: 'easy', label: DIFFICULTY_LABELS.easy },
  { value: 'medium', label: DIFFICULTY_LABELS.medium },
  { value: 'hard', label: DIFFICULTY_LABELS.hard },
]

function normalizeDifficulty(value: string) {
  return value.trim().toLowerCase()
}

function toDetailContentQuestion(q: PaperQuestion) {
  return {
    content: q.content,
    imageUrl: q.imageUrl,
    imageAlt: q.imageDescription,
    sampleAnswer: q.sampleAnswer,
    rubric: {
      name: q.rubricName ?? 'Thang điểm câu hỏi',
      totalMaxScore: q.totalMaxScore,
      criteria: q.criteria.map((c, idx) => ({
        id: `${q.questionId}-criterion-${idx}`,
        criterionName: c.name,
        description: c.description,
        maxScore: c.maxScore,
      })),
    },
  }
}

export function SamplePaperCreateDialog({
  courseId,
  courseCode,
  structureId,
  sessionId,
  clos,
  onClose,
  onSaved,
}: {
  courseId: string
  courseCode: string
  structureId: string
  sessionId: string
  clos: CourseClo[]
  onClose: () => void
  onSaved: () => void
}) {
  const [setCode, setSetCode] = useState(`MAU-${courseCode}`)
  const [difficulty, setDifficulty] = useState('')
  const [cloId, setCloId] = useState('')
  const [searchText, setSearchText] = useState('')
  const [questionsState, setQuestionsState] = useState<{
    key: string
    items?: QuestionSummary[]
    error?: string
  }>()
  const [selectedIds, setSelectedIds] = useState<string[]>([])
  const [retryCount, setRetryCount] = useState(0)
  const [busy, setBusy] = useState(false)
  const [submitError, setSubmitError] = useState('')
  const pending = useRef(false)
  const requestKey = `${courseId}:${retryCount}`
  const loading = questionsState?.key !== requestKey
  const loadError = questionsState?.key === requestKey ? (questionsState.error ?? '') : ''

  useEffect(() => {
    let active = true
    async function loadBank() {
      const first = await questionService.getQuestions({
        courseId,
        usageScope: 'exam',
        approvalStatus: 'APPROVED',
        bankReadyOnly: true,
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
              bankReadyOnly: true,
              pageNumber: next + offset,
              pageSize: 100,
            }),
          ),
        )
        for (const page of pages) rows.push(...page.items)
      }
      return rows
    }
    void loadBank()
      .then((rows) => {
        if (active) setQuestionsState({ key: requestKey, items: rows })
      })
      .catch((failure) => {
        if (active) setQuestionsState({ key: requestKey, error: preparationError(failure) })
      })
    return () => {
      active = false
    }
  }, [courseId, requestKey])

  const filteredQuestions = useMemo(() => {
    const questions = questionsState?.items ?? []
    const query = searchText.trim().toLocaleLowerCase('vi-VN')
    return questions.filter((q) => {
      if (q.isActive === false) return false
      if (
        q.bankCurationStatus &&
        q.bankCurationStatus !== 'BANK_READY' &&
        q.bankCurationStatus !== 'CURATED_INTO_BANK'
      ) {
        return false
      }
      if (difficulty && normalizeDifficulty(q.difficulty) !== difficulty) return false
      if (cloId && q.cloId !== cloId) return false
      if (!query) return true
      const matchedClo = clos.find((item) => item.id === q.cloId)?.cloCode ?? ''
      return `${q.title} ${q.bloomLevel} ${matchedClo}`
        .toLocaleLowerCase('vi-VN')
        .includes(query)
    })
  }, [questionsState?.items, difficulty, cloId, searchText, clos])

  function toggleQuestion(id: string) {
    setSelectedIds((current) =>
      current.includes(id) ? current.filter((item) => item !== id) : [...current, id],
    )
  }

  async function save() {
    if (pending.current) return
    const trimmedCode = setCode.trim().toUpperCase()
    if (!trimmedCode) {
      setSubmitError('Vui lòng nhập mã đề mẫu.')
      return
    }
    if (selectedIds.length === 0) {
      setSubmitError('Vui lòng chọn ít nhất 1 câu hỏi cho đề mẫu.')
      return
    }
    pending.current = true
    setBusy(true)
    setSubmitError('')
    try {
      await service.createSamplePaper({
        structureId,
        courseId,
        sessionId,
        setCode: trimmedCode,
        questionIds: selectedIds,
      })
      toast.success(`Đã tạo đề mẫu ${trimmedCode}.`)
      onSaved()
      onClose()
    } catch (failure) {
      setSubmitError(preparationError(failure))
    } finally {
      pending.current = false
      setBusy(false)
    }
  }

  const cloFilterOptions = [
    { value: '', label: 'Tất cả CLO' },
    ...clos.map((c) => ({ value: c.id, label: c.cloCode })),
  ]

  return (
    <FormDialog
      title="Tạo đề mẫu tham khảo"
      eyebrow={courseCode}
      icon={FileCheck2}
      size="lg"
      busy={busy}
      onClose={() => {
        if (!pending.current) onClose()
      }}
      footer={
        <DialogFooter>
          <span className="mr-auto text-sm font-semibold text-[#1C4D8D]">
            Đã chọn {selectedIds.length} câu hỏi
          </span>
          <button type="button" className={ds.outlineButton} disabled={busy} onClick={onClose}>
            Hủy
          </button>
          <button
            type="button"
            className={ds.primaryButton}
            disabled={busy || loading || !setCode.trim() || selectedIds.length === 0}
            onClick={() => void save()}
          >
            <Check className="size-4" />
            {busy ? 'Đang lưu…' : 'Lưu đề mẫu'}
          </button>
        </DialogFooter>
      }
    >
      <div className="space-y-4">
        <p className="rounded-xl border border-[#82aedb]/55 bg-[#F4FBFD] px-3.5 py-2.5 text-sm text-[#4a5d75]">
          Đề mẫu giúp giảng viên tham khảo chuẩn ra đề. Các câu thuộc đề mẫu sẽ tự động ẩn khỏi danh
          sách chọn đề thi thật và không tính vào số lượng bộ đề cần bàn giao.
        </p>

        <div className="grid gap-3 sm:grid-cols-2">
          <div>
            <label
              htmlFor="sample-set-code"
              className="block text-xs font-bold tracking-[0.06em] text-[#5b6b84] uppercase"
            >
              Mã đề mẫu
            </label>
            <input
              id="sample-set-code"
              type="text"
              maxLength={50}
              className={cn(ds.field, 'mt-1.5 uppercase')}
              placeholder="VD: MAU-PRN231-01"
              value={setCode}
              disabled={busy}
              onChange={(e) => setSetCode(e.target.value)}
            />
          </div>
          <div>
            <span className="block text-xs font-bold tracking-[0.06em] text-[#5b6b84] uppercase">
              Tìm câu hỏi trong ngân hàng
            </span>
            <div className="mt-1.5">
              <SearchField
                aria-label="Tìm câu hỏi đề mẫu"
                placeholder="Tìm theo tiêu đề hoặc CLO"
                value={searchText}
                onChange={(e) => setSearchText(e.target.value)}
              />
            </div>
          </div>
        </div>

        <div className="flex flex-wrap gap-2">
          <FilterDropMenu
            label="Tất cả độ khó"
            value={difficulty}
            options={difficultyFilterOptions}
            onChange={setDifficulty}
            className={ds.filterRole}
            menuMinWidth={0}
          />
          <FilterDropMenu
            label="Tất cả CLO"
            value={cloId}
            options={cloFilterOptions}
            onChange={setCloId}
            className={ds.filterRole}
            wrapOptions
            menuMinWidth={0}
          />
        </div>

        {loading ? (
          <LoadingState label="Đang tải câu hỏi đã duyệt trong ngân hàng…" />
        ) : loadError ? (
          <ExamLoadError
            message={loadError}
            onRetry={() => setRetryCount((current) => current + 1)}
          />
        ) : filteredQuestions.length === 0 ? (
          <EmptyState
            icon={BookOpenCheck}
            title="Chưa có câu hỏi phù hợp"
            description="Chưa tìm thấy câu hỏi thi thật đã duyệt trong ngân hàng theo bộ lọc hiện tại."
          />
        ) : (
          <div className="max-h-80 divide-y divide-[#82aedb]/45 overflow-y-auto rounded-2xl border border-[#82aedb]/55 bg-white">
            {filteredQuestions.map((q) => {
              const checked = selectedIds.includes(q.id)
              const questionClo = clos.find((item) => item.id === q.cloId)
              return (
                <label
                  key={q.id}
                  className={cn(
                    'flex cursor-pointer items-start gap-3 p-3.5 transition-colors',
                    checked ? 'bg-[#E7F3FB]' : 'hover:bg-[#F4FBFD]',
                    busy && 'cursor-wait',
                  )}
                >
                  <input
                    type="checkbox"
                    className="mt-1 size-4 shrink-0 accent-[#1C4D8D]"
                    checked={checked}
                    disabled={busy}
                    onChange={() => toggleQuestion(q.id)}
                  />
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="text-sm font-bold wrap-anywhere text-[#0F2854]">
                        {q.title}
                      </span>
                      <QuestionDifficultyBadge difficulty={q.difficulty} />
                      {questionClo && <span className={ds.infoChip}>{questionClo.cloCode}</span>}
                      {q.bloomLevel && <span className={ds.infoChip}>{q.bloomLevel}</span>}
                    </div>
                  </div>
                </label>
              )
            })}
          </div>
        )}

        {submitError && <ExamLoadError message={submitError} />}
      </div>
    </FormDialog>
  )
}

export function SamplePaperViewerDialog({
  paper,
  clos,
  canDelete = false,
  onClose,
  onDeleted,
}: {
  paper: ExamPaper
  clos: CourseClo[]
  canDelete?: boolean
  onClose: () => void
  onDeleted?: () => void
}) {
  const [confirmingDelete, setConfirmingDelete] = useState(false)
  const [busy, setBusy] = useState(false)
  const [deleteError, setDeleteError] = useState('')
  const pending = useRef(false)
  const orderedQuestions = useMemo(
    () => [...paper.questions].sort((a, b) => a.orderIndex - b.orderIndex),
    [paper.questions],
  )

  async function handleDelete() {
    if (pending.current) return
    pending.current = true
    setBusy(true)
    setDeleteError('')
    try {
      await service.deleteSamplePaper(paper.id)
      toast.success(`Đã xóa đề mẫu ${paper.setCode}.`)
      onDeleted?.()
      onClose()
    } catch (failure) {
      setDeleteError(preparationError(failure))
    } finally {
      pending.current = false
      setBusy(false)
    }
  }

  return (
    <FormDialog
      title={`Đề mẫu: ${paper.setCode}`}
      eyebrow={`Tham khảo · ${orderedQuestions.length} câu hỏi`}
      icon={FileCheck2}
      size="lg"
      busy={busy}
      onClose={() => {
        if (!pending.current) onClose()
      }}
      footer={
        <DialogFooter>
          {canDelete && !confirmingDelete && (
            <button
              type="button"
              className={cn(
                ds.outlineButton,
                'mr-auto border-[#c2415b] text-[#c2415b] hover:border-[#a83249] hover:bg-[#fff1f3]',
              )}
              disabled={busy}
              onClick={() => {
                setConfirmingDelete(true)
                setDeleteError('')
              }}
            >
              <Trash2 className="size-4" />
              Xóa đề mẫu
            </button>
          )}
          {canDelete && confirmingDelete && (
            <div className="mr-auto flex flex-wrap items-center gap-2">
              <span className="text-sm font-semibold text-[#c2415b]">
                Xác nhận xóa đề mẫu này?
              </span>
              <button
                type="button"
                className={ds.outlineButton}
                disabled={busy}
                onClick={() => setConfirmingDelete(false)}
              >
                Hủy
              </button>
              <button
                type="button"
                className={cn(
                  ds.primaryButton,
                  'bg-[#c2415b] hover:bg-[#a83249]',
                )}
                disabled={busy}
                onClick={() => void handleDelete()}
              >
                <Trash2 className="size-4" />
                {busy ? 'Đang xóa…' : 'Xác nhận xóa'}
              </button>
            </div>
          )}
          <button type="button" className={ds.outlineButton} disabled={busy} onClick={onClose}>
            Đóng
          </button>
        </DialogFooter>
      }
    >
      <div className="space-y-4">
        <div className="flex flex-wrap items-center gap-2 rounded-xl border border-[#82aedb]/55 bg-[#F4FBFD] px-3.5 py-2.5 text-sm text-[#4a5d75]">
          <span className={ds.infoChip}>Đề mẫu tham khảo</span>
          <span className={ds.infoChip}>{PAPER_LABELS[paper.status] ?? paper.status}</span>
          <span>Tạo lúc {preparationDate(paper.generatedAt)}</span>
        </div>

        {deleteError && <ExamLoadError message={deleteError} />}

        {orderedQuestions.length === 0 ? (
          <EmptyState
            icon={FileCheck2}
            title="Đề mẫu chưa có câu hỏi"
            description="Không có câu hỏi nào trong bộ đề mẫu này."
          />
        ) : (
          <div className="space-y-4">
            {orderedQuestions.map((question, index) => {
              const clo = clos.find((item) => item.id === question.cloId)
              return (
                <article
                  key={question.questionId}
                  className="space-y-3 rounded-2xl border border-[#82aedb]/55 bg-[#F9FCFF] p-4"
                >
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="text-sm font-bold text-[#0F2854]">
                        Câu {index + 1}: {question.title}
                      </span>
                      <QuestionDifficultyBadge difficulty={question.difficulty} />
                      {clo && <span className={ds.infoChip}>{clo.cloCode}</span>}
                      {question.bloomLevel && (
                        <span className={ds.infoChip}>{question.bloomLevel}</span>
                      )}
                    </div>
                  </div>
                  <QuestionDetailContent question={toDetailContentQuestion(question)} />
                </article>
              )
            })}
          </div>
        )}
      </div>
    </FormDialog>
  )
}
