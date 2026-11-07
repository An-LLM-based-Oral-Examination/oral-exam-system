export interface SemesterOption {
  id: string
  code: string
  name: string
  startDate: string
  endDate: string
  isActive: boolean
}

export interface LecturerCourseOffering {
  courseOfferingId: string
  courseId: string
  courseCode: string
  courseName: string
  semesterId: string
  semesterCode: string
}

export interface ManagedClass {
  id: string
  code: string
  name: string
  joinCode: string
  /** Mật khẩu SV dùng để tham gia — chỉ GV thấy. */
  joinPassword: string | null
  courseCode: string
  courseName: string
  semesterId: string
  semesterCode: string
  enrolledCount: number
  maxStudents: number
  hasPassword: boolean
  isJoinEnabled: boolean
}

export interface ClassStudent {
  studentId: string
  fullName: string
  email: string
  studentCode: string
  enrolledAt: string
  status: string
  joinMethod: string
}

export interface ClassShiftSummary {
  shiftId: string
  shiftName: string
  startTime: string
  endTime: string
  status: string
  isLocked: boolean
  participantsCount: number
  submissionsCount: number
}

export interface ClassDetail {
  classId: string
  code: string
  name: string
  joinCode: string
  joinPassword: string | null
  courseCode: string
  courseName: string
  semesterCode: string
  lecturerName: string
  enrolledCount: number
  maxStudents: number
  hasPassword: boolean
  isJoinEnabled: boolean
  allowSelfEnrollment: boolean
  isActive: boolean
  createdAt: string
  students: ClassStudent[]
  shifts: ClassShiftSummary[]
}

export interface CreateManagedClassRequest {
  courseOfferingId: string
  code: string
  name: string
  password: string
  maxStudents: number
}

export interface StudentClassItem {
  classId: string
  classCode: string
  className: string
  courseId: string
  courseCode: string
  courseName: string
  semesterId: string
  semesterCode: string
  lecturerName: string
  enrolledAt?: string
}

export interface JoinableClassItem {
  classId: string
  classCode: string
  className: string
  joinCode: string
  courseCode: string
  courseName: string
  semesterId: string
  semesterCode: string
  lecturerName: string
  enrolledCount: number
  maxStudents: number
  requiresPassword: boolean
  isAlreadyEnrolled: boolean
  canJoin: boolean
}
