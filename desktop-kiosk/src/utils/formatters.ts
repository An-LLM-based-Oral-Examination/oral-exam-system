import { ApiProblemError } from '../services/api'

export function formatDuration(totalSeconds: number): string {
  const safeSeconds = Math.max(0, Math.floor(totalSeconds))
  return `${String(Math.floor(safeSeconds / 60)).padStart(2, '0')}:${String(safeSeconds % 60).padStart(2, '0')}`
}

export function formatDateTime(value: string): string {
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString('vi-VN')
}

export function errorMessage(error: unknown): string {
  if (error instanceof ApiProblemError) return error.problem.detail ?? error.message
  if (error instanceof Error) return error.message
  return 'Không thể hoàn tất thao tác với máy chủ.'
}

export function isStudentRole(role: string): boolean {
  return role.toLowerCase() === 'student'
}
