import { QuestionDifficultyBadge } from './QuestionDifficultyBadge'
import { useEffect, useRef, useState } from 'react'
import {
  ArrowLeft,
  ArrowRight,
  BookOpenCheck,
  Check,
  FilePenLine,
  History,
  LoaderCircle,
  X,
} from 'lucide-react'
import { SideDrawer, FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { QuestionDetailContent } from '@/components/question/QuestionDetailContent'
import { StatusTabs } from '@/components/common/StatusTabs'
import { DialogScope } from '@/design/DialogContext'
import { SubmitDialogFooter } from '@/components/common/SubmitDialogFooter'
import { TextareaField } from '@/components/common/TextareaField'
import { LoadingState } from '@/components/common/LoadingState'
import { toast } from '@/components/common/toastStore'
import { examPreparationService as service } from '@/services/examPreparation.service'
import { PAPER_LABELS, preparationDate, preparationError } from '@/lib/examPreparation'
import type { CourseClo } from '@/types/courseClo.types'
import type {
  PaperSimilarityReport,
  BankPromotionCandidate,
  ExamPaper,
  PaperDecision,
  PaperRevision,
  PaperStatus,
} from '@/types/examPreparation.types'
import { PaperSimilarityPanel } from './PaperSimilarityPanel'
import { PaperBankPromotionDialog } from './PaperBankPromotionDialog'
import { ExamLoadError, ExamStatusBadge } from './ExamPreparationShared'

const navButton = cn(ds.outlineButton, 'size-9 shrink-0 p-0 disabled:opacity-35')
export function ExamPaperReviewDrawer({
  paper,
  author,
  clos,
  readOnly,
  onClose,
  onPrevious,
  onNext,
  onReviewed,
  reviewer = true,
}: {
  reviewer?: boolean
  paper: ExamPaper
  author: string
  clos: CourseClo[]
  readOnly: boolean
  onClose: () => void
  onPrevious?: () => void
  onNext?: () => void
  onReviewed: (id: string, status: PaperStatus, notes: string) => void
}) {
  const questions = [...paper.questions].sort((a, b) => a.orderIndex - b.orderIndex)
  const [questionIndex, setQuestionIndex] = useState('0')
  const [seen, setSeen] = useState(() => new Set(['0']))
  const [similarity, setSimilarity] = useState<PaperSimilarityReport>()
  const [promotion, setPromotion] = useState<BankPromotionCandidate[]>()
  const [approved, setApproved] = useState(false)
  const [duplicateError, setDuplicateError] = useState('')
  const [retry, setRetry] = useState(0)
  const [action, setAction] = useState<'request-changes' | 'reject'>()
  const [notes, setNotes] = useState('')
  const [noteError, setNoteError] = useState('')
  const [decisionError, setDecisionError] = useState('')
  const [busy, setBusy] = useState(false)
  const pending = useRef(false)
  const [historyOpen, setHistoryOpen] = useState(false)
  const [history, setHistory] = useState<PaperRevision[]>()
  const [historyError, setHistoryError] = useState('')
  const [historyRetry, setHistoryRetry] = useState(0)
  const question = questions[Number(questionIndex)]
  const clo = clos.find((item) => item.id === question?.cloId)
  const canReview = !approved && !readOnly && paper.status === 'SUBMITTED_FOR_REVIEW'

  useEffect(() => {
    const controller = new AbortController()
    void (reviewer ? service.similarity(paper.id, controller.signal) : Promise.resolve(undefined))
      .then((data) => {
        if (!controller.signal.aborted) {
          setSimilarity(data)
          setDuplicateError('')
        }
      })
      .catch((error) => {
        if (!controller.signal.aborted) setDuplicateError(preparationError(error))
      })
    return () => controller.abort()
  }, [paper.id, retry, reviewer])
  useEffect(() => {
    if (!historyOpen) return
    const controller = new AbortController()
    void service
      .history(paper.id, controller.signal)
      .then((data) => {
        if (!controller.signal.aborted) {
          setHistory(data)
          setHistoryError('')
        }
      })
      .catch((error) => {
        if (!controller.signal.aborted) setHistoryError(preparationError(error))
      })
    return () => controller.abort()
  }, [paper.id, paper.status, historyOpen, historyRetry])

  async function decide(decision: PaperDecision) {
    if (pending.current) return
    const message = decision === 'approve' ? '' : notes.trim()
    if (decision !== 'approve' && !message) {
      setNoteError('Vui lòng nhập phản hồi cho giảng viên.')
      return
    }
    if (message.length > 2000) {
      setNoteError('Phản hồi tối đa 2000 ký tự.')
      return
    }
    pending.current = true
    setBusy(true)
    setDecisionError('')
    try {
      const result = await service.decide(paper.id, decision, message)
      toast.success(
        decision === 'approve'
          ? 'Đã duyệt bộ đề.'
          : decision === 'reject'
            ? 'Đã từ chối bộ đề.'
            : 'Đã gửi yêu cầu chỉnh sửa.',
      )
      setAction(undefined)
      if (decision === 'approve' && result.bankCandidates?.length) {
        setApproved(true)
        if (result.similarity) setSimilarity(result.similarity)
        setPromotion(result.bankCandidates)
      } else {
        onReviewed(
          paper.id,
          decision === 'approve'
            ? 'OFFICIAL'
            : decision === 'reject'
              ? 'REJECTED'
              : 'NEEDS_REVISION',
          message,
        )
      }
    } catch (error) {
      setDecisionError(preparationError(error))
    } finally {
      pending.current = false
      setBusy(false)
    }
  }
  const close = () => {
    if (!pending.current) {
      if (approved) onReviewed(paper.id, 'OFFICIAL', '')
      else onClose()
    }
  }
  async function openBankCandidates() {
    if (pending.current) return
    pending.current = true
    setBusy(true)
    setDecisionError('')
    try {
      const candidates = await service.bankCandidates(paper.id)
      if (candidates.length) setPromotion(candidates)
      else toast.info('Không còn câu mới cần thêm vào ngân hàng.')
    } catch (failure) {
      setDecisionError(preparationError(failure))
    } finally {
      pending.current = false
      setBusy(false)
    }
  }
  return (
    <DialogScope name={paper.setCode}>
      <SideDrawer
        label={reviewer ? 'Xem và duyệt bộ đề' : 'Xem bộ đề'}
        size="2xl"
        onClose={close}
        bodyClassName="bg-white p-0 sm:p-0"
        header={
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0 flex-1">
              <p className="text-xs font-bold tracking-wider text-[#1C4D8D] uppercase">Xem bộ đề</p>
              <div className="mt-2 flex flex-wrap items-center gap-2">
                <h2 className="text-xl font-bold wrap-anywhere text-[#0F2854]">{paper.setCode}</h2>
                <ExamStatusBadge status={approved ? 'OFFICIAL' : paper.status} />
              </div>
              <p className="mt-2 text-sm font-semibold wrap-anywhere text-[#5b6b84]">{author}</p>
              <p className="mt-1 text-xs text-[#6f8198]">
                {questions.length} câu · Đã xem {Math.min(seen.size, questions.length)}/
                {questions.length} câu
              </p>
            </div>
            <div className="flex shrink-0 gap-1.5">
              <button
                type="button"
                className={navButton}
                aria-label="Bộ đề trước"
                disabled={!onPrevious || busy || approved}
                onClick={onPrevious}
              >
                <ArrowLeft className="size-4" />
              </button>
              <button
                type="button"
                className={navButton}
                aria-label="Bộ đề tiếp"
                disabled={!onNext || busy || approved}
                onClick={onNext}
              >
                <ArrowRight className="size-4" />
              </button>
              <button
                type="button"
                className={navButton}
                aria-label="Đóng bộ đề"
                disabled={busy}
                onClick={close}
              >
                <X className="size-4" />
              </button>
            </div>
          </div>
        }
        footer={
          <div className="shrink-0 border-t border-[#82aedb]/55 bg-white px-5 py-3">
            {decisionError && !action && (
              <div className="mb-3">
                <ExamLoadError message={decisionError} />
              </div>
            )}
            {canReview ? (
              <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between sm:gap-3">
                <p className="min-w-0 text-xs text-[#5b6b84] sm:flex-1">
                  Quyết định áp dụng cho toàn bộ bộ đề.
                </p>
                <div className="grid w-full shrink-0 grid-cols-3 items-center gap-2 sm:flex sm:w-auto sm:justify-end [&>button]:min-w-0 [&>button]:px-1 [&>button]:text-xs sm:[&>button]:px-4 sm:[&>button]:text-sm">
                  <button
                    type="button"
                    className={cn(
                      ds.outlineButton,
                      'border-[#c2415b] text-[#c2415b] hover:border-[#a83249] hover:bg-[#fff1f3]',
                    )}
                    disabled={busy}
                    onClick={() => {
                      setAction('reject')
                      setNotes('')
                      setNoteError('')
                      setDecisionError('')
                    }}
                  >
                    <X className="size-4" />
                    Từ chối
                  </button>
                  <button
                    type="button"
                    className={ds.outlineButton}
                    disabled={busy}
                    onClick={() => {
                      setAction('request-changes')
                      setNotes('')
                      setNoteError('')
                      setDecisionError('')
                    }}
                  >
                    <FilePenLine className="size-4" />
                    Yêu cầu sửa
                  </button>
                  <button
                    type="button"
                    className={ds.primaryButton}
                    disabled={busy || !questions.length || !similarity || Boolean(duplicateError)}
                    onClick={() => void decide('approve')}
                  >
                    {busy ? (
                      <LoaderCircle className="size-4 animate-spin" />
                    ) : (
                      <Check className="size-4" />
                    )}
                    Duyệt bộ đề
                  </button>
                </div>
              </div>
            ) : (
              <div className="flex flex-wrap items-center justify-between gap-3">
                <p className="text-sm font-semibold text-[#5b6b84]">
                  {readOnly
                    ? 'Chế độ xem bộ đề.'
                    : approved
                      ? 'Bộ đề đã duyệt.'
                      : `Bộ đề ${PAPER_LABELS[paper.status]?.toLocaleLowerCase('vi-VN') ?? paper.status}.`}
                </p>
                {reviewer &&
                  !readOnly &&
                  !approved &&
                  paper.assignmentId &&
                  paper.status === 'OFFICIAL' && (
                    <button
                      type="button"
                      disabled={busy}
                      className={ds.outlineButton}
                      onClick={() => void openBankCandidates()}
                    >
                      <BookOpenCheck className="size-4" />
                      Thêm câu vào ngân hàng
                    </button>
                  )}
              </div>
            )}
          </div>
        }
      >
        <div className="sticky top-0 z-10 border-b border-[#82aedb]/55 bg-white px-5 pt-3">
          <StatusTabs
            label="Các câu trong bộ đề"
            className="flex-nowrap [&>button]:min-w-0 [&>button]:flex-1 [&>button]:justify-center [&>button]:gap-1 [&>button]:px-1 [&>button]:text-xs sm:[&>button]:px-2 sm:[&>button]:text-sm"
            value={questionIndex}
            options={questions.map((q, index) => ({
              value: String(index),
              label: `Câu ${index + 1}${reviewer && similarity ? ` · ${similarity.questions.find((item) => item.questionId === q.questionId)?.percent ?? 0}%` : ''}`,
              dotClass:
                (similarity?.questions.find((item) => item.questionId === q.questionId)?.percent ??
                  0) >= 70
                  ? 'bg-[#d99a10]'
                  : undefined,
            }))}
            onChange={(value) => {
              setQuestionIndex(value)
              setSeen((current) => new Set([...current, value]))
            }}
          />
        </div>
        <div className="min-w-0 space-y-4 overflow-x-hidden p-5">
          {duplicateError ? (
            <ExamLoadError
              message={`Chưa đối chiếu được bộ đề: ${duplicateError}`}
              onRetry={() => setRetry((n) => n + 1)}
            />
          ) : reviewer && !similarity ? (
            <p role="status" className="text-xs text-[#5b6b84]">
              Đang đối chiếu bộ đề…
            </p>
          ) : null}
          {reviewer && similarity && (
            <PaperSimilarityPanel report={similarity} questionId={question?.questionId} />
          )}
          {paper.reviewNotes &&
            ['NEEDS_REVISION', 'REJECTED', 'OFFICIAL', 'HANDED_OVER'].includes(paper.status) && (
              <section className="rounded-xl border border-[#f3d19a] bg-[#fff8eb] p-3">
                <h3 className="text-sm font-bold text-[#92600c]">Phản hồi trưởng bộ môn</h3>
                <p className="mt-1 text-sm wrap-anywhere whitespace-pre-wrap text-[#5b6b84]">
                  {paper.reviewNotes}
                </p>
              </section>
            )}
          {question ? (
            <>
              <div>
                <div className="flex flex-wrap gap-2">
                  <span className={ds.infoChip}>
                    {clo?.cloCode ?? (question.cloId ? 'CLO' : 'Chưa gắn CLO')}
                  </span>
                  <QuestionDifficultyBadge difficulty={question.difficulty} />
                  <span className="inline-flex shrink-0 items-center rounded-lg bg-[#DCEAF5] px-2 py-1 text-xs font-bold text-[#1C4D8D]">
                    {question.bloomLevel}
                  </span>
                </div>
                <h3 className="mt-3 text-base font-bold wrap-anywhere text-[#0F2854]">
                  {question.title}
                </h3>
                {clo && (
                  <p className="mt-1 text-xs leading-relaxed wrap-anywhere text-[#5b6b84]">
                    {clo.description} · Bloom mục tiêu: {clo.bloomLevel}
                  </p>
                )}
              </div>
              <QuestionDetailContent
                question={{
                  content: question.content,
                  imageUrl: question.imageUrl,
                  imageAlt: question.imageDescription,
                  sampleAnswer: question.sampleAnswer,
                  rubric: {
                    name: question.rubricName ?? undefined,
                    totalMaxScore: question.totalMaxScore,
                    criteria: [...question.criteria]
                      .sort((a, b) => a.orderIndex - b.orderIndex)
                      .map((c, index) => ({
                        id: `${question.questionId}-${index}`,
                        criterionName: c.name,
                        description: c.description,
                        maxScore: c.maxScore,
                      })),
                  },
                }}
              />
            </>
          ) : (
            <p className="text-sm text-[#5b6b84]">Bộ đề chưa có câu hỏi.</p>
          )}
          <details
            onToggle={(event) => setHistoryOpen(event.currentTarget.open)}
            className="rounded-xl border border-[#82aedb]/55 p-3"
          >
            <summary className="cursor-pointer text-sm font-bold text-[#1C4D8D]">
              <History className="mr-1 inline size-4" />
              Lịch sử xử lý
            </summary>
            {historyOpen &&
              (historyError ? (
                <ExamLoadError
                  message={historyError}
                  onRetry={() => setHistoryRetry((n) => n + 1)}
                />
              ) : !history ? (
                <LoadingState />
              ) : history.length === 0 ? (
                <p className="mt-2 text-sm text-[#5b6b84]">Chưa có lịch sử.</p>
              ) : (
                <ol className="mt-3 divide-y divide-[#82aedb]/40">
                  {history.map((item) => (
                    <li key={item.id} className="py-2 text-sm">
                      <p className="font-bold text-[#0F2854]">
                        {PAPER_LABELS[item.action] ??
                          { CREATED: 'Tạo bản nháp', DRAFT_SAVED: 'Lưu bản nháp' }[item.action] ??
                          item.action}{' '}
                        · {preparationDate(item.createdAt)}
                      </p>
                      {item.notes && (
                        <p className="mt-1 wrap-anywhere whitespace-pre-wrap text-[#5b6b84]">
                          {item.notes}
                        </p>
                      )}
                    </li>
                  ))}
                </ol>
              ))}
          </details>
        </div>
      </SideDrawer>
      {promotion && (
        <PaperBankPromotionDialog
          paperId={paper.id}
          setCode={paper.setCode}
          candidates={promotion}
          onClose={() => {
            setPromotion(undefined)
            if (approved) onReviewed(paper.id, 'OFFICIAL', '')
          }}
        />
      )}
      {action && (
        <FormDialog
          title={action === 'reject' ? 'Từ chối bộ đề' : 'Yêu cầu chỉnh sửa'}
          eyebrow={paper.setCode}
          icon={action === 'reject' ? X : FilePenLine}
          size="sm"
          nested
          busy={busy}
          onClose={() => {
            if (!busy) setAction(undefined)
          }}
          footer={
            <SubmitDialogFooter
              form="paper-review-notes"
              label={action === 'reject' ? 'Xác nhận từ chối' : 'Gửi yêu cầu'}
              busyLabel="Đang gửi…"
              busy={busy}
              danger={action === 'reject'}
              onCancel={() => setAction(undefined)}
            />
          }
        >
          <form
            id="paper-review-notes"
            className="space-y-3"
            onSubmit={(event) => {
              event.preventDefault()
              void decide(action)
            }}
          >
            <p className="text-sm text-[#5b6b84]">Phản hồi áp dụng cho cả bộ đề {paper.setCode}.</p>
            <TextareaField
              label="Phản hồi cho giảng viên"
              placeholder="Nêu nội dung cần điều chỉnh hoặc lý do từ chối"
              value={notes}
              onChange={(event) => {
                setNotes(event.target.value)
                setNoteError('')
              }}
              maxLength={2000}
              rows={3}
              autoFocus
              error={noteError}
            />
            {decisionError && <ExamLoadError message={decisionError} />}
          </form>
        </FormDialog>
      )}
    </DialogScope>
  )
}
