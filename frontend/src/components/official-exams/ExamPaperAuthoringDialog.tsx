import { QuestionDifficultyBadge } from './QuestionDifficultyBadge'
import { useRef, useState } from 'react'
import {
  BookOpen,
  CheckCircle2,
  Circle,
  CircleAlert,
  FilePenLine,
  Save,
  Sparkles,
  Trash2,
} from 'lucide-react'
import { FormDialog, DialogFooter } from '@/design/overlays'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { TextareaField } from '@/components/common/TextareaField'
import { FieldError } from '@/components/common/formField'
import { fieldLabelClass } from '@/components/common/formFieldClasses'
import { toast } from '@/components/common/toastStore'
import { questionService } from '@/services/question.service'
import { examPreparationService as service } from '@/services/examPreparation.service'
import { DIFFICULTY_LABELS, preparationError } from '@/lib/examPreparation'
import {
  bloomOptions,
  difficulties,
  emptyQuestion,
  generatedQuestion,
  initialSlots,
  placementPlan,
  questionErrors,
  touched,
  type PaperQuestionDraft,
  type QuestionSlot,
} from '@/lib/examPaperAuthoring'
import type { CourseClo } from '@/types/courseClo.types'
import type {
  ExamAuthoringAssignment,
  ExamBlueprint,
  ExamPaper,
  ExamPreparation,
} from '@/types/examPreparation.types'
import { ExamLoadError } from './ExamPreparationShared'
import { PaperQuestionFields } from './PaperQuestionFields'
import { PaperBankDialog, type IncomingQuestion } from './PaperBankDialog'

export function ExamPaperAuthoringDialog({
  preparation,
  assignment,
  blueprint,
  clos,
  paper,
  onClose,
  onSaved,
}: {
  preparation: ExamPreparation
  assignment: ExamAuthoringAssignment
  blueprint: ExamBlueprint
  clos: CourseClo[]
  paper?: ExamPaper
  onClose: () => void
  onSaved: () => void
}) {
  const activeClos = clos.filter((c) => c.isActive && blueprint.cloIds.includes(c.id))
  const [slots, setSlots] = useState<QuestionSlot[]>(() =>
    initialSlots(blueprint, activeClos, paper),
  )
  const [index, setIndex] = useState(0)
  const [code, setCode] = useState(paper?.setCode ?? '')
  const [draftId, setDraftId] = useState(paper?.id)
  const [dirty, setDirty] = useState(false)
  const [busy, setBusy] = useState(false)
  const [uploading, setUploading] = useState(false)
  const [error, setError] = useState('')
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [codeError, setCodeError] = useState('')
  const [bank, setBank] = useState(false)
  const [ai, setAi] = useState(false)
  const [aiNotes, setAiNotes] = useState('')
  const [aiError, setAiError] = useState('')
  const [confirmClose, setConfirmClose] = useState(false)
  const [clearIndex, setClearIndex] = useState<number>()
  const [replacement, setReplacement] = useState<ReturnType<typeof placementPlan>>()
  const pending = useRef(false)
  const slot = slots[index]
  const blocked = busy || uploading
  const completed = slots.filter(
    (s) => touched(s.question) && !Object.keys(questionErrors(s.question, activeClos)).length,
  ).length
  const remaining = slots.filter((s) => !touched(s.question))
  const close = () => {
    if (pending.current || uploading) return
    if (dirty) setConfirmClose(true)
    else onClose()
  }
  function changeQuestion(question: PaperQuestionDraft) {
    setSlots((current) => current.map((s, i) => (i === index ? { ...s, question } : s)))
    setDirty(true)
    setErrors({})
  }
  function selectStep(step: 1 | 2) {
    if (step === 2 && slot.step === 1 && slot.question.origin !== 'bank') {
      const validation = questionErrors(slot.question, activeClos)
      const contentErrors = Object.fromEntries(
        Object.entries(validation).filter(([key]) =>
          ['title', 'content', 'sampleAnswer', 'cloId', 'bloomLevel'].includes(key),
        ),
      )
      setErrors(contentErrors)
      if (Object.keys(contentErrors).length) return
    }
    setErrors({})
    setSlots((current) => current.map((s, i) => (i === index ? { ...s, step } : s)))
  }
  function applyPlan(plan: ReturnType<typeof placementPlan>) {
    setSlots((current) =>
      current.map((s, i) => {
        const item = plan.find((p) => p.target === i)
        return item ? { ...s, question: item.question, step: 1 } : s
      }),
    )
    setIndex(plan[0]?.target ?? index)
    setDirty(true)
    setErrors({})
    setReplacement(undefined)
  }
  function fromBank(incoming: IncomingQuestion[]) {
    const plan = placementPlan(slots, incoming)
    setBank(false)
    if (plan.some((p) => touched(slots[p.target].question))) setReplacement(plan)
    else applyPlan(plan)
  }
  async function generate() {
    if (pending.current || !remaining.length) return
    pending.current = true
    setBusy(true)
    setAiError('')
    const counts = Object.fromEntries(
      difficulties.map((d) => [d, remaining.filter((s) => s.difficulty === d).length]),
    ) as Record<'easy' | 'medium' | 'hard', number>
    try {
      const result = await questionService.generateFromClo({
        courseId: preparation.courseId,
        usageScope: 'exam',
        selectedCloCodes: activeClos.map((c) => c.cloCode),
        topics: aiNotes.trim() ? [aiNotes.trim()] : [],
        numberOfQuestions: remaining.length,
        targetBloomLevels: bloomOptions,
        difficultyDistribution: counts,
      })
      const incoming = result.questions.map((q) => ({
        difficulty: q.difficulty,
        question: generatedQuestion(q, activeClos),
      }))
      if (
        result.courseId !== preparation.courseId ||
        incoming.length !== remaining.length ||
        difficulties.some((d) => incoming.filter((q) => q.difficulty === d).length !== counts[d]) ||
        incoming.some((q) => !activeClos.some((c) => c.id === q.question.cloId))
      )
        throw new Error('AI trả về chưa đúng số câu, độ khó hoặc phạm vi CLO. Hãy thử lại.')
      applyPlan(placementPlan(slots, incoming))
      setAi(false)
      toast.success('Đã thêm đề xuất AI. Hãy rà soát trước khi lưu.')
    } catch (failure) {
      setAiError(preparationError(failure))
    } finally {
      pending.current = false
      setBusy(false)
    }
  }
  async function save() {
    if (pending.current || uploading) return
    if (!code.trim() || code.trim().length > 50) {
      setCodeError('Nhập mã đề, tối đa 50 ký tự.')
      return
    }
    const invalid = slots.findIndex(
      (s) => touched(s.question) && Object.keys(questionErrors(s.question, activeClos)).length,
    )
    if (invalid >= 0) {
      const found = questionErrors(slots[invalid].question, activeClos)
      setIndex(invalid)
      setErrors(found)
      setSlots((current) =>
        current.map((s, i) =>
          i === invalid
            ? {
                ...s,
                step:
                  found.title ||
                  found.content ||
                  found.sampleAnswer ||
                  found.cloId ||
                  found.bloomLevel
                    ? 1
                    : 2,
              }
            : s,
        ),
      )
      setError(
        'Hoàn thiện câu đang soạn hoặc bỏ câu đó trước khi lưu. Các vị trí trống vẫn được lưu trong bản nháp.',
      )
      return
    }
    pending.current = true
    setBusy(true)
    setError('')
    try {
      let id = draftId
      if (!id) {
        const created = await service.createPaper({
          assignmentId: assignment.id,
          structureId: assignment.structureId,
          courseId: preparation.courseId,
          sessionId: preparation.sessionId,
          setCode: code.trim(),
          questionIds: [],
        })
        id = created.id
        setDraftId(id)
      }
      const ids: string[] = []
      for (let i = 0; i < slots.length; i++) {
        const q = slots[i].question
        if (!touched(q)) continue
        if (q.id && q.persisted) {
          ids.push(q.id)
          continue
        }
        const created = await service.createQuestion({
          courseId: preparation.courseId,
          title: q.title.trim(),
          content: q.content.trim(),
          sampleAnswer: q.sampleAnswer.trim(),
          keyPoints: q.keyPoints,
          difficulty: slots[i].difficulty,
          bloomLevel: q.bloomLevel,
          source: q.origin === 'ai' ? 'flm_api' : 'manual',
          usageScope: 'exam',
          hasFollowUp: false,
          cloId: q.cloId,
          imageUrl: q.imageUrl || null,
          imageDescription: q.imageDescription || null,
          rubric: {
            ...q.rubric,
            totalMaxScore: 10,
            criteria: q.rubric.criteria.map((c, n) => ({
              ...c,
              weight: c.maxScore / 10,
              orderIndex: n + 1,
            })),
          },
        })
        ids.push(created.questionId)
        setSlots((current) =>
          current.map((s, n) =>
            n === i
              ? {
                  ...s,
                  question: {
                    ...s.question,
                    id: created.questionId,
                    originId: created.questionId,
                    persisted: true,
                  },
                }
              : s,
          ),
        )
      }
      await service.savePaper(id, ids)
      toast.success('Đã lưu bản nháp bộ đề.')
      onSaved()
      onClose()
    } catch (failure) {
      setError(preparationError(failure))
    } finally {
      pending.current = false
      setBusy(false)
    }
  }
  return (
    <>
      <FormDialog
        title={paper ? 'Chỉnh sửa bộ đề' : 'Soạn bộ đề'}
        eyebrow={`${preparation.courseCode} · ${preparation.title}`}
        icon={FilePenLine}
        size="xl"
        maxHeight="h-[min(94dvh,960px)] max-h-[94dvh]"
        busy={blocked}
        onClose={close}
        bodyClassName="p-0 sm:p-0"
        footer={
          <DialogFooter className="grid grid-cols-2 items-center gap-2 sm:flex sm:flex-row sm:flex-wrap">
            <div className="col-span-2 mr-auto min-w-0">
              <p className="text-sm font-bold text-[#1C4D8D]">
                {completed}/{slots.length} câu hoàn chỉnh
              </p>
              <p className="mt-1 text-xs text-[#5b6b84]">
                {difficulties
                  .map(
                    (d) =>
                      `${DIFFICULTY_LABELS[d]} ${slots.filter((s) => s.difficulty === d && touched(s.question)).length}/${slots.filter((s) => s.difficulty === d).length}`,
                  )
                  .join(' · ')}
              </p>
            </div>
            <button type="button" className={ds.outlineButton} disabled={blocked} onClick={close}>
              Hủy
            </button>
            <button
              type="button"
              className={ds.primaryButton}
              disabled={blocked || !activeClos.length}
              onClick={() => void save()}
            >
              <Save className="size-4" />
              {busy ? 'Đang lưu…' : 'Lưu bản nháp'}
            </button>
          </DialogFooter>
        }
      >
        <div className="min-w-0 border-b border-[#82aedb]/55 bg-[#F4FBFD] p-4 sm:px-5">
          <label className="block min-w-0">
            <span className={fieldLabelClass(Boolean(codeError))}>Mã đề</span>
            <input
              className={cn(ds.field, 'mt-1.5 h-10')}
              value={code}
              readOnly={Boolean(draftId)}
              disabled={blocked}
              maxLength={50}
              placeholder="Ví dụ: PRN231-FE-01"
              onChange={(e) => {
                setCode(e.target.value)
                setCodeError('')
                setDirty(true)
              }}
            />
            <FieldError message={codeError} />
          </label>
          <div className="mt-3 flex min-w-0 flex-wrap items-center gap-2">
            <p className="text-sm leading-relaxed font-semibold text-[#1C4D8D]">
              {blueprint.totalQuestions} câu · {blueprint.durationMinutes} phút · Dễ{' '}
              {blueprint.easyCount} / TB {blueprint.mediumCount} / Khó {blueprint.hardCount} ·{' '}
              {blueprint.cloIds.length} CLO
            </p>
          </div>
          <div className="mt-3 flex flex-wrap gap-2">
            <button
              type="button"
              className={ds.outlineButton}
              disabled={blocked}
              onClick={() => setBank(true)}
            >
              <BookOpen className="size-4" />
              Ngân hàng câu hỏi
            </button>
            <button
              type="button"
              className={ds.outlineButton}
              disabled={blocked || !remaining.length || !activeClos.length}
              onClick={() => {
                setAi(true)
                setAiError('')
              }}
            >
              <Sparkles className="size-4" />
              Tạo bằng AI ({remaining.length})
            </button>
          </div>
        </div>
        {error && (
          <div className="p-4">
            <ExamLoadError message={error} />
          </div>
        )}
        {!activeClos.length && (
          <div className="p-4">
            <ExamLoadError message="Không còn CLO đang hoạt động trong cấu trúc. Liên hệ Trưởng bộ môn để kiểm tra phân công." />
          </div>
        )}
        {paper?.reviewNotes && ['NEEDS_REVISION', 'REJECTED'].includes(paper.status) && (
          <section className="mx-4 mt-3 rounded-xl border border-[#f3d19a] bg-[#fff8eb] p-4">
            <h3 className="text-sm font-bold text-[#92600c]">Phản hồi trưởng bộ môn</h3>
            <p className="mt-2 text-sm wrap-anywhere whitespace-pre-wrap text-[#5b6b84]">
              {paper.reviewNotes}
            </p>
          </section>
        )}
        <div className="grid min-w-0 md:grid-cols-[192px_minmax(0,1fr)]">
          <nav
            role="tablist"
            aria-label="Các câu đang soạn"
            aria-orientation="vertical"
            className="grid min-w-0 grid-cols-2 content-start gap-2 border-b border-[#82aedb]/55 bg-[#F8FBFD] p-3 md:grid-cols-1 md:border-r md:border-b-0"
          >
            {slots.map((s, i) => {
              const filled = touched(s.question)
              const validation = questionErrors(s.question, activeClos)
              const complete = filled && !Object.keys(validation).length
              const missingContent = [
                'title',
                'content',
                'sampleAnswer',
                'cloId',
                'bloomLevel',
              ].some((key) => validation[key])
              const state = !filled
                ? {
                    label: 'Chưa soạn',
                    tone: 'border-slate-200 bg-slate-100 text-[#64748b]',
                    icon: Circle,
                  }
                : complete
                  ? {
                      label: 'Hoàn chỉnh',
                      tone: 'border-[#a9dfc7] bg-[#eaf8f1] text-[#16836a]',
                      icon: CheckCircle2,
                    }
                  : {
                      label: missingContent ? 'Thiếu nội dung' : 'Thiếu barem',
                      tone: 'border-[#eed39a] bg-[#fff4df] text-[#a5660e]',
                      icon: CircleAlert,
                    }
              return (
                <button
                  type="button"
                  id={`author-tab-${i}`}
                  key={i}
                  role="tab"
                  aria-selected={index === i}
                  aria-controls="author-question-panel"
                  tabIndex={index === i ? 0 : -1}
                  disabled={blocked}
                  onClick={() => {
                    setIndex(i)
                    setErrors({})
                  }}
                  onKeyDown={(e) => {
                    if (['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(e.key)) {
                      e.preventDefault()
                      const next =
                        e.key === 'Home'
                          ? 0
                          : e.key === 'End'
                            ? slots.length - 1
                            : (i + (e.key === 'ArrowDown' ? 1 : -1) + slots.length) % slots.length
                      setIndex(next)
                      setErrors({})
                      document.getElementById(`author-tab-${next}`)?.focus()
                    }
                  }}
                  className={cn(
                    'w-full max-w-full min-w-0 overflow-hidden rounded-xl border px-3 py-3 text-left focus-visible:outline-2 focus-visible:outline-[#1C4D8D]',
                    index === i ? 'border-[#4988C4] bg-[#DCEEF9]' : 'border-[#82aedb]/40 bg-white',
                  )}
                >
                  <span className="flex flex-wrap items-center gap-2">
                    <strong className="text-sm text-[#0F2854]">Câu {i + 1}</strong>
                    <QuestionDifficultyBadge difficulty={s.difficulty} />
                  </span>
                  <span className="mt-1.5 block max-w-full truncate text-xs text-[#5b6b84]">
                    {s.question.title || 'Chưa có câu hỏi'}
                  </span>
                  <span
                    className={cn(
                      'mt-2 inline-flex items-center gap-1.5 rounded-lg border px-2 py-1 text-xs font-bold',
                      state.tone,
                    )}
                  >
                    <state.icon className="size-3.5 shrink-0" aria-hidden="true" />
                    {state.label}
                  </span>
                </button>
              )
            })}
          </nav>
          <div
            role="tabpanel"
            id="author-question-panel"
            aria-labelledby={`author-tab-${index}`}
            className="w-full max-w-xl min-w-0 space-y-3 justify-self-center px-4 pt-4 pb-2 sm:p-5 sm:pb-2"
          >
            <PaperQuestionFields
              key={index}
              slot={slot}
              index={index}
              clos={activeClos}
              errors={errors}
              busy={blocked}
              onChange={changeQuestion}
              onStep={selectStep}
              onUploading={setUploading}
              onClear={touched(slot.question) ? () => setClearIndex(index) : undefined}
            />
          </div>
        </div>
      </FormDialog>
      {bank && (
        <PaperBankDialog
          courseId={preparation.courseId}
          blueprint={blueprint}
          clos={activeClos}
          slots={slots}
          onClose={() => setBank(false)}
          onApply={fromBank}
        />
      )}
      {replacement && (
        <FormDialog
          title="Xác nhận thay câu hỏi"
          icon={BookOpen}
          size="md"
          nested
          onClose={() => setReplacement(undefined)}
          footer={
            <DialogFooter>
              <button
                type="button"
                className={ds.outlineButton}
                onClick={() => setReplacement(undefined)}
              >
                Giữ câu hiện tại
              </button>
              <button
                type="button"
                className={ds.primaryButton}
                disabled={new Set(replacement.map((p) => p.target)).size !== replacement.length}
                onClick={() => applyPlan(replacement)}
              >
                Thay thế và đưa vào đề
              </button>
            </DialogFooter>
          }
        >
          <p className="mb-3 text-sm text-[#5b6b84]">
            Chọn vị trí nhận câu hỏi. Nội dung hiện tại chỉ được thay sau khi xác nhận.
          </p>
          <div className="space-y-3">
            {replacement.map((p, i) => (
              <div key={i} className="min-w-0 rounded-xl border border-[#82aedb]/55 p-3">
                <p className="mb-2 text-sm font-bold wrap-anywhere text-[#0F2854]">
                  {p.question.title}
                </p>
                <label className="block text-xs font-semibold text-[#5b6b84]">
                  Vị trí nhận câu {i + 1}
                  <select
                    aria-label={`Vị trí nhận câu ${i + 1}`}
                    className={cn(ds.field, 'mt-1.5 h-10')}
                    value={p.target}
                    onChange={(e) =>
                      setReplacement((current) =>
                        current?.map((x, n) =>
                          n === i ? { ...x, target: Number(e.target.value) } : x,
                        ),
                      )
                    }
                  >
                    {slots.map(
                      (s, n) =>
                        s.difficulty === p.difficulty && (
                          <option key={n} value={n}>
                            Câu {n + 1} · {DIFFICULTY_LABELS[s.difficulty]}
                            {touched(s.question) ? ' · Thay câu hiện tại' : ' · Trống'}
                          </option>
                        ),
                    )}
                  </select>
                </label>
                {touched(slots[p.target].question) && (
                  <p className="mt-2 text-xs wrap-anywhere text-[#a5660e]">
                    Sẽ thay: {slots[p.target].question.title || 'Nội dung đang soạn'}
                  </p>
                )}
              </div>
            ))}
          </div>
          {new Set(replacement.map((p) => p.target)).size !== replacement.length && (
            <FieldError message="Mỗi câu cần một vị trí khác nhau." />
          )}
        </FormDialog>
      )}
      {ai && (
        <FormDialog
          title="AI tạo câu còn thiếu"
          eyebrow={preparation.courseCode}
          icon={Sparkles}
          size="md"
          nested
          busy={busy}
          onClose={() => setAi(false)}
          footer={
            <DialogFooter>
              <button
                type="button"
                className={ds.outlineButton}
                disabled={busy}
                onClick={() => setAi(false)}
              >
                Hủy
              </button>
              <button
                type="button"
                className={ds.primaryButton}
                disabled={busy || !remaining.length}
                onClick={() => void generate()}
              >
                <Sparkles className="size-4" />
                {busy ? 'Đang tạo…' : `Tạo ${remaining.length} câu hỏi`}
              </button>
            </DialogFooter>
          }
        >
          <section className="mb-3 space-y-3 rounded-2xl border border-[#82aedb]/55 bg-white p-4">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div>
                <h3 className="text-sm font-bold text-[#0F2854]">Câu hỏi cần tạo</h3>
                <p className="mt-1 text-xs text-[#5b6b84]">Theo độ khó và phạm vi CLO được giao.</p>
              </div>
              <span className={ds.infoChip}>{remaining.length} câu</span>
            </div>
            <div className="grid grid-cols-3 gap-2">
              {difficulties.map((difficulty) => (
                <div
                  key={difficulty}
                  className={cn(
                    'min-w-0 rounded-xl border px-3 py-2',
                    difficulty === 'easy'
                      ? 'border-[#a9dfc7] bg-[#eaf8f1]'
                      : difficulty === 'medium'
                        ? 'border-[#eed39a] bg-[#fff4df]'
                        : 'border-[#efbdc6] bg-[#fdf0f2]',
                  )}
                >
                  <QuestionDifficultyBadge difficulty={difficulty} />
                  <p className="mt-1 text-base font-bold text-[#0F2854] tabular-nums">
                    {remaining.filter((s) => s.difficulty === difficulty).length}{' '}
                    <span className="text-xs font-medium text-[#5b6b84]">câu</span>
                  </p>
                </div>
              ))}
            </div>
            <p className="border-t border-[#82aedb]/35 pt-3 text-xs leading-relaxed text-[#5b6b84]">
              AI chỉ điền các câu còn trống. Câu đã soạn được giữ nguyên.
            </p>
          </section>
          <TextareaField
            label="Yêu cầu bổ sung (không bắt buộc)"
            placeholder="Ví dụ: Ưu tiên tình huống thực tế và khả năng giải thích"
            value={aiNotes}
            onChange={(e) => setAiNotes(e.target.value)}
            maxLength={2000}
            disabled={busy}
            rows={2}
            className="min-h-[calc(2lh+1.25rem+2px)]"
          />
          <p className="mt-2 text-xs leading-relaxed text-[#5b6b84]">
            Rà soát nội dung, đáp án mẫu và barem sau khi AI tạo xong.
          </p>
          {aiError && (
            <div className="mt-3">
              <ExamLoadError message={aiError} />
            </div>
          )}
        </FormDialog>
      )}
      {(confirmClose || clearIndex !== undefined) && (
        <FormDialog
          title={confirmClose ? 'Bỏ thay đổi chưa lưu?' : 'Bỏ câu khỏi đề?'}
          icon={Trash2}
          size="sm"
          nested
          onClose={() => {
            setConfirmClose(false)
            setClearIndex(undefined)
          }}
          footer={
            <DialogFooter>
              <button
                type="button"
                className={ds.outlineButton}
                onClick={() => {
                  setConfirmClose(false)
                  setClearIndex(undefined)
                }}
              >
                Tiếp tục soạn
              </button>
              <button
                type="button"
                className={cn(ds.primaryButton, 'bg-[#c2415b]')}
                onClick={() => {
                  if (confirmClose) {
                    onSaved()
                    onClose()
                  } else {
                    setSlots((current) =>
                      current.map((s, i) =>
                        i === clearIndex
                          ? { ...s, question: emptyQuestion(activeClos[0]?.id), step: 1 }
                          : s,
                      ),
                    )
                    setDirty(true)
                    setClearIndex(undefined)
                    setErrors({})
                  }
                }}
              >
                {confirmClose ? 'Bỏ thay đổi' : 'Bỏ câu'}
              </button>
            </DialogFooter>
          }
        >
          <p className="text-sm text-[#5b6b84]">
            {confirmClose
              ? 'Nội dung vừa chỉnh sửa sẽ không được lưu.'
              : 'Vị trí câu hỏi sẽ trở về trống và giữ nguyên độ khó theo cấu trúc.'}
          </p>
        </FormDialog>
      )}
    </>
  )
}
