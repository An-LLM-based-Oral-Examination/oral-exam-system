import { apiClient } from '@/services/api.client'
import type {
  ClassDetail,
  CreateManagedClassRequest,
  JoinableClassItem,
  LecturerCourseOffering,
  ManagedClass,
  SemesterOption,
  StudentClassItem,
} from '@/types/class.types'

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function readString(record: Record<string, unknown>, ...keys: string[]) {
  for (const key of keys) {
    const value = record[key]
    if (typeof value === 'string' && value.trim()) return value
  }
  return ''
}

function readNumber(record: Record<string, unknown>, ...keys: string[]) {
  for (const key of keys) {
    const value = record[key]
    if (typeof value === 'number' && Number.isFinite(value)) return value
  }
  return 0
}

function readBool(record: Record<string, unknown>, ...keys: string[]) {
  for (const key of keys) {
    if (typeof record[key] === 'boolean') return record[key] as boolean
  }
  return false
}

function unwrapList(payload: unknown): unknown[] {
  if (Array.isArray(payload)) return payload
  if (!isRecord(payload)) return []
  for (const key of ['items', 'data', 'results', 'value']) {
    if (Array.isArray(payload[key])) return payload[key]
  }
  return []
}

export const classService = {
  async listSemesters(): Promise<SemesterOption[]> {
    const response = await apiClient.get<unknown>('/semesters')
    return unwrapList(response.data)
      .filter(isRecord)
      .map((item) => ({
        id: readString(item, 'id'),
        code: readString(item, 'code'),
        name: readString(item, 'name') || readString(item, 'code'),
        startDate: readString(item, 'startDate'),
        endDate: readString(item, 'endDate'),
        isActive: readBool(item, 'isActive'),
      }))
      .filter((item) => item.id)
  },

  async listMyOfferings(): Promise<LecturerCourseOffering[]> {
    const response = await apiClient.get<unknown>('/lecturer/course-offerings')
    return unwrapList(response.data)
      .filter(isRecord)
      .map((item) => ({
        courseOfferingId: readString(item, 'courseOfferingId'),
        courseId: readString(item, 'courseId'),
        courseCode: readString(item, 'courseCode'),
        courseName: readString(item, 'courseName'),
        semesterId: readString(item, 'semesterId'),
        semesterCode: readString(item, 'semesterCode'),
      }))
      .filter((item) => item.courseOfferingId)
  },

  async listLecturerClasses(semesterId?: string): Promise<ManagedClass[]> {
    const response = await apiClient.get<unknown>('/lecturer/classes', {
      params: semesterId ? { semesterId } : undefined,
    })
    return unwrapList(response.data)
      .filter(isRecord)
      .map((item) => ({
        id: readString(item, 'classId', 'id'),
        code: readString(item, 'code', 'classCode'),
        name: readString(item, 'name', 'className'),
        joinCode: readString(item, 'joinCode'),
        joinPassword: readString(item, 'joinPassword') || null,
        courseCode: readString(item, 'courseCode'),
        courseName: readString(item, 'courseName'),
        semesterId: readString(item, 'semesterId'),
        semesterCode: readString(item, 'semesterCode'),
        enrolledCount: readNumber(item, 'enrolledCount'),
        maxStudents: readNumber(item, 'maxStudents'),
        hasPassword: readBool(item, 'hasPassword'),
        isJoinEnabled: readBool(item, 'isJoinEnabled'),
      }))
      .filter((item) => item.id)
  },

  async getClassDetail(classId: string): Promise<ClassDetail> {
    const response = await apiClient.get<unknown>(`/lecturer/classes/${classId}`)
    const item = isRecord(response.data) ? response.data : {}
    return {
      classId: readString(item, 'classId', 'id'),
      code: readString(item, 'code', 'classCode'),
      name: readString(item, 'name', 'className'),
      joinCode: readString(item, 'joinCode'),
      joinPassword: readString(item, 'joinPassword') || null,
      courseCode: readString(item, 'courseCode'),
      courseName: readString(item, 'courseName'),
      semesterCode: readString(item, 'semesterCode'),
      lecturerName: readString(item, 'lecturerName'),
      enrolledCount: readNumber(item, 'enrolledCount'),
      maxStudents: readNumber(item, 'maxStudents'),
      hasPassword: readBool(item, 'hasPassword'),
      isJoinEnabled: readBool(item, 'isJoinEnabled'),
      allowSelfEnrollment: readBool(item, 'allowSelfEnrollment'),
      isActive: readBool(item, 'isActive'),
      createdAt: readString(item, 'createdAt'),
      students: unwrapList(item.enrolledStudents)
        .filter(isRecord)
        .map((student) => ({
          studentId: readString(student, 'studentId'),
          fullName: readString(student, 'fullName'),
          email: readString(student, 'email'),
          studentCode: readString(student, 'studentCode'),
          enrolledAt: readString(student, 'enrolledAt'),
          status: readString(student, 'status'),
          joinMethod: readString(student, 'joinMethod'),
        }))
        .filter((student) => student.studentId),
      shifts: unwrapList(item.shifts)
        .filter(isRecord)
        .map((shift) => ({
          shiftId: readString(shift, 'shiftId', 'id'),
          shiftName: readString(shift, 'shiftName', 'name'),
          startTime: readString(shift, 'startTime'),
          endTime: readString(shift, 'endTime'),
          status: readString(shift, 'status'),
          isLocked: readBool(shift, 'isLocked'),
          participantsCount: readNumber(shift, 'participantsCount'),
          submissionsCount: readNumber(shift, 'submissionsCount'),
        }))
        .filter((shift) => shift.shiftId),
    }
  },

  async createClass(payload: CreateManagedClassRequest): Promise<void> {
    await apiClient.post(`/lecturer/course-offerings/${payload.courseOfferingId}/classes`, {
      code: payload.code.trim(),
      name: payload.name.trim(),
      password: payload.password,
      maxStudents: payload.maxStudents,
      allowSelfEnrollment: true,
      isJoinEnabled: true,
    })
  },

  async listMyClasses(): Promise<StudentClassItem[]> {
    const response = await apiClient.get<unknown>('/student/classes/my-classes')
    return unwrapList(response.data)
      .filter(isRecord)
      .map((item) => ({
        classId: readString(item, 'classId'),
        classCode: readString(item, 'classCode'),
        className: readString(item, 'className'),
        courseId: readString(item, 'courseId'),
        courseCode: readString(item, 'courseCode'),
        courseName: readString(item, 'courseName'),
        semesterId: readString(item, 'semesterId'),
        semesterCode: readString(item, 'semesterCode'),
        lecturerName: readString(item, 'lecturerName'),
        enrolledAt: readString(item, 'enrolledAt') || undefined,
      }))
      .filter((item) => item.classId)
  },

  async searchClasses(params?: { semesterId?: string; keyword?: string }): Promise<JoinableClassItem[]> {
    const response = await apiClient.get<unknown>('/student/classes/search', {
      params: {
        ...(params?.semesterId ? { semesterId: params.semesterId } : {}),
        ...(params?.keyword?.trim() ? { keyword: params.keyword.trim() } : {}),
        onlyAvailableToJoin: false,
      },
    })
    return unwrapList(response.data)
      .filter(isRecord)
      .map((item) => ({
        classId: readString(item, 'classId'),
        classCode: readString(item, 'classCode'),
        className: readString(item, 'className'),
        joinCode: readString(item, 'joinCode'),
        courseCode: readString(item, 'courseCode'),
        courseName: readString(item, 'courseName'),
        semesterId: readString(item, 'semesterId'),
        semesterCode: readString(item, 'semesterCode'),
        lecturerName: readString(item, 'lecturerName'),
        enrolledCount: readNumber(item, 'enrolledCount'),
        maxStudents: readNumber(item, 'maxStudents'),
        requiresPassword: readBool(item, 'requiresPassword'),
        isAlreadyEnrolled: readBool(item, 'isAlreadyEnrolled'),
        canJoin: readBool(item, 'canJoin'),
      }))
      .filter((item) => item.classId)
  },

  async joinClass(classId: string, password?: string): Promise<void> {
    await apiClient.post(`/student/classes/${classId}/join`, {
      classId,
      password: password?.trim() || null,
    })
  },
}
