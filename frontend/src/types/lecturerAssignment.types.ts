export interface AssignmentOffering {
  courseOfferingId: string
  courseId: string
  courseCode: string
  courseName: string
  semesterId: string
  semesterCode: string
  isOfferingEnabled: boolean
}

export interface AssignedLecturer {
  lecturerId: string
  fullName: string
  email: string
  assignedAt?: string | null
  isActive: boolean
  semesterId: string | null
  semesterCode: string | null
  courseOfferingId: string | null
}

export interface LecturerDirectoryItem {
  id: string
  fullName: string
  email: string
}

export interface SyncLecturerAssignmentsResponse {
  courseOfferingId: string
  assignedCount: number
  disabledCount: number
  lecturers: AssignedLecturer[]
}
