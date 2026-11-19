import { useEffect, useState } from 'react'
import { ArrowRight, History } from 'lucide-react'
import { FormDialog, DialogFooter } from '@/design/overlays'
import { ds } from '@/design/system'
import { FilterDropMenu, type FilterDropMenuOption } from '@/components/common/FilterDropMenu'
import { EmptyState } from '@/components/common/EmptyState'
import { LoadingState } from '@/components/common/LoadingState'
import { CompactPagination } from '@/components/common/CompactPagination'
import { ProgressBar } from '@/components/common/ProgressBar'
import { DividedList } from '@/components/common/DividedList'
import {
  questionReviewRequestService,
  type ReviewRequestPage,
} from '@/services/questionReviewRequest.service'
import { userMessage } from '@/lib/userMessage'
import { cn } from '@/lib/utils'

const statuses = [
  { value: '', label: 'Tất cả trạng thái' },
  { value: 'PENDING', label: 'Chưa xử lý' },
  { value: 'IN_PROGRESS', label: 'Đang xử lý' },
  { value: 'COMPLETED', label: 'Đã xử lý' },
]

export function SentReviewRequestsDialog({
  courses,
  onClose,
  onRevise,
}: {
  courses: FilterDropMenuOption[]
  onClose: () => void
  onRevise: (courseId: string) => void
}) {
  const [courseId, setCourseId] = useState('')
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const [data, setData] = useState<ReviewRequestPage>()
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [reload, setReload] = useState(0)
  useEffect(() => {
    const controller = new AbortController()
    void questionReviewRequestService
      .listMine(
        { courseId: courseId || undefined, status: status || undefined, page },
        controller.signal,
      )
      .then((result) => {
        if (!controller.signal.aborted) setData(result)
      })
      .catch((failure: unknown) => {
        if (!controller.signal.aborted)
          setError(userMessage(failure, 'Không tải được lịch sử yêu cầu.'))
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })
    return () => controller.abort()
  }, [courseId, status, page, reload])
  return (
    <FormDialog
      titleId="sent-review-requests-title"
      title="Yêu cầu đã gửi"
      eyebrow="Câu hỏi của tôi"
      icon={History}
      size="2xl"
      maxHeight="max-h-[min(90dvh,820px)]"
      bodyClassName="p-5"
      onClose={onClose}
      headerExtra={
        !loading && !error ? (
          <span className={cn(ds.sectionBadge, 'whitespace-nowrap')}>
            {data?.totalCount ?? 0} yêu cầu
          </span>
        ) : undefined
      }
      footer={
        !loading && !error && data?.items.length ? (
          <DialogFooter className="justify-center bg-white sm:justify-center">
            <CompactPagination
              label="Phân trang yêu cầu đã gửi"
              page={data.page}
              pageCount={Math.max(1, Math.ceil(data.totalCount / data.pageSize))}
              onPageChange={(value) => {
                if (value !== data.page) {
                  setLoading(true)
                  setError('')
                  setPage(value)
                }
              }}
            />
          </DialogFooter>
        ) : undefined
      }
      beforeBody={
        <div className="flex shrink-0 flex-wrap items-center gap-2 border-b border-[#82aedb]/55 bg-white px-5 py-3">
          <FilterDropMenu
            label="Lọc môn trong lịch sử"
            value={courseId}
            options={[
              { value: '', label: 'Tất cả môn' },
              ...courses.filter((course) => course.value),
            ]}
            onChange={(value) => {
              if (value !== courseId) {
                setLoading(true)
                setError('')
                setCourseId(value)
                setPage(1)
              }
            }}
            className={cn(ds.filterCourse, 'w-full sm:w-56')}
            menuMinWidth={0}
            wrapOptions
          />
          <FilterDropMenu
            label="Lọc trạng thái yêu cầu đã gửi"
            value={status}
            options={statuses}
            onChange={(value) => {
              if (value !== status) {
                setLoading(true)
                setError('')
                setStatus(value)
                setPage(1)
              }
            }}
            className={cn(ds.filterStatus, 'w-full sm:w-44')}
            menuMinWidth={0}
          />
        </div>
      }
    >
      <div className="flex min-h-[340px] flex-col">
        {loading ? (
          <LoadingState className="flex-1" />
        ) : error ? (
          <div role="alert" className="space-y-3 py-6 text-center">
            <p className="text-sm text-[#a93d48]">{error}</p>
            <button
              type="button"
              className={ds.outlineButton}
              onClick={() => {
                setLoading(true)
                setError('')
                setReload((value) => value + 1)
              }}
            >
              Thử lại
            </button>
          </div>
        ) : !data?.items.length ? (
          <EmptyState
            icon={History}
            className="flex-1 justify-center"
            title="Chưa có yêu cầu phù hợp"
            description={
              courseId || status
                ? 'Thử thay đổi bộ lọc để xem các yêu cầu đã gửi.'
                : 'Các lần gửi câu hỏi duyệt sẽ hiển thị tại đây.'
            }
          />
        ) : (
          <>
            <DividedList
              as="div"
              className="overflow-hidden rounded-2xl border border-[#82aedb]/55 bg-white"
            >
              {data.items.map((request) => (
                <div key={request.id} className="min-w-0 space-y-3 px-4 py-4 sm:px-5">
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <div className="min-w-0 flex-1">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className={ds.sectionBadge}>{request.courseCode}</span>
                        <span
                          className={cn(
                            ds.infoChip,
                            request.status === 'COMPLETED'
                              ? 'border-[#b7e4cf] bg-[#e8f8f1] text-[#16836a]'
                              : request.status === 'PENDING'
                                ? 'border-[#efdbaf] bg-[#fff8e8] text-[#925812]'
                                : 'border-[#82aedb]/55 bg-[#E7F3FB] text-[#1C4D8D]',
                          )}
                        >
                          {statuses.find((item) => item.value === request.status)?.label ??
                            request.status}
                        </span>
                        <span className="text-sm text-[#5b6b84]">{request.total} câu hỏi</span>
                      </div>
                      <p className="mt-2 text-base font-bold wrap-anywhere text-[#0F2854]">
                        {request.courseName}
                      </p>
                      <p className="mt-1 text-sm text-[#7d8fa3]">
                        Gửi{' '}
                        {request.submittedAt
                          ? new Date(request.submittedAt).toLocaleString('vi-VN')
                          : '—'}
                      </p>
                    </div>
                    {request.needsRevision > 0 && (
                      <button
                        type="button"
                        className={ds.outlineButton}
                        onClick={() => onRevise(request.courseId)}
                      >
                        Đi tới chỉnh sửa <ArrowRight className="size-4" aria-hidden="true" />
                      </button>
                    )}
                  </div>
                  <p className="text-sm wrap-anywhere whitespace-pre-wrap text-[#5b6b84]">
                    {request.notes || 'Không có ghi chú.'}
                  </p>
                  <div className="space-y-1.5">
                    <div className="flex items-center justify-between gap-3 text-xs text-[#5b6b84]">
                      <span>Tiến độ duyệt</span>
                      <span className="font-bold text-[#1C4D8D] tabular-nums">
                        {request.total - request.pending}/{request.total} câu đã xử lý
                      </span>
                    </div>
                    <ProgressBar
                      aria-label={`Tiến độ duyệt ${request.courseCode}`}
                      value={request.total - request.pending}
                      max={Math.max(1, request.total)}
                    />
                  </div>
                  <div className="flex flex-wrap gap-2 text-xs font-bold">
                    <span
                      className={cn(ds.infoChip, 'border-[#efdbaf] bg-[#fff8e8] text-[#925812]')}
                    >
                      {request.pending} chờ duyệt
                    </span>
                    <span
                      className={cn(ds.infoChip, 'border-[#b7e4cf] bg-[#e8f8f1] text-[#16836a]')}
                    >
                      {request.approved} đã duyệt
                    </span>
                    <span
                      className={cn(ds.infoChip, 'border-[#82aedb]/55 bg-[#E7F3FB] text-[#1C4D8D]')}
                    >
                      {request.needsRevision} cần chỉnh sửa
                    </span>
                    <span
                      className={cn(ds.infoChip, 'border-[#efc8cc] bg-[#fff2f3] text-[#a93d48]')}
                    >
                      {request.rejected} từ chối
                    </span>
                  </div>
                </div>
              ))}
            </DividedList>
          </>
        )}
      </div>
    </FormDialog>
  )
}
