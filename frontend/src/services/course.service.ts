import apiClient from './api.client'
import { classService } from '@/services/class.service'
import type { CourseLecturer } from '@/types/class.types'
import type { CourseConfigurationDto, CourseDto, ExamInputMode, UpdateCourseConfigurationRequest } from '@/types/practice.types'

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
  if (typeof value === 'string' && value.trim()) return value
  // Guid / số từ một số payload JSON vẫn dùng được làm id môn học.
  if (typeof value === 'number' && Number.isFinite(value)) return String(value)
  if (typeof value === 'object' && value !== null && 'toString' in value) {
    const text = String(value)
    if (text && text !== '[object Object]') return text
  }
  return null
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
    mockExamDurationMinutes: readNumber(value, 'mockExamDurationMinutes') ?? undefined,
    mockExamTotalQuestions: readNumber(value, 'mockExamTotalQuestions') ?? undefined,
    mockExamDifficultyDistribution: readString(value, 'mockExamDifficultyDistribution') ?? undefined,
    mockExamBloomDistribution: readString(value, 'mockExamBloomDistribution') ?? undefined,
  }
}

export interface ShiftStaffCandidate {
  userId: string
  fullName: string
  detail?: string
}

export interface ShiftStaffCandidatePool {
  proctors: ShiftStaffCandidate[]
  examiners: ShiftStaffCandidate[]
  departmentHeadIds: string[]
}

function unwrapArrayPayload(payload: unknown): unknown[] {
  if (Array.isArray(payload)) return payload
  if (isRecord(payload) && Array.isArray(payload.value)) return payload.value
  if (isRecord(payload) && Array.isArray(payload.items)) return payload.items
  return []
}

function normalizeCourseLecturer(value: unknown): CourseLecturer | null {
  if (!isRecord(value)) return null

  const lecturerId = readString(value, 'lecturerId')
  const fullName = readString(value, 'fullName')
  if (!lecturerId || !fullName) return null

  return {
    lecturerId,
    fullName,
    email: readString(value, 'email') ?? '',
    studentCode: readString(value, 'studentCode'),
    isActive: readBoolean(value, 'isActive') ?? true,
    semesterId: readString(value, 'semesterId'),
    semesterCode: readString(value, 'semesterCode'),
    courseOfferingId: readString(value, 'courseOfferingId'),
  }
}

export const courseService = {
  async getEnrolledCourses(): Promise<CourseDto[]> {
    const [enrolled, courses] = await Promise.all([classService.listMyClasses(), this.getCourses()])
    const courseIds = new Set(enrolled.map((item) => item.courseId).filter(Boolean))
    return courses.filter((course) => courseIds.has(course.id))
  },

  async getCourses(): Promise<CourseDto[]> {
    const response = await apiClient.get<unknown>('/courses')
    if (!Array.isArray(response.data)) {
      return invalidResponse('API không trả về danh sách môn học.')
    }
    return response.data.map(normalizeCourse)
  },

  /**
   * Môn học GV được phân công (`GET /lecturer/courses`).
   * Trưởng BM chỉ nhận môn phụ trách; Admin nhận toàn bộ môn active.
   * Danh sách rỗng được giữ nguyên, không cấp thêm môn bằng fallback toàn bộ.
   */
  async getLecturerCourses(): Promise<Array<{ id: string; code: string; name: string }>> {
    const response = await apiClient.get<unknown>('/lecturer/courses')
    const payload = response.data
    const rawList = Array.isArray(payload)
      ? payload
      : isRecord(payload) && Array.isArray(payload.value)
        ? payload.value
        : isRecord(payload) && Array.isArray(payload.items)
          ? payload.items
          : null

    if (rawList) {
      const assigned = rawList
        .map((item) => {
          if (!isRecord(item)) return null
          const id = readString(item, 'id')
          const code = readString(item, 'code')
          const name = readString(item, 'name')
          if (!id || !code || !name) return null
          return { id, code, name }
        })
        .filter((item): item is { id: string; code: string; name: string } => item !== null)

      return assigned
    }
    return invalidResponse('API không trả về danh sách môn được phân công.')
  },

  async getCourseLecturers(courseId: string): Promise<CourseLecturer[]> {
    const response = await apiClient.get<unknown>(`/courses/${courseId}/lecturers`)
    const payload = response.data
    const rawList = Array.isArray(payload)
      ? payload
      : isRecord(payload) && Array.isArray(payload.value)
        ? payload.value
        : isRecord(payload) && Array.isArray(payload.items)
          ? payload.items
          : null

    if (!rawList) {
      return invalidResponse('API không trả về danh sách giảng viên của môn học.')
    }

    return rawList
      .map(normalizeCourseLecturer)
      .filter((lecturer): lecturer is CourseLecturer => lecturer !== null && lecturer.isActive)
  },

  async getConfiguration(courseId: string): Promise<CourseConfigurationDto> {
    const response = await apiClient.get<unknown>(`/courses/${courseId}/configurations`)
    return normalizeConfiguration(response.data)
  },

  async updateConfiguration(courseId: string, payload: UpdateCourseConfigurationRequest): Promise<CourseConfigurationDto> {
    const response = await apiClient.put<unknown>(`/courses/${encodeURIComponent(courseId)}/configurations`, payload)
    return normalizeConfiguration(response.data)
  },

  /**
   * Tổng hợp danh sách Giám thị & Giám khảo hợp lệ cho Cán bộ Khảo thí từ các endpoint danh mục
   * đã mở quyền sẵn trên Backend (`/semesters/classes`, `/examination-officer/appeals/allocation-proposal`, `/academic-programs`).
   */
  async getShiftStaffCandidates(params: {
    courseId: string
    semesterId?: string
    academicProgramId?: string | null
  }): Promise<ShiftStaffCandidatePool> {
    const [courseClassesRes, allocationProposalRes, semesterClassesRes, programsRes] =
      await Promise.allSettled([
        apiClient.get<unknown>('/semesters/classes', {
          params: {
            courseId: params.courseId,
            semesterId: params.semesterId || undefined,
            isActive: true,
          },
        }),
        apiClient.get<unknown>('/examination-officer/appeals/allocation-proposal'),
        apiClient.get<unknown>('/semesters/classes', {
          params: {
            semesterId: params.semesterId || undefined,
            isActive: true,
          },
        }),
        apiClient.get<unknown>('/academic-programs', {
          params: { isActive: true },
        }),
      ])

    const proctorMap = new Map<string, ShiftStaffCandidate>()
    const examinerMap = new Map<string, ShiftStaffCandidate>()
    const departmentHeadIds = new Set<string>()

    const addLecturerCandidate = (record: JsonRecord, fallbackLabel: string) => {
      const lecturerId = readString(record, 'lecturerId')
      const lecturerName = readString(record, 'lecturerName') ?? readString(record, 'fullName')
      const email = readString(record, 'email')
      if (!lecturerId || !lecturerName) return
      const detail = email || fallbackLabel
      if (!proctorMap.has(lecturerId)) {
        proctorMap.set(lecturerId, { userId: lecturerId, fullName: lecturerName, detail })
      }
      if (!examinerMap.has(lecturerId)) {
        examinerMap.set(lecturerId, { userId: lecturerId, fullName: lecturerName, detail })
      }
    }

    if (courseClassesRes.status === 'fulfilled') {
      for (const item of unwrapArrayPayload(courseClassesRes.value.data)) {
        if (isRecord(item)) addLecturerCandidate(item, 'Giảng viên phụ trách môn')
      }
    }

    if (allocationProposalRes.status === 'fulfilled' && isRecord(allocationProposalRes.value.data)) {
      const summaries =
        allocationProposalRes.value.data.lecturerSummaries ??
        allocationProposalRes.value.data.LecturerSummaries
      for (const item of unwrapArrayPayload(summaries)) {
        if (isRecord(item)) addLecturerCandidate(item, 'Giảng viên')
      }
    }

    if (semesterClassesRes.status === 'fulfilled') {
      for (const item of unwrapArrayPayload(semesterClassesRes.value.data)) {
        if (isRecord(item)) addLecturerCandidate(item, 'Giảng viên trong học kỳ')
      }
    }

    if (programsRes.status === 'fulfilled') {
      const programItems = unwrapArrayPayload(programsRes.value.data).filter(isRecord)
      const sortedPrograms = [...programItems].sort((left, right) => {
        const leftMatch =
          params.academicProgramId && readString(left, 'id') === params.academicProgramId ? 0 : 1
        const rightMatch =
          params.academicProgramId && readString(right, 'id') === params.academicProgramId ? 0 : 1
        return leftMatch - rightMatch
      })

      for (const program of sortedPrograms) {
        const headId = readString(program, 'departmentHeadUserId')
        const headName = readString(program, 'departmentHeadName')
        const programCode = readString(program, 'code')
        if (!headId) continue
        departmentHeadIds.add(headId)
        if (headName && !examinerMap.has(headId)) {
          examinerMap.set(headId, {
            userId: headId,
            fullName: headName,
            detail: programCode ? `Trưởng bộ môn ${programCode}` : 'Trưởng bộ môn',
          })
        }
      }
    }

    for (const headId of departmentHeadIds) {
      proctorMap.delete(headId)
    }

    return {
      proctors: Array.from(proctorMap.values()),
      examiners: Array.from(examinerMap.values()),
      departmentHeadIds: Array.from(departmentHeadIds),
    }
  },
}
