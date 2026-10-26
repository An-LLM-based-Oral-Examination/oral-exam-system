import apiClient from './api.client'

export interface CourseSummary {
  id: string
  code: string
  name: string
  credits: number
  isActive: boolean
}

export interface PracticeHistoryItem {
  sessionId: string
  courseId: string
  courseCode: string
  courseName: string
  practiceMode: string
  status: string
  startedAt: string
  endedAt: string | null
  totalQuestions: number
  answeredQuestions: number
  averageScore: number | null
}

export interface MockExamQuota {
  courseId: string
  usedCount: number
  remainingCount: number
  maxDailyQuota: number
  canStartExam: boolean
}

export interface OfficialExamTicketOverview {
  ticketId: string
  courseCode: string
  courseName: string
  shiftName: string
  roomLab: string
  startTime: string
  seatNumber: number
  status: string
}

type JsonRecord = Record<string, unknown>

function isRecord(value: unknown): value is JsonRecord {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function readString(record: JsonRecord, ...keys: string[]) {
  for (const key of keys) {
    const value = record[key]
    if (typeof value === 'string') return value
  }
  return ''
}

function readNumber(record: JsonRecord, ...keys: string[]) {
  for (const key of keys) {
    const value = record[key]
    if (typeof value === 'number' && Number.isFinite(value)) return value
  }
  return null
}

function readBoolean(record: JsonRecord, ...keys: string[]) {
  for (const key of keys) {
    const value = record[key]
    if (typeof value === 'boolean') return value
  }
  return false
}

function asList(payload: unknown) {
  if (Array.isArray(payload)) return payload.filter(isRecord)
  if (!isRecord(payload)) return []
  const nested = payload.value ?? payload.Value ?? payload.data ?? payload.Data ?? payload.items
  return Array.isArray(nested) ? nested.filter(isRecord) : []
}

function mapCourse(record: JsonRecord): CourseSummary | null {
  const id = readString(record, 'id', 'Id')
  if (!id) return null
  return {
    id,
    code: readString(record, 'code', 'Code'),
    name: readString(record, 'name', 'Name'),
    credits: readNumber(record, 'credits', 'Credits') ?? 0,
    isActive: readBoolean(record, 'isActive', 'IsActive'),
  }
}

function mapHistory(record: JsonRecord): PracticeHistoryItem | null {
  const sessionId = readString(record, 'sessionId', 'SessionId')
  if (!sessionId) return null
  const endedAt = readString(record, 'endedAt', 'EndedAt')
  return {
    sessionId,
    courseId: readString(record, 'courseId', 'CourseId'),
    courseCode: readString(record, 'courseCode', 'CourseCode'),
    courseName: readString(record, 'courseName', 'CourseName'),
    practiceMode: readString(record, 'practiceMode', 'PracticeMode'),
    status: readString(record, 'status', 'Status'),
    startedAt: readString(record, 'startedAt', 'StartedAt'),
    endedAt: endedAt || null,
    totalQuestions: readNumber(record, 'totalQuestions', 'TotalQuestions') ?? 0,
    answeredQuestions: readNumber(record, 'answeredQuestions', 'AnsweredQuestions') ?? 0,
    averageScore: readNumber(record, 'averageScore', 'AverageScore'),
  }
}

function mapQuota(record: JsonRecord): MockExamQuota | null {
  const courseId = readString(record, 'courseId', 'CourseId')
  if (!courseId) return null
  return {
    courseId,
    usedCount: readNumber(record, 'usedCount', 'UsedCount') ?? 0,
    remainingCount: readNumber(record, 'remainingCount', 'RemainingCount') ?? 0,
    maxDailyQuota: readNumber(record, 'maxDailyQuota', 'MaxDailyQuota') ?? 0,
    canStartExam: readBoolean(record, 'canStartExam', 'CanStartExam'),
  }
}

export const studentOverviewService = {
  async getCourses(): Promise<CourseSummary[]> {
    const response = await apiClient.get<unknown>('/courses')
    return asList(response.data)
      .map(mapCourse)
      .filter((course): course is CourseSummary => course !== null)
  },

  async getPracticeHistory(): Promise<PracticeHistoryItem[]> {
    const response = await apiClient.get<unknown>('/practice/student/history')
    return asList(response.data)
      .map(mapHistory)
      .filter((item): item is PracticeHistoryItem => item !== null)
  },

  async getMockExamQuota(courseId: string): Promise<MockExamQuota> {
    const response = await apiClient.get<unknown>('/mock-exams/quota', {
      params: { courseId },
    })
    const raw = isRecord(response.data)
      ? (response.data.value ?? response.data.Value ?? response.data.data ?? response.data.Data ?? response.data)
      : null
    const record = isRecord(raw) ? raw : null
    const quota = record ? mapQuota(record) : null
    if (!quota) {
      throw {
        status: 502,
        title: 'Phản hồi hạn ngạch không hợp lệ',
        detail: 'Backend không trả về hạn ngạch thi thử.',
      }
    }
    return quota
  },

  async getOfficialExamTickets(): Promise<OfficialExamTicketOverview[]> {
    const response = await apiClient.get<unknown>('/student/dashboard')
    const root = isRecord(response.data)
      ? (response.data.value ?? response.data.Value ?? response.data.data ?? response.data.Data ?? response.data)
      : null
    const rawList = isRecord(root)
      ? (root.officialExamTickets ?? root.OfficialExamTickets)
      : null
    if (!Array.isArray(rawList)) return []
    return rawList
      .filter(isRecord)
      .map((item) => ({
        ticketId: readString(item, 'ticketId', 'TicketId'),
        courseCode: readString(item, 'courseCode', 'CourseCode'),
        courseName: readString(item, 'courseName', 'CourseName', 'sessionTitle', 'SessionTitle'),
        shiftName: readString(item, 'shiftName', 'ShiftName'),
        roomLab: readString(item, 'roomLab', 'RoomLab'),
        startTime: readString(item, 'startTime', 'StartTime', 'examDate', 'ExamDate'),
        seatNumber: readNumber(item, 'seatNumber', 'SeatNumber') ?? 0,
        status: readString(item, 'status', 'Status'),
      }))
      .filter((t) => Boolean(t.ticketId))
  },
}
