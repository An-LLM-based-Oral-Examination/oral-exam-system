import { apiClient } from '@/services/api.client'
import type {
  ClassDetail,
  ClassStudentLookup,
  CreateClassResponse,
  CreateManagedClassRequest,
  JoinableClassItem,
  LecturerCourseOffering,
  ManagedClass,
  ResetClassPasswordRequest,
  ResetClassPasswordResponse,
  SemesterOption,
  SemesterCourseOffering,
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

  async listSemesterCourseOfferings(
    semesterId: string,
    includeInactive = false,
  ): Promise<SemesterCourseOffering[]> {
    const response = await apiClient.get<unknown>(`/semesters/${semesterId}/course-offerings`, {
      params: { includeInactive },
    })
    return unwrapList(response.data)
      .filter(isRecord)
      .map((item) => ({
        courseOfferingId: readString(item, 'courseOfferingId'),
        courseId: readString(item, 'courseId'),
        courseCode: readString(item, 'code', 'courseCode'),
        courseName: readString(item, 'name', 'courseName'),
        description: readString(item, 'description') || null,
        credits: readNumber(item, 'credits'),
        academicProgramId: readString(item, 'academicProgramId') || null,
        academicProgramCode: readString(item, 'academicProgramCode') || null,
        isCourseActive: readBool(item, 'isCourseActive', 'isActive'),
        isSelected: readBool(item, 'isSelected'),
      }))
      .filter((item) => item.courseId && item.isCourseActive && item.isSelected)
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
        shiftCount: readNumber(item, 'shiftsCount', 'shiftCount'),
        activeShiftCount: readNumber(item, 'activeShiftCount'),
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

  async lookupStudent(query: string): Promise<ClassStudentLookup> {
    const response = await apiClient.get<unknown>('/lecturer/classes/students/lookup', {
      params: { q: query.trim() },
    })
    const item = isRecord(response.data) ? response.data : {}
    return {
      studentId: readString(item, 'studentId'),
      fullName: readString(item, 'fullName'),
      email: readString(item, 'email'),
      studentCode: readString(item, 'studentCode'),
    }
  },

  async assignStudent(classId: string, studentId: string): Promise<void> {
    await apiClient.post(`/lecturer/classes/${classId}/students`, { studentId })
  },

  async dropStudent(classId: string, studentId: string): Promise<void> {
    await apiClient.delete(`/lecturer/classes/${classId}/students/${studentId}`)
  },

  async createClass(payload: CreateManagedClassRequest): Promise<CreateClassResponse> {
    const response = await apiClient.post<unknown>(
      `/lecturer/course-offerings/${payload.courseOfferingId}/classes`,
      {
        code: payload.code.trim(),
        name: payload.name.trim(),
        password: payload.password,
        maxStudents: payload.maxStudents,
        allowSelfEnrollment: true,
        isJoinEnabled: true,
      },
    )
    const item = isRecord(response.data) ? response.data : {}
    return {
      id: readString(item, 'id', 'classId'),
      code: readString(item, 'code', 'classCode'),
      name: readString(item, 'name', 'className'),
      joinCode: readString(item, 'joinCode'),
      courseId: readString(item, 'courseId'),
      courseCode: readString(item, 'courseCode'),
      courseName: readString(item, 'courseName'),
      semesterId: readString(item, 'semesterId'),
      semesterCode: readString(item, 'semesterCode'),
      lecturerId: readString(item, 'lecturerId') || null,
      lecturerName: readString(item, 'lecturerName') || null,
      maxStudents: readNumber(item, 'maxStudents'),
      hasPassword: readBool(item, 'hasPassword'),
      isJoinEnabled: readBool(item, 'isJoinEnabled'),
      allowSelfEnrollment: readBool(item, 'allowSelfEnrollment'),
      createdAt: readString(item, 'createdAt'),
      courseOfferingId: readString(item, 'courseOfferingId') || null,
      joinPassword: readString(item, 'joinPassword') || null,
    }
  },

  async resetPassword(
    classId: string,
    payload: ResetClassPasswordRequest,
  ): Promise<ResetClassPasswordResponse> {
    const body: Record<string, unknown> = {
      newPassword: payload.newPassword.trim(),
    }
    if (payload.newJoinCode?.trim()) {
      body.newJoinCode = payload.newJoinCode.trim()
    }
    if (typeof payload.isJoinEnabled === 'boolean') {
      body.isJoinEnabled = payload.isJoinEnabled
    }

    const response = await apiClient.post<unknown>(`/lecturer/classes/${classId}/reset-password`, body)
    const item = isRecord(response.data) ? response.data : {}
    return {
      classId: readString(item, 'classId', 'id') || classId,
      code: readString(item, 'code', 'classCode'),
      name: readString(item, 'name', 'className'),
      joinCode: readString(item, 'joinCode'),
      hasPassword: readBool(item, 'hasPassword'),
      isJoinEnabled: readBool(item, 'isJoinEnabled'),
      updatedAt: readString(item, 'updatedAt'),
      joinPassword: readString(item, 'joinPassword') || null,
    }
  },

  async resetJoinPassword(classId: string, newPassword: string): Promise<void> {
    await this.resetPassword(classId, { newPassword })
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
        enrolledCount: typeof item.enrolledCount === 'number' ? item.enrolledCount : undefined,
        maxStudents: typeof item.maxStudents === 'number' ? item.maxStudents : undefined,
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
