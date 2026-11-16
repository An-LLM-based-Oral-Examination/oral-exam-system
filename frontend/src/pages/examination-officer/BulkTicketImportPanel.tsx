import { useEffect, useMemo, useRef, useState, type DragEvent } from 'react'
import {
  CheckCircle2,
  ClipboardCheck,
  Download,
  FileSpreadsheet,
  FileUp,
  LoaderCircle,
  TicketCheck,
  UploadCloud,
  XCircle,
} from 'lucide-react'
import { officialExamService } from '@/services/officialExam.service'
import { proctorService } from '@/services/proctor.service'
import type {
  BulkExamTicketImportIssue,
  BulkExamTicketImportPreview,
  BulkExamTicketImportCommitResult,
  ExamShift,
  ExamTicket,
} from '@/types/officialExam.types'
import type { ShiftAttendanceMonitor } from '@/types/proctor.types'
import { SectionHeader } from '@/design/ui'
import { DialogFooter, FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { extractProblemErrorCode, userMessage } from '@/lib/userMessage'
import { DEFAULT_SHIFT_CAPACITY } from '@/lib/shiftCapacity'
import { toast } from '@/components/common/toastStore'

const MAX_FILE_SIZE = 1_048_576
const ACCEPTED_EXTENSIONS = ['.csv', '.xlsx']

interface BulkTicketImportPanelProps {
  shiftId: string
  shift?: ExamShift | null
  availableHandedOverSetCount?: number | null
  embedded?: boolean
  onBusyChange?: (busy: boolean) => void
  onCompleted?: (result: BulkExamTicketImportCommitResult) => void
  onRefreshShift?: () => void | Promise<void>
}

function issueMessage(issue: BulkExamTicketImportIssue) {
  return issue.sourceRow ? `Dòng ${issue.sourceRow}: ${issue.message}` : issue.message
}

function isConflict(error: unknown) {
  if (typeof error !== 'object' || error === null) return false
  if ('status' in error && error.status === 409) return true
  if (
    'response' in error &&
    typeof error.response === 'object' &&
    error.response !== null &&
    'status' in error.response
  ) {
    return error.response.status === 409
  }
  return false
}

function conflictMessage(error: unknown) {
  const message = userMessage(
    error,
    'Dữ liệu ca thi đã thay đổi. Hãy preview lại file trước khi xác nhận import.',
  )
  const code = extractProblemErrorCode(error)
  if (code) {
    return `${message} (Mã lỗi: ${code}) · Vui lòng preview lại tệp roster.`
  }
  return `${message} · Vui lòng preview lại tệp roster.`
}

function triggerDownload(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = fileName
  anchor.click()
  URL.revokeObjectURL(url)
}

export function BulkTicketImportPanel({
  shiftId,
  shift = null,
  availableHandedOverSetCount = null,
  embedded = false,
  onBusyChange,
  onCompleted,
  onRefreshShift,
}: BulkTicketImportPanelProps) {
  const fileInputRef = useRef<HTMLInputElement>(null)
  const previousShiftIdRef = useRef(shiftId)
  const [selectedFile, setSelectedFile] = useState<File | null>(null)
  const [preview, setPreview] = useState<BulkExamTicketImportPreview | null>(null)
  const [attendance, setAttendance] = useState<ShiftAttendanceMonitor | null>(null)
  const [lastCommittedTickets, setLastCommittedTickets] = useState<ExamTicket[]>([])
  const [downloadingFormat, setDownloadingFormat] = useState<'csv' | 'xlsx' | null>(null)
  const [isPreviewing, setIsPreviewing] = useState(false)
  const [isCommitting, setIsCommitting] = useState(false)
  const [isRefreshingRoster, setIsRefreshingRoster] = useState(false)
  const [showConfirm, setShowConfirm] = useState(false)
  const [successMessage, setSuccessMessage] = useState('')
  const [error, setError] = useState('')

  useEffect(() => {
    if (previousShiftIdRef.current === shiftId) return
    previousShiftIdRef.current = shiftId
    setSelectedFile(null)
    setPreview(null)
    setAttendance(null)
    setLastCommittedTickets([])
    setShowConfirm(false)
    setSuccessMessage('')
    setError('')
    if (fileInputRef.current) fileInputRef.current.value = ''
  }, [shiftId])

  const setBusyState = (busy: boolean) => {
    onBusyChange?.(busy)
  }

  const selectFile = (file: File | undefined) => {
    if (!file || isPreviewing || isCommitting) return
    const extension = file.name.slice(file.name.lastIndexOf('.')).toLowerCase()
    if (!ACCEPTED_EXTENSIONS.includes(extension)) {
      setSelectedFile(null)
      setPreview(null)
      setAttendance(null)
      setShowConfirm(false)
      setSuccessMessage('')
      setError('Chỉ hỗ trợ tệp .csv hoặc .xlsx.')
      return
    }
    if (file.size > MAX_FILE_SIZE) {
      setSelectedFile(null)
      setPreview(null)
      setAttendance(null)
      setShowConfirm(false)
      setSuccessMessage('')
      setError('Dung lượng file không được vượt quá 1 MB.')
      return
    }
    setSelectedFile(file)
    setPreview(null)
    setAttendance(null)
    setShowConfirm(false)
    setSuccessMessage('')
    setError('')
  }

  const handleDrop = (event: DragEvent<HTMLDivElement>) => {
    event.preventDefault()
    if (isPreviewing || isCommitting) return
    selectFile(event.dataTransfer.files?.[0])
  }

  const previewFile = async () => {
    if (!selectedFile || !shiftId || isPreviewing || isCommitting) return
    setIsPreviewing(true)
    setBusyState(true)
    setShowConfirm(false)
    setError('')
    try {
      setPreview(await officialExamService.previewTicketRosterImport(shiftId, selectedFile))
    } catch (previewError) {
      setPreview(null)
      setError(userMessage(previewError, 'Không thể kiểm tra file roster.'))
    } finally {
      setIsPreviewing(false)
      setBusyState(false)
    }
  }

  const commitImport = async () => {
    if (!selectedFile || !canCommit || !shiftId || isPreviewing || isCommitting) return
    setIsCommitting(true)
    setBusyState(true)
    setError('')
    try {
      const result = await officialExamService.commitTicketRosterImport(shiftId, selectedFile)
      setLastCommittedTickets(result.tickets)
      setIsRefreshingRoster(true)
      try {
        const [refreshedAttendance] = await Promise.all([
          proctorService.getShiftAttendance(shiftId),
          Promise.resolve(onRefreshShift?.()),
        ])
        setAttendance(refreshedAttendance)
      } catch {
        toast.warning('Đã phát vé thành công nhưng chưa tải lại được danh sách ghế của ca thi.')
      } finally {
        setIsRefreshingRoster(false)
      }
      onCompleted?.(result)
      setShowConfirm(false)
      setSelectedFile(null)
      setPreview(null)
      if (fileInputRef.current) fileInputRef.current.value = ''
      setSuccessMessage(`Đã phát ${result.createdTicketCount} vé thi và phân bộ đề thành công.`)
      toast.success(`Đã phát ${result.createdTicketCount} vé thi và phân bộ đề thành công.`)
    } catch (commitError) {
      setShowConfirm(false)
      if (isConflict(commitError)) {
        setPreview(null)
        setError(conflictMessage(commitError))
        void Promise.resolve(onRefreshShift?.()).catch(() => {})
      } else {
        setError(userMessage(commitError, 'Không thể xác nhận import roster.'))
      }
    } finally {
      setIsCommitting(false)
      setBusyState(false)
    }
  }

  const downloadTemplate = async (format: 'csv' | 'xlsx') => {
    if (downloadingFormat || isCommitting) return
    setDownloadingFormat(format)
    try {
      const file = await officialExamService.downloadTicketRosterTemplate(format)
      triggerDownload(file.blob, file.fileName)
    } catch (downloadError) {
      const message = userMessage(downloadError, 'Không tải được template roster.')
      setError(message)
      toast.error(message)
    } finally {
      setDownloadingFormat(null)
    }
  }

  const effectiveMaxSeats =
    preview?.maxSeats ?? shift?.maxSeats ?? attendance?.totalSeats ?? DEFAULT_SHIFT_CAPACITY
  const effectiveExistingTickets =
    preview?.existingTicketCount ??
    attendance?.assignedTicketsCount ??
    shift?.assignedTicketsCount ??
    0
  const effectiveRemainingSeats =
    preview?.remainingSeats ??
    shift?.remainingSeats ??
    Math.max(0, effectiveMaxSeats - effectiveExistingTickets)
  const effectiveAvailableSets =
    preview?.availableHandedOverSetCount ?? availableHandedOverSetCount ?? 0
  const reservedSetCodes = useMemo(() => {
    if (!preview) return []
    if (Array.isArray(preview.reservedExamSetCodes) && preview.reservedExamSetCodes.length > 0) {
      return preview.reservedExamSetCodes
    }
    return preview.rows
      .map((row) => row.setCode)
      .filter((code): code is string => typeof code === 'string' && code.trim().length > 0)
  }, [preview])

  const committedTicketBySeat = useMemo(() => {
    const map = new Map<number, ExamTicket>()
    for (const ticket of lastCommittedTickets) {
      map.set(ticket.seatNumber, ticket)
    }
    return map
  }, [lastCommittedTickets])

  const hasAnyError = preview
    ? preview.errorRowCount > 0 ||
      preview.globalErrors.length > 0 ||
      preview.rows.some(
        (row) =>
          !row.isValid ||
          (row.errors && row.errors.length > 0) ||
          (row.issues && row.issues.length > 0),
      )
    : true

  const canCommit = Boolean(
    preview &&
      preview.canCommit &&
      !hasAnyError &&
      selectedFile &&
      !isPreviewing &&
      !isCommitting,
  )
  const sectionClassName = embedded ? 'rounded-2xl border border-[#d9e8f2] bg-white' : ds.section

  return (
    <div className={embedded ? 'grid gap-4' : ds.page}>
      {error ? (
        <div
          role="alert"
          className="rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-4 py-3 text-sm font-semibold text-[#a43d4b]"
        >
          {error}
        </div>
      ) : null}

      {successMessage ? (
        <div
          role="status"
          className="flex items-start gap-3 rounded-xl border border-[#b7e4cf] bg-[#e8f8f1] px-4 py-3 text-sm font-semibold text-[#16836a]"
        >
          <CheckCircle2 className="mt-0.5 size-5 shrink-0" />
          <span>{successMessage}</span>
        </div>
      ) : null}

      <section className={sectionClassName}>
        <SectionHeader
          icon={FileUp}
          title="1. Chọn file roster"
          hint="File chỉ được giữ trong phiên làm việc hiện tại, không lưu vào browser storage."
          action={
            <div className="flex flex-wrap gap-2">
              <button
                type="button"
                onClick={() => void downloadTemplate('csv')}
                disabled={downloadingFormat !== null || isCommitting}
                className={ds.outlineButton}
              >
                {downloadingFormat === 'csv' ? (
                  <LoaderCircle className="size-4 animate-spin" />
                ) : (
                  <Download className="size-4" />
                )}{' '}
                Template CSV
              </button>
              <button
                type="button"
                onClick={() => void downloadTemplate('xlsx')}
                disabled={downloadingFormat !== null || isCommitting}
                className={ds.outlineButton}
              >
                {downloadingFormat === 'xlsx' ? (
                  <LoaderCircle className="size-4 animate-spin" />
                ) : (
                  <FileSpreadsheet className="size-4" />
                )}{' '}
                Template XLSX
              </button>
            </div>
          }
        />
        <div className="grid gap-4 p-5">
          <div className="flex flex-wrap items-center gap-x-6 gap-y-2 rounded-xl border border-[#bcd6ef] bg-[#f5fbff] px-4 py-3 text-xs font-bold text-[#0f2854]">
            <span>Sức chứa ca thi: {effectiveMaxSeats}</span>
            <span>Ghế hợp lệ: 1–{effectiveMaxSeats}</span>
            <span className="font-semibold text-[#52657d]">
              Số vé đã phát: {effectiveExistingTickets}
            </span>
            <span className="font-semibold text-[#52657d]">
              Số chỗ còn lại: {effectiveRemainingSeats}
            </span>
          </div>
          <div
            className={cn(
              'rounded-xl border border-dashed border-[#82aedb] bg-[#f7fbff] p-6 text-center',
              (isPreviewing || isCommitting) && 'pointer-events-none opacity-60',
            )}
            onDragOver={(event) => event.preventDefault()}
            onDrop={handleDrop}
          >
            <input
              ref={fileInputRef}
              id={embedded ? 'embedded-exam-roster-file' : 'exam-roster-file'}
              aria-label="Chọn file roster"
              type="file"
              accept=".csv,.xlsx"
              disabled={isPreviewing || isCommitting}
              className="sr-only"
              onChange={(event) => selectFile(event.target.files?.[0])}
            />
            <label
              htmlFor={embedded ? 'embedded-exam-roster-file' : 'exam-roster-file'}
              className="mx-auto flex max-w-md cursor-pointer flex-col items-center gap-2 text-[#315b8f]"
            >
              <UploadCloud className="size-9" aria-hidden="true" />
              <strong>Kéo thả hoặc chọn file roster</strong>
              <span className="text-xs text-[#64758c]">
                studentCode, seatNumber, ipAddress · CSV/XLSX · tối đa 1 MB (không thêm họ tên hay
                CCCD)
              </span>
            </label>
          </div>
          {selectedFile ? (
            <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-[#d9e8f2] bg-[#f8fbfd] px-4 py-3">
              <div>
                <strong className="block text-sm text-[#0f2854]">{selectedFile.name}</strong>
                <span className="text-xs text-[#64758c]">
                  {Math.ceil(selectedFile.size / 1024)} KB
                </span>
              </div>
              <button
                type="button"
                onClick={previewFile}
                disabled={isPreviewing || isCommitting}
                className={ds.primaryButton}
              >
                <ClipboardCheck className="size-4" />{' '}
                {isPreviewing ? 'Đang kiểm tra...' : 'Preview roster'}
              </button>
            </div>
          ) : null}
        </div>
      </section>

      {preview ? (
        <section className={sectionClassName}>
          <SectionHeader
            icon={ClipboardCheck}
            title="2. Preview trước khi ghi"
            badge={`${preview.validRowCount}/${preview.totalRowCount} hợp lệ`}
            hint="Họ tên và bộ đề dự kiến được backend tra cứu, không lấy từ file import."
          />
          <div className="grid gap-3 p-5 sm:grid-cols-2 lg:grid-cols-3">
            {[
              ['Sức chứa ca thi', effectiveMaxSeats],
              ['Số vé đã tồn tại', effectiveExistingTickets],
              ['Số chỗ còn lại', effectiveRemainingSeats],
              ['Tổng số dòng hợp lệ', preview.validRowCount],
              [
                'Tổng số dòng/lỗi không hợp lệ',
                preview.errorRowCount + preview.globalErrors.length,
              ],
              ['Bộ đề HANDED_OVER khả dụng', effectiveAvailableSets],
            ].map(([label, value]) => (
              <div
                key={String(label)}
                className="rounded-xl border border-[#d9e8f2] bg-[#f8fbfd] p-3"
              >
                <span className="block text-xs font-semibold text-[#64758c]">{label}</span>
                <strong className="mt-1 block text-2xl text-[#0f2854]">{value}</strong>
              </div>
            ))}
          </div>
          <div className="mx-5 mb-4 rounded-xl border border-[#d9e8f2] bg-[#f8fbfd] px-4 py-3 text-xs font-semibold text-[#0f2854]">
            {`Danh sách mã bộ đề dự kiến được phân (${reservedSetCodes.length}): ${
              reservedSetCodes.length > 0 ? reservedSetCodes.join(', ') : 'Chưa có bộ đề dự kiến'
            }`}
          </div>
          {preview.globalErrors.length > 0 ? (
            <div className="mx-5 mb-4 grid gap-2 rounded-xl border border-[#f3c2c8] bg-[#fdecec] p-4 text-sm text-[#a43d4b]">
              {preview.globalErrors.map((issue, index) => (
                <span key={`${issue.code}-${index}`}>{issueMessage(issue)}</span>
              ))}
            </div>
          ) : null}
          <div className="overflow-x-auto px-5 pb-5">
            <table className="w-full min-w-[920px] border-collapse text-left text-sm">
              <thead>
                <tr className="border-b border-[#cbddeb] text-xs tracking-wide text-[#64758c] uppercase">
                  <th className="px-3 py-3">Dòng</th>
                  <th className="px-3 py-3">MSSV</th>
                  <th className="px-3 py-3">Họ tên</th>
                  <th className="px-3 py-3">Ghế</th>
                  <th className="px-3 py-3">IP</th>
                  <th className="px-3 py-3">Bộ đề dự kiến</th>
                  <th className="px-3 py-3">Trạng thái/lỗi</th>
                </tr>
              </thead>
              <tbody>
                {preview.rows.map((row) => {
                  const rowIssueTexts =
                    row.issues && row.issues.length > 0
                      ? row.issues
                      : (row.errors ?? []).map((issue) => issue.message)
                  const isRowValid = row.isValid && rowIssueTexts.length === 0
                  return (
                    <tr
                      key={row.sourceRow}
                      className={cn(
                        'border-b border-[#edf3f7] align-top',
                        isRowValid ? 'bg-white' : 'bg-[#fff5f6]',
                      )}
                    >
                      <td className="px-3 py-3 font-semibold text-[#64758c]">{row.sourceRow}</td>
                      <td className="px-3 py-3 font-semibold text-[#0f2854]">
                        {row.studentCode || '—'}
                      </td>
                      <td className="px-3 py-3 text-[#315b8f]">
                        {row.fullName || 'Không tra cứu được'}
                      </td>
                      <td className="px-3 py-3">{row.seatNumber ?? '—'}</td>
                      <td className="px-3 py-3">{row.ipAddress || '—'}</td>
                      <td className="px-3 py-3 font-semibold">{row.setCode || '—'}</td>
                      <td className="px-3 py-3">
                        {isRowValid ? (
                          <span className={ds.successChip}>
                            <CheckCircle2 className="mr-1 inline size-3" /> Hợp lệ
                          </span>
                        ) : (
                          <div className="grid gap-1 text-xs font-semibold text-[#a43d4b]">
                            <span>
                              <XCircle className="mr-1 inline size-3" /> Có lỗi
                            </span>
                            {rowIssueTexts.map((message, index) => (
                              <span key={`${row.sourceRow}-issue-${index}`}>{message}</span>
                            ))}
                          </div>
                        )}
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
          <div className="flex justify-end border-t border-[#e3edf4] p-5">
            <button
              type="button"
              onClick={() => setShowConfirm(true)}
              disabled={!canCommit}
              className={ds.primaryButton}
            >
              <TicketCheck className="size-4" />{' '}
              {isCommitting ? 'Đang phát vé...' : 'Xác nhận phát vé hàng loạt'}
            </button>
          </div>
        </section>
      ) : null}

      {showConfirm && preview ? (
        <FormDialog
          open
          align="center"
          size="md"
          eyebrow="XÁC NHẬN IMPORT ROSTER"
          title="3. Xác nhận phát vé"
          icon={TicketCheck}
          busy={isCommitting}
          onClose={() => !isCommitting && setShowConfirm(false)}
          footer={
            <DialogFooter className="justify-end bg-white">
              <button
                type="button"
                onClick={() => setShowConfirm(false)}
                disabled={isCommitting}
                className={ds.outlineButton}
              >
                Kiểm tra lại
              </button>
              <button
                type="button"
                onClick={() => void commitImport()}
                disabled={isCommitting}
                className={ds.primaryButton}
              >
                {isCommitting ? <LoaderCircle className="size-4 animate-spin" /> : null}
                {isCommitting ? 'Đang xử lý...' : 'Xác nhận import'}
              </button>
            </DialogFooter>
          }
        >
          <div className="rounded-xl border border-[#edca8c] bg-[#fff8ed] p-4 text-sm leading-6 text-[#8a5c1c]">
            <p>
              Hệ thống sẽ phát <strong>{preview.validRowCount} vé thi</strong> và phân{' '}
              <strong>{preview.validRowCount} bộ đề đã bàn giao</strong>.
            </p>
            <p className="mt-1 font-bold text-[#7f4d0b]">Thao tác không thể hoàn tác.</p>
          </div>
        </FormDialog>
      ) : null}

      {isRefreshingRoster ? (
        <div className="flex items-center gap-2 rounded-xl border border-[#d9e8f2] bg-white px-4 py-3 text-sm font-semibold text-[#5b6b84]">
          <LoaderCircle className="size-4 animate-spin" /> Đang làm mới danh sách vé và sức chứa ca
          thi…
        </div>
      ) : null}

      {attendance ? (
        <section className={sectionClassName}>
          <SectionHeader
            icon={TicketCheck}
            title="Danh sách vé vừa cập nhật"
            badge={`${attendance.assignedTicketsCount} vé`}
            hint="Dữ liệu được tải lại từ API điểm danh sau khi import thành công."
          />
          <div className="overflow-x-auto p-5">
            <table className="w-full min-w-[820px] border-collapse text-left text-sm">
              <thead>
                <tr className="border-b border-[#cbddeb] text-xs tracking-wide text-[#64758c] uppercase">
                  <th className="px-3 py-3">Ghế</th>
                  <th className="px-3 py-3">MSSV</th>
                  <th className="px-3 py-3">Họ tên</th>
                  <th className="px-3 py-3">IP máy</th>
                  <th className="px-3 py-3">Bộ đề được phân</th>
                  <th className="px-3 py-3">Trạng thái</th>
                </tr>
              </thead>
              <tbody>
                {attendance.seats
                  .filter((seat) => seat.isOccupied)
                  .map((seat) => {
                    const committedTicket = committedTicketBySeat.get(seat.seatNumber)
                    const assignedSetCode = seat.setCode || committedTicket?.setCode || '—'
                    return (
                      <tr key={seat.seatNumber} className="border-b border-[#edf3f7]">
                        <td className="px-3 py-3 font-semibold text-[#0f2854]">{seat.seatNumber}</td>
                        <td className="px-3 py-3 font-semibold text-[#0f2854]">
                          {seat.studentCode || '—'}
                        </td>
                        <td className="px-3 py-3 text-[#315b8f]">{seat.fullName || '—'}</td>
                        <td className="px-3 py-3 text-[#52657d]">{seat.ipAddress || '—'}</td>
                        <td className="px-3 py-3 font-semibold text-[#0f2854]">{assignedSetCode}</td>
                        <td className="px-3 py-3">
                          <span className={ds.infoChip}>{seat.status || 'SCHEDULED'}</span>
                        </td>
                      </tr>
                    )
                  })}
              </tbody>
            </table>
          </div>
        </section>
      ) : null}
    </div>
  )
}
