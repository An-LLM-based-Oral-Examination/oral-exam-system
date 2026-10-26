/**
 * Bộ kiểm tra transcript dùng chung cho MF-01 (luyện tập) và MF-02 (thi thử).
 * - Lọc transcript im lặng / ảo giác STT.
 * - Phát hiện tiếng vọng loa (micro thu lại giọng đọc đề).
 * - Voice-First Gate: chỉ cho hiệu đính nhỏ transcript đã có từ ghi âm.
 */

/** Độ dài tối thiểu (sau chuẩn hóa) để bắt đầu so khớp tiếng vọng. */
const ECHO_MIN_LENGTH = 16
/** Transcript dài hơn đề quá ngưỡng này (ký tự) → sinh viên đã trả lời thêm, không phải tiếng vọng. */
const ECHO_LENGTH_TOLERANCE = 24

/** Ngưỡng Voice-First Gate — đồng bộ với backend MF-02 (`VOICE_FIRST_GATE:`). */
export const VOICE_FIRST_MAX_WORD_CHANGE_RATIO = 0.35
export const VOICE_FIRST_MIN_LENGTH_RATIO = 0.7
export const VOICE_FIRST_MAX_LENGTH_RATIO = 1.3

export function normalizeForCompare(value: string) {
  return value
    .toLowerCase()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/[^\p{L}\p{N}\s]/gu, ' ')
    .replace(/\s+/g, ' ')
    .trim()
}

const INLINE_HALLUCINATION_STRIP_REGEX =
  /(?:(?:các\s+bạn|mọi\s+người|quý\s+vị(?:\s+và\s+các\s+bạn)?)\s*,?\s*)?(?:hãy\s+|nhớ\s+|đừng\s+quên\s+|nhấn\s+|bấm\s+|vui\s+lòng\s+)(?:subscribe|đăng\s+k[ýíy])(?:\s+cho)?(?:\s+kênh)?(?:\s+(?:ghiền\s*mì\s*gõ|la\s*la\s*school|fap\s*tv|kem\s*xôi\s*tv|trắng\s*tv))?(?:\s+và\s+bấm\s+chuông(?:\s+theo\s+dõi)?)?(?:\s+để\s+không\s+bỏ\s+lỡ\s+những\s+video(?:\s+hấp\s+dẫn|\s+mới\s+nhất)?)?|để\s+không\s+bỏ\s+lỡ\s+những\s+video(?:\s+hấp\s+dẫn|\s+mới\s+nhất)?|kênh\s+(?:ghiền\s*mì\s*gõ|la\s*la\s*school|fap\s*tv|kem\s*xôi\s*tv|trắng\s*tv)|ghiền\s*mì\s*gõ|la\s*la\s*school|fap\s*tv|kem\s*xôi\s*tv|trắng\s*tv|subtitles\s+by\s+the\s+amara\.org\s+community|amara\.org|dạ\s+thưa\s+thầy\s+cô\s*,?\s*em\s+xin\s+trả\s+lời\s+câu\s+hỏi\s+thi\s+vấn\s+đáp(?:\s+bằng\s+tiếng\s+việt\s+kết\s+hợp\s+thuật\s+ngữ\s+tiếng\s+anh\s+tại\s+đại\s+học\s+fpt)?\s*[:.,]?/giu

const NOISE_HALLUCINATION_SENTENCE_REGEX =
  /^(?:thank\s+you(?:\s+very\s+much|\s+so\s+much|\s+for\s+watching|\s+for\s+listening)?|thanks(?:\s+for\s+watching|\s+for\s+listening)?|subtitles?\s+(?:by|created\s+by).*|captions?\s+by.*|amara\.org.*|please\s+subscribe.*|like\s+and\s+subscribe.*|don't\s+forget\s+to\s+subscribe.*|(?:(?:các\s+bạn|mọi\s+người|quý\s+vị(?:\s+và\s+các\s+bạn)?)\s*,?\s*)?(?:hãy\s+|nhớ\s+|đừng\s+quên\s+|nhấn\s+|bấm\s+|vui\s+lòng\s+)?(?:subscribe|đăng\s+k[ýíy]|theo\s+dõi|like\s+và\s+(?:subscribe|đăng\s+k[ýíy]))(?:\s+cho)?(?:\s+kênh)?.*|bấm\s+chuông.*|ghiền\s*mì\s*gõ.*|la\s*la\s*school.*|fap\s*tv.*|kem\s*xôi\s*tv.*|trắng\s*tv.*|để\s+không\s+bỏ\s+lỡ\s+những\s+video.*|(?:cảm\s+ơn|cám\s+ơn)\s+(?:các\s+bạn|mọi\s+người|quý\s+vị(?:\s+và\s+các\s+bạn)?)\s+đã\s+(?:theo\s+dõi|xem|lắng\s+nghe|đón\s+xem).*|hẹn\s+gặp\s+lại\s+(?:các\s+bạn|mọi\s+người|quý\s+vị).*|(?:phụ\s+đề|biên\s+tập\s+phụ\s+đề|dịch\s+phụ\s+đề)\s*(?:bởi|được\s+thực\s+hiện)?.*|dạ\s+thưa\s+thầy\s+cô\s*,?\s*em\s+xin\s+trả\s+lời\s+câu\s+hỏi\s+thi\s+vấn\s+đáp(?:\s+bằng\s+tiếng\s+việt\s+kết\s+hợp\s+thuật\s+ngữ\s+tiếng\s+anh\s+tại\s+đại\s+học\s+fpt)?\s*[:.,]?|you(?:\s+you)*|bye(?:\s+bye)*|mbc\s+news.*)$/iu

export function isWhisperSilenceHallucination(transcript: string): boolean {
  const raw = transcript.trim()
  if (!raw) return true
  const stripped = raw
    .replace(INLINE_HALLUCINATION_STRIP_REGEX, ' ')
    .replace(/^[\s\p{P}]+|[\s\p{P}]+$/gu, '')
    .replace(/\s+/g, ' ')
    .trim()
  if (!stripped) return true
  if (NOISE_HALLUCINATION_SENTENCE_REGEX.test(stripped)) return true

  const sentences = stripped
    .split(/(?<=[.!?。])\s+|[\r\n]+/u)
    .map((item) => item.replace(/^[\s\p{P}]+|[\s\p{P}]+$/gu, '').trim())
    .filter(Boolean)
  if (sentences.length === 0) return true
  return sentences.every((sentence) => NOISE_HALLUCINATION_SENTENCE_REGEX.test(sentence))
}

export function isLikelySilentOrInvalidTranscript(transcript: string, recordingSeconds: number) {
  const text = transcript.trim()
  if (!text) return true
  if (isWhisperSilenceHallucination(text)) return true
  // Ghi quá ngắn mà transcript dài → thường là ảo giác khi không nói.
  if (recordingSeconds > 0 && recordingSeconds < 2 && text.split(/\s+/).length > 12) return true
  return false
}

/**
 * STT đôi khi "đọc" lại đề khi im lặng hoặc micro thu tiếng loa — không phải lời sinh viên.
 * Chỉ coi là tiếng vọng khi transcript GẦN NHƯ CHỈ LÀ ĐỀ:
 * - transcript nằm trọn trong đề (đủ dài), hoặc
 * - transcript chứa đề / một đoạn dài của đề nhưng tổng độ dài không vượt đề quá `ECHO_LENGTH_TOLERANCE`.
 * Sinh viên nhắc lại đề rồi trả lời dài sẽ không bị chặn nhầm.
 */
export function looksLikeQuestionEcho(
  transcript: string,
  question: { title: string; content: string },
) {
  const normalizedTranscript = normalizeForCompare(transcript)
  if (normalizedTranscript.length < ECHO_MIN_LENGTH) return false
  const candidates = [normalizeForCompare(question.title), normalizeForCompare(question.content)].filter(
    (candidate) => candidate.length >= ECHO_MIN_LENGTH,
  )

  for (const candidate of candidates) {
    if (
      candidate.length >= 24 &&
      candidate.includes(normalizedTranscript) &&
      normalizedTranscript.length >= Math.min(36, Math.floor(candidate.length * 0.6))
    ) {
      return true
    }

    if (normalizedTranscript.length > candidate.length + ECHO_LENGTH_TOLERANCE) continue

    if (normalizedTranscript.includes(candidate)) return true

    if (candidate.length >= 36) {
      const windowSize = Math.min(48, Math.max(28, Math.floor(candidate.length * 0.45)))
      const offsets = [
        0,
        Math.max(0, Math.floor((candidate.length - windowSize) / 2)),
        Math.max(0, candidate.length - windowSize),
      ]
      for (const offset of offsets) {
        const snippet = candidate.slice(offset, offset + windowSize).trim()
        if (snippet.length >= 24 && normalizedTranscript.includes(snippet)) return true
      }
    }
  }
  return false
}

function tokenizeForRevision(value: string) {
  const normalized = normalizeForCompare(value)
  return normalized ? normalized.split(' ') : []
}

function computeTokenEditDistance(sourceTokens: string[], targetTokens: string[]) {
  if (sourceTokens.length === 0) return targetTokens.length
  if (targetTokens.length === 0) return sourceTokens.length
  let previousRow = Array.from({ length: targetTokens.length + 1 }, (_, index) => index)
  for (let sourceIndex = 1; sourceIndex <= sourceTokens.length; sourceIndex += 1) {
    const currentRow = [sourceIndex]
    for (let targetIndex = 1; targetIndex <= targetTokens.length; targetIndex += 1) {
      const substitutionCost = sourceTokens[sourceIndex - 1] === targetTokens[targetIndex - 1] ? 0 : 1
      currentRow[targetIndex] = Math.min(
        previousRow[targetIndex] + 1,
        currentRow[targetIndex - 1] + 1,
        previousRow[targetIndex - 1] + substitutionCost,
      )
    }
    previousRow = currentRow
  }
  return previousRow[targetTokens.length]
}

export interface TranscriptRevisionCheck {
  isAllowed: boolean
  wordChangeRatio: number
  lengthRatio: number
  reason: string | null
}

/**
 * Voice-First Gate (MF-02): bản hiệu đính chỉ hợp lệ khi thay đổi ≤ 35% số từ
 * và độ dài (ký tự, sau trim) nằm trong 0.7–1.3 lần transcript gốc từ ghi âm.
 */
export function checkTranscriptRevision(baselineTranscript: string, revisedTranscript: string): TranscriptRevisionCheck {
  const baseline = baselineTranscript.trim()
  const revised = revisedTranscript.trim()
  if (!baseline) {
    return {
      isAllowed: false,
      wordChangeRatio: 1,
      lengthRatio: 0,
      reason: 'Câu này chưa có transcript từ ghi âm. Hãy ghi âm câu trả lời trước khi hiệu đính.',
    }
  }
  if (!revised) {
    return {
      isAllowed: false,
      wordChangeRatio: 1,
      lengthRatio: 0,
      reason: 'Transcript không được để trống. Hãy giữ nội dung hoặc ghi âm lại.',
    }
  }

  const baselineTokens = tokenizeForRevision(baseline)
  const revisedTokens = tokenizeForRevision(revised)
  const wordChangeRatio =
    computeTokenEditDistance(baselineTokens, revisedTokens) / Math.max(1, baselineTokens.length)
  const lengthRatio = revised.length / baseline.length

  if (wordChangeRatio > VOICE_FIRST_MAX_WORD_CHANGE_RATIO) {
    return {
      isAllowed: false,
      wordChangeRatio,
      lengthRatio,
      reason: `Chỉ được hiệu đính nhỏ (tối đa ${Math.round(VOICE_FIRST_MAX_WORD_CHANGE_RATIO * 100)}% số từ). Muốn thay đổi nhiều hơn, hãy ghi âm lại câu trả lời.`,
    }
  }
  if (lengthRatio < VOICE_FIRST_MIN_LENGTH_RATIO || lengthRatio > VOICE_FIRST_MAX_LENGTH_RATIO) {
    return {
      isAllowed: false,
      wordChangeRatio,
      lengthRatio,
      reason: 'Độ dài bản hiệu đính chỉ được dao động trong khoảng 70%–130% transcript ghi âm. Hãy ghi âm lại nếu cần trả lời khác.',
    }
  }
  return { isAllowed: true, wordChangeRatio, lengthRatio, reason: null }
}
