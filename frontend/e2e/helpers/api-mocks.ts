import type { Page, Route } from '@playwright/test'
import { isLiveApiMode } from '../fixtures/env'
import {
  BACKEND_ROLE_BY_CANONICAL,
  E2E_IDS,
  makeClass,
  makeClo,
  makeCourse,
  makeExamStructure,
  makeProblemDetails,
  makeQuestion,
  makeRubric,
  makeSemester,
  makeUser,
  makeUuid,
  type CanonicalRole,
} from './mock-data'

export interface ApiRequestRecord {
  method: string
  url: string
  pathname: string
  search: string
  headers: Record<string, string>
  body: unknown
}

export interface Wave01DefaultOverrides {
  activeUser?: ReturnType<typeof makeUser>
  notificationsUnread?: number
  notificationsItems?: Array<Record<string, unknown>>
}

export interface ApiMocksFixture {
  setupWave01Defaults: (overrides?: Wave01DefaultOverrides) => Promise<void>
  mockJson: (
    method: string,
    urlPattern: string | RegExp,
    status: number,
    body: unknown,
    headers?: Record<string, string>,
  ) => Promise<void>
  mockProblem: (
    method: string,
    urlPattern: string | RegExp,
    status: number,
    detail: string,
    extra?: {
      title?: string
      code?: string
      errorCode?: string
      errors?: Record<string, string[]>
      headers?: Record<string, string>
    },
  ) => Promise<void>
  getRequests: (urlPattern?: string | RegExp) => ApiRequestRecord[]
  getUnmockedHits: () => string[]
}

function parseRequestBody(raw: string | null): unknown {
  if (!raw) return null
  try {
    return JSON.parse(raw) as unknown
  } catch {
    return raw
  }
}

function normalizePatternPath(rawPath: string): string {
  const trimmed = rawPath.replace(/\/+$/, '')
  return trimmed.length > 0 ? trimmed : '/'
}

function globToRegExp(glob: string): RegExp {
  const SLASH_DOUBLE_STAR = '__E2E_SLASH_DOUBLE_STAR__'
  const DOUBLE_STAR = '__E2E_DOUBLE_STAR__'
  const escaped = glob
    .replace(/[.+?^${}()|[\]\\]/g, '\\$&')
    .replace(/\/\*\*\//g, SLASH_DOUBLE_STAR)
    .replace(/\*\*/g, DOUBLE_STAR)
    .replace(/\*/g, '[^/]*')
    .replaceAll(SLASH_DOUBLE_STAR, '/(?:.*/)?')
    .replaceAll(DOUBLE_STAR, '.*')
  return new RegExp(`^${escaped}$`)
}

function matchesUrlPattern(url: string, pattern: string | RegExp): boolean {
  if (typeof pattern === 'string') {
    const parsed = new URL(url, 'http://127.0.0.1')
    const normalizedPathname = normalizePatternPath(parsed.pathname)
    const hasQuery = pattern.includes('?')

    if (pattern.includes('*')) {
      if (pattern.startsWith('/')) {
        const [rawPath, rawSearch] = pattern.split('?')
        const normalizedPatPath = normalizePatternPath(rawPath ?? '/')
        const glob = hasQuery ? `${normalizedPatPath}?${rawSearch ?? ''}` : normalizedPatPath
        const target = hasQuery ? `${normalizedPathname}${parsed.search}` : normalizedPathname
        return globToRegExp(glob).test(target)
      }
      const target = hasQuery ? url : `${parsed.origin}${normalizedPathname}`
      const glob = hasQuery ? pattern : normalizePatternPath(pattern)
      return globToRegExp(glob).test(target)
    }

    if (pattern.startsWith('http://') || pattern.startsWith('https://')) {
      const parsedPattern = new URL(pattern)
      const patPath = normalizePatternPath(parsedPattern.pathname)
      return hasQuery
        ? `${normalizedPathname}${parsed.search}` === `${patPath}${parsedPattern.search}`
        : normalizedPathname === patPath
    }

    const [rawPath, rawSearch] = pattern.split('?')
    const patPath = normalizePatternPath(rawPath ?? '/')
    return hasQuery
      ? `${normalizedPathname}${parsed.search}` === `${patPath}?${rawSearch ?? ''}`
      : normalizedPathname === patPath
  }
  return pattern.test(url)
}

function jsonFulfill(
  route: Route,
  status: number,
  body: unknown,
  headers?: Record<string, string>,
): Promise<void> {
  if (status === 204) {
    return route.fulfill({ status: 204, headers, body: '' })
  }
  return route.fulfill({
    status,
    contentType: 'application/json',
    headers,
    body: JSON.stringify(body),
  })
}

export function createRequestTracker(page: Page): ApiRequestRecord[] {
  const records: ApiRequestRecord[] = []
  page.on('request', (request) => {
    const rawUrl = request.url()
    if (!rawUrl.includes('/api/')) return
    const parsed = new URL(rawUrl)
    records.push({
      method: request.method().toUpperCase(),
      url: rawUrl,
      pathname: parsed.pathname,
      search: parsed.search,
      headers: request.headers(),
      body: parseRequestBody(request.postData()),
    })
  })
  return records
}

function buildAdminUserList() {
  const roles: CanonicalRole[] = [
    'Student',
    'Instructor',
    'DepartmentHead',
    'ExaminationOfficer',
    'Proctor',
    'Admin',
  ]
  return roles.map((role) => {
    const user = makeUser(role)
    return {
      ...user,
      role: BACKEND_ROLE_BY_CANONICAL[role],
    }
  })
}

export async function mountCatchAllApiFallback(
  page: Page,
  unmockedHits: string[],
): Promise<void> {
  if (isLiveApiMode()) return

  await page.route('**/api/v1/**', async (route) => {
    const req = route.request()
    const parsed = new URL(req.url())
    const signature = `${req.method().toUpperCase()} ${parsed.pathname}${parsed.search}`
    unmockedHits.push(signature)
    await route.fulfill({
      status: 501,
      contentType: 'application/problem+json',
      body: JSON.stringify(
        makeProblemDetails(
          501,
          'Unmocked E2E Endpoint',
          `Endpoint ${signature} was not mocked in mocked-contract mode.`,
          { instance: parsed.pathname, code: 'UNMOCKED_E2E_ROUTE' },
        ),
      ),
    })
  })
}

export function createApiMocksController(
  page: Page,
  tracker: ApiRequestRecord[],
  unmockedHits: string[],
): ApiMocksFixture {
  const defaultState: Wave01DefaultOverrides = {}

  const setupWave01Defaults = async (overrides: Wave01DefaultOverrides = {}): Promise<void> => {
    Object.assign(defaultState, overrides)
    if (isLiveApiMode()) return

    await page.route('**/api/v1/**', async (route) => {
      const request = route.request()
      const method = request.method().toUpperCase()
      const parsed = new URL(request.url())
      const path = parsed.pathname.replace(/\/+$/, '')

      // 1. Auth defaults (refresh-token MUST default to 401 ProblemDetails, never 501!)
      if (path === '/api/v1/auth/refresh-token' && method === 'POST') {
        return route.fulfill({
          status: 401,
          contentType: 'application/problem+json',
          body: JSON.stringify(
            makeProblemDetails(401, 'Unauthorized', 'Phiên đăng nhập đã hết hạn hoặc không tồn tại.', {
              instance: path,
              code: 'REFRESH_TOKEN_INVALID',
            }),
          ),
        })
      }

      if (path === '/api/v1/auth/sign-out' && method === 'POST') {
        return jsonFulfill(route, 200, { message: 'Đã đăng xuất thành công.' })
      }

      if (path === '/api/v1/auth/google-challenge' && method === 'POST') {
        return jsonFulfill(route, 200, {
          nonce: 'E'.repeat(43),
          expiresIn: 120,
        })
      }

      if (path === '/api/v1/auth/me' && method === 'GET') {
        if (defaultState.activeUser) {
          return jsonFulfill(route, 200, defaultState.activeUser)
        }
        return route.fulfill({
          status: 401,
          contentType: 'application/problem+json',
          body: JSON.stringify(
            makeProblemDetails(401, 'Unauthorized', 'Yêu cầu xác thực.', { instance: path }),
          ),
        })
      }

      // 2. Notifications 3NF (FE-11)
      if (path === '/api/v1/notifications' && method === 'GET') {
        const items = defaultState.notificationsItems ?? []
        const unreadCount = defaultState.notificationsUnread ?? items.filter((n) => n.isRead === false).length
        return jsonFulfill(route, 200, {
          items,
          unreadCount,
          totalCount: items.length,
          page: 1,
          pageSize: 20,
        })
      }
      if (/^\/api\/v1\/notifications\/[^/]+\/read$/.test(path) && (method === 'POST' || method === 'PUT')) {
        const id = path.split('/')[4] ?? E2E_IDS.notification
        return jsonFulfill(route, 200, {
          id,
          notificationId: id,
          isRead: true,
          readAt: '2026-11-17T08:05:00.000Z',
        })
      }
      if (path === '/api/v1/notifications/read-all' && (method === 'POST' || method === 'PUT')) {
        return jsonFulfill(route, 200, {
          isReadAll: true,
          updatedCount: defaultState.notificationsUnread ?? 0,
          message: 'Đã đánh dấu tất cả thông báo là đã đọc.',
        })
      }

      // 3. Diagnostics & Admin Dashboard mounts
      if (path === '/api/v1/diagnostics/abuse' && method === 'GET') {
        return jsonFulfill(route, 200, {
          requests: 120,
          rejected: 0,
          loginLockouts: 0,
          scope: 'api-process',
        })
      }
      if (path === '/api/v1/admin/diagnostics/runtime' && method === 'GET') {
        return jsonFulfill(route, 200, {
          timestamp: '2026-11-17T08:00:00.000Z',
          uptime: '02:15:30',
          boundedChannels: {
            gradingQueue: { count: 0, capacity: 1000 },
          },
          deadLetterQueue: {
            totalPending: 0,
            byFlowType: { practice: 0, mockExam: 0, inClassExam: 0 },
          },
          abuseMetrics: {
            requests: 120,
            rejected: 0,
            loginLockouts: 0,
          },
          performance: {
            totalRequests: 120,
            serverErrors5xx: 0,
            serverErrorRatePercent: 0,
            averageLatencyMs: 24.5,
            p95LatencyMs: 68.2,
          },
          system: {
            workingSetMb: 128,
            gcTotalMemoryMb: 42,
            threadCount: 16,
          },
        })
      }
      if (path === '/api/v1/academic-programs' && method === 'GET') {
        const deptHead = makeUser('DepartmentHead')
        return jsonFulfill(route, 200, [
          {
            id: makeUuid('0014', 1),
            code: 'SE',
            name: 'Software Engineering',
            departmentHeadUserId: deptHead.id,
            departmentHeadName: deptHead.fullName,
            isActive: true,
            coursesCount: 1,
          },
        ])
      }
      if (path === '/api/v1/admin/users' && method === 'GET') {
        const allUsers = buildAdminUserList()
        const roleFilter = parsed.searchParams.get('role')
        const items = roleFilter ? allUsers.filter((u) => u.role === roleFilter) : allUsers
        return jsonFulfill(route, 200, {
          items,
          totalCount: items.length,
          page: 1,
          pageSize: 15,
          totalPages: 1,
        })
      }
      if (path === '/api/v1/admin/courses' && method === 'GET') {
        return jsonFulfill(route, 200, [makeCourse()])
      }
      if (path === '/api/v1/admin/system-configs' && method === 'GET') {
        return jsonFulfill(route, 200, [
          {
            id: E2E_IDS.course,
            key: 'MaxPracticeFollowUpQuestions',
            value: '2',
            description: 'Số câu hỏi phụ tối đa trong MF-01',
            updatedAt: '2026-11-17T08:00:00.000Z',
          },
          {
            id: E2E_IDS.secondCourse,
            key: 'TranscriptBufferSeconds',
            value: '60',
            description: 'Thời gian sửa transcript trước khi nộp',
            updatedAt: '2026-11-17T08:00:00.000Z',
          },
        ])
      }
      if (path === '/api/v1/admin/notifications' && method === 'GET') {
        return jsonFulfill(route, 200, {
          items: [],
          totalCount: 0,
          unreadTotalCount: 0,
          page: 1,
          pageSize: 20,
          totalPages: 1,
        })
      }
      if (path === '/api/v1/admin/audit-logs' && method === 'GET') {
        return jsonFulfill(route, 200, {
          items: [],
          totalCount: 0,
          page: 1,
          pageSize: 20,
          totalPages: 1,
        })
      }
      if ((path === '/api/v1/admin/dlq/tasks' || path === '/api/v1/dlq/tasks') && method === 'GET') {
        return jsonFulfill(route, 200, [])
      }

      // 4. Semesters, Courses, CLOs, Lecturer Assignments
      if (path === '/api/v1/semesters' && method === 'GET') {
        return jsonFulfill(route, 200, [makeSemester()])
      }
      if (/^\/api\/v1\/semesters\/[^/]+\/course-offerings$/.test(path) && method === 'GET') {
        const course = makeCourse()
        return jsonFulfill(route, 200, [
          {
            courseId: course.id,
            code: course.code,
            name: course.name,
            description: course.description,
            credits: course.credits,
            academicProgramId: null,
            academicProgramCode: 'SE',
            isCourseActive: course.isActive,
            courseOfferingId: E2E_IDS.offering,
            isSelected: true,
          },
        ])
      }
      if (path === '/api/v1/courses/managed-for-clos' && method === 'GET') {
        const course = makeCourse()
        return jsonFulfill(route, 200, [{ id: course.id, code: course.code, name: course.name }])
      }
      if (/^\/api\/v1\/courses\/[^/]+\/clos$/.test(path) && method === 'GET') {
        return jsonFulfill(route, 200, [makeClo()])
      }
      if (/^\/api\/v1\/courses\/[^/]+\/configurations$/.test(path) && method === 'GET') {
        const course = makeCourse()
        return jsonFulfill(route, 200, {
          courseId: course.id,
          courseCode: course.code,
          courseName: course.name,
          transcriptBufferSeconds: course.transcriptBufferSeconds,
          maxFollowUpQuestions: course.maxFollowUpQuestions,
          hasFollowUp: course.hasFollowUp,
          examInputMode: course.examInputMode,
          isActive: course.isActive,
        })
      }
      if (path === '/api/v1/courses' && method === 'GET') {
        return jsonFulfill(route, 200, [makeCourse()])
      }
      if (/^\/api\/v1\/course-offerings\/[^/]+\/lecturers$/.test(path) && method === 'GET') {
        return jsonFulfill(route, 200, [])
      }
      if (/^\/api\/v1\/course-offerings\/[^/]+\/lecturer-directory$/.test(path) && method === 'GET') {
        const lecturer = makeUser('Instructor')
        return jsonFulfill(route, 200, [
          { id: lecturer.id, fullName: lecturer.fullName, email: lecturer.email },
        ])
      }

      // 5. Lecturer, Classes, Questions, Rubrics, Exam Structures
      if (path === '/api/v1/lecturer/course-offerings' && method === 'GET') {
        const course = makeCourse()
        const semester = makeSemester()
        return jsonFulfill(route, 200, [
          {
            courseOfferingId: E2E_IDS.offering,
            courseId: course.id,
            courseCode: course.code,
            courseName: course.name,
            semesterId: semester.id,
            semesterCode: semester.code,
            isOfferingEnabled: true,
          },
        ])
      }
      if (path === '/api/v1/lecturer/courses' && method === 'GET') {
        const course = makeCourse()
        return jsonFulfill(route, 200, [{ id: course.id, code: course.code, name: course.name }])
      }
      if (path === '/api/v1/lecturer/classes' && method === 'GET') {
        return jsonFulfill(route, 200, [makeClass()])
      }
      if (path === '/api/v1/lecturer/classes/students/lookup' && method === 'GET') {
        const stu = makeUser('Student')
        return jsonFulfill(route, 200, {
          studentId: stu.id,
          fullName: stu.fullName,
          email: stu.email,
          studentCode: stu.studentCode ?? 'SE180001',
        })
      }
      if (/^\/api\/v1\/lecturer\/classes\/[^/]+$/.test(path) && method === 'GET') {
        return jsonFulfill(route, 200, makeClass())
      }
      if (path === '/api/v1/lecturer/in-class-shifts/pool-stats' && method === 'GET') {
        return jsonFulfill(route, 200, {
          classId: E2E_IDS.class1,
          courseId: E2E_IDS.course,
          courseCode: 'PRN231',
          courseBankCounts: { easy: 2, medium: 2, hard: 1, total: 5 },
          clos: [
            {
              cloId: E2E_IDS.clo1,
              cloCode: 'CLO1',
              description: makeClo().description,
              counts: { easy: 2, medium: 2, hard: 1, total: 5 },
            },
          ],
        })
      }
      if (/^\/api\/v1\/lecturer\/in-class-shifts\/by-class\/[^/]+$/.test(path) && method === 'GET') {
        return jsonFulfill(route, 200, [])
      }
      if (path === '/api/v1/questions' && method === 'GET') {
        return jsonFulfill(route, 200, {
          items: [makeQuestion()],
          totalCount: 1,
          pageNumber: 1,
          pageSize: 100,
          totalPages: 1,
        })
      }
      if (/^\/api\/v1\/questions\/[^/]+$/.test(path) && method === 'GET') {
        return jsonFulfill(route, 200, makeQuestion())
      }
      if (path === '/api/v1/rubrics' && method === 'GET') {
        return jsonFulfill(route, 200, [makeRubric()])
      }
      if (path === '/api/v1/exam-structures' && method === 'GET') {
        return jsonFulfill(route, 200, [makeExamStructure()])
      }

      // 6. Student Dashboard, Classes, Practice, Mock Exams
      if (path === '/api/v1/student/dashboard' && method === 'GET') {
        return jsonFulfill(route, 200, { officialExamTickets: [] })
      }
      if (/^\/api\/v1\/student\/official-exams\/[^/]+\/grade$/.test(path) && method === 'GET') {
        const student = makeUser('Student')
        return jsonFulfill(route, 200, {
          ticketId: E2E_IDS.ticket,
          studentCode: student.studentCode ?? 'SE180001',
          fullName: student.fullName,
          courseCode: 'PRN231',
          status: 'PUBLISHED',
          isLocked: true,
          totalScore: 8.5,
          publishedAt: '2026-11-17T08:00:00.000Z',
          publishedBy: 'Phạm Hoàng Khảo Thí',
          studentAcknowledgementStatus: 'PENDING',
          detailedScorecard: [],
          internalAppeal: {
            canAppeal: true,
            instruction: 'Sinh viên có 3 ngày để xác nhận hoặc phúc khảo.',
            appealDeadlineDays: 3,
            appealDeadlineAtUtc: '2026-11-20T08:00:00.000Z',
            canAcknowledge: true,
            appealEndpoint: '/api/v1/appeals',
          },
        })
      }
      if (path === '/api/v1/student/classes/my-classes' && method === 'GET') {
        return jsonFulfill(route, 200, [makeClass()])
      }
      if (path === '/api/v1/student/classes/search' && method === 'GET') {
        return jsonFulfill(route, 200, [makeClass({ isAlreadyEnrolled: false, canJoin: true })])
      }
      if (path === '/api/v1/student/in-class-shifts/my' && method === 'GET') {
        return jsonFulfill(route, 200, [])
      }
      if (path === '/api/v1/practice/student/history' && method === 'GET') {
        return jsonFulfill(route, 200, [])
      }
      if (path === '/api/v1/mock-exams/quota' && method === 'GET') {
        return jsonFulfill(route, 200, {
          courseId: E2E_IDS.course,
          studentId: E2E_IDS.users.Student,
          quotaDate: '2026-11-17',
          usedCount: 0,
          remainingCount: 3,
          maxDailyQuota: 3,
          canStartExam: true,
        })
      }
      if (path === '/api/v1/mock-exams/history' && method === 'GET') {
        return jsonFulfill(route, 200, {
          items: [],
          totalCount: 0,
          pageIndex: 1,
          pageSize: 10,
          totalPages: 1,
        })
      }

      // 7. Examination Officer & Official Exams (for RBAC portal mount checks only)
      if (path === '/api/v1/examination-officer/dlq/tasks' && method === 'GET') {
        return jsonFulfill(route, 200, [])
      }
      if (path === '/api/v1/official-exams/sessions' && method === 'GET') {
        return jsonFulfill(route, 200, [])
      }
      if (path === '/api/v1/official-exams/shifts' && method === 'GET') {
        return jsonFulfill(route, 200, [])
      }
      if (path === '/api/v1/official-exams/exam-sets' && method === 'GET') {
        return jsonFulfill(route, 200, [])
      }
      if (path === '/api/v1/appeals' && method === 'GET') {
        return jsonFulfill(route, 200, [])
      }

      // Fall through to lowest-priority 501 catch-all if genuinely unmocked
      return route.fallback()
    })
  }

  const mockJson = async (
    method: string,
    urlPattern: string | RegExp,
    status: number,
    body: unknown,
    headers?: Record<string, string>,
  ): Promise<void> => {
    if (isLiveApiMode()) return
    const upperMethod = method.toUpperCase()
    await page.route(
      (url) => matchesUrlPattern(url.toString(), urlPattern),
      async (route) => {
        if (route.request().method().toUpperCase() !== upperMethod) {
          return route.fallback()
        }
        return jsonFulfill(route, status, body, headers)
      },
    )
  }

  const mockProblem = async (
    method: string,
    urlPattern: string | RegExp,
    status: number,
    detail: string,
    extra: {
      title?: string
      code?: string
      errorCode?: string
      errors?: Record<string, string[]>
      headers?: Record<string, string>
    } = {},
  ): Promise<void> => {
    if (isLiveApiMode()) return
    const upperMethod = method.toUpperCase()
    const payload = makeProblemDetails(status, extra.title ?? 'Request Failed', detail, {
      code: extra.code,
      errorCode: extra.errorCode,
      errors: extra.errors,
    })
    await page.route(
      (url) => matchesUrlPattern(url.toString(), urlPattern),
      async (route) => {
        if (route.request().method().toUpperCase() !== upperMethod) {
          return route.fallback()
        }
        return route.fulfill({
          status,
          contentType: 'application/problem+json',
          headers: extra.headers,
          body: JSON.stringify(payload),
        })
      },
    )
  }

  const getRequests = (urlPattern?: string | RegExp): ApiRequestRecord[] => {
    if (!urlPattern) return [...tracker]
    return tracker.filter((entry) => matchesUrlPattern(entry.url, urlPattern))
  }

  const getUnmockedHits = (): string[] => [...unmockedHits]

  return {
    setupWave01Defaults,
    mockJson,
    mockProblem,
    getRequests,
    getUnmockedHits,
  }
}
