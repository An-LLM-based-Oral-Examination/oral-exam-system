import { userMessage } from '@/lib/userMessage'
import type { ExamPaper, ExamPreparation } from '@/types/examPreparation.types'
export const EXAM_PREPARATION_PATH = '/department-head/exam-structures'
export const PAPER_LABELS: Record<string, string> = {
  DRAFT: 'Bản nháp',
  SUBMITTED_FOR_REVIEW: 'Chờ duyệt',
  NEEDS_REVISION: 'Cần chỉnh sửa',
  REJECTED: 'Từ chối',
  OFFICIAL: 'Đã duyệt',
  HANDED_OVER: 'Đã bàn giao',
}
export const PREPARATION_LABELS: Record<string, string> = {
  REQUESTED: 'Chưa cấu hình',
  CONFIGURED: 'Chờ phân công',
  IN_PROGRESS: 'Đang chuẩn bị',
  READY_FOR_HANDOVER: 'Đủ đề bàn giao',
  HANDED_OVER: 'Đã bàn giao',
}
export const DIFFICULTY_LABELS: Record<string, string> = {
  easy: 'Dễ',
  medium: 'Trung bình',
  hard: 'Khó',
}
export function preparationError(error: unknown) {
  return userMessage(error, 'Không thể tải dữ liệu. Vui lòng thử lại.')
    .replace(/^(VALIDATION|CONFLICT|NOT_FOUND|FORBIDDEN)\s*:\s*/i, '')
    .trim()
}
export function preparationDate(value?: string | null) {
  if (!value || !Number.isFinite(Date.parse(value))) return '—'
  return new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short' }).format(
    new Date(value),
  )
}
export function preparationMatches(item: ExamPreparation, value: string) {
  return (
    !value ||
    (value === 'PREPARING'
      ? item.status === 'CONFIGURED' || item.status === 'IN_PROGRESS'
      : item.status === value)
  )
}
export function isDeliverable(paper: ExamPaper, preparation: ExamPreparation) {
  return (
    paper.isActive &&
    paper.status === 'OFFICIAL' &&
    paper.sessionId === preparation.sessionId &&
    paper.structureId === preparation.examStructureId &&
    Boolean(paper.assignmentId)
  )
}
export function localDateTime(value: string) {
  const date = new Date(value)
  if (!Number.isFinite(date.getTime())) return ''
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60000)
  return local.toISOString().slice(0, 16)
}

export function isValidExamDeadline(value: string, maximum: string, now = Date.now()) {
  const time = Date.parse(value)
  return Number.isFinite(time) && time > now && time <= Date.parse(maximum)
}

export const similarityTone = (percent: number) =>
  percent >= 85
    ? 'border-[#f3bdc9] bg-[#fff1f3] text-[#b73550]'
    : percent >= 70
      ? 'border-[#efd092] bg-[#fff7e6] text-[#94600e]'
      : 'border-[#b8e3d5] bg-[#edf9f4] text-[#16836a]'
