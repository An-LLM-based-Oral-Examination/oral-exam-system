import type { SemesterOption } from '@/types/class.types'

function dateKey(value: string) {
  const [year, month, day] = value.slice(0, 10).split('-').map(Number)
  if (!year || !month || !day) return null
  return year * 10000 + month * 100 + day
}

export function pickCurrentSemester(semesters: SemesterOption[]) {
  const today = new Date()
  const todayKey = today.getFullYear() * 10000 + (today.getMonth() + 1) * 100 + today.getDate()
  const covering = semesters.filter((item) => {
    const start = dateKey(item.startDate)
    const end = dateKey(item.endDate)
    return start !== null && end !== null && todayKey >= start && todayKey <= end
  })
  return covering.find((item) => item.isActive) ?? covering[0] ?? semesters.find((item) => item.isActive) ?? semesters[0]
}
