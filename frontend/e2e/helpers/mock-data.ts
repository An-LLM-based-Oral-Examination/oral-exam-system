import type { CanonicalRole } from '../fixtures/env'

export type { CanonicalRole }

export type BackendRole =
  | 'student'
  | 'lecturer'
  | 'department_head'
  | 'examination_officer'
  | 'proctor'
  | 'admin'

export const BACKEND_ROLE_BY_CANONICAL: Record<CanonicalRole, BackendRole> = {
  Student: 'student',
  Instructor: 'lecturer',
  DepartmentHead: 'department_head',
  ExaminationOfficer: 'examination_officer',
  Proctor: 'proctor',
  Admin: 'admin',
}

export const PORTAL_HOME_BY_ROLE: Record<CanonicalRole, string> = {
  Student: '/student/overview',
  Instructor: '/lecturer/dashboard',
  DepartmentHead: '/department-head/dashboard',
  ExaminationOfficer: '/examination-officer/dashboard',
  Proctor: '/proctor/attendance',
  Admin: '/admin/dashboard',
}

export function makeUuid(groupHex4: string, seq: number): string {
  const paddedGroup = groupHex4.replace(/[^0-9a-f]/gi, '0').slice(0, 4).padStart(4, '0').toLowerCase()
  const paddedSeq = Math.max(1, Math.trunc(seq)).toString(16).padStart(12, '0').slice(-12)
  return `a000${paddedGroup}-0000-4000-8000-${paddedSeq}`
}

export const E2E_IDS = {
  users: {
    Student: makeUuid('0001', 1),
    Instructor: makeUuid('0001', 2),
    DepartmentHead: makeUuid('0001', 3),
    ExaminationOfficer: makeUuid('0001', 4),
    Proctor: makeUuid('0001', 5),
    Admin: makeUuid('0001', 6),
    secondStudent: makeUuid('0001', 7),
    secondInstructor: makeUuid('0001', 8),
  },
  semester: makeUuid('0002', 1),
  course: makeUuid('0003', 1),
  secondCourse: makeUuid('0003', 2),
  offering: makeUuid('0004', 1),
  clo1: makeUuid('0005', 1),
  clo2: makeUuid('0005', 2),
  class1: makeUuid('0006', 1),
  rubric: makeUuid('0007', 1),
  criterion1: makeUuid('0008', 1),
  criterion2: makeUuid('0008', 2),
  question1: makeUuid('0009', 1),
  question2: makeUuid('0009', 2),
  question3: makeUuid('0009', 3),
  examStructure: makeUuid('000a', 1),
  practiceSession: makeUuid('000b', 1),
  practiceAnswer: makeUuid('000c', 1),
  mockSession: makeUuid('000d', 1),
  mockAnswer: makeUuid('000e', 1),
  inClassShift: makeUuid('000f', 1),
  inClassSubmission: makeUuid('0010', 1),
  notification: makeUuid('0011', 1),
  ticket: makeUuid('0012', 1),
  dlqTask: makeUuid('0013', 1),
} as const

const BASE_ISO = '2026-11-17T08:00:00.000Z'

export interface MockUser {
  id: string
  email: string
  username: string
  fullName: string
  role: CanonicalRole
  studentCode: string | null
  department: string | null
  isActive: boolean
  mustChangePassword: boolean
  createdAt: string
  updatedAt: string
}

const ROLE_DISPLAY_NAMES: Record<CanonicalRole, string> = {
  Student: 'Nguyễn Minh Sinh Viên',
  Instructor: 'Trần Văn Giảng Viên',
  DepartmentHead: 'Lê Thị Trưởng Bộ Môn',
  ExaminationOfficer: 'Phạm Hoàng Khảo Thí',
  Proctor: 'Vũ Quang Giám Thị',
  Admin: 'Nguyễn Quản Trị Viên',
}

export function makeUser(role: CanonicalRole, overrides: Partial<MockUser> = {}): MockUser {
  const slug = BACKEND_ROLE_BY_CANONICAL[role]
  return {
    id: E2E_IDS.users[role],
    email: `${slug}.e2e@fe.edu.vn`,
    username: `${slug}.e2e`,
    fullName: ROLE_DISPLAY_NAMES[role],
    role,
    studentCode: role === 'Student' ? 'SE180001' : null,
    department: role === 'Student' ? null : 'Software Engineering',
    isActive: true,
    mustChangePassword: false,
    createdAt: BASE_ISO,
    updatedAt: BASE_ISO,
    ...overrides,
  }
}

export interface MockSemester {
  id: string
  code: string
  name: string
  startDate: string
  endDate: string
  isActive: boolean
  coursesCount: number
  classesCount: number
  createdAt: string
}

export function makeSemester(overrides: Partial<MockSemester> = {}): MockSemester {
  return {
    id: E2E_IDS.semester,
    code: 'FA26',
    name: 'Fall 2026',
    startDate: '2026-09-01T00:00:00.000Z',
    endDate: '2026-12-31T00:00:00.000Z',
    isActive: true,
    coursesCount: 1,
    classesCount: 1,
    createdAt: BASE_ISO,
    ...overrides,
  }
}

export interface MockCourse {
  id: string
  code: string
  name: string
  description: string
  credits: number
  semesterId: string
  departmentHeadUserId: string | null
  departmentHeadName: string | null
  maxMockExamsPerDay: number
  mockExamDurationMinutes: number
  mockExamTotalQuestions: number
  transcriptBufferSeconds: number
  maxFollowUpQuestions: number
  hasFollowUp: boolean
  examInputMode: 'VoiceOnly' | 'VoiceWithTranscriptEdit'
  isActive: boolean
}

export function makeCourse(overrides: Partial<MockCourse> = {}): MockCourse {
  return {
    id: E2E_IDS.course,
    code: 'PRN231',
    name: 'Building Cross-Platform Back-End Applications',
    description: 'Kiến trúc API .NET 8 và phân tán nâng cao',
    credits: 3,
    semesterId: E2E_IDS.semester,
    departmentHeadUserId: E2E_IDS.users.DepartmentHead,
    departmentHeadName: ROLE_DISPLAY_NAMES.DepartmentHead,
    maxMockExamsPerDay: 3,
    mockExamDurationMinutes: 20,
    mockExamTotalQuestions: 5,
    transcriptBufferSeconds: 60,
    maxFollowUpQuestions: 2,
    hasFollowUp: true,
    examInputMode: 'VoiceWithTranscriptEdit',
    isActive: true,
    ...overrides,
  }
}

export interface MockClo {
  id: string
  courseId: string
  cloCode: string
  description: string
  bloomLevel: 'Remember' | 'Understand' | 'Apply' | 'Analyze' | 'Evaluate' | 'Create'
  orderIndex: number
  isActive: boolean
  createdAt: string
}

export function makeClo(overrides: Partial<MockClo> = {}): MockClo {
  return {
    id: E2E_IDS.clo1,
    courseId: E2E_IDS.course,
    cloCode: 'CLO1',
    description: 'Phân tích kiến trúc Clean Architecture và nguyên lý SOLID trong hệ thống .NET 8.',
    bloomLevel: 'Analyze',
    orderIndex: 1,
    isActive: true,
    createdAt: BASE_ISO,
    ...overrides,
  }
}

export interface MockClass {
  id: string
  classId: string
  code: string
  classCode: string
  name: string
  className: string
  joinCode: string
  joinPassword: string | null
  hasPassword: boolean
  isJoinEnabled: boolean
  allowSelfEnrollment: boolean
  isActive: boolean
  courseId: string
  courseCode: string
  courseName: string
  semesterId: string
  semesterCode: string
  lecturerId: string
  lecturerName: string
  enrolledCount: number
  maxStudents: number
  shiftCount: number
  shiftsCount: number
  activeShiftCount: number
  requiresPassword: boolean
  isAlreadyEnrolled: boolean
  canJoin: boolean
  createdAt: string
  enrolledStudents: Array<{
    studentId: string
    fullName: string
    email: string
    studentCode: string
    enrolledAt: string
    status: string
    joinMethod: string
  }>
  shifts: Array<{
    shiftId: string
    shiftName: string
    startTime: string
    endTime: string
    status: string
    isLocked: boolean
    participantsCount: number
    submissionsCount: number
  }>
}

export function makeClass(overrides: Partial<MockClass> = {}): MockClass {
  const id = overrides.id ?? overrides.classId ?? E2E_IDS.class1
  return {
    id,
    classId: id,
    code: 'SE1801-PRN231',
    classCode: 'SE1801-PRN231',
    name: 'Lớp SE1801 - PRN231',
    className: 'Lớp SE1801 - PRN231',
    joinCode: 'PRN1801',
    joinPassword: null,
    hasPassword: true,
    isJoinEnabled: true,
    allowSelfEnrollment: true,
    isActive: true,
    courseId: E2E_IDS.course,
    courseCode: 'PRN231',
    courseName: 'Building Cross-Platform Back-End Applications',
    semesterId: E2E_IDS.semester,
    semesterCode: 'FA26',
    lecturerId: E2E_IDS.users.Instructor,
    lecturerName: ROLE_DISPLAY_NAMES.Instructor,
    enrolledCount: 1,
    maxStudents: 35,
    shiftCount: 0,
    shiftsCount: 0,
    activeShiftCount: 0,
    requiresPassword: true,
    isAlreadyEnrolled: true,
    canJoin: false,
    createdAt: BASE_ISO,
    enrolledStudents: [
      {
        studentId: E2E_IDS.users.Student,
        fullName: ROLE_DISPLAY_NAMES.Student,
        email: 'student.e2e@fe.edu.vn',
        studentCode: 'SE180001',
        enrolledAt: BASE_ISO,
        status: 'enrolled',
        joinMethod: 'self',
      },
    ],
    shifts: [],
    ...overrides,
  }
}

export interface MockNotification {
  id: string
  userId: string
  type: string
  title: string
  message: string
  relatedEntityName: string | null
  relatedEntityId: string | null
  metadata: Record<string, unknown> | null
  isRead: boolean
  readAt: string | null
  createdAt: string
}

export function makeNotification(overrides: Partial<MockNotification> = {}): MockNotification {
  return {
    id: E2E_IDS.notification,
    userId: E2E_IDS.users.Student,
    type: 'SYSTEM_ANNOUNCEMENT',
    title: 'Thông báo lịch kiểm tra vấn đáp',
    message: 'Vui lòng kiểm tra micro trước khi vào ca thi vấn đáp.',
    relatedEntityName: null,
    relatedEntityId: null,
    metadata: null,
    isRead: false,
    readAt: null,
    createdAt: BASE_ISO,
    ...overrides,
  }
}

export interface MockRubricCriterion {
  id: string
  criterionName: string
  description: string | null
  maxScore: number
  weight: number
  bloomLevel: 'Remember' | 'Understand' | 'Apply' | 'Analyze' | 'Evaluate' | 'Create'
  orderIndex: number
}

export interface MockRubric {
  id: string
  name: string
  description: string | null
  totalMaxScore: number
  criteria: MockRubricCriterion[]
}

export function makeRubric(overrides: Partial<MockRubric> = {}): MockRubric {
  return {
    id: E2E_IDS.rubric,
    name: 'Rubric chuẩn vấn đáp 10.0',
    description: 'Đánh giá khái niệm cốt lõi và ví dụ thực tế',
    totalMaxScore: 10,
    criteria: [
      {
        id: E2E_IDS.criterion1,
        criterionName: 'Giải thích đúng nguyên lý cốt lõi',
        description: 'Nêu chính xác định nghĩa và vai trò',
        maxScore: 5,
        weight: 0.5,
        bloomLevel: 'Understand',
        orderIndex: 1,
      },
      {
        id: E2E_IDS.criterion2,
        criterionName: 'Ví dụ minh họa & phân tích đánh đổi',
        description: 'Đưa ra ví dụ thực tế và trade-offs',
        maxScore: 5,
        weight: 0.5,
        bloomLevel: 'Analyze',
        orderIndex: 2,
      },
    ],
    ...overrides,
  }
}

export interface MockQuestion {
  id: string
  courseId: string
  courseCode: string
  cloId: string | null
  cloCode: string | null
  rubricId: string
  title: string
  content: string
  imageUrl: string | null
  imageAlt: string | null
  imageDescription?: string | null
  sampleAnswer: string
  keyPoints: string[]
  difficulty: 'easy' | 'medium' | 'hard'
  bloomLevel: 'Remember' | 'Understand' | 'Apply' | 'Analyze' | 'Evaluate' | 'Create'
  source: 'manual' | 'ai_generated' | 'flm_api'
  usageScope: 'practice' | 'exam'
  approvalStatus: 'DRAFT' | 'SUBMITTED_FOR_REVIEW' | 'APPROVED' | 'NEEDS_REVISION' | 'REJECTED'
  submittedBy: string | null
  submittedByName: string | null
  approvedBy: string | null
  approvedByName: string | null
  reviewNotes: string | null
  isActive: boolean
  createdAt: string
  totalRubricScore: number
  rubric: MockRubric
}

export function makeQuestion(overrides: Partial<MockQuestion> = {}): MockQuestion {
  const rubric = overrides.rubric ?? makeRubric()
  return {
    id: E2E_IDS.question1,
    courseId: E2E_IDS.course,
    courseCode: 'PRN231',
    cloId: E2E_IDS.clo1,
    cloCode: 'CLO1',
    rubricId: rubric.id,
    title: 'Nguyên lý Dependency Inversion (DIP)',
    content: 'Trình bày nguyên lý Dependency Inversion và cách áp dụng trong Clean Architecture.',
    imageUrl: null,
    imageAlt: null,
    imageDescription: null,
    sampleAnswer:
      'Module cấp cao không nên phụ thuộc trực tiếp vào module cấp thấp; cả hai nên phụ thuộc vào abstraction thông qua interface.',
    keyPoints: [
      'Module cấp cao và cấp thấp phụ thuộc abstraction',
      'Giảm coupling khi kiểm thử và mở rộng',
    ],
    difficulty: 'medium',
    bloomLevel: 'Analyze',
    source: 'manual',
    usageScope: 'practice',
    approvalStatus: 'APPROVED',
    submittedBy: E2E_IDS.users.Instructor,
    submittedByName: ROLE_DISPLAY_NAMES.Instructor,
    approvedBy: E2E_IDS.users.DepartmentHead,
    approvedByName: ROLE_DISPLAY_NAMES.DepartmentHead,
    reviewNotes: 'Đạt chuẩn học thuật và rõ ràng tiêu chí chấm.',
    isActive: true,
    createdAt: BASE_ISO,
    totalRubricScore: 10,
    rubric,
    ...overrides,
  }
}

export interface MockExamStructure {
  id: string
  courseId: string
  courseCode: string
  createdBy: string
  name: string
  totalQuestions: number
  durationMinutes: number
  bloomDistribution: string
  isActive: boolean
  createdAt: string
}

export function makeExamStructure(overrides: Partial<MockExamStructure> = {}): MockExamStructure {
  return {
    id: E2E_IDS.examStructure,
    courseId: E2E_IDS.course,
    courseCode: 'PRN231',
    createdBy: E2E_IDS.users.DepartmentHead,
    name: 'Cấu trúc đề chuẩn PRN231',
    totalQuestions: 3,
    durationMinutes: 20,
    bloomDistribution: JSON.stringify({ Understand: 1, Apply: 1, Analyze: 1 }),
    isActive: true,
    createdAt: BASE_ISO,
    ...overrides,
  }
}

export interface MockPracticeSession {
  sessionId: string
  courseId: string
  courseCode: string
  courseName: string
  studentId: string
  studentName: string
  practiceMode: 'per_question' | 'full_session'
  status: 'in_progress' | 'completed' | 'abandoned'
  transcriptBufferSeconds: number
  startedAt: string
  endedAt: string | null
  questions: Array<{
    questionId: string
    content: string
    imageUrl: string | null
    imageAlt: string | null
    rubricCriteria: string[]
  }>
  answers: Array<Record<string, unknown>>
}

export function makePracticeSession(
  overrides: Partial<MockPracticeSession> = {},
): MockPracticeSession {
  return {
    sessionId: E2E_IDS.practiceSession,
    courseId: E2E_IDS.course,
    courseCode: 'PRN231',
    courseName: 'Building Cross-Platform Back-End Applications',
    studentId: E2E_IDS.users.Student,
    studentName: ROLE_DISPLAY_NAMES.Student,
    practiceMode: 'per_question',
    status: 'in_progress',
    transcriptBufferSeconds: 60,
    startedAt: BASE_ISO,
    endedAt: null,
    questions: [
      {
        questionId: E2E_IDS.question1,
        content: 'Trình bày nguyên lý Dependency Inversion và cách áp dụng trong Clean Architecture.',
        imageUrl: null,
        imageAlt: null,
        rubricCriteria: ['Giải thích đúng nguyên lý cốt lõi', 'Ví dụ minh họa & phân tích đánh đổi'],
      },
    ],
    answers: [],
    ...overrides,
  }
}

export interface MockExamSession {
  sessionId: string
  courseId: string
  courseCode: string
  courseName: string
  hasFollowUp: boolean
  durationMinutes: number
  startedAt: string
  expiresAt: string
  usedQuota: number
  remainingQuota: number
  maxDailyQuota: number
  questions: Array<{
    id: string
    orderIndex: number
    title: string
    content: string
    imageUrl: string | null
    imageAlt: string | null
    bloomLevel: string
    difficulty: 'easy' | 'medium' | 'hard'
    rubricCriteria: Array<{ name: string; maxScore: number }>
  }>
}

export function makeMockExamSession(overrides: Partial<MockExamSession> = {}): MockExamSession {
  return {
    sessionId: E2E_IDS.mockSession,
    courseId: E2E_IDS.course,
    courseCode: 'PRN231',
    courseName: 'Building Cross-Platform Back-End Applications',
    hasFollowUp: true,
    durationMinutes: 20,
    startedAt: BASE_ISO,
    expiresAt: '2026-11-17T08:20:00.000Z',
    usedQuota: 1,
    remainingQuota: 2,
    maxDailyQuota: 3,
    questions: [
      {
        id: E2E_IDS.question1,
        orderIndex: 1,
        title: 'Nguyên lý Dependency Inversion (DIP)',
        content: 'Trình bày nguyên lý Dependency Inversion và cách áp dụng trong Clean Architecture.',
        imageUrl: null,
        imageAlt: null,
        bloomLevel: 'Analyze',
        difficulty: 'medium',
        rubricCriteria: [
          { name: 'Giải thích đúng nguyên lý cốt lõi', maxScore: 5 },
          { name: 'Ví dụ minh họa & phân tích đánh đổi', maxScore: 5 },
        ],
      },
    ],
    ...overrides,
  }
}

export interface MockInClassShift {
  id: string
  classId: string
  className: string
  courseCode: string
  title: string
  description: string
  status: 'SCHEDULED' | 'ACTIVE' | 'CLOSED' | 'FINALIZED'
  isLocked: boolean
  startTime: string
  endTime: string
  durationMinutes: number
  examDurationMinutes: number
  questionCount: number
  hasFollowUp: boolean
  maxFollowUpQuestions: number
  isAutoPublishGrades: boolean
  questionPoolMode: 'ALL_COURSE_BANK' | 'SELECTED_CLOS' | 'SELECTED_POOL'
  selectedDifficulties: Array<'easy' | 'medium' | 'hard'>
  selectedCloIds: string[]
  selectedCloCodes: string[]
  passwordRotationSeconds: number
  gracePeriodSeconds: number
  joinedCount: number
  submittedCount: number
  createdAt: string
  finalizedAt?: string | null
  myParticipationStatus?: string | null
  myJoinedAt?: string | null
  myExamEndsAt?: string | null
  myRemainingSeconds?: number | null
}

export function makeInClassShift(overrides: Partial<MockInClassShift> = {}): MockInClassShift {
  return {
    id: E2E_IDS.inClassShift,
    classId: E2E_IDS.class1,
    className: 'Lớp SE1801 - PRN231',
    courseCode: 'PRN231',
    title: 'Kiểm tra vấn đáp Chương 1–3',
    description: 'Đánh giá hiểu biết về Clean Architecture và REST API.',
    status: 'SCHEDULED',
    isLocked: false,
    startTime: BASE_ISO,
    endTime: '2026-11-17T09:00:00.000Z',
    durationMinutes: 30,
    examDurationMinutes: 15,
    questionCount: 2,
    hasFollowUp: true,
    maxFollowUpQuestions: 2,
    isAutoPublishGrades: false,
    questionPoolMode: 'SELECTED_CLOS',
    selectedDifficulties: ['easy', 'medium'],
    selectedCloIds: [E2E_IDS.clo1],
    selectedCloCodes: ['CLO1'],
    passwordRotationSeconds: 300,
    gracePeriodSeconds: 45,
    joinedCount: 0,
    submittedCount: 0,
    createdAt: BASE_ISO,
    ...overrides,
  }
}

export interface MockProblemDetails {
  type: string
  title: string
  status: number
  detail: string
  instance?: string
  code?: string
  errorCode?: string
  traceId?: string
  errors?: Record<string, string[]>
}

export function makeProblemDetails(
  status: number,
  title: string,
  detail: string,
  extra: Partial<MockProblemDetails> = {},
): MockProblemDetails {
  return {
    type: 'https://tools.ietf.org/html/rfc7807',
    title,
    status,
    detail,
    traceId: 'e2e-trace-0001',
    ...extra,
  }
}
