import { clarifyToastText, extractProblemErrorCode, userMessage } from '@/lib/userMessage'

export interface NormalizedProblem {
  status: number | null
  title: string
  detail: string
  errorCode: string | null
  traceId: string | null
  fieldErrors: Record<string, string>
  fieldMessages: string[]
  isAuthError: boolean
  isConflict: boolean
  isGone: boolean
  isRateLimited: boolean
}

const TECHNICAL_PATTERNS =
  /\b(?:42[0-9A-Z]{3}|22[0-9]{3}|235\d{2}|42703)\b|column\s+.+\s+does not exist|does not exist|POSITION:\s*\d+|Npgsql|PostgresException|EntityFrameworkCore|SqlException|System\.\w*Exception|at\s+\S+\.cs:line\s+\d+/i

const AXIOS_TRANSPORT_ERROR_PATTERN = /^Request failed with status code \d+/i

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function readOptionalString(record: Record<string, unknown>, ...keys: string[]): string | null {
  for (const key of keys) {
    const val = record[key]
    if (typeof val === 'string' && val.trim()) {
      return val.trim()
    }
  }
  return null
}

function readOptionalNumber(record: Record<string, unknown>, ...keys: string[]): number | null {
  for (const key of keys) {
    const val = record[key]
    if (typeof val === 'number' && Number.isFinite(val)) {
      return val
    }
  }
  return null
}

function toCamelCaseKey(rawKey: string): string {
  const stripped = rawKey.replace(/^request\./i, '').trim()
  if (!stripped) return ''
  return stripped
    .split('.')
    .map((segment) => {
      const clean = segment.trim()
      if (!clean) return ''
      return clean.charAt(0).toLowerCase() + clean.slice(1)
    })
    .filter(Boolean)
    .join('.')
}

function extractFieldErrorsMap(payload: Record<string, unknown>): {
  fieldErrors: Record<string, string>
  fieldMessages: string[]
} {
  const rawErrors = payload.errors ?? payload.Errors
  if (!isRecord(rawErrors)) {
    return { fieldErrors: {}, fieldMessages: [] }
  }

  const fieldErrors: Record<string, string> = {}
  const fieldMessages: string[] = []

  for (const [rawKey, rawValue] of Object.entries(rawErrors)) {
    const normalizedKey = toCamelCaseKey(rawKey)
    if (!normalizedKey) continue

    let firstMessage = ''
    if (Array.isArray(rawValue)) {
      const valid = rawValue
        .filter((item): item is string => typeof item === 'string' && item.trim().length > 0)
        .map((item) => clarifyToastText(item.trim()))
      if (valid.length > 0) {
        firstMessage = valid[0]
        fieldMessages.push(...valid)
      }
    } else if (typeof rawValue === 'string' && rawValue.trim()) {
      firstMessage = clarifyToastText(rawValue.trim())
      fieldMessages.push(firstMessage)
    }

    if (firstMessage && !fieldErrors[normalizedKey]) {
      fieldErrors[normalizedKey] = firstMessage
    }
  }

  return { fieldErrors, fieldMessages }
}

function statusFallbackMessage(status: number | null, defaultFallback: string): string {
  switch (status) {
    case 401:
      return 'Phiên đăng nhập đã hết hạn hoặc không hợp lệ. Vui lòng đăng nhập lại.'
    case 403:
      return 'Bạn không có quyền thực hiện thao tác quản trị này.'
    case 409:
      return 'Dữ liệu đang xung đột với trạng thái hiện tại. Vui lòng kiểm tra lại.'
    case 410:
      return 'Tài nguyên hoặc phiên thao tác không còn khả dụng.'
    case 422:
      return 'Dữ liệu gửi lên chưa hợp lệ theo quy tắc nghiệp vụ.'
    case 429:
      return 'Hệ thống đang giới hạn tần suất truy cập. Vui lòng thử lại sau ít phút.'
    default:
      return defaultFallback
  }
}

export function unwrapProblemPayload(error: unknown): {
  payload: Record<string, unknown>
  status: number | null
} {
  if (!isRecord(error)) {
    return { payload: {}, status: null }
  }

  const response = isRecord(error.response) ? error.response : null
  const responseStatus = response ? readOptionalNumber(response, 'status') : null

  if (response && isRecord(response.data)) {
    const dataPayload = response.data
    const dataStatus = readOptionalNumber(dataPayload, 'status', 'Status')
    return {
      payload: dataPayload,
      status: dataStatus ?? responseStatus,
    }
  }

  const directStatus = readOptionalNumber(error, 'status', 'Status')
  return {
    payload: error,
    status: directStatus ?? responseStatus,
  }
}

export function extractProblemDetails(error: unknown, fallback: string): NormalizedProblem {
  const { payload, status } = unwrapProblemPayload(error)
  const title = readOptionalString(payload, 'title', 'Title') ?? 'Không thể xử lý yêu cầu'
  const errorCode =
    readOptionalString(payload, 'errorCode', 'ErrorCode', 'code', 'Code') ??
    extractProblemErrorCode(error)
  const traceId = readOptionalString(payload, 'traceId', 'TraceId')
  const { fieldErrors, fieldMessages } = extractFieldErrorsMap(payload)

  const resolvedFallback = statusFallbackMessage(status, fallback)
  let detail: string

  if (typeof error === 'string') {
    detail = userMessage(error, resolvedFallback)
  } else {
    const rawDetail = readOptionalString(payload, 'detail', 'Detail', 'message', 'Message')
    const rawDetailWasSuppressed = Boolean(
      rawDetail &&
        (TECHNICAL_PATTERNS.test(rawDetail) ||
          AXIOS_TRANSPORT_ERROR_PATTERN.test(rawDetail)),
    )
    const sanitizedTitle = readOptionalString(payload, 'title', 'Title')
    const safeTitle =
      sanitizedTitle &&
      sanitizedTitle !== 'Không thể xử lý yêu cầu' &&
      !TECHNICAL_PATTERNS.test(sanitizedTitle) &&
      !AXIOS_TRANSPORT_ERROR_PATTERN.test(sanitizedTitle)
        ? clarifyToastText(sanitizedTitle)
        : null

    if (rawDetail && !rawDetailWasSuppressed) {
      detail = userMessage({ detail: rawDetail, code: errorCode ?? undefined }, resolvedFallback)
    } else if (fieldMessages.length > 0) {
      detail = fieldMessages[0]
    } else if (
      !rawDetailWasSuppressed &&
      error instanceof Error &&
      error.message.trim() &&
      !TECHNICAL_PATTERNS.test(error.message) &&
      !AXIOS_TRANSPORT_ERROR_PATTERN.test(error.message.trim())
    ) {
      detail = userMessage(error, resolvedFallback)
    } else {
      detail = safeTitle ?? resolvedFallback
    }
  }

  return {
    status,
    title,
    detail,
    errorCode,
    traceId,
    fieldErrors,
    fieldMessages,
    isAuthError: status === 401 || status === 403,
    isConflict: status === 409,
    isGone: status === 410,
    isRateLimited: status === 429,
  }
}

export function mapProblemFieldErrors<T extends string>(
  error: unknown,
  allowedFields: readonly T[],
  aliases?: Record<string, T>,
): Partial<Record<T, string>> {
  const { fieldErrors } = extractProblemDetails(error, '')
  const mapped: Partial<Record<T, string>> = {}

  const lowerFieldLookup = new Map<string, T>()
  for (const field of allowedFields) {
    lowerFieldLookup.set(field.toLowerCase(), field)
  }
  if (aliases) {
    for (const [aliasKey, targetField] of Object.entries(aliases)) {
      lowerFieldLookup.set(aliasKey.toLowerCase(), targetField)
    }
  }

  for (const [key, message] of Object.entries(fieldErrors)) {
    const matched = lowerFieldLookup.get(key.toLowerCase())
    if (matched && !mapped[matched]) {
      mapped[matched] = message
    }
  }

  return mapped
}
