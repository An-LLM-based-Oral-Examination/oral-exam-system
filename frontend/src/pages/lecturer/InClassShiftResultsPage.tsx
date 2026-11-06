import { useEffect, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import {
  AlertCircle,
  ArrowLeft,
  CheckCircle2,
  ClipboardList,
  LoaderCircle,
  Lock,
  Pencil,
  Save,
  Search,
  X,
} from 'lucide-react'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { inClassShiftService } from '@/services/inClassShift.service'
import type {
  InClassQuestionAnswer,
  InClassShift,
  InClassSubmission,
} from '@/types/inClassShift.types'

type SubmissionFilter = 'ALL' | 'PENDING' | 'AI' | 'OVERRIDE' | 'LOCKED'

const SUBMISSION_TABS: Array<{
  value: SubmissionFilter
  label: string
  countIdleClass: string
  dotClass: string
}> = [
  {
    value: 'ALL',
    label: 'Tất cả',
    countIdleClass: 'bg-[#e8eef6] text-[#1C4D8D]',
    dotClass: 'bg-[#1C4D8D]',
  },
  {
    value: 'PENDING',
    label: 'Chưa nộp',
    countIdleClass: 'bg-[#fff4df] text-[#8d6118]',
    dotClass: 'bg-[#d4a017]',
  },
  {
    value: 'AI',
    label: 'Đã chấm AI',
    countIdleClass: 'bg-[#e8f7f1] text-[#197158]',
    dotClass: 'bg-[#1a9a72]',
  },
  {
    value: 'OVERRIDE',
    label: 'Đã chỉnh',
    countIdleClass: 'bg-[#eef1f5] text-[#52647d]',
    dotClass: 'bg-[#7a8aa0]',
  },
  {
    value: 'LOCKED',
    label: 'Đã khóa',
    countIdleClass: 'bg-[#fceef4] text-[#963b66]',
    dotClass: 'bg-[#b04a78]',
  },
]

function getErrorMessage(error: unknown) {
  if (typeof error === 'object' && error !== null && 'detail' in error) {
    const detail = (error as { detail?: unknown }).detail
    if (typeof detail === 'string' && detail.trim()) return detail
  }
  if (error instanceof Error && error.message) return error.message
  return 'Không thể tải bảng điểm.'
}

function submissionLabel(status: InClassSubmission['status']) {
  switch (status) {
    case 'IN_PROGRESS':
    case 'JOINED':
      return 'Đang làm'
    case 'SUBMITTED':
      return 'Đã nộp'
    case 'GRADED':
      return 'Đã chấm'
    case 'FINALIZED':
      return 'Đã khóa'
    default:
      return status
  }
}

function isInProgress(status: InClassSubmission['status']) {
  return status === 'JOINED' || status === 'IN_PROGRESS'
}

function answerDisplayScore(answer: InClassQuestionAnswer) {
  return answer.lecturerScore ?? answer.aiScore ?? null
}

export function InClassShiftResultsPage() {
  const { shiftId = '' } = useParams()
  const [shift, setShift] = useState<InClassShift | null>(null)
  const [submissions, setSubmissions] = useState<InClassSubmission[]>([])
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [filter, setFilter] = useState<SubmissionFilter>('ALL')
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [answerScore, setAnswerScore] = useState('')
  const [answerComment, setAnswerComment] = useState('')
  const [confirmFinalize, setConfirmFinalize] = useState(false)
  const [gradeDialogOpen, setGradeDialogOpen] = useState(false)
  const [activeAnswerIndex, setActiveAnswerIndex] = useState(0)

  const locked = shift?.status === 'FINALIZED'

  const load = async () => {
    if (!shiftId) {
      setError('Thiếu mã ca thi.')
      setLoading(false)
      return
    }
    setLoading(true)
    setError('')
    try {
      const [shiftResult, submissionResult] = await Promise.all([
        inClassShiftService.getShift(shiftId),
        inClassShiftService.listSubmissions(shiftId),
      ])
      setShift(shiftResult)
      setSubmissions(submissionResult)
      setSelectedId((current) => {
        if (current && submissionResult.some((item) => item.id === current)) return current
        const firstReady = submissionResult.find((item) => !isInProgress(item.status))
        return firstReady?.id ?? submissionResult[0]?.id ?? null
      })
    } catch (loadError) {
      setError(getErrorMessage(loadError))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void load()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [shiftId])

  const selected = useMemo(() => {
    const found = submissions.find((item) => item.id === selectedId) ?? null
    if (!found) return null
    return {
      ...found,
      answers: Array.isArray(found.answers) ? found.answers : [],
    }
  }, [submissions, selectedId])

  const answers = selected?.answers ?? []

  useEffect(() => {
    setGradeDialogOpen(false)
    if (!selected || isInProgress(selected.status)) {
      setAnswerScore('')
      setAnswerComment('')
      setActiveAnswerIndex(0)
      return
    }
    const list = selected.answers
    if (list.length === 0) {
      setActiveAnswerIndex(0)
      return
    }
    const firstAnswered = list.findIndex((item) => item.status !== 'PENDING')
    setActiveAnswerIndex(firstAnswered >= 0 ? firstAnswered : 0)
  }, [selected])

  const safeAnswerIndex =
    answers.length === 0 ? 0 : Math.min(Math.max(activeAnswerIndex, 0), answers.length - 1)
  const activeAnswer = answers[safeAnswerIndex] ?? null
  const viewingInProgress = selected ? isInProgress(selected.status) : false

  useEffect(() => {
    if (!activeAnswer || viewingInProgress) {
      setAnswerScore('')
      setAnswerComment('')
      return
    }
    const score = answerDisplayScore(activeAnswer)
    setAnswerScore(score != null ? String(score) : '')
    setAnswerComment(activeAnswer.lecturerComment ?? '')
  }, [activeAnswer, viewingInProgress])

  const filtered = useMemo(() => {
    const query = search.trim().toLocaleLowerCase('vi-VN')
    return submissions.filter((item) => {
      if (filter === 'PENDING' && !['JOINED', 'IN_PROGRESS'].includes(item.status)) return false
      if (filter === 'AI' && (item.overridden || item.aiScore == null)) return false
      if (filter === 'OVERRIDE' && !item.overridden) return false
      if (filter === 'LOCKED' && item.status !== 'FINALIZED') return false
      if (!query) return true
      return (
        item.studentCode.toLocaleLowerCase('vi-VN').includes(query) ||
        item.studentName.toLocaleLowerCase('vi-VN').includes(query)
      )
    })
  }, [submissions, filter, search])

  const stats = useMemo(() => {
    const ready = submissions.filter((item) => !isInProgress(item.status))
    const graded = ready.filter((item) => item.finalScore != null)
    const avg =
      graded.length === 0
        ? null
        : graded.reduce((sum, item) => sum + (item.finalScore ?? 0), 0) / graded.length
    const inProgressCount = submissions.length - ready.length
    return {
      graded: graded.length,
      total: ready.length,
      avg,
      inProgressCount,
    }
  }, [submissions])

  const filterCounts = useMemo(() => {
    const counts: Record<SubmissionFilter, number> = {
      ALL: submissions.length,
      PENDING: 0,
      AI: 0,
      OVERRIDE: 0,
      LOCKED: 0,
    }
    for (const item of submissions) {
      if (['JOINED', 'IN_PROGRESS'].includes(item.status)) counts.PENDING += 1
      if (!item.overridden && item.aiScore != null) counts.AI += 1
      if (item.overridden) counts.OVERRIDE += 1
      if (item.status === 'FINALIZED') counts.LOCKED += 1
    }
    return counts
  }, [submissions])

  const prefillGradeForm = (answer: InClassQuestionAnswer) => {
    const score = answerDisplayScore(answer)
    setAnswerScore(score != null ? String(score) : '')
    setAnswerComment(answer.lecturerComment ?? '')
  }

  const openGradeDialog = (index: number) => {
    if (locked || viewingInProgress) return
    const answer = answers[index]
    if (!answer) return
    setActiveAnswerIndex(index)
    prefillGradeForm(answer)
    setError('')
    setGradeDialogOpen(true)
  }

  const closeGradeDialog = () => {
    if (busy) return
    setGradeDialogOpen(false)
  }

  const saveAnswerGrade = async () => {
    if (!shiftId || !selected || !activeAnswer || locked || busy || viewingInProgress) return
    const score = Number(answerScore)
    if (!Number.isFinite(score) || score < 0 || score > 10) {
      setError('Điểm từng câu phải trong khoảng 0–10.')
      return
    }
    const comment = answerComment.trim()
    if (!comment) {
      setError('Nhập nhận xét / lý do khi chỉnh điểm câu này.')
      return
    }
    setBusy(true)
    setError('')
    setNotice('')
    try {
      const updated = await inClassShiftService.gradeAnswer(
        shiftId,
        selected.id,
        activeAnswer.answerId,
        { lecturerScore: score, lecturerComment: comment },
      )
      setSubmissions((current) =>
        current.map((item) => (item.id === updated.id ? updated : item)),
      )
      setNotice(
        `Đã lưu điểm câu ${activeAnswer.orderIndex} cho ${updated.studentCode} (${score.toFixed(1)}).`,
      )
      setGradeDialogOpen(false)
    } catch (gradeError) {
      setError(getErrorMessage(gradeError))
    } finally {
      setBusy(false)
    }
  }

  const finalize = async () => {
    if (!shiftId || locked || busy) return
    setBusy(true)
    setError('')
    setNotice('')
    try {
      const result = await inClassShiftService.finalizeGrades(shiftId)
      setShift((current) =>
        current
          ? { ...current, status: 'FINALIZED', finalizedAt: result.finalizedAt }
          : current,
      )
      setSubmissions((current) =>
        current.map((item) => ({
          ...item,
          status: 'FINALIZED',
          finalScore: item.finalScore ?? item.aiScore,
        })),
      )
      setConfirmFinalize(false)
      setNotice(result.message)
    } catch (finalizeError) {
      setError(getErrorMessage(finalizeError))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div lang="vi" className={cn(ds.page, 'pb-28')}>
      <div className="space-y-3">
        <section className={ds.banner}>
          <div className="pointer-events-none absolute inset-0 [background-image:radial-gradient(rgba(255,255,255,0.3)_0.8px,transparent_0.8px)] [background-size:22px_22px] opacity-35" />
          <div className="relative z-10 min-w-0 max-w-2xl p-6 sm:p-8">
            <p className="text-xs font-bold tracking-[0.1em] text-[#BDE8F5] uppercase">
              MF-05 · Bảng điểm thành phần
            </p>
            <h1 className="mt-2 text-2xl font-bold tracking-[-0.03em] text-white sm:text-3xl">
              {shift?.title ?? 'Ca thi trên lớp'}
            </h1>
            <p className="mt-2 text-sm text-white/80">
              {shift
                ? `${shift.className} · ${shift.courseCode} · ${shift.durationMinutes} phút · ${shift.questionCount} câu`
                : 'Đang tải thông tin ca...'}
            </p>
            {locked && (
              <p className="mt-3 inline-flex items-center gap-2 rounded-full border border-white/25 bg-white/12 px-3 py-1.5 text-xs font-bold text-white">
                <Lock className="size-3.5" />
                Đã chốt One-Way Lock
                {shift?.finalizedAt
                  ? ` · ${new Date(shift.finalizedAt).toLocaleString('vi-VN')}`
                  : ''}
              </p>
            )}
          </div>
        </section>

        <Link
          to="/in-class-shifts"
          className="inline-flex min-h-8 w-fit items-center gap-1.5 rounded-lg text-base font-bold text-[#1C4D8D] transition hover:text-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]"
        >
          <ArrowLeft className="size-5" aria-hidden="true" />
          Quay lại danh sách ca
        </Link>

        {(error || notice) && (
          <div
            className={cn(
              'flex items-center justify-between gap-3 rounded-2xl border px-4 py-3 text-sm',
              error
                ? 'border-[#f3c8cf] bg-[#fff5f6] text-[#b33d52]'
                : 'border-[#bcebdc] bg-[#effbf6] text-[#16785f]',
            )}
            role={error ? 'alert' : 'status'}
          >
            <span className="flex items-center gap-2 font-medium">
              {error ? (
                <AlertCircle className="size-4 shrink-0" />
              ) : (
                <CheckCircle2 className="size-4 shrink-0" />
              )}
              {error || notice}
            </span>
            <button
              type="button"
              onClick={() => {
                setError('')
                setNotice('')
              }}
              className="text-xs font-bold underline-offset-2 hover:underline"
            >
              Đóng
            </button>
          </div>
        )}

        <section className={cn(ds.section, 'overflow-hidden')}>
        <div className={ds.sectionHeader}>
          <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
            <div className={ds.sectionTitleRow}>
              <span className={ds.sectionIconWrap}>
                <ClipboardList className={ds.sectionIcon} />
              </span>
              <div>
                <h2 className={ds.sectionTitle}>Bài làm sinh viên</h2>
                <p className={ds.sectionHint}>
                  Đã có điểm {stats.graded}/{stats.total}
                  {stats.avg != null ? ` · TB ${stats.avg.toFixed(1)}` : ''}
                </p>
              </div>
            </div>
            <div className="relative min-w-[200px] sm:max-w-xs sm:flex-1">
              <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-[#6485b2]" />
              <input
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="MSSV hoặc họ tên..."
                className="h-10 w-full rounded-full border border-[#4988C4]/45 bg-white pr-3 pl-9 text-sm font-semibold outline-none focus:ring-2 focus:ring-[#4988C4]/30"
              />
            </div>
          </div>
        </div>

        <div className="bg-white">
          <div className="px-4 pt-3 sm:px-6">
            <div
              className="flex flex-wrap items-end gap-0 border-b-2 border-[#1C4D8D]"
              role="tablist"
              aria-label="Lọc trạng thái bài làm"
            >
              {SUBMISSION_TABS.map((tab) => {
                const count = filterCounts[tab.value]
                const active = filter === tab.value
                return (
                  <button
                    key={tab.value}
                    type="button"
                    role="tab"
                    aria-selected={active}
                    onClick={() => setFilter(tab.value)}
                    className={cn(
                      'relative inline-flex items-center gap-1.5 border px-3.5 py-2.5 text-[14px] font-bold whitespace-nowrap transition sm:px-4',
                      active
                        ? '-mb-0.5 rounded-t-xl border-[#1C4D8D] border-b-white bg-[linear-gradient(165deg,#BDE8F5_0%,#EAF6FB_42%,#ffffff_100%)] text-[#1C4D8D]'
                        : 'mb-0 border-transparent text-[#6f7f93] hover:bg-[#F4FBFD] hover:text-[#1C4D8D]',
                    )}
                  >
                    <span
                      className={cn(
                        'size-1.5 rounded-full',
                        active ? 'bg-[#1C4D8D]' : tab.dotClass,
                      )}
                      aria-hidden="true"
                    />
                    <span>{tab.label}</span>
                    <span
                      className={cn(
                        'rounded-full px-1.5 py-0.5 text-[12px] font-bold tabular-nums',
                        active ? 'bg-[#1C4D8D]/12 text-[#1C4D8D]' : tab.countIdleClass,
                      )}
                    >
                      {count}
                    </span>
                  </button>
                )
              })}
            </div>
          </div>
        </div>

        <div className="grid min-h-[420px] lg:grid-cols-[minmax(0,1fr)_minmax(320px,420px)]">
          <div className="border-b border-[#4988C4]/35 lg:border-r lg:border-b-0">
            {loading ? (
              <div className="flex min-h-80 flex-col items-center justify-center gap-3 text-sm text-[#5b6b84]">
                <LoaderCircle className="size-7 animate-spin text-[#1C4D8D]" />
                Đang tải bài nộp...
              </div>
            ) : filtered.length === 0 ? (
              <div className="px-5 py-12 text-center text-sm text-[#5b6b84]">
                Không có bài nộp phù hợp bộ lọc.
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="min-w-full text-left text-sm">
                  <thead className="bg-[#F4FBFD] text-xs font-bold tracking-[0.04em] text-[#5b6b84] uppercase">
                    <tr>
                      <th className="px-4 py-3">MSSV</th>
                      <th className="px-4 py-3">Họ tên</th>
                      <th className="px-4 py-3">Trạng thái</th>
                      <th className="px-4 py-3 text-right">AI</th>
                      <th className="px-4 py-3 text-right">Cuối</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filtered.map((item) => {
                      const active = item.id === selectedId
                      const inProgress = isInProgress(item.status)
                      return (
                        <tr
                          key={item.id}
                          aria-selected={active}
                          className={cn(
                            'cursor-pointer border-t border-[#4988C4]/25 transition',
                            active
                              ? 'bg-[#D7F1F8] shadow-[inset_4px_0_0_0_#1C4D8D] ring-1 ring-inset ring-[#4988C4]/55'
                              : 'hover:bg-[#F4FBFD]',
                            inProgress && !active && 'opacity-80',
                          )}
                          onClick={() => setSelectedId(item.id)}
                        >
                          <td className="px-4 py-3 font-mono text-xs font-bold text-[#1C4D8D]">
                            {item.studentCode}
                          </td>
                          <td className="px-4 py-3 font-semibold text-[#0F2854]">
                            {item.studentName}
                            {item.overridden && !inProgress && (
                              <span className="ml-2 rounded-full bg-[#fff4e8] px-2 py-0.5 text-[10px] font-bold text-[#b86a1c]">
                                Đã sửa
                              </span>
                            )}
                          </td>
                          <td className="px-4 py-3 text-xs font-semibold text-[#5b6b84]">
                            {submissionLabel(item.status)}
                          </td>
                          <td className="px-4 py-3 text-right font-mono text-xs font-bold text-[#5b6b84]">
                            {inProgress || item.aiScore == null ? '—' : item.aiScore.toFixed(1)}
                          </td>
                          <td className="px-4 py-3 text-right font-mono text-sm font-black text-[#0F2854]">
                            {inProgress || item.finalScore == null
                              ? '—'
                              : item.finalScore.toFixed(1)}
                          </td>
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </div>

          <aside className="flex max-h-[70vh] flex-col overflow-y-auto bg-[#F8FBFD] p-4 sm:p-5 lg:max-h-none">
            {!selected ? (
              <p className="m-auto text-sm text-[#5b6b84]">Chọn một sinh viên để xem chi tiết.</p>
            ) : (
              <>
                <div>
                  <p className="text-xs font-bold tracking-[0.08em] text-[#1C4D8D] uppercase">
                    {selected.studentCode}
                  </p>
                  <h3 className="mt-1 text-lg font-bold text-[#0F2854]">{selected.studentName}</h3>
                  <p className="mt-1 text-xs font-semibold text-[#5b6b84]">
                    {submissionLabel(selected.status)}
                    {!viewingInProgress && selected.submittedAt
                      ? ` · nộp ${new Date(selected.submittedAt).toLocaleString('vi-VN')}`
                      : ''}
                  </p>
                </div>

                {viewingInProgress ? (
                  <div className="mt-5 rounded-2xl border border-[#ead5a7] bg-[#fff8e9] px-4 py-5 text-sm leading-relaxed text-[#8d6118]">
                    <p className="font-bold">Sinh viên đang làm bài</p>
                    <p className="mt-1.5 text-xs">
                      Chưa nộp xong nên chưa mở bảng điểm. Không hiển thị nội dung câu hỏi, câu trả
                      lời, nhận xét AI hay điểm cho đến khi sinh viên hoàn tất bài.
                    </p>
                  </div>
                ) : (
                  <>
                    <div className="mt-3 grid grid-cols-2 gap-2">
                      <div className="rounded-xl border border-[#4988C4]/40 bg-white px-3 py-2.5">
                        <p className="text-[11px] font-bold text-[#5b6b84] uppercase">
                          Điểm AI (cả bài)
                        </p>
                        <p className="mt-1 font-mono text-xl font-black text-[#1C4D8D]">
                          {selected.aiScore != null ? selected.aiScore.toFixed(1) : '—'}
                        </p>
                      </div>
                      <div className="rounded-xl border border-[#4988C4]/40 bg-white px-3 py-2.5">
                        <p className="text-[11px] font-bold text-[#5b6b84] uppercase">Điểm cuối</p>
                        <p className="mt-1 font-mono text-xl font-black text-[#0F2854]">
                          {selected.finalScore != null ? selected.finalScore.toFixed(1) : '—'}
                        </p>
                      </div>
                    </div>

                    {answers.length === 0 ? (
                      <p className="mt-4 rounded-xl border border-dashed border-[#4988C4]/45 bg-white px-3 py-3 text-xs text-[#5b6b84]">
                        Chưa có danh sách câu hỏi cho bài nộp này.
                      </p>
                    ) : (
                      <div className="mt-4 space-y-3">
                        <section className="overflow-hidden rounded-2xl border border-[#4988C4]/40 bg-white">
                          <div className="flex items-center justify-between gap-2 border-b border-[#4988C4]/30 bg-[#F4FBFD] px-3.5 py-2.5">
                            <p className="text-xs font-bold tracking-[0.06em] text-[#5b6b84] uppercase">
                              Điểm theo câu · {answers.length} câu
                            </p>
                            <p className="font-mono text-xs font-bold text-[#1C4D8D]">
                              TB{' '}
                              {selected.finalScore != null
                                ? selected.finalScore.toFixed(1)
                                : selected.aiScore != null
                                  ? selected.aiScore.toFixed(1)
                                  : '—'}
                            </p>
                          </div>
                          <ul className="divide-y divide-[#4988C4]/25">
                            {answers.map((answer, index) => {
                              const active = index === safeAnswerIndex
                              const finalScore = answerDisplayScore(answer)
                              const edited = answer.lecturerScore != null
                              return (
                                <li key={answer.answerId}>
                                  <div
                                    className={cn(
                                      'flex items-start gap-1 px-2 py-2 transition',
                                      active
                                        ? 'bg-[#E8F4FA] ring-inset ring-[#1C4D8D]/25 ring-1'
                                        : 'hover:bg-[#F8FBFD]',
                                    )}
                                  >
                                    <button
                                      type="button"
                                      onClick={() => setActiveAnswerIndex(index)}
                                      className="flex min-w-0 flex-1 items-start gap-3 px-1.5 py-1 text-left"
                                    >
                                      <span
                                        className={cn(
                                          'mt-0.5 flex size-7 shrink-0 items-center justify-center rounded-lg text-xs font-black',
                                          active
                                            ? 'bg-[#1C4D8D] text-white'
                                            : 'bg-[#4988C4]/16 text-[#1C4D8D]',
                                        )}
                                      >
                                        {answer.orderIndex}
                                      </span>
                                      <span className="min-w-0 flex-1">
                                        <span className="flex flex-wrap items-center gap-1.5">
                                          <span className="truncate text-sm font-bold text-[#0F2854]">
                                            {answer.title}
                                          </span>
                                          {answer.isFollowUp && (
                                            <span className="rounded-full bg-[#F4FBFD] px-1.5 py-0.5 text-[10px] font-bold text-[#1C4D8D]">
                                              Phụ
                                            </span>
                                          )}
                                          {edited && (
                                            <span className="rounded-full bg-[#fff4e8] px-1.5 py-0.5 text-[10px] font-bold text-[#b86a1c]">
                                              GV sửa
                                            </span>
                                          )}
                                        </span>
                                        <span className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-[11px] font-semibold text-[#5b6b84]">
                                          <span>
                                            AI{' '}
                                            <span className="font-mono text-[#1C4D8D]">
                                              {answer.aiScore != null
                                                ? answer.aiScore.toFixed(1)
                                                : '—'}
                                            </span>
                                          </span>
                                          <span>
                                            GV{' '}
                                            <span className="font-mono text-[#0F2854]">
                                              {answer.lecturerScore != null
                                                ? answer.lecturerScore.toFixed(1)
                                                : '—'}
                                            </span>
                                          </span>
                                        </span>
                                      </span>
                                      <span className="shrink-0 text-right">
                                        <span className="block text-[10px] font-bold tracking-[0.04em] text-[#8aa0b8] uppercase">
                                          Cuối
                                        </span>
                                        <span className="font-mono text-base font-black text-[#0F2854]">
                                          {finalScore != null ? finalScore.toFixed(1) : '—'}
                                        </span>
                                      </span>
                                    </button>
                                    {!locked && (
                                      <button
                                        type="button"
                                        aria-label={`Chỉnh điểm câu ${answer.orderIndex}`}
                                        title="Chỉnh điểm"
                                        onClick={() => openGradeDialog(index)}
                                        className="mt-0.5 flex size-8 shrink-0 items-center justify-center rounded-lg border border-[#4988C4]/40 text-[#1C4D8D] transition hover:bg-white"
                                      >
                                        <Pencil className="size-3.5" />
                                      </button>
                                    )}
                                  </div>
                                </li>
                              )
                            })}
                          </ul>
                        </section>

                        {activeAnswer && (
                          <div className="space-y-3 rounded-2xl border border-[#4988C4]/40 bg-white p-3.5">
                            <div className="flex items-start justify-between gap-2">
                              <div className="min-w-0">
                                <p className="text-[11px] font-bold tracking-[0.06em] text-[#5b6b84] uppercase">
                                  Chi tiết câu {activeAnswer.orderIndex}
                                  {activeAnswer.isFollowUp ? ' · Follow-up' : ''}
                                </p>
                                <h4 className="mt-1 text-sm font-bold text-[#0F2854]">
                                  {activeAnswer.title}
                                </h4>
                              </div>
                              {!locked && (
                                <button
                                  type="button"
                                  onClick={() => openGradeDialog(safeAnswerIndex)}
                                  className="inline-flex shrink-0 items-center gap-1.5 rounded-xl border border-[#4988C4]/50 bg-[#F4FBFD] px-2.5 py-1.5 text-xs font-bold text-[#1C4D8D] transition hover:bg-[#E8F4FA]"
                                >
                                  <Pencil className="size-3.5" />
                                  Chỉnh điểm
                                </button>
                              )}
                            </div>
                            <p className="text-sm leading-relaxed text-[#415774]">
                              {activeAnswer.content?.trim() || 'Không có nội dung đề bài.'}
                            </p>

                            <div className="rounded-xl bg-[#F8FBFD] px-3 py-2.5">
                              <p className="text-[11px] font-bold tracking-[0.06em] text-[#5b6b84] uppercase">
                                Câu trả lời (transcript)
                              </p>
                              <p className="mt-1.5 max-h-36 overflow-y-auto text-sm leading-relaxed text-[#0F2854]">
                                {activeAnswer.transcript?.trim() || 'Chưa có transcript.'}
                              </p>
                            </div>

                            <div className="rounded-xl border border-[#bcebdc] bg-[#effbf6] px-3 py-2.5">
                              <div className="flex items-center justify-between gap-2">
                                <p className="text-[11px] font-bold tracking-[0.06em] text-[#176f58] uppercase">
                                  Nhận xét AI
                                </p>
                                <span className="font-mono text-sm font-black text-[#1C4D8D]">
                                  {activeAnswer.aiScore != null
                                    ? `${activeAnswer.aiScore.toFixed(1)} / 10`
                                    : '—'}
                                </span>
                              </div>
                              <p className="mt-1.5 text-sm leading-relaxed text-[#176f58]">
                                {activeAnswer.aiFeedback?.trim() || 'AI chưa trả nhận xét.'}
                              </p>
                            </div>

                            {activeAnswer.lecturerComment?.trim() ? (
                              <div className="rounded-xl border border-[#f0d7b0] bg-[#fff8eb] px-3 py-2.5">
                                <div className="flex items-center justify-between gap-2">
                                  <p className="text-[11px] font-bold tracking-[0.06em] text-[#9a6b12] uppercase">
                                    Nhận xét GV
                                  </p>
                                  <span className="font-mono text-sm font-black text-[#0F2854]">
                                    {activeAnswer.lecturerScore != null
                                      ? `${activeAnswer.lecturerScore.toFixed(1)} / 10`
                                      : '—'}
                                  </span>
                                </div>
                                <p className="mt-1.5 text-sm leading-relaxed text-[#8d6118]">
                                  {activeAnswer.lecturerComment}
                                </p>
                              </div>
                            ) : null}

                            {locked && (
                              <p className="rounded-xl border border-[#c5d0e0] bg-[#f3f6fa] px-3 py-2.5 text-xs text-[#5b6b84]">
                                Ca đã chốt. Điểm GV câu này:{' '}
                                <strong className="font-mono text-[#0F2854]">
                                  {activeAnswer.lecturerScore != null
                                    ? activeAnswer.lecturerScore.toFixed(1)
                                    : 'giữ điểm AI'}
                                </strong>
                              </p>
                            )}
                          </div>
                        )}
                      </div>
                    )}
                  </>
                )}
              </>
            )}
          </aside>
        </div>
      </section>
      </div>

      {!locked && (
        <footer className="fixed right-0 bottom-0 left-0 z-40 border-t border-[#4988C4]/45 bg-white/95 px-4 py-3 backdrop-blur-sm md:left-[220px] xl:left-[240px]">
          <div className="mx-auto flex max-w-[1380px] flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <p className="text-sm text-[#5b6b84]">
              Đã có điểm <strong className="text-[#0F2854]">{stats.graded}/{stats.total}</strong>
              {stats.inProgressCount > 0 && (
                <> · {stats.inProgressCount} SV đang làm (chưa mở điểm)</>
              )}
              {stats.avg != null && (
                <>
                  {' '}
                  · TB{' '}
                  <strong className="font-mono text-[#0F2854]">{stats.avg.toFixed(1)}</strong>
                </>
              )}
            </p>
            <button
              type="button"
              disabled={busy || stats.total === 0}
              onClick={() => setConfirmFinalize(true)}
              className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[#16836a] px-4 text-sm font-bold text-white shadow-[0_8px_18px_rgba(22,131,106,0.22)] transition hover:bg-[#136f5a] disabled:opacity-50"
            >
              <Lock className="size-4" />
              Chốt bảng điểm thành phần
            </button>
          </div>
        </footer>
      )}

      {gradeDialogOpen && activeAnswer && !locked && !viewingInProgress && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-[#0F2854]/50 p-4 backdrop-blur-[2px]"
          role="presentation"
          onClick={(event) => {
            if (event.target === event.currentTarget) closeGradeDialog()
          }}
        >
          <div
            role="dialog"
            aria-modal="true"
            aria-labelledby="grade-answer-dialog-title"
            className="w-full max-w-md rounded-2xl border border-[#4988C4]/45 bg-white p-5 shadow-[0_24px_60px_rgba(15,40,84,0.28)]"
          >
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0">
                <p className="text-xs font-bold tracking-[0.08em] text-[#1C4D8D] uppercase">
                  Chỉnh điểm
                </p>
                <h3
                  id="grade-answer-dialog-title"
                  className="mt-1 text-lg font-bold text-[#0F2854]"
                >
                  Câu {activeAnswer.orderIndex}
                  {activeAnswer.isFollowUp ? ' · Follow-up' : ''}
                </h3>
                <p className="mt-1 truncate text-sm text-[#5b6b84]">{activeAnswer.title}</p>
              </div>
              <button
                type="button"
                aria-label="Đóng"
                disabled={busy}
                onClick={closeGradeDialog}
                className="flex size-8 shrink-0 items-center justify-center rounded-lg border border-[#4988C4]/40 text-[#5b6b84] disabled:opacity-50"
              >
                <X className="size-4" />
              </button>
            </div>

            <div className="mt-4 grid grid-cols-2 gap-2">
              <div className="rounded-xl border border-[#4988C4]/35 bg-[#F8FBFD] px-3 py-2.5">
                <p className="text-[11px] font-bold text-[#5b6b84] uppercase">Điểm AI</p>
                <p className="mt-1 font-mono text-lg font-black text-[#1C4D8D]">
                  {activeAnswer.aiScore != null ? activeAnswer.aiScore.toFixed(1) : '—'}
                </p>
              </div>
              <div className="rounded-xl border border-[#4988C4]/35 bg-[#F8FBFD] px-3 py-2.5">
                <p className="text-[11px] font-bold text-[#5b6b84] uppercase">Điểm GV hiện tại</p>
                <p className="mt-1 font-mono text-lg font-black text-[#0F2854]">
                  {activeAnswer.lecturerScore != null
                    ? activeAnswer.lecturerScore.toFixed(1)
                    : '—'}
                </p>
              </div>
            </div>

            <div className="mt-4 space-y-3">
              <label className="block">
                <span className="text-xs font-semibold text-[#5b6b84]">Điểm giảng viên</span>
                <input
                  type="number"
                  min={0}
                  max={10}
                  step={0.1}
                  value={answerScore}
                  onChange={(event) => setAnswerScore(event.target.value)}
                  className={cn(ds.field, 'mt-1')}
                  autoFocus
                />
              </label>
              <label className="block">
                <span className="text-xs font-semibold text-[#5b6b84]">
                  Nhận xét / lý do chỉnh
                </span>
                <textarea
                  value={answerComment}
                  onChange={(event) => setAnswerComment(event.target.value)}
                  rows={3}
                  placeholder="Bắt buộc khi lưu điểm câu..."
                  className="mt-1 min-h-20 w-full resize-y rounded-xl border border-[#4988C4] bg-white px-3 py-2.5 text-sm outline-none focus:ring-2 focus:ring-[#4988C4]/30"
                />
              </label>
            </div>

            <div className="mt-5 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
              <button
                type="button"
                disabled={busy}
                onClick={closeGradeDialog}
                className={ds.outlineButton}
              >
                Hủy
              </button>
              <button
                type="button"
                disabled={busy}
                onClick={() => void saveAnswerGrade()}
                className={ds.primaryButton}
              >
                {busy ? (
                  <LoaderCircle className="size-4 animate-spin" />
                ) : (
                  <Save className="size-4" />
                )}
                Lưu điểm câu {activeAnswer.orderIndex}
              </button>
            </div>
          </div>
        </div>
      )}

      {confirmFinalize && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-[#0F2854]/50 p-4 backdrop-blur-[2px]"
          role="presentation"
          onClick={(event) => {
            if (event.target === event.currentTarget) setConfirmFinalize(false)
          }}
        >
          <div
            role="dialog"
            aria-modal="true"
            aria-label="Xác nhận chốt điểm"
            className="w-full max-w-md rounded-2xl border border-[#4988C4]/45 bg-white p-5 shadow-[0_24px_60px_rgba(15,40,84,0.28)]"
          >
            <div className="flex items-start justify-between gap-3">
              <div>
                <h3 className="text-lg font-bold text-[#0F2854]">Chốt One-Way Lock?</h3>
                <p className="mt-2 text-sm leading-relaxed text-[#5b6b84]">
                  Sau khi chốt, bảng điểm thành phần của ca này không thể chỉnh sửa. Hành động
                  không hoàn tác.
                </p>
              </div>
              <button
                type="button"
                aria-label="Đóng"
                onClick={() => setConfirmFinalize(false)}
                className="flex size-8 items-center justify-center rounded-lg border border-[#4988C4]/40 text-[#5b6b84]"
              >
                <X className="size-4" />
              </button>
            </div>
            <div className="mt-5 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
              <button
                type="button"
                onClick={() => setConfirmFinalize(false)}
                className={ds.outlineButton}
              >
                Hủy
              </button>
              <button
                type="button"
                disabled={busy}
                onClick={() => void finalize()}
                className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[#16836a] px-4 text-sm font-bold text-white hover:bg-[#136f5a] disabled:opacity-50"
              >
                {busy ? (
                  <LoaderCircle className="size-4 animate-spin" />
                ) : (
                  <Lock className="size-4" />
                )}
                Xác nhận chốt
              </button>
            </div>
          </div>
        </div>
      )}

    </div>
  )
}
