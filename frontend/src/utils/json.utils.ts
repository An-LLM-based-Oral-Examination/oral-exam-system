/**
 * Phân tích cú pháp JSON phòng thủ tuyệt đối.
 * Miễn nhiễm với: null, undefined, chuỗi rỗng "", chuỗi khoảng trắng "   ",
 * và các chuỗi rác dạng "undefined", "null".
 */
export function safeJsonParse<T>(raw: string | null | undefined, fallback: T): T {
  if (typeof raw !== 'string') return fallback
  const trimmed = raw.trim()
  if (!trimmed || trimmed === 'undefined' || trimmed === 'null') {
    return fallback
  }

  try {
    const parsed = JSON.parse(trimmed) as unknown
    return (parsed ?? fallback) as T
  } catch {
    return fallback
  }
}
