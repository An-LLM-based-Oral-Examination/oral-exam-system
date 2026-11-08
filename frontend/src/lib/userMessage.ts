const STATUS_PREFIX =
  /^(CONFLICT|BAD\s*REQUEST|UNAUTHORIZED|FORBIDDEN|NOT\s*FOUND|INTERNAL\s*SERVER\s*ERROR|ERROR|WARNING|INFO)\s*[:\-–—]\s*/i

const TECHNICAL =
  /\b(?:42[0-9A-Z]{3}|22[0-9]{3}|235\d{2}|42703)\b|column\s+.+\s+does not exist|does not exist|POSITION:\s*\d+|Npgsql|PostgresException|EntityFrameworkCore|SqlException|System\.\w*Exception|at\s+\S+\.cs:line\s+\d+/i

function readDetail(error: unknown) {
  if (typeof error === 'string') return error.trim()
  if (typeof error === 'object' && error !== null) {
    if ('detail' in error && typeof error.detail === 'string') return error.detail.trim()
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
