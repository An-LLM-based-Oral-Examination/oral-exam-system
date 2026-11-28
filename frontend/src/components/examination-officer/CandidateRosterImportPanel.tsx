import { useCallback, useEffect, useRef, useState, type DragEvent } from 'react'
import {
  CheckCircle2,
  ClipboardCheck,
  Download,
  FileSpreadsheet,
  LoaderCircle,
  Search,
  Trash2,
  UploadCloud,
  UsersRound,
  XCircle,
} from 'lucide-react'
import { officialExamService } from '@/services/officialExam.service'
import type {
  CandidateImportMode,
  CandidateImportPreviewResponse,
  ExamAttemptType,
  SessionCandidateItem,
  SessionCandidatesResponse,
} from '@/types/officialExam.types'
import { SectionHeader } from '@/design/ui'
import { ConfirmDialog } from '@/design/ConfirmDialog'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { EmptyState } from '@/components/common/EmptyState'
import { ListPagination } from '@/components/common/ListPagination'
import { toast } from '@/components/common/toastStore'
import { userMessage } from '@/lib/userMessage'

const MAX_CANDIDATE_FILE_BYTES = 2 * 1024 * 1024
const ACCEPTED_EXTENSIONS = ['.csv', '.xlsx']

interface CandidateRosterImportPanelProps {
  sessionId: string
  examAttemptType: ExamAttemptType
  onRosterChanged: () => Promise<void> | void
}

function triggerDownload(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = fileName
  anchor.click()
  URL.revokeObjectURL(url)
}

export function CandidateRosterImportPanel({
  sessionId,
  examAttemptType,
  onRosterChanged,
}: CandidateRosterImportPanelProps) {
  const fileInputRef = useRef<HTMLInputElement>(null)
  const loadEpochRef = useRef(0)
  const [selectedFile, setSelectedFile] = useState<File | null>(null)
  const [mode, setMode] = useState<CandidateImportMode>('REPLACE')
  const [preview, setPreview] = useState<CandidateImportPreviewResponse | null>(null)
  const [candidatesPage, setCandidatesPage] = useState<SessionCandidatesResponse | null>(null)
  const [search, setSearch] = useState('')
  const [pageNumber, setPageNumber] = useState(1)
  const [loadingList, setLoadingList] = useState(true)
  const [downloadingFormat, setDownloadingFormat] = useState<'csv' | 'xlsx' | null>(null)
  const [previewing, setPreviewing] = useState(false)
  const [committing, setCommitting] = useState(false)
  const [showCommitConfirm, setShowCommitConfirm] = useState(false)
  const [removingCandidate, setRemovingCandidate] = useState<SessionCandidateItem | null>(null)
  const [removingBusy, setRemovingBusy] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')

  const loadCandidates = useCallback(async () => {
    const epoch = ++loadEpochRef.current
    if (!sessionId || typeof officialExamService.getSessionCandidates !== 'function') {
      setLoadingList(false)
      return
    }
    setLoadingList(true)
    try {
      const response = await officialExamService.getSessionCandidates(sessionId, {
        search: search.trim() || undefined,
        pageNumber,
        pageSize: 100,
      })
      if (epoch !== loadEpochRef.current) return
      setCandidatesPage(response)
    } catch (loadError) {
      if (epoch !== loadEpochRef.current) return
      setError(userMessage(loadError, 'Không tải được danh sách thí sinh đã import.'))
    } finally {
      if (epoch === loadEpochRef.current) {
        setLoadingList(false)
      }
    }
  }, [pageNumber, search, sessionId])

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadCandidates()
    }, 0)
    return () => {
      window.clearTimeout(timer)
      loadEpochRef.current += 1
    }
  }, [loadCandidates])

  const selectFile = (file: File | undefined) => {
    if (!file || previewing || committing) return
    const dotIndex = file.name.lastIndexOf('.')
    const ext = dotIndex >= 0 ? file.name.slice(dotIndex).toLowerCase() : ''
    if (!ACCEPTED_EXTENSIONS.includes(ext)) {
      setSelectedFile(null)
      setPreview(null)
      setError('Chỉ hỗ trợ file danh sách thí sinh định dạng .xlsx hoặc .csv.')
      return
    }
    if (file.size > MAX_CANDIDATE_FILE_BYTES) {
      setSelectedFile(null)
      setPreview(null)
      setError('Dung lượng file danh sách thí sinh không được vượt quá 2 MB.')
      return
    }
    setSelectedFile(file)
    setPreview(null)
    setError('')
    setNotice('')
  }

  const handleDrop = (event: DragEvent<HTMLDivElement>) => {
    event.preventDefault()
    selectFile(event.dataTransfer.files?.[0])
  }

  const handleDownloadTemplate = async (format: 'csv' | 'xlsx') => {
    if (downloadingFormat || committing) return
    setDownloadingFormat(format)
    setError('')
    try {
      const file = await officialExamService.downloadCandidateTemplate(format)
      triggerDownload(file.blob, file.fileName)
      toast.success(`Đã tải file mẫu DSSV (${format.toUpperCase()}).`)
    } catch (downloadError) {
      const message = userMessage(downloadError, 'Không thể tải file mẫu danh sách thí sinh.')
      setError(message)
      toast.error(message)
    } finally {
      setDownloadingFormat(null)
    }
  }

  const handlePreview = async () => {
    if (!selectedFile || previewing || committing) return
    setPreviewing(true)
    setError('')
    setNotice('')
    try {
      const result = await officialExamService.previewCandidateImport(sessionId, selectedFile, mode)
      setPreview(result)
    } catch (previewError) {
      setPreview(null)
      setError(userMessage(previewError, 'Không thể kiểm tra file danh sách thí sinh.'))
    } finally {
      setPreviewing(false)
    }
  }

  const handleCommit = async () => {
    if (!selectedFile || !preview?.canCommit || committing) return
    setCommitting(true)
    setError('')
    try {
      const result = await officialExamService.commitCandidateImport(sessionId, selectedFile, mode)
      setShowCommitConfirm(false)
      setSelectedFile(null)
      setPreview(null)
      if (fileInputRef.current) fileInputRef.current.value = ''
      const successMsg = `Đã lưu ${result.importedCount} thí sinh vào đợt ${result.examAttemptType} (tổng ${result.totalCandidateCount} SV · đề xuất ${result.recommendedTotalRooms} phòng).`
      setNotice(successMsg)
      toast.success(successMsg)
      if (pageNumber !== 1) {
        setPageNumber(1)
      } else {
        await loadCandidates()
      }
      await Promise.resolve(onRosterChanged())
    } catch (commitError) {
      setShowCommitConfirm(false)
      const message = userMessage(commitError, 'Không thể lưu danh sách thí sinh dự thi.')
      setError(message)
      toast.error(message)
    } finally {
      setCommitting(false)
    }
  }

  const handleConfirmRemove = async () => {
    if (!removingCandidate || removingBusy) return
    setRemovingBusy(true)
    setError('')
    try {
      await officialExamService.removeSessionCandidate(sessionId, removingCandidate.studentId)
      toast.success(`Đã xóa thí sinh ${removingCandidate.studentCode} khỏi đợt ${examAttemptType}.`)
      setRemovingCandidate(null)
      await loadCandidates()
      await Promise.resolve(onRosterChanged())
    } catch (removeError) {
      setRemovingCandidate(null)
      const message = userMessage(
        removeError,
        'Không thể xóa thí sinh (có thể thí sinh đã được cấp vé thi).',
      )
      setError(message)
      toast.error(message)
    } finally {
      setRemovingBusy(false)
    }
  }

  const items = candidatesPage?.items ?? []

  return (
    <div className="space-y-4">
      <section className={ds.section} aria-labelledby="eo-step-2-import-heading">
        <SectionHeader
          icon={UploadCloud}
          title={`Bước 2 · Import Danh sách Thí sinh dự thi (${examAttemptType})`}
          badge={`${candidatesPage?.totalCount ?? 0} thí sinh`}
          hint="Chỉ cần cột MSSV (studentCode) — tối đa 2 MB (<= 2.000 dòng). Không cần số ghế hay mã thiết bị BYOD."
          action={
            <div className="flex flex-wrap gap-2">
              <button
                type="button"
                className={ds.outlineButton}
                disabled={downloadingFormat !== null}
                onClick={() => void handleDownloadTemplate('xlsx')}
              >
                {downloadingFormat === 'xlsx' ? (
                  <LoaderCircle className="size-4 animate-spin" />
                ) : (
                  <FileSpreadsheet className="size-4" />
                )}
                Tải mẫu XLSX
              </button>
              <button
                type="button"
                className={ds.outlineButton}
                disabled={downloadingFormat !== null}
                onClick={() => void handleDownloadTemplate('csv')}
              >
                {downloadingFormat === 'csv' ? (
                  <LoaderCircle className="size-4 animate-spin" />
                ) : (
                  <Download className="size-4" />
                )}
                Tải mẫu CSV
              </button>
            </div>
          }
        />

        <div className="space-y-4 p-5">
          {error ? (
            <div
              role="alert"
              className="rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-4 py-3 text-sm font-semibold text-[#a43d4b]"
            >
              {error}
            </div>
          ) : null}

          {notice ? (
            <div
              role="status"
              className="flex items-center gap-2 rounded-xl border border-[#b7e4cf] bg-[#effaf6] px-4 py-3 text-sm font-semibold text-[#176f58]"
            >
              <CheckCircle2 className="size-4 shrink-0" aria-hidden="true" />
              <span>{notice}</span>
            </div>
          ) : null}

          <div className="flex flex-wrap items-center gap-4">
            <span className="text-xs font-bold text-[#52657d] uppercase">Chế độ import:</span>
            <label className="inline-flex items-center gap-2 text-xs font-semibold text-[#0F2854]">
              <input
                type="radio"
                name={`candidate-import-mode-${sessionId}`}
                value="REPLACE"
                checked={mode === 'REPLACE'}
                onChange={() => {
                  setMode('REPLACE')
                  setPreview(null)
                }}
              />
              Thay thế toàn bộ (REPLACE)
            </label>
            <label className="inline-flex items-center gap-2 text-xs font-semibold text-[#0F2854]">
              <input
                type="radio"
                name={`candidate-import-mode-${sessionId}`}
                value="APPEND"
                checked={mode === 'APPEND'}
                onChange={() => {
                  setMode('APPEND')
                  setPreview(null)
                }}
              />
              Bổ sung thêm thí sinh (APPEND)
            </label>
          </div>

          <div
            className={cn(
              'rounded-xl border border-dashed border-[#82aedb] bg-[#f7fbff] p-6 text-center',
              (previewing || committing) && 'pointer-events-none opacity-60',
            )}
            onDragOver={(event) => event.preventDefault()}
            onDrop={handleDrop}
          >
            <input
              ref={fileInputRef}
              id={`candidate-roster-input-${sessionId}`}
              aria-label="Chọn file danh sách thí sinh (Excel/CSV)"
              type="file"
              accept=".csv,.xlsx"
              className="sr-only"
              onChange={(event) => selectFile(event.target.files?.[0])}
            />
            <label
              htmlFor={`candidate-roster-input-${sessionId}`}
              className="mx-auto flex max-w-lg cursor-pointer flex-col items-center gap-2 text-[#1C4D8D]"
            >
              <UploadCloud className="size-8" aria-hidden="true" />
              <strong className="text-sm">Kéo thả hoặc chọn file DSSV (.xlsx / .csv)</strong>
              <span className="text-xs text-[#5b6b84]">
                Chỉ cần cột <code>studentCode</code> / <code>MSSV</code> (đã lọc SV cấm thi cho FE
                hoặc SV thi lại cho RE) · Tối đa 2 MB
              </span>
            </label>
          </div>

          {selectedFile ? (
            <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] px-4 py-3">
              <div>
                <strong className="block text-sm text-[#0F2854]">{selectedFile.name}</strong>
                <span className="text-xs text-[#64758c]">
                  {Math.ceil(selectedFile.size / 1024)} KB · Chế độ {mode}
                </span>
              </div>
              <button
                type="button"
                disabled={previewing || committing}
                onClick={() => void handlePreview()}
                className={ds.primaryButton}
              >
                {previewing ? (
                  <LoaderCircle className="size-4 animate-spin" />
                ) : (
                  <ClipboardCheck className="size-4" />
                )}
                Kiểm tra file (Preview)
              </button>
            </div>
          ) : null}

          {preview ? (
            <div className="space-y-4 rounded-2xl border border-[#82aedb]/55 bg-white p-4">
              <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
                {[
                  ['Tổng dòng đọc', preview.totalRows],
                  ['Hợp lệ', preview.validCount],
                  ['Không hợp lệ', preview.invalidCount],
                  ['Trùng lặp', preview.duplicateCount],
                  ['Đề xuất phòng', `${preview.recommendedTotalRooms} phòng`],
                ].map(([label, value]) => (
                  <div
                    key={String(label)}
                    className="rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] px-3 py-2.5"
                  >
                    <span className="block text-[11px] font-bold text-[#5b6b84] uppercase">
                      {label}
                    </span>
                    <strong className="mt-1 block font-mono text-lg font-black text-[#0F2854]">
                      {value}
                    </strong>
                  </div>
                ))}
              </div>

              {preview.issues.length > 0 ? (
                <div
                  role="alert"
                  className="space-y-1 rounded-xl border border-[#f3c2c8] bg-[#fdecec] p-3 text-xs font-semibold text-[#a43d4b]"
                >
                  {preview.issues.map((issue, index) => (
                    <p key={`${issue.sourceRow ?? 'global'}-${index}`}>
                      {issue.sourceRow ? `Dòng ${issue.sourceRow}: ` : ''}
                      {issue.message}
                    </p>
                  ))}
                </div>
              ) : null}

              <div className="max-h-72 overflow-auto rounded-xl border border-[#d9e8f2]">
                <table className="w-full min-w-[680px] text-left text-xs">
                  <thead className="bg-[#F4FBFD] font-bold text-[#5b6b84] uppercase">
                    <tr>
                      <th className="px-3 py-2.5">Dòng</th>
                      <th className="px-3 py-2.5">MSSV</th>
                      <th className="px-3 py-2.5">Họ tên</th>
                      <th className="px-3 py-2.5">Trạng thái</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[#edf3f7]">
                    {preview.rows.map((row) => (
                      <tr
                        key={`${row.sourceRow}-${row.studentCode}`}
                        className={row.isValid ? 'bg-white' : 'bg-[#fff5f6]'}
                      >
                        <td className="px-3 py-2 font-mono text-[#64758c]">{row.sourceRow}</td>
                        <td className="px-3 py-2 font-mono font-bold text-[#0F2854]">
                          {row.studentCode || '—'}
                        </td>
                        <td className="px-3 py-2 text-[#52657d]">{row.fullName || '—'}</td>
                        <td className="px-3 py-2">
                          {row.isValid ? (
                            <span className={ds.successChip}>
                              <CheckCircle2 className="mr-1 inline size-3" /> Hợp lệ
                              {!row.isEnrolledInSemester ? ' (Ngoài lớp kỳ này)' : ''}
                            </span>
                          ) : (
                            <div className="space-y-0.5 font-semibold text-[#a43d4b]">
                              <span className="inline-flex items-center gap-1">
                                <XCircle className="size-3" /> Lỗi
                              </span>
                              {row.issues.map((msg, idx) => (
                                <p key={`${row.sourceRow}-${idx}`}>{msg}</p>
                              ))}
                            </div>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              <div className="flex justify-end">
                <button
                  type="button"
                  disabled={!preview.canCommit || committing}
                  onClick={() => setShowCommitConfirm(true)}
                  className={ds.primaryButton}
                >
                  <CheckCircle2 className="size-4" /> Xác nhận lưu {preview.validCount} thí sinh
                </button>
              </div>
            </div>
          ) : null}
        </div>
      </section>

      <section className={ds.section} aria-label="Danh sách thí sinh đã import">
        <SectionHeader
          icon={UsersRound}
          title="Danh sách thí sinh trong đợt thi"
          badge={`${candidatesPage?.allocatedCount ?? 0}/${candidatesPage?.totalCount ?? 0} đã xếp phòng`}
          hint="Tìm kiếm MSSV/họ tên hoặc gỡ thí sinh chưa xếp phòng."
          action={
            <label className="relative block w-64">
              <span className="sr-only">Tìm kiếm thí sinh theo MSSV hoặc họ tên</span>
              <Search
                className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-[#687a91]"
                aria-hidden="true"
              />
              <input
                type="search"
                className={cn(ds.field, 'h-9 pl-9 text-xs')}
                placeholder="Tìm MSSV hoặc họ tên…"
                value={search}
                onChange={(event) => {
                  setSearch(event.target.value)
                  setPageNumber(1)
                }}
              />
            </label>
          }
        />

        {loadingList ? (
          <div className="flex items-center justify-center gap-2 p-8 text-xs font-semibold text-[#5b6b84]">
            <LoaderCircle className="size-4 animate-spin text-[#1C4D8D]" />
            Đang tải danh sách thí sinh…
          </div>
        ) : items.length === 0 ? (
          <div className="p-5">
            <EmptyState
              compact
              icon={UsersRound}
              title="Chưa có thí sinh nào"
              description="Tải file danh sách MSSV ở phía trên để nạp thí sinh cho đợt thi này."
            />
          </div>
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="w-full min-w-[740px] text-left text-sm">
                <thead className="bg-[#F4FBFD] text-xs font-bold text-[#5b6b84] uppercase">
                  <tr>
                    <th className="px-5 py-3">MSSV</th>
                    <th className="px-5 py-3">Họ tên</th>
                    <th className="px-5 py-3">Phân bổ phòng / ghế</th>
                    <th className="px-5 py-3 text-right">Thao tác</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[#d9e8f2]">
                  {items.map((candidate) => (
                    <tr key={candidate.studentId} className="hover:bg-[#F8FBFD]">
                      <td className="px-5 py-3 font-mono font-bold text-[#1C4D8D]">
                        {candidate.studentCode}
                      </td>
                      <td className="px-5 py-3">
                        <strong className="block text-[#0F2854]">{candidate.fullName}</strong>
                        {candidate.email ? (
                          <span className="text-xs text-[#5b6b84]">{candidate.email}</span>
                        ) : null}
                      </td>
                      <td className="px-5 py-3">
                        {candidate.isAllocated ? (
                          <span className={ds.successChip}>
                            {candidate.roomLab || 'Đã xếp phòng'} · Ghế #{candidate.seatNumber ?? '—'}
                          </span>
                        ) : (
                          <span className={ds.infoChip}>Chưa xếp phòng</span>
                        )}
                      </td>
                      <td className="px-5 py-3 text-right">
                        <button
                          type="button"
                          disabled={candidate.isAllocated || removingBusy}
                          onClick={() => setRemovingCandidate(candidate)}
                          className="inline-flex items-center gap-1 rounded-lg border border-[#f3c2c8] bg-white px-2.5 py-1.5 text-xs font-bold text-[#a43d4b] transition hover:bg-[#fff5f6] disabled:cursor-not-allowed disabled:opacity-45"
                        >
                          <Trash2 className="size-3.5" /> Gỡ
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {(() => {
              const totalCount = candidatesPage?.totalCount ?? items.length
              const pageSize = candidatesPage?.pageSize || 100
              const pageCount = Math.max(1, Math.ceil(totalCount / pageSize))
              if (pageCount <= 1) return null
              return (
                <div className="flex flex-col gap-2 border-t border-[#d9e8f2] bg-[#F8FBFD] sm:flex-row sm:items-center sm:justify-between">
                  <p className="px-5 pt-3 text-xs font-medium text-[#5b6b84] sm:py-3">
                    Hiển thị {items.length}/{totalCount} thí sinh · Trang {pageNumber}/{pageCount}
                  </p>
                  <ListPagination
                    label="Phân trang danh sách thí sinh"
                    page={pageNumber}
                    pageCount={pageCount}
                    onPageChange={setPageNumber}
                  />
                </div>
              )
            })()}
          </>
        )}
      </section>

      {showCommitConfirm && preview ? (
        <ConfirmDialog
          variant="calm"
          tone={mode === 'REPLACE' ? 'warning' : 'info'}
          eyebrow="XÁC NHẬN IMPORT DSSV"
          title={`Lưu ${preview.validCount} thí sinh theo chế độ ${mode}?`}
          message={`Sau khi lưu, đợt ${preview.examAttemptType} sẽ có dự kiến ${preview.projectedTotalCandidates} thí sinh (đề xuất ${preview.recommendedTotalRooms} phòng chuẩn ${preview.maxStudentsPerRoom} SV/phòng).`}
          confirmLabel="Xác nhận lưu danh sách"
          cancelLabel="Kiểm tra lại"
          pending={committing}
          onConfirm={() => void handleCommit()}
          onCancel={() => !committing && setShowCommitConfirm(false)}
        />
      ) : null}

      {removingCandidate ? (
        <ConfirmDialog
          variant="calm"
          tone="danger"
          eyebrow="GỠ THÍ SINH KHỎI ĐỢT THI"
          title={`Gỡ thí sinh ${removingCandidate.studentCode}?`}
          message={`Xóa ${removingCandidate.fullName} (${removingCandidate.studentCode}) khỏi danh sách thí sinh đợt ${examAttemptType}.`}
          confirmLabel="Xác nhận gỡ"
          cancelLabel="Hủy"
          pending={removingBusy}
          onConfirm={() => void handleConfirmRemove()}
          onCancel={() => !removingBusy && setRemovingCandidate(null)}
        />
      ) : null}
    </div>
  )
}
