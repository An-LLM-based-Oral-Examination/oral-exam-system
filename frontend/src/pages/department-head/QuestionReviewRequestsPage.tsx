import { LoadingState } from '@/components/common/LoadingState'
import { SearchField } from '@/components/common/SearchField'
import { useSearchInput } from '@/hooks/useSearchInput'
import { StatusTabs } from '@/components/common/StatusTabs'
import { SubmitDialogFooter } from '@/components/common/SubmitDialogFooter'
import { TextareaField } from '@/components/common/TextareaField'
import { QuestionRowMetrics } from '@/components/question/QuestionRowMetrics'
import { QuestionDrawer } from '@/components/question/QuestionDrawer'
import { DividedList } from '@/components/common/DividedList'
import { ProgressBar } from '@/components/common/ProgressBar'
import { BackLink } from '@/components/common/BackLink'
import { QuestionManagementTabs } from './QuestionManagementTabs'
import { useEffect, useState } from 'react'
import {
  AlertCircle,
  BookOpenCheck,
  Check,
  CheckCheck,
  Eye,
  FilePenLine,
  RefreshCw,
  Send,
  X,
} from 'lucide-react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { EmptyState } from '@/components/common/EmptyState'
import { FilterDropMenu } from '@/components/common/FilterDropMenu'
import { ListPagination } from '@/components/common/ListPagination'
import { toast } from '@/components/common/toastStore'
import { QuestionDetailContent } from '@/components/question/QuestionDetailContent'
import { FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { userMessage } from '@/lib/userMessage'
import { scheduleUi } from '@/lib/scheduleUi'
import { courseCloService } from '@/services/courseClo.service'
import {
  questionReviewRequestService as service,
  type ReviewRequestPage,
  type ReviewRequestDetail,
  type ReviewRequestItem,
  type ReviewDecision,
} from '@/services/questionReviewRequest.service'
import { ROUTE_PATHS } from '@/routes/routePaths'

const statuses = [
  { value: '', label: 'Tất cả', dotClass: 'bg-[#4988C4]' },
  { value: 'PENDING', label: 'Chưa xử lý', dotClass: 'bg-[#d99a10]' },
  { value: 'IN_PROGRESS', label: 'Đang xử lý', dotClass: 'bg-[#4988C4]' },
  { value: 'COMPLETED', label: 'Đã xử lý', dotClass: 'bg-[#16836a]' },
]
const decisionLabels = {
  PENDING: 'Chờ duyệt',
  APPROVED: 'Đã duyệt',
  NEEDS_REVISION: 'Cần chỉnh sửa',
  REJECTED: 'Từ chối',
}
const difficultyLabels: Record<string, string> = {
  easy: 'Dễ',
  medium: 'Trung bình',
  hard: 'Khó',
  progressive: 'Tăng dần',
}
const dateFormatter = new Intl.DateTimeFormat('vi-VN', {
  dateStyle: 'short',
  timeStyle: 'short',
  timeZone: 'Asia/Ho_Chi_Minh',
})
function dateLabel(value: string | null) {
  if (!value) return 'Dữ liệu chờ duyệt trước đây'
  const date = new Date(value)
  return Number.isFinite(date.getTime()) ? dateFormatter.format(date) : '—'
}
function DecisionChip({ value }: { value: keyof typeof decisionLabels }) {
  return (
    <span
      className={cn(
        'inline-flex items-center rounded-full border border-transparent px-2.5 py-1 text-xs font-bold whitespace-nowrap normal-case',
        value === 'APPROVED'
          ? 'bg-[#eaf9f4] text-[#17745b]'
          : value === 'REJECTED'
            ? 'bg-[#fff0f1] text-[#c2415b]'
            : value === 'NEEDS_REVISION'
              ? 'bg-[#fff4e8] text-[#c26a12]'
              : 'border-[#ead5a7] bg-[#fff8e9] text-[#8d6118]',
      )}
    >
      {decisionLabels[value]}
    </span>
  )
}

export default function QuestionReviewRequestsPage() {
  const { requestId } = useParams()
  const [params, setParams] = useSearchParams()
  const status = params.get('status') ?? 'PENDING'
  const courseId = params.get('course') ?? ''
  const page = Math.max(1, Number(params.get('page')) || 1)
  const changeFilter = (key: string, value: string) => {
    const next = new URLSearchParams(params)
    next.set(key, value)
    next.set('page', '1')
    setParams(next, { replace: true })
  }

  const search = useSearchInput(
    (value) => changeFilter('keyword', value),
    params.get('keyword') ?? '',
  )
  const [courses, setCourses] = useState<Array<{ value: string; label: string }>>([])
  const [data, setData] = useState<ReviewRequestPage>()
  const [detail, setDetail] = useState<ReviewRequestDetail>()
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [endpointUnavailable, setEndpointUnavailable] = useState(false)
  const [revision, setRevision] = useState(0)
  const [selected, setSelected] = useState<string[]>([])
  const [questionFilter, setQuestionFilter] = useState<ReviewRequestItem['decision'] | 'ALL'>('ALL')
  const [viewing, setViewing] = useState<ReviewRequestItem>()
  const [action, setAction] = useState<{ ids: string[]; decision: ReviewDecision }>()
  const [notes, setNotes] = useState('')
  const [noteError, setNoteError] = useState('')
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    let active = true
    void courseCloService
      .getCurrentSemesterManagedCourses()
      .then((rows) => {
        if (active)
          setCourses(
            rows.map((course) => ({ value: course.id, label: `${course.code} – ${course.name}` })),
          )
      })
      .catch(() => {})
    return () => {
      active = false
    }
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    const load = async () => {
      setLoading(true)
      setError('')
      setEndpointUnavailable(false)
      setSelected([])
      try {
        if (requestId) {
          const result = await service.detail(requestId, controller.signal)
          if (!controller.signal.aborted) setDetail(result)
        } else {
          const result = await service.list(
            { courseId: courseId || undefined, status, keyword: search.query || undefined, page },
            controller.signal,
          )
          if (!controller.signal.aborted) setData(result)
        }
      } catch (failure) {
        if (!controller.signal.aborted) {
          setEndpointUnavailable(
            !requestId &&
              typeof failure === 'object' &&
              failure !== null &&
              'status' in failure &&
              failure.status === 404,
          )
          setError(userMessage(failure, 'Không tải được yêu cầu gửi duyệt.'))
        }
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }
    const cancel = scheduleUi(() => {
      void load()
    })
    return () => {
      cancel()
      controller.abort()
    }
  }, [requestId, courseId, status, search.query, page, revision])

  const beginDecision = (ids: string[], decision: ReviewDecision) => {
    setNotes('')
    setNoteError('')
    setAction({ ids, decision })
  }
  const confirmDecision = async () => {
    if (!action || !detail || saving) return
    if (action.decision !== 'APPROVED' && notes.trim().length < 10) {
      setNoteError('Nhập nhận xét từ 10 ký tự để hướng dẫn giảng viên.')
      return
    }
    setSaving(true)
    setNoteError('')
    try {
      await service.decide(detail.summary.id, {
        itemIds: action.ids,
        decision: action.decision,
        notes: notes.trim() || undefined,
        version: detail.summary.version,
      })
      toast.success('Đã lưu quyết định thẩm định.')
      setAction(undefined)
      setViewing(undefined)
      setRevision((value) => value + 1)
    } catch (failure) {
      setNoteError(userMessage(failure, 'Không lưu được quyết định.'))
    } finally {
      setSaving(false)
    }
  }
  const questions = detail?.items ?? []
  const visibleQuestions = questions.filter(
    (item) => questionFilter === 'ALL' || item.decision === questionFilter,
  )
  const questionTabs = [
    { value: 'ALL', label: 'Tất cả', dot: 'bg-[#1C4D8D]' },
    { value: 'PENDING', label: 'Chờ duyệt', dot: 'bg-[#d99a10]' },
    { value: 'NEEDS_REVISION', label: 'Cần chỉnh sửa', dot: 'bg-[#c2415b]' },
    { value: 'APPROVED', label: 'Đã duyệt', dot: 'bg-[#16836a]' },
    { value: 'REJECTED', label: 'Từ chối', dot: 'bg-[#a84070]' },
  ] as const
  const pending = visibleQuestions.filter((item) => item.decision === 'PENDING' && item.question)
  const allSelected = pending.length > 0 && pending.every((item) => selected.includes(item.id))
  const viewableItems = visibleQuestions.filter((item) => item.question)
  const viewingIndex = viewableItems.findIndex((item) => item.id === viewing?.id)
  const summary = detail?.summary
  const filterQuery = params.size ? `?${params.toString()}` : ''

  return (
    <div lang="vi" className={cn(ds.page, requestId && '[&>a:first-child]:mb-3')}>
      {requestId && (
        <BackLink to={`${ROUTE_PATHS.departmentHead.questionReview}${filterQuery}`}>
          Về danh sách
        </BackLink>
      )}
      {requestId && summary && !loading && !error && (
        <section className={ds.section}>
          <div className={cn(ds.sectionHeader, 'space-y-3 border-b-0')}>
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div className="min-w-0">
                <div className={ds.sectionTitleRow}>
                  <span className={ds.sectionIconWrap}>
                    <BookOpenCheck className={ds.sectionIcon} aria-hidden="true" />
                  </span>
                  <h1 className={cn(ds.sectionTitle, 'min-w-0 wrap-anywhere')}>
                    Yêu cầu của {summary.lecturerName}
                  </h1>
                </div>
                <p className={cn(ds.sectionHint, 'wrap-anywhere')}>
                  <span className="font-bold text-[#1C4D8D]">{summary.courseCode}</span> ·{' '}
                  {summary.courseName}
                </p>
                <p className="mt-1 text-xs wrap-anywhere text-[#5b6b84]">{summary.lecturerEmail}</p>
              </div>
              <div className="rounded-xl border border-[#82aedb]/55 bg-white px-3 py-2">
                <p className="text-xs font-semibold text-[#5b6b84]">Thời gian gửi</p>
                <p className="text-sm font-bold text-[#0F2854]">{dateLabel(summary.submittedAt)}</p>
              </div>
            </div>
            {summary.notes && (
              <div className="rounded-xl border border-[#82aedb]/55 bg-white/80 px-3 py-2.5">
                <h2 className="text-xs font-bold text-[#5b6b84]">Ghi chú của giảng viên</h2>
                <p className="mt-1 text-sm leading-relaxed wrap-anywhere whitespace-pre-wrap text-[#24364f]">
                  {summary.notes}
                </p>
              </div>
            )}
            <div className="max-w-sm">
              <ProgressBar
                aria-label="Tiến độ duyệt yêu cầu"
                value={summary.total - summary.pending}
                max={Math.max(1, summary.total)}
                className="mb-2"
              />
              <div className="flex flex-wrap items-center justify-between gap-x-3 gap-y-1 text-sm">
                <span className="inline-flex items-center gap-1.5 text-xs font-semibold text-[#5b6b84]">
                  <span
                    className={cn(
                      'size-1.5 rounded-full',
                      statuses.find((status) => status.value === summary.status)?.dotClass,
                    )}
                    aria-hidden="true"
                  />
                  {statuses.find((status) => status.value === summary.status)?.label}
                </span>
                <span className="font-bold text-[#1C4D8D] tabular-nums">
                  {summary.total - summary.pending}/{summary.total} câu đã xử lý
                </span>
              </div>
            </div>
          </div>
        </section>
      )}
      <section className={ds.section}>
        <div className={cn(ds.sectionHeader, 'space-y-3')}>
          {!requestId && <QuestionManagementTabs revision={revision} />}
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <div className={ds.sectionTitleRow}>
                <span className={ds.sectionIconWrap}>
                  <BookOpenCheck className={ds.sectionIcon} />
                </span>
                {requestId ? (
                  <h2 className={ds.sectionTitle}>Danh sách câu hỏi</h2>
                ) : (
                  <h1 className={ds.sectionTitle}>Yêu cầu duyệt câu hỏi</h1>
                )}
                {requestId && summary && (
                  <span className={ds.sectionBadge}>{summary.total} câu hỏi</span>
                )}
                {!requestId && data && (
                  <span className={ds.sectionBadge}>{data.totalCount} yêu cầu</span>
                )}
              </div>
              <p className={ds.sectionHint}>
                {requestId && summary
                  ? 'Các câu hỏi trong yêu cầu duyệt'
                  : 'Thẩm định câu hỏi do giảng viên gửi theo từng yêu cầu'}
              </p>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              {!requestId && (
                <>
                  <FilterDropMenu
                    label="Môn học"
                    value={courseId}
                    options={[{ value: '', label: 'Tất cả môn' }, ...courses]}
                    onChange={(value) => changeFilter('course', value)}
                    className={ds.filterCourse}
                    menuMinWidth={ds.filterMenuMinWidth}
                    wrapOptions
                  />
                  <div className={cn('relative', ds.searchControl)}>
                    <SearchField
                      type="search"
                      aria-label="Tìm yêu cầu"
                      placeholder="Tên hoặc email giảng viên"
                      value={search.draft}
                      onChange={(event) => search.change(event.target.value)}
                      onKeyDown={search.onKeyDown}
                      className={cn(ds.field, 'h-10 min-h-10 rounded-full pl-9 shadow-[0_2px_8px_rgba(23,48,96,0.04)]')}
                    />
                  </div>
                </>
              )}
              <button
                type="button"
                className={cn(ds.outlineButton, 'size-10 shrink-0 justify-center p-0')}
                aria-label="Làm mới"
                title="Làm mới"
                disabled={loading || saving}
                onClick={() => setRevision((value) => value + 1)}
              >
                <RefreshCw className={cn('size-3.5', loading && 'animate-spin')} aria-hidden="true" />
              </button>
            </div>
          </div>
        </div>
        {!requestId && (
          <div className="bg-white px-4 pt-3 sm:px-6">
            <StatusTabs
              label="Tiến độ xử lý"
              value={status}
              options={statuses}
              onChange={(value) => changeFilter('status', value)}
            />
          </div>
        )}
        <div
          className={cn(
            ds.sectionBody,
            !loading &&
              !error &&
              Boolean(requestId ? detail?.items.length : data?.items.length) &&
              'p-0 sm:p-0',
          )}
        >
          {error ? (
            <div role="alert">
              <EmptyState
                icon={AlertCircle}
                title="Không tải được yêu cầu duyệt"
                description={
                  endpointUnavailable
                    ? 'Dịch vụ yêu cầu duyệt chưa sẵn sàng. Vui lòng cập nhật backend rồi tải lại.'
                    : error
                }
                action={
                  <button
                    type="button"
                    className={ds.outlineButton}
                    onClick={() => setRevision((value) => value + 1)}
                  >
                    <RefreshCw className="size-3.5" /> Thử lại
                  </button>
                }
              />
            </div>
          ) : loading ? (
            <LoadingState label="Đang tải yêu cầu…" className="min-h-40" />
          ) : !requestId ? (
            !data?.items.length ? (
              <EmptyState
                icon={Send}
                title={
                  courseId || search.query || status
                    ? 'Chưa có yêu cầu phù hợp'
                    : 'Chưa có yêu cầu duyệt'
                }
                description={
                  courseId || search.query
                    ? 'Thử đổi môn học hoặc tìm theo tên, email giảng viên khác.'
                    : status === 'COMPLETED'
                      ? 'Các yêu cầu đã xử lý xong sẽ xuất hiện tại đây.'
                      : status === 'IN_PROGRESS'
                        ? 'Các yêu cầu đang được duyệt sẽ xuất hiện tại đây.'
                        : 'Yêu cầu giảng viên gửi duyệt sẽ xuất hiện tại đây.'
                }
              />
            ) : (
              <>
                <DividedList>
                  {data.items.map((item) => (
                    <li
                      key={item.id}
                      className="grid items-center gap-3 px-4 py-4 transition hover:bg-[#F4FBFD] sm:grid-cols-2 sm:gap-x-6 sm:px-5 xl:grid-cols-[minmax(0,1.2fr)_minmax(0,1fr)_minmax(7rem,0.55fr)_minmax(10rem,0.85fr)_max-content]"
                    >
                      <div className="min-w-0 sm:col-span-2 xl:col-span-1">
                        <div className="flex flex-wrap items-center gap-2">
                          <span className="font-mono text-xs font-bold text-[#1C4D8D]">
                            {item.courseCode}
                          </span>
                          <span
                            className={cn(
                              ds.infoChip,
                              item.status === 'COMPLETED'
                                ? 'border-[#b7e4cf] bg-[#e8f8f1] text-[#16836a]'
                                : item.status === 'PENDING' &&
                                    'border-[#efdbaf] bg-[#fff8e8] text-[#925812]',
                            )}
                          >
                            {statuses.find((s) => s.value === item.status)?.label}
                          </span>
                        </div>
                        <h2 className="mt-1.5 text-base font-bold text-[#0F2854]">
                          {item.lecturerName}
                        </h2>
                        <div className="mt-1.5 space-y-1 leading-snug">
                          <p className="text-xs wrap-anywhere text-[#5b6b84]">
                            {item.lecturerEmail}
                          </p>
                          <p className="text-sm font-semibold wrap-anywhere text-[#1C4D8D]">
                            {item.courseName}
                          </p>
                        </div>
                      </div>
                      <div className="min-w-0 sm:col-span-2 xl:col-span-1">
                        <p className="text-xs text-[#5b6b84]">Ghi chú</p>
                        <p
                          className="mt-1 line-clamp-2 text-sm wrap-anywhere text-[#0F2854]"
                          title={item.notes?.trim() || undefined}
                        >
                          {item.notes?.trim() || '—'}
                        </p>
                      </div>
                      <div className="text-sm text-[#0F2854]">
                        <p className="text-xs text-[#5b6b84]">Thời gian gửi</p>
                        <p className="mt-1 font-semibold">{dateLabel(item.submittedAt)}</p>
                      </div>
                      <div className="min-w-0 space-y-1.5 text-sm">
                        <p className="font-semibold text-[#0F2854]">
                          <span className="tabular-nums">
                            {item.total - item.pending}/{item.total}
                          </span>{' '}
                          câu đã xử lý
                        </p>
                        <ProgressBar
                          aria-label="Tiến độ duyệt"
                          value={item.total - item.pending}
                          max={Math.max(1, item.total)}
                          className="max-w-56"
                        />
                        <p className="text-xs text-[#5b6b84]">
                          {item.approved} đã duyệt · {item.needsRevision} cần sửa · {item.rejected}{' '}
                          từ chối
                        </p>
                      </div>
                      <Link
                        to={`${ROUTE_PATHS.departmentHead.questionReview}/${item.id}${filterQuery}`}
                        className={cn(
                          ds.outlineButton,
                          'justify-self-end whitespace-nowrap xl:ml-4',
                        )}
                      >
                        <Eye className="size-3.5 shrink-0" aria-hidden="true" /> Xem chi tiết
                      </Link>
                    </li>
                  ))}
                </DividedList>
                <ListPagination
                  label="Phân trang yêu cầu"
                  page={data.page}
                  pageCount={Math.max(1, Math.ceil(data.totalCount / data.pageSize))}
                  onPageChange={(value) => {
                    const next = new URLSearchParams(params)
                    next.set('page', String(value))
                    setParams(next)
                  }}
                />
              </>
            )
          ) : (
            detail &&
            summary && (
              <>
                <div className="px-4 pt-3 sm:px-6">
                  <StatusTabs
                    label="Trạng thái câu hỏi"
                    value={questionFilter}
                    options={questionTabs.map((tab) => ({
                      ...tab,
                      dotClass: tab.dot,
                      count:
                        tab.value === 'ALL'
                          ? questions.length
                          : questions.filter((item) => item.decision === tab.value).length,
                    }))}
                    onChange={(value) => {
                      setQuestionFilter(value)
                      setSelected([])
                      setViewing(undefined)
                    }}
                  />
                </div>
                {questionFilter === 'PENDING' && pending.length > 0 && (
                  <div className="flex flex-wrap items-center gap-2 border-b border-[#82aedb]/55 bg-[#F4FBFD] px-4 py-3 sm:px-6">
                    <button
                      type="button"
                      className={cn(
                        ds.outlineButton,
                        'h-9 px-3 text-xs text-[#5b6b84] hover:bg-white',
                      )}
                      onClick={() => setSelected(allSelected ? [] : pending.map((item) => item.id))}
                    >
                      {allSelected ? <X className="size-4" /> : <Check className="size-4" />}
                      {allSelected ? 'Bỏ chọn' : 'Chọn tất cả'}
                    </button>
                    {selected.length > 0 && (
                      <button
                        type="button"
                        className={cn(ds.primaryButton, 'h-9 whitespace-nowrap')}
                        onClick={() => beginDecision(selected, 'APPROVED')}
                      >
                        <CheckCheck className="size-4" /> Phê duyệt {selected.length} câu
                      </button>
                    )}
                  </div>
                )}
                {visibleQuestions.length === 0 ? (
                  <EmptyState
                    className="m-4 sm:m-5"
                    icon={BookOpenCheck}
                    title={
                      questions.length
                        ? 'Không có câu hỏi ở trạng thái này'
                        : 'Yêu cầu chưa có câu hỏi'
                    }
                  />
                ) : (
                  <DividedList>
                    {visibleQuestions.map((item) => (
                      <li
                        key={item.id}
                        onClick={(event) => {
                          if (
                            item.question &&
                            event.target instanceof Element &&
                            !event.target.closest('button, input, label')
                          ) {
                            setViewing(item)
                          }
                        }}
                        className={cn(
                          'grid grid-cols-[auto_minmax(0,1fr)] items-center gap-8 px-4 py-4 transition sm:px-5 lg:grid-cols-[auto_minmax(0,1fr)_100px_100px_auto]',
                          item.question && 'cursor-pointer',
                          viewing?.id === item.id
                            ? 'bg-[#D7F1F8] shadow-[inset_4px_0_0_0_#1C4D8D] ring-1 ring-[#4988C4]/55 ring-inset'
                            : selected.includes(item.id) && 'bg-[#4988C4]/16',
                        )}
                      >
                        <div className="w-4">
                          {questionFilter === 'PENDING' &&
                            item.decision === 'PENDING' &&
                            item.question && (
                              <input
                                type="checkbox"
                                aria-label={`Chọn ${item.question.title}`}
                                checked={selected.includes(item.id)}
                                onChange={(event) =>
                                  setSelected(
                                    event.target.checked
                                      ? [...selected, item.id]
                                      : selected.filter((id) => id !== item.id),
                                  )
                                }
                                className="size-4 rounded border-[#4988C4] text-[#1C4D8D] accent-[#4988C4] focus:ring-[#1C4D8D]"
                              />
                            )}
                        </div>
                        <div className="min-w-0">
                          <div className="flex flex-wrap items-center gap-2">
                            <span className="font-mono text-xs font-bold text-[#1C4D8D]">
                              {summary.courseCode}
                            </span>
                            <DecisionChip value={item.decision} />
                          </div>
                          <p className="mt-1 line-clamp-2 text-sm leading-snug font-bold wrap-anywhere text-[#0F2854]">
                            {item.question?.title ?? 'Câu hỏi không còn dữ liệu'}
                          </p>
                          {item.question && (
                            <p className="mt-1 text-xs text-[#8794a6]">
                              {item.question.createdAt && (
                                <>
                                  Tạo ngày{' '}
                                  {new Date(item.question.createdAt).toLocaleDateString('vi-VN', {
                                    timeZone: 'Asia/Ho_Chi_Minh',
                                  })}{' '}
                                  ·{' '}
                                </>
                              )}
                              Nguồn{' '}
                              {item.question.source
                                ? item.question.source === 'flm_api'
                                  ? 'AI'
                                  : 'Thủ công'
                                : '—'}
                            </p>
                          )}
                          {item.reviewNotes && (
                            <p className="mt-1 text-sm wrap-anywhere whitespace-pre-wrap text-[#5b6b84]">
                              {item.reviewNotes}
                            </p>
                          )}
                          {item.reviewedAt && (
                            <p className="mt-1 text-xs text-[#8794a6]">
                              {item.reviewerName} · {dateLabel(item.reviewedAt)}
                            </p>
                          )}
                        </div>
                        <QuestionRowMetrics
                          className="col-start-2 lg:col-start-auto"
                          bloom={item.question?.bloomLevel}
                          score={
                            item.question
                              ? (item.question.rubricTotalMaxScore ??
                                item.question.criteria.reduce(
                                  (sum, criterion) => sum + criterion.maxScore,
                                  0,
                                ))
                              : undefined
                          }
                        />

                        <button
                          type="button"
                          className={cn(
                            'col-start-2 inline-flex min-h-9 items-center justify-center gap-1.5 justify-self-start rounded-xl border px-3 text-xs font-bold transition focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:opacity-45 lg:col-start-auto lg:justify-self-end',
                            viewing?.id === item.id
                              ? 'border-[#1C4D8D] bg-[#1C4D8D] text-white'
                              : 'border-[#4988C4] bg-white text-[#49617f] hover:bg-[#4988C4]/18',
                          )}
                          disabled={!item.question}
                          onClick={() => setViewing(item)}
                        >
                          <Eye className="size-3.5" aria-hidden="true" />{' '}
                          {viewing?.id === item.id ? 'Đang xem' : 'Xem chi tiết'}
                        </button>
                      </li>
                    ))}
                  </DividedList>
                )}
              </>
            )
          )}
        </div>
      </section>
      {viewing?.question && (
        <QuestionDrawer
          onClose={() => {
            if (!action) setViewing(undefined)
          }}
          title={viewing.question.title}
          badges={
            <>
              <span className="font-mono text-xs font-bold text-[#1C4D8D]">
                {summary?.courseCode}
              </span>
              <DecisionChip value={viewing.decision} />
              <span className="text-[11px] font-semibold text-[#7a8aa0]">
                {viewingIndex + 1}/{viewableItems.length}
              </span>
            </>
          }
          meta={[
            difficultyLabels[viewing.question.difficulty] ?? viewing.question.difficulty,
            viewing.question.bloomLevel,
            viewing.question.source,
            viewing.question.cloCode,
            summary && dateLabel(summary.submittedAt),
            summary?.lecturerName,
          ]
            .filter(Boolean)
            .join(' · ')}
          onPrevious={
            viewingIndex > 0 ? () => setViewing(viewableItems[viewingIndex - 1]) : undefined
          }
          onNext={
            viewingIndex >= 0 && viewingIndex < viewableItems.length - 1
              ? () => setViewing(viewableItems[viewingIndex + 1])
              : undefined
          }
          footer={
            viewing.decision === 'PENDING' && (
              <div className="flex flex-wrap justify-end gap-2">
                <button
                  type="button"
                  className={cn(
                    ds.outlineButton,
                    'border-[#eab7c2] bg-[#fff5f6] text-[#a83249] hover:border-[#c2415b] hover:bg-[#fdecec] focus-visible:outline-[#c2415b]',
                  )}
                  onClick={() => beginDecision([viewing.id], 'REJECTED')}
                >
                  Từ chối
                </button>
                <button
                  type="button"
                  className={ds.outlineButton}
                  onClick={() => beginDecision([viewing.id], 'NEEDS_REVISION')}
                >
                  Yêu cầu chỉnh sửa
                </button>
                <button
                  type="button"
                  className={ds.primaryButton}
                  onClick={() => beginDecision([viewing.id], 'APPROVED')}
                >
                  Phê duyệt
                </button>
              </div>
            )
          }
        >
          <div className="space-y-4 px-4 pt-4 pb-6 sm:px-5 sm:pt-5">
            <QuestionDetailContent
              question={{
                ...viewing.question,
                imageAlt: viewing.question.imageDescription,
                rubric: {
                  name: viewing.question.rubricName?.trim() || 'Tiêu chí chấm điểm',
                  totalMaxScore:
                    viewing.question.rubricTotalMaxScore ??
                    viewing.question.criteria.reduce(
                      (sum, criterion) => sum + criterion.maxScore,
                      0,
                    ),
                  criteria: viewing.question.criteria.map((criterion, index) => ({
                    ...criterion,
                    id: criterion.id ?? String(index),
                    criterionName: criterion.name,
                  })),
                },
              }}
            />
          </div>
        </QuestionDrawer>
      )}
      {action && (
        <FormDialog
          eyebrow="Chi tiết yêu cầu duyệt"
          title={
            action.decision === 'APPROVED'
              ? 'Xác nhận phê duyệt'
              : action.decision === 'NEEDS_REVISION'
                ? 'Yêu cầu chỉnh sửa'
                : 'Từ chối câu hỏi'
          }
          icon={
            action.decision === 'REJECTED'
              ? X
              : action.decision === 'NEEDS_REVISION'
                ? FilePenLine
                : BookOpenCheck
          }
          size="sm"
          nested={Boolean(viewing)}
          busy={saving}
          onClose={() => setAction(undefined)}
          footer={
            <SubmitDialogFooter
              form="review-decision"
              label="Xác nhận"
              busyLabel="Đang lưu…"
              busy={saving}
              danger={action.decision === 'REJECTED'}
              onCancel={() => setAction(undefined)}
            />
          }
        >
          <form
            id="review-decision"
            onSubmit={(event) => {
              event.preventDefault()
              void confirmDecision()
            }}
            className="space-y-3"
          >
            <div
              className={cn(
                'rounded-xl border px-3.5 py-2.5 text-sm text-[#0F2854]',
                action.decision === 'REJECTED'
                  ? 'border-[#eab7c2] bg-[#fff5f6]'
                  : 'border-[#82aedb]/55 bg-[#F4FBFD]',
              )}
            >
              <p className="font-semibold wrap-anywhere">
                {viewing?.question?.title ?? `${action.ids.length} câu hỏi được chọn`}
              </p>
              {action.decision !== 'APPROVED' && (
                <p className="mt-1 text-xs text-[#5b6b84]">
                  {action.decision === 'REJECTED'
                    ? 'Giảng viên sẽ thấy lý do từ chối.'
                    : 'Giảng viên sẽ thấy những điểm cần chỉnh sửa.'}
                </p>
              )}
            </div>
            <TextareaField
              label={
                action.decision === 'APPROVED' ? 'Nhận xét (không bắt buộc)' : 'Nhận xét (bắt buộc)'
              }
              error={noteError}
              value={notes}
              disabled={saving}
              maxLength={2000}

              onChange={(event) => {
                setNotes(event.target.value)
                setNoteError('')
              }}
              placeholder={
                action.decision === 'REJECTED'
                  ? 'Nêu lý do từ chối câu hỏi'
                  : action.decision === 'NEEDS_REVISION'
                    ? 'Nêu rõ những điểm giảng viên cần chỉnh sửa'
                    : 'Nhập nhận xét thẩm định'
              }
            />
            {noteError && (
              <button
                type="button"
                className={ds.outlineButton}
                onClick={() => {
                  setAction(undefined)
                  setViewing(undefined)
                  setRevision((value) => value + 1)
                }}
              >
                Tải lại yêu cầu
              </button>
            )}
          </form>
        </FormDialog>
      )}
    </div>
  )
}
