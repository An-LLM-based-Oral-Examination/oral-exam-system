import { useRef, useState, type ChangeEvent } from 'react'
import {
  AlertTriangle,
  CheckCircle2,
  Download,
  Eye,
  EyeOff,
  FileSpreadsheet,
  Loader2,
  RotateCcw,
  Upload,
  XCircle,
} from 'lucide-react'
import { FieldError } from '@/components/common/formField'
import { fieldInvalidClass } from '@/components/common/formFieldClasses'
import { toast } from '@/components/common/toastStore'
import { FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'
import { extractProblemDetails, type NormalizedProblem } from '@/lib/problemDetails'
import { cn } from '@/lib/utils'
import {
  adminService,
  type AdminImportPasswordSource,
  type AdminUserImportCommitResponse,
  type AdminUserImportPreviewResponse,
} from '@/services/admin.service'
import { getAdminRoleShortLabel } from '@/pages/admin/adminConstants'
import { AdminStateBanner } from '@/pages/admin/components/AdminStateBanner'
import { adminUi } from '@/pages/admin/components/adminUi'

const MAX_IMPORT_BYTES = 2 * 1024 * 1024
const ALLOWED_EXTENSIONS = ['.xlsx', '.csv'] as const

type PreviewFilter = 'all' | 'valid' | 'skipped'
type ResultTab = 'created' | 'skipped'

interface AdminImportUsersDialogProps {
  open: boolean
  onClose: () => void
  onImported: (createdCount: number, skippedCount: number) => void
}

function triggerBlobDownload(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = fileName
  document.body.appendChild(anchor)
  anchor.click()
  anchor.remove()
  window.setTimeout(() => URL.revokeObjectURL(url), 1000)
}

function decodeBase64ToBlob(base64: string, contentType: string): Blob {
  const binary = window.atob(base64)
  const bytes = new Uint8Array(binary.length)
  for (let i = 0; i < binary.length; i += 1) {
    bytes[i] = binary.charCodeAt(i)
  }
  return new Blob([bytes], { type: contentType })
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(2)} MB`
}

function formatPasswordSource(source: AdminImportPasswordSource): string {
  switch (source) {
    case 'ROW':
      return 'Từ file'
    case 'DEFAULT':
      return 'Mặc định chung'
    case 'AUTO_GENERATED':
      return 'Tự sinh ngẫu nhiên'
    default:
      return '—'
  }
}

export function AdminImportUsersDialog({
  open,
  onClose,
  onImported,
}: AdminImportUsersDialogProps) {
  const fileInputRef = useRef<HTMLInputElement | null>(null)

  const [selectedFile, setSelectedFile] = useState<File | null>(null)
  const [fileError, setFileError] = useState<string | undefined>(undefined)
  const [defaultPassword, setDefaultPassword] = useState('')
  const [showDefaultPassword, setShowDefaultPassword] = useState(false)
  const [defaultPasswordError, setDefaultPasswordError] = useState<string | undefined>(undefined)

  const [downloadingFormat, setDownloadingFormat] = useState<'xlsx' | 'csv' | null>(null)
  const [isPreviewing, setIsPreviewing] = useState(false)
  const [isCommitting, setIsCommitting] = useState(false)
  const [dialogProblem, setDialogProblem] = useState<NormalizedProblem | null>(null)

  const [preview, setPreview] = useState<AdminUserImportPreviewResponse | null>(null)
  const [previewFilter, setPreviewFilter] = useState<PreviewFilter>('all')

  const [commitResult, setCommitResult] = useState<AdminUserImportCommitResponse | null>(null)
  const [resultTab, setResultTab] = useState<ResultTab>('created')
  const [hasDownloadedResult, setHasDownloadedResult] = useState(false)
  const [showCreatedPasswords, setShowCreatedPasswords] = useState(false)

  const busy = isPreviewing || isCommitting

  const resetAllState = () => {
    setSelectedFile(null)
    setFileError(undefined)
    setDefaultPassword('')
    setShowDefaultPassword(false)
    setDefaultPasswordError(undefined)
    setDialogProblem(null)
    setPreview(null)
    setPreviewFilter('all')
    setCommitResult(null)
    setResultTab('created')
    setHasDownloadedResult(false)
    setShowCreatedPasswords(false)
    if (fileInputRef.current) {
      fileInputRef.current.value = ''
    }
  }

  const handleClose = () => {
    if (busy) return
    resetAllState()
    onClose()
  }

  const handleDownloadTemplate = async (format: 'xlsx' | 'csv') => {
    if (downloadingFormat) return
    setDownloadingFormat(format)
    setDialogProblem(null)
    try {
      const { blob, fileName } = await adminService.downloadUsersImportTemplate(format)
      triggerBlobDownload(blob, fileName)
      toast.success(`Đã tải file mẫu ${fileName}.`)
    } catch (err: unknown) {
      const parsed = extractProblemDetails(err, 'Không thể tải file mẫu import tài khoản.')
      setDialogProblem(parsed)
      toast.error(parsed.detail)
    } finally {
      setDownloadingFormat(null)
    }
  }

  const handleFileChange = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0] ?? null
    setDialogProblem(null)
    setPreview(null)
    setCommitResult(null)
    setHasDownloadedResult(false)

    if (!file) {
      setSelectedFile(null)
      setFileError(undefined)
      return
    }

    const lowerName = file.name.toLowerCase()
    const validExt = ALLOWED_EXTENSIONS.some((ext) => lowerName.endsWith(ext))
    if (!validExt) {
      setSelectedFile(null)
      setFileError('Định dạng không hỗ trợ. Vui lòng chọn tệp .xlsx hoặc .csv.')
      return
    }

    if (file.size === 0) {
      setSelectedFile(null)
      setFileError('Tệp đang rỗng (0 byte).')
      return
    }

    if (file.size > MAX_IMPORT_BYTES) {
      setSelectedFile(null)
      setFileError('Kích thước tệp vượt quá giới hạn tối đa 2 MB.')
      return
    }

    setSelectedFile(file)
    setFileError(undefined)
  }

  const validateInputs = (): boolean => {
    let valid = true
    if (!selectedFile) {
      setFileError('Vui lòng chọn tệp .xlsx hoặc .csv.')
      valid = false
    } else {
      setFileError(undefined)
    }

    if (defaultPassword.length > 0) {
      const trimmedStart = defaultPassword.replace(/^ +/, '')
      if (
        defaultPassword.trim().length === 0 ||
        defaultPassword.length < 8 ||
        defaultPassword.length > 128
      ) {
        setDefaultPasswordError(
          'Mật khẩu mặc định phải từ 8 đến 128 ký tự và không chỉ gồm khoảng trắng.',
        )
        valid = false
      } else if (/^[=+\-@\t\r\n]/.test(defaultPassword) || /^[=+\-@\t\r\n]/.test(trimmedStart)) {
        setDefaultPasswordError(
          "Mật khẩu mặc định không được bắt đầu bằng '=', '+', '-', '@' hoặc ký tự điều khiển.",
        )
        valid = false
      } else {
        setDefaultPasswordError(undefined)
      }
    } else {
      setDefaultPasswordError(undefined)
    }

    return valid
  }

  const handlePreview = async () => {
    if (busy || !validateInputs() || !selectedFile) return

    setIsPreviewing(true)
    setDialogProblem(null)
    setCommitResult(null)
    try {
      const response = await adminService.previewUsersImport(
        selectedFile,
        defaultPassword.length > 0 ? defaultPassword : undefined,
      )
      setPreview(response)
      setPreviewFilter('all')
    } catch (err: unknown) {
      const parsed = extractProblemDetails(err, 'Không thể kiểm tra tệp danh sách tài khoản.')
      setDialogProblem(parsed)
      toast.error(parsed.detail)
    } finally {
      setIsPreviewing(false)
    }
  }

  const handleCommit = async () => {
    if (busy || !selectedFile || !preview || preview.validCount === 0) return

    setIsCommitting(true)
    setDialogProblem(null)
    try {
      const response = await adminService.commitUsersImport(
        selectedFile,
        defaultPassword.length > 0 ? defaultPassword : undefined,
      )
      setCommitResult(response)
      setResultTab('created')
      setHasDownloadedResult(false)
      toast.success(
        `Đã tạo thành công ${response.createdCount} tài khoản${response.skippedCount > 0 ? ` (bỏ qua ${response.skippedCount} dòng)` : ''}.`,
      )
      onImported(response.createdCount, response.skippedCount)
    } catch (err: unknown) {
      const parsed = extractProblemDetails(err, 'Không thể hoàn tất import tài khoản.')
      setDialogProblem(parsed)
      toast.error(parsed.detail)
    } finally {
      setIsCommitting(false)
    }
  }

  const handleDownloadResultWorkbook = () => {
    if (!commitResult) return
    try {
      const blob = decodeBase64ToBlob(
        commitResult.resultFileBase64,
        commitResult.resultFileContentType ||
          'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      )
      triggerBlobDownload(blob, commitResult.resultFileName)
      setHasDownloadedResult(true)
      toast.success(`Đã tải file kết quả ${commitResult.resultFileName}.`)
    } catch {
      toast.error('Không thể giải mã tệp Excel kết quả. Vui lòng thử lại.')
    }
  }

  const filteredPreviewRows = (preview?.rows ?? []).filter((row) => {
    if (previewFilter === 'valid') return row.isValid
    if (previewFilter === 'skipped') return !row.isValid
    return true
  })

  const renderFooter = () => {
    if (commitResult) {
      return (
        <div className="flex w-full flex-col-reverse gap-2 sm:flex-row sm:items-center sm:justify-between">
          <button
            type="button"
            onClick={resetAllState}
            className={adminUi.btnSecondary}
          >
            <RotateCcw className="size-4" strokeWidth={1.75} aria-hidden="true" />
            Import file khác
          </button>
          <div className="flex flex-col-reverse gap-2 sm:flex-row">
            <button
              type="button"
              onClick={handleDownloadResultWorkbook}
              className={adminUi.btnPrimary}
            >
              <Download className="size-4" strokeWidth={1.75} aria-hidden="true" />
              {hasDownloadedResult ? 'Tải lại file kết quả (.xlsx)' : 'Tải file kết quả (.xlsx)'}
            </button>
            <button type="button" onClick={handleClose} className={adminUi.btnSecondary}>
              Hoàn tất &amp; Đóng
            </button>
          </div>
        </div>
      )
    }

    if (preview) {
      return (
        <div className="flex w-full flex-col-reverse gap-2 sm:flex-row sm:items-center sm:justify-between">
          <button
            type="button"
            disabled={busy}
            onClick={() => {
              setPreview(null)
              setDialogProblem(null)
            }}
            className={adminUi.btnSecondary}
          >
            <RotateCcw className="size-4" strokeWidth={1.75} aria-hidden="true" />
            Chọn lại file
          </button>
          <div className="flex flex-col-reverse gap-2 sm:flex-row">
            <button
              type="button"
              disabled={busy}
              onClick={handleClose}
              className={adminUi.btnSubtle}
            >
              Hủy
            </button>
            <button
              type="button"
              disabled={busy || preview.validCount === 0}
              onClick={() => void handleCommit()}
              className={adminUi.btnPrimary}
            >
              {isCommitting ? (
                <>
                  <Loader2 className="size-4 animate-spin motion-reduce:animate-none" aria-hidden="true" />
                  Đang tạo tài khoản…
                </>
              ) : (
                <>
                  <Upload className="size-4" strokeWidth={1.75} aria-hidden="true" />
                  Xác nhận import {preview.validCount} tài khoản hợp lệ
                </>
              )}
            </button>
          </div>
        </div>
      )
    }

    return (
      <div className="flex w-full flex-col-reverse gap-2 sm:flex-row sm:justify-end">
        <button type="button" disabled={busy} onClick={handleClose} className={adminUi.btnSubtle}>
          Hủy
        </button>
        <button
          type="button"
          disabled={busy || !selectedFile}
          onClick={() => void handlePreview()}
          className={adminUi.btnPrimary}
        >
          {isPreviewing ? (
            <>
              <Loader2 className="size-4 animate-spin motion-reduce:animate-none" aria-hidden="true" />
              Đang kiểm tra file…
            </>
          ) : (
            <>
              <FileSpreadsheet className="size-4" strokeWidth={1.75} aria-hidden="true" />
              Kiểm tra dữ liệu
            </>
          )}
        </button>
      </div>
    )
  }

  return (
    <FormDialog
      open={open}
      onClose={handleClose}
      busy={busy}
      variant="calm"
      size="2xl"
      icon={FileSpreadsheet}
      title="Nhập tài khoản hàng loạt từ Excel / CSV"
      footer={renderFooter()}
    >
      <div className="space-y-4">
        {dialogProblem ? (
          <AdminStateBanner
            problem={dialogProblem}
            onDismiss={() => setDialogProblem(null)}
          />
        ) : null}

        {/* STEP 3: COMMIT RESULT */}
        {commitResult ? (
          <div className="space-y-4">
            <div className="rounded-xl border border-emerald-300 bg-emerald-50 p-4 text-emerald-950">
              <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                <div className="flex items-start gap-2.5">
                  <CheckCircle2
                    className="mt-0.5 size-5 shrink-0 text-emerald-700"
                    strokeWidth={2}
                    aria-hidden="true"
                  />
                  <div>
                    <p className="text-sm font-bold text-emerald-950">
                      Import hoàn tất: đã tạo {commitResult.createdCount}/{commitResult.totalRows}{' '}
                      tài khoản
                    </p>
                    <p className="mt-0.5 text-xs text-emerald-900">
                      Mọi tài khoản mới đều đặt <strong>mustChangePassword = true</strong>. File kết
                      quả gồm 2 sheet: <code>TaiKhoanDaTao</code> và <code>DongBoQua</code>.
                    </p>
                  </div>
                </div>
                <button
                  type="button"
                  onClick={handleDownloadResultWorkbook}
                  className={cn(adminUi.btnPrimary, 'shrink-0')}
                >
                  <Download className="size-4" strokeWidth={1.75} aria-hidden="true" />
                  {hasDownloadedResult ? 'Đã tải — Tải lại (.xlsx)' : 'Tải file kết quả (.xlsx)'}
                </button>
              </div>
            </div>

            <div
              role="status"
              className="flex items-start gap-2.5 rounded-xl border border-amber-400 bg-[#fff8eb] p-3.5 text-xs leading-relaxed text-amber-950"
            >
              <AlertTriangle
                className="mt-0.5 size-4 shrink-0 text-amber-700"
                strokeWidth={2}
                aria-hidden="true"
              />
              <div>
                <p className="font-semibold">
                  Tải file kết quả ngay bây giờ — mật khẩu tạm không lưu lại trên máy chủ
                </p>
                <p className="mt-0.5 text-amber-900">
                  Hệ thống chỉ lưu mật khẩu đã băm (PBKDF2-SHA256) và không thể xuất lại danh sách
                  mật khẩu tạm sau khi bạn đóng hộp thoại này.
                </p>
              </div>
            </div>

            <div className="flex flex-wrap items-center justify-between gap-2">
              <div className="flex flex-wrap gap-2">
                <button
                  type="button"
                  onClick={() => setResultTab('created')}
                  className={
                    resultTab === 'created' ? adminUi.btnRowPrimary : adminUi.btnRowNeutral
                  }
                >
                  Tài khoản đã tạo ({commitResult.createdCount})
                </button>
                <button
                  type="button"
                  onClick={() => setResultTab('skipped')}
                  className={
                    resultTab === 'skipped' ? adminUi.btnRowDanger : adminUi.btnRowNeutral
                  }
                >
                  Dòng bỏ qua ({commitResult.skippedCount})
                </button>
              </div>

              {resultTab === 'created' && commitResult.createdRows.length > 0 ? (
                <button
                  type="button"
                  onClick={() => setShowCreatedPasswords((visible) => !visible)}
                  className={adminUi.btnRowNeutral}
                >
                  {showCreatedPasswords ? (
                    <>
                      <EyeOff className="size-3.5" aria-hidden="true" />
                      Ẩn mật khẩu tạm
                    </>
                  ) : (
                    <>
                      <Eye className="size-3.5" aria-hidden="true" />
                      Hiện mật khẩu tạm
                    </>
                  )}
                </button>
              ) : null}
            </div>

            {resultTab === 'created' ? (
              <div className="max-h-72 overflow-auto rounded-xl border border-slate-300 bg-white">
                <table className={cn(adminUi.table, 'min-w-[680px]')}>
                  <thead className={adminUi.thead}>
                    <tr>
                      <th className="px-3 py-2.5">Dòng</th>
                      <th className="px-3 py-2.5">Người dùng</th>
                      <th className="px-3 py-2.5">Vai trò</th>
                      <th className="px-3 py-2.5">MSSV / Username</th>
                      <th className="px-3 py-2.5">Mật khẩu tạm</th>
                    </tr>
                  </thead>
                  <tbody className={adminUi.tbody}>
                    {commitResult.createdRows.map((row) => (
                      <tr key={`${row.rowNumber}-${row.userId}`} className={adminUi.tr}>
                        <td className="px-3 py-2.5 font-mono text-xs text-slate-600">
                          {row.rowNumber}
                        </td>
                        <td className="px-3 py-2.5">
                          <p className="text-xs font-semibold text-[#0F2854]">{row.fullName}</p>
                          <p className="text-xs text-slate-600">{row.email}</p>
                        </td>
                        <td className="px-3 py-2.5">
                          <span className={adminUi.badgeInfo}>
                            {getAdminRoleShortLabel(row.role)}
                          </span>
                        </td>
                        <td className="px-3 py-2.5 font-mono text-xs text-[#0F2854]">
                          {row.studentCode ? (
                            <div>
                              <span className="font-semibold">{row.studentCode}</span>
                              {row.username ? (
                                <p className="text-slate-600">@{row.username}</p>
                              ) : null}
                            </div>
                          ) : row.username ? (
                            <span>@{row.username}</span>
                          ) : (
                            '—'
                          )}
                        </td>
                        <td className="px-3 py-2.5 font-mono text-xs font-semibold text-[#0F2854]">
                          {showCreatedPasswords ? row.temporaryPassword : '••••••••••••'}
                          <p className="font-sans text-[11px] font-normal text-slate-500">
                            {formatPasswordSource(row.passwordSource)}
                          </p>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : commitResult.skippedRows.length === 0 ? (
              <div className={adminUi.infoBox}>Không có dòng nào bị bỏ qua.</div>
            ) : (
              <div className="max-h-72 overflow-auto rounded-xl border border-slate-300 bg-white">
                <table className={cn(adminUi.table, 'min-w-[600px]')}>
                  <thead className={adminUi.thead}>
                    <tr>
                      <th className="px-3 py-2.5">Dòng</th>
                      <th className="px-3 py-2.5">Người dùng</th>
                      <th className="px-3 py-2.5">MSSV</th>
                      <th className="px-3 py-2.5">Lý do bỏ qua</th>
                    </tr>
                  </thead>
                  <tbody className={adminUi.tbody}>
                    {commitResult.skippedRows.map((row) => (
                      <tr key={`${row.rowNumber}-${row.email}`} className={adminUi.tr}>
                        <td className="px-3 py-2.5 font-mono text-xs text-slate-600">
                          {row.rowNumber}
                        </td>
                        <td className="px-3 py-2.5">
                          <p className="text-xs font-semibold text-[#0F2854]">
                            {row.fullName || '—'}
                          </p>
                          <p className="text-xs text-slate-600">{row.email || '—'}</p>
                        </td>
                        <td className="px-3 py-2.5 font-mono text-xs text-slate-700">
                          {row.studentCode || '—'}
                        </td>
                        <td className="px-3 py-2.5 text-xs font-medium text-rose-700">
                          {row.reason}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        ) : preview ? (
          /* STEP 2: PREVIEW VALIDATION */
          <div className="space-y-4">
            <div className="grid gap-3 sm:grid-cols-3">
              <div className={adminUi.subCard}>
                <p className="text-xs font-medium text-slate-600">Tổng số dòng đọc được</p>
                <p className="mt-1 text-2xl font-bold text-[#0F2854] tabular-nums">
                  {preview.totalRows}
                </p>
                <p className="mt-0.5 truncate text-xs text-slate-500">{preview.fileName}</p>
              </div>
              <div className="rounded-xl border border-emerald-300 bg-emerald-50 p-4">
                <p className="text-xs font-medium text-emerald-800">Hợp lệ (sẽ tạo mới)</p>
                <p className="mt-1 text-2xl font-bold text-emerald-900 tabular-nums">
                  {preview.validCount}
                </p>
                <p className="mt-0.5 text-xs text-emerald-800">Buộc đổi MK lần đầu</p>
              </div>
              <div className="rounded-xl border border-rose-300 bg-rose-50 p-4">
                <p className="text-xs font-medium text-rose-800">Lỗi / Trùng (sẽ bỏ qua)</p>
                <p className="mt-1 text-2xl font-bold text-rose-900 tabular-nums">
                  {preview.skippedCount}
                </p>
                <p className="mt-0.5 text-xs text-rose-800">
                  {preview.invalidCount} lỗi dữ liệu · {preview.duplicateCount} trùng lặp
                </p>
              </div>
            </div>

            {preview.skippedCount > 0 ? (
              <div className="rounded-xl border border-amber-300 bg-[#fff8eb] p-3 text-xs leading-relaxed text-amber-950">
                Các dòng lỗi hoặc trùng lặp sẽ được bỏ qua tự động khi bạn xác nhận import và được
                ghi đầy đủ trong sheet <strong>DongBoQua</strong> của file Excel kết quả.
              </div>
            ) : null}

            <div className="flex flex-wrap gap-2">
              <button
                type="button"
                onClick={() => setPreviewFilter('all')}
                className={
                  previewFilter === 'all' ? adminUi.btnRowPrimary : adminUi.btnRowNeutral
                }
              >
                Tất cả ({preview.totalRows})
              </button>
              <button
                type="button"
                onClick={() => setPreviewFilter('valid')}
                className={
                  previewFilter === 'valid' ? adminUi.btnRowSuccess : adminUi.btnRowNeutral
                }
              >
                Hợp lệ ({preview.validCount})
              </button>
              <button
                type="button"
                onClick={() => setPreviewFilter('skipped')}
                className={
                  previewFilter === 'skipped' ? adminUi.btnRowDanger : adminUi.btnRowNeutral
                }
              >
                Lỗi / Trùng ({preview.skippedCount})
              </button>
            </div>

            <div className="max-h-72 overflow-auto rounded-xl border border-slate-300 bg-white">
              <table className={cn(adminUi.table, 'min-w-[700px]')}>
                <thead className={adminUi.thead}>
                  <tr>
                    <th className="px-3 py-2.5">Dòng</th>
                    <th className="px-3 py-2.5">Người dùng</th>
                    <th className="px-3 py-2.5">Vai trò</th>
                    <th className="px-3 py-2.5">MSSV / Username dự kiến</th>
                    <th className="px-3 py-2.5">Nguồn MK</th>
                    <th className="px-3 py-2.5">Trạng thái</th>
                  </tr>
                </thead>
                <tbody className={adminUi.tbody}>
                  {filteredPreviewRows.length === 0 ? (
                    <tr>
                      <td colSpan={6} className="px-4 py-6 text-center text-xs text-slate-600">
                        Không có dòng dữ liệu nào phù hợp với bộ lọc đang chọn.
                      </td>
                    </tr>
                  ) : (
                    filteredPreviewRows.map((row) => (
                    <tr key={`${row.rowNumber}-${row.email}`} className={adminUi.tr}>
                      <td className="px-3 py-2.5 font-mono text-xs text-slate-600">
                        {row.rowNumber}
                      </td>
                      <td className="px-3 py-2.5">
                        <p className="text-xs font-semibold text-[#0F2854]">
                          {row.fullName || '—'}
                        </p>
                        <p className="text-xs text-slate-600">{row.email || '—'}</p>
                      </td>
                      <td className="px-3 py-2.5">
                        {row.role ? (
                          <span className={adminUi.badgeInfo}>
                            {getAdminRoleShortLabel(row.role)}
                          </span>
                        ) : (
                          '—'
                        )}
                      </td>
                      <td className="px-3 py-2.5 font-mono text-xs text-[#0F2854]">
                        {row.studentCode ? (
                          <div>
                            <span className="font-semibold">{row.studentCode}</span>
                            {row.username ? (
                              <p className="text-slate-600">@{row.username}</p>
                            ) : null}
                          </div>
                        ) : row.username ? (
                          <span>@{row.username}</span>
                        ) : (
                          '—'
                        )}
                      </td>
                      <td className="px-3 py-2.5 text-xs text-slate-700">
                        {formatPasswordSource(row.passwordSource)}
                      </td>
                      <td className="px-3 py-2.5">
                        {row.isValid ? (
                          <span className={adminUi.badgeSuccess}>
                            <CheckCircle2 className="size-3" aria-hidden="true" />
                            Hợp lệ
                          </span>
                        ) : (
                          <div className="space-y-1">
                            <span
                              className={
                                row.status === 'DUPLICATE'
                                  ? adminUi.badgeWarning
                                  : adminUi.badgeDanger
                              }
                            >
                              <XCircle className="size-3" aria-hidden="true" />
                              {row.status === 'DUPLICATE' ? 'Trùng lặp' : 'Lỗi dữ liệu'}
                            </span>
                            {row.errorMessage ? (
                              <p className="text-[11px] leading-snug font-medium text-rose-700">
                                {row.errorMessage}
                              </p>
                            ) : null}
                          </div>
                        )}
                      </td>
                    </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>
        ) : (
          /* STEP 1: SELECT FILE & OPTIONS */
          <div className="space-y-4">
            <div className={adminUi.infoBox}>
              <p className="font-semibold text-[#0F2854]">
                Quy chuẩn file import (.xlsx hoặc .csv — tối đa 2 MB, 500 dòng/lần)
              </p>
              <ul className="mt-1.5 list-disc space-y-1 pl-4">
                <li>
                  Cột bắt buộc: <code>email</code>, <code>full_name</code>, <code>role</code>. Cột
                  tùy chọn: <code>student_code</code> (bắt buộc với <code>student</code>),{' '}
                  <code>username</code>, <code>password</code>.
                </li>
                <li>
                  Sinh viên tự động sinh Username theo chuẩn FPT (VD:{' '}
                  <strong>Nguyễn Quang Thành</strong> + <code>SE184236</code> →{' '}
                  <strong className="font-mono text-[#0F2854]">ThanhNQSE184236</strong>), đăng nhập
                  không phân biệt chữ hoa/thường.
                </li>
                <li>
                  Không hỗ trợ import vai trò <code>admin</code>. Sau khi xác nhận, hệ thống trả về{' '}
                  <strong>file Excel kết quả mới (.xlsx)</strong> chứa danh sách mật khẩu tạm và
                  các dòng bỏ qua.
                </li>
              </ul>
            </div>

            <div className="flex flex-wrap items-center gap-2">
              <button
                type="button"
                disabled={Boolean(downloadingFormat)}
                onClick={() => void handleDownloadTemplate('xlsx')}
                className={adminUi.btnSecondary}
              >
                {downloadingFormat === 'xlsx' ? (
                  <Loader2 className="size-4 animate-spin motion-reduce:animate-none" aria-hidden="true" />
                ) : (
                  <Download className="size-4" strokeWidth={1.75} aria-hidden="true" />
                )}
                Tải file mẫu (.xlsx)
              </button>
              <button
                type="button"
                disabled={Boolean(downloadingFormat)}
                onClick={() => void handleDownloadTemplate('csv')}
                className={adminUi.btnSecondary}
              >
                {downloadingFormat === 'csv' ? (
                  <Loader2 className="size-4 animate-spin motion-reduce:animate-none" aria-hidden="true" />
                ) : (
                  <Download className="size-4" strokeWidth={1.75} aria-hidden="true" />
                )}
                Tải file mẫu (.csv)
              </button>
            </div>

            <div>
              <label htmlFor="admin-import-users-file" className={adminUi.label}>
                Chọn tệp danh sách tài khoản (.xlsx / .csv)
              </label>
              <div
                className={cn(
                  'mt-1.5 rounded-xl border border-dashed border-slate-400 bg-[#f8fbfe] p-4 transition-colors',
                  fileError && 'border-rose-400 bg-rose-50/40',
                )}
              >
                <input
                  ref={fileInputRef}
                  id="admin-import-users-file"
                  type="file"
                  accept=".xlsx,.csv"
                  onChange={handleFileChange}
                  aria-invalid={Boolean(fileError)}
                  className="block w-full text-sm text-[#0F2854] file:mr-3 file:min-h-10 file:cursor-pointer file:rounded-lg file:border file:border-[#94b8da] file:bg-white file:px-3.5 file:py-1.5 file:text-xs file:font-semibold file:text-[#1C4D8D] hover:file:bg-[#edf5fd]"
                />
                {selectedFile ? (
                  <p className="mt-2 text-xs font-medium text-emerald-800">
                    Đã chọn: <strong className="font-semibold">{selectedFile.name}</strong> (
                    {formatFileSize(selectedFile.size)})
                  </p>
                ) : (
                  <p className="mt-2 text-xs text-slate-600">
                    Hỗ trợ Excel <code>.xlsx</code> hoặc <code>.csv</code> UTF-8. Dòng đầu tiên là
                    tiêu đề cột.
                  </p>
                )}
              </div>
              <FieldError message={fileError} />
            </div>

            <div>
              <label htmlFor="admin-import-default-password" className={adminUi.label}>
                Mật khẩu tạm mặc định chung (tùy chọn)
              </label>
              <div className="relative mt-1.5">
                <input
                  id="admin-import-default-password"
                  type={showDefaultPassword ? 'text' : 'password'}
                  value={defaultPassword}
                  onChange={(event) => {
                    setDefaultPassword(event.target.value)
                    setDefaultPasswordError(undefined)
                  }}
                  placeholder="Để trống để hệ thống tự sinh mật khẩu ngẫu nhiên mạnh 12 ký tự…"
                  aria-invalid={Boolean(defaultPasswordError)}
                  className={cn(
                    ds.field,
                    adminUi.input,
                    'pr-11',
                    defaultPasswordError && fieldInvalidClass,
                  )}
                />
                <button
                  type="button"
                  aria-label={showDefaultPassword ? 'Ẩn mật khẩu mặc định' : 'Hiện mật khẩu mặc định'}
                  aria-pressed={showDefaultPassword}
                  onClick={() => setShowDefaultPassword((visible) => !visible)}
                  className="absolute top-1/2 right-1.5 flex size-9 -translate-y-1/2 items-center justify-center rounded-lg text-slate-600 transition-colors hover:bg-[#E7F3FB] hover:text-[#1C4D8D] focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
                >
                  {showDefaultPassword ? (
                    <EyeOff className="size-4" aria-hidden="true" />
                  ) : (
                    <Eye className="size-4" aria-hidden="true" />
                  )}
                </button>
              </div>
              <p className={adminUi.helperText}>
                Thứ tự áp dụng cho mỗi dòng: (1) cột <code>password</code> trong file → (2) mật
                khẩu mặc định chung ở đây (8–128 ký tự) → (3) tự sinh mật khẩu ngẫu nhiên 12 ký tự.
              </p>
              <FieldError message={defaultPasswordError} />
            </div>
          </div>
        )}
      </div>
    </FormDialog>
  )
}
