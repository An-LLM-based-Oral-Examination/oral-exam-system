import type { ExamShift } from '@/types/officialExam.types'

export const MIN_SHIFT_CAPACITY = 1
export const MAX_SHIFT_CAPACITY = 40
export const DEFAULT_SHIFT_CAPACITY = 24

export function getShiftCapacityMetrics(
  shift?: Pick<ExamShift, 'maxSeats' | 'assignedTicketsCount' | 'remainingSeats'> | null,
) {
  if (!shift) {
    return {
      maxSeats: undefined,
      assignedTicketsCount: 0,
      remainingSeats: undefined,
    }
  }
  const maxSeats = typeof shift.maxSeats === 'number' ? shift.maxSeats : undefined
  const assignedTicketsCount =
    typeof shift.assignedTicketsCount === 'number' ? shift.assignedTicketsCount : 0
  const remainingSeats =
    typeof shift.remainingSeats === 'number'
      ? shift.remainingSeats
      : maxSeats !== undefined
        ? Math.max(0, maxSeats - assignedTicketsCount)
        : undefined

  return {
    maxSeats,
    assignedTicketsCount,
    remainingSeats,
  }
}

export function validateShiftCapacityInput(
  raw: string,
  options: { defaultWhenEmpty?: number } = {},
): { value: number | null; error: string } {
  const trimmed = raw.trim()
  if (!trimmed) {
    if (typeof options.defaultWhenEmpty === 'number') {
      return { value: options.defaultWhenEmpty, error: '' }
    }
    return {
      value: null,
      error: `Sức chứa ca thi phải là số nguyên từ ${MIN_SHIFT_CAPACITY} đến ${MAX_SHIFT_CAPACITY}.`,
    }
  }

  const parsed = Number(trimmed)
  if (!Number.isFinite(parsed) || !Number.isInteger(parsed) || /[.,]/.test(trimmed)) {
    return {
      value: null,
      error: `Sức chứa ca thi phải là số nguyên từ ${MIN_SHIFT_CAPACITY} đến ${MAX_SHIFT_CAPACITY} (không nhập số thập phân).`,
    }
  }

  if (parsed < MIN_SHIFT_CAPACITY) {
    return {
      value: null,
      error: `Sức chứa ca thi phải từ ${MIN_SHIFT_CAPACITY} đến ${MAX_SHIFT_CAPACITY} (không nhập 0 hoặc số âm).`,
    }
  }

  if (parsed > MAX_SHIFT_CAPACITY) {
    return {
      value: null,
      error: `Trần sức chứa tối đa của hệ thống là ${MAX_SHIFT_CAPACITY} ghế. Không được nhập lớn hơn ${MAX_SHIFT_CAPACITY}.`,
    }
  }

  return { value: parsed, error: '' }
}
