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
  shiftCount: number
  activeShiftCount: number
  hasPassword: boolean
  isJoinEnabled: boolean
}

export interface ClassStudentLookup {
  studentId: string
  fullName: string
  email: string
  studentCode: string
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

export interface CreateClassResponse {
  id: string
  code: string
  name: string
  joinCode: string
  courseId: string
  courseCode: string
  courseName: string
  semesterId: string
  semesterCode: string
  lecturerId: string | null
  lecturerName: string | null
  maxStudents: number
  hasPassword: boolean
  isJoinEnabled: boolean
  allowSelfEnrollment: boolean
  createdAt: string
  courseOfferingId: string | null
  joinPassword: string | null
}

export interface ResetClassPasswordRequest {
  newPassword: string
  newJoinCode?: string | null
  isJoinEnabled?: boolean
}

export interface ResetClassPasswordResponse {
  classId: string
  code: string
  name: string
  joinCode: string
  hasPassword: boolean
  isJoinEnabled: boolean
  updatedAt: string
  joinPassword: string | null
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
  enrolledCount?: number | null
  maxStudents?: number | null
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
