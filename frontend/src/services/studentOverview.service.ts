import apiClient from './api.client'
import { mockExamService } from './mockExam.service'

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
    return mockExamService.getQuota(courseId)
  },
}
