const STATUS_PREFIX =
  /^(VALIDATION|CONFLICT|BAD[\s_]*REQUEST|UNAUTHORIZED|FORBIDDEN|NOT[\s_]*FOUND|INTERNAL[\s_]*SERVER[\s_]*ERROR|ERROR|WARNING|INFO)\s*[:\-–—]\s*/i

const TECHNICAL =
  /\b(?:42[0-9A-Z]{3}|22[0-9]{3}|235\d{2}|42703)\b|column\s+.+\s+does not exist|does not exist|POSITION:\s*\d+|Npgsql|PostgresException|EntityFrameworkCore|SqlException|System\.\w*Exception|at\s+\S+\.cs:line\s+\d+/i

export const SHIFT_CAPACITY_BELOW_ALLOCATION_MESSAGE =
  'Không thể giảm sức chứa thấp hơn số vé hoặc số ghế cao nhất đã cấp.'

export function extractProblemErrorCode(error: unknown): string | null {
  if (typeof error === 'object' && error !== null) {
    if ('code' in error && typeof error.code === 'string' && error.code.trim()) {
      return error.code.trim()
    }
    if ('errorCode' in error && typeof error.errorCode === 'string' && error.errorCode.trim()) {
      return error.errorCode.trim()
    }
  }
  return null
}

function readDetail(error: unknown) {
  const code = extractProblemErrorCode(error)
  if (code === 'SHIFT_CAPACITY_BELOW_ALLOCATION') {
    return SHIFT_CAPACITY_BELOW_ALLOCATION_MESSAGE
  }
  if (typeof error === 'string') {
    const trimmed = error.trim()
    if (
      trimmed === 'SHIFT_CAPACITY_BELOW_ALLOCATION' ||
      trimmed.includes('Sức chứa mới không được thấp hơn số vé hoặc số ghế cao nhất đã cấp')
    ) {
      return SHIFT_CAPACITY_BELOW_ALLOCATION_MESSAGE
    }
    return trimmed
  }
  if (typeof error === 'object' && error !== null) {
    if ('detail' in error && typeof error.detail === 'string') {
      const trimmed = error.detail.trim()
      if (
        trimmed === 'SHIFT_CAPACITY_BELOW_ALLOCATION' ||
        trimmed.includes('Sức chứa mới không được thấp hơn số vé hoặc số ghế cao nhất đã cấp')
      ) {
        return SHIFT_CAPACITY_BELOW_ALLOCATION_MESSAGE
      }
      return trimmed
    }
    if (error instanceof Error && error.message.trim()) return error.message.trim()
  }
  return ''
}

/** Câu người dùng đọc được. Lỗi SQL, mã Postgres và stack được thay bằng fallback. */
export function userMessage(error: unknown, fallback: string) {
  const detail = readDetail(error).replace(STATUS_PREFIX, '').trim()
  if (!detail || TECHNICAL.test(detail)) return fallback
  return detail
}

export function clarifyToastText(message: string) {
  const detail = message.replace(STATUS_PREFIX, '').trim()
  if (!detail || TECHNICAL.test(detail)) return 'Không thực hiện được thao tác. Vui lòng thử lại.'
  return detail
}
