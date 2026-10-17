import apiClient from './api.client'
import type { CourseConfigurationDto, CourseDto, ExamInputMode } from '@/types/practice.types'

type JsonRecord = Record<string, unknown>

const EXAM_INPUT_MODES = new Set<ExamInputMode>(['VoiceOnly', 'VoiceWithTranscriptEdit'])

function isRecord(value: unknown): value is JsonRecord {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function invalidResponse(detail: string): never {
  throw { status: 502, title: 'Phản hồi API không hợp lệ', detail }
}

function readString(record: JsonRecord, key: string) {
  const value = record[key] ?? record[`${key.charAt(0).toUpperCase()}${key.slice(1)}`]
  return typeof value === 'string' ? value : null
}

function readNumber(record: JsonRecord, key: string) {
  const value = record[key] ?? record[`${key.charAt(0).toUpperCase()}${key.slice(1)}`]
  return typeof value === 'number' && Number.isFinite(value) ? value : null
}

function readBoolean(record: JsonRecord, key: string) {
  const value = record[key] ?? record[`${key.charAt(0).toUpperCase()}${key.slice(1)}`]
  return typeof value === 'boolean' ? value : null
}

function normalizeCourse(value: unknown): CourseDto {
  if (!isRecord(value)) return invalidResponse('Một môn học không phải dữ liệu JSON hợp lệ.')

  const id = readString(value, 'id')
  const code = readString(value, 'code')
  const name = readString(value, 'name')
  const credits = readNumber(value, 'credits')
  const transcriptBufferSeconds = readNumber(value, 'transcriptBufferSeconds')
  const maxFollowUpQuestions = readNumber(value, 'maxFollowUpQuestions')
  const hasFollowUp = readBoolean(value, 'hasFollowUp')
  const examInputMode = readString(value, 'examInputMode')
  const isActive = readBoolean(value, 'isActive')

  if (
    !id ||
    !code ||
    !name ||
    credits === null ||
    transcriptBufferSeconds === null ||
    maxFollowUpQuestions === null ||
    hasFollowUp === null ||
    !examInputMode ||
    !EXAM_INPUT_MODES.has(examInputMode as ExamInputMode) ||
    isActive === null
  ) {
    return invalidResponse('API môn học trả về thiếu trường hoặc sai kiểu dữ liệu.')
  }

  return {
    id,
    code,
    name,
    credits,
    transcriptBufferSeconds,
    maxFollowUpQuestions,
    hasFollowUp,
    examInputMode: examInputMode as ExamInputMode,
    isActive,
  }
}

function normalizeConfiguration(value: unknown): CourseConfigurationDto {
  if (!isRecord(value)) return invalidResponse('Cấu hình môn học không phải JSON hợp lệ.')

  const course = normalizeCourse({
    ...value,
    id: readString(value, 'courseId'),
    code: readString(value, 'courseCode'),
    name: readString(value, 'courseName'),
    credits: 0,
  })

  return {
    courseId: course.id,
    courseCode: course.code,
    courseName: course.name,
    transcriptBufferSeconds: course.transcriptBufferSeconds,
    maxFollowUpQuestions: course.maxFollowUpQuestions,
    hasFollowUp: course.hasFollowUp,
    examInputMode: course.examInputMode,
    isActive: course.isActive,
  }
}

export const courseService = {
  async getCourses(): Promise<CourseDto[]> {
    const response = await apiClient.get<unknown>('/courses')
    if (!Array.isArray(response.data)) {
      return invalidResponse('API không trả về danh sách môn học.')
    }
    return response.data.map(normalizeCourse)
  },

  async getConfiguration(courseId: string): Promise<CourseConfigurationDto> {
    const response = await apiClient.get<unknown>(`/courses/${courseId}/configurations`)
    return normalizeConfiguration(response.data)
  },

  async updateConfiguration(
    courseId: string,
    payload: {
      transcriptBufferSeconds: number
      maxFollowUpQuestions: number
      hasFollowUp: boolean
      examInputMode: ExamInputMode
      maxMockExamsPerDay?: number
    },
  ): Promise<CourseConfigurationDto> {
    const response = await apiClient.put<unknown>(`/courses/${courseId}/configurations`, payload)
    if (
      isRecord(response.data) &&
      (readString(response.data, 'courseId') || readString(response.data, 'id')) &&
      readString(response.data, 'courseCode')
    ) {
      return normalizeConfiguration(response.data)
    }
    return this.getConfiguration(courseId)
  },
}
