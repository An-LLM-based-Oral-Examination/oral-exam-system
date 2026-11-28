import { expect, test } from '../fixtures/test'
import { isLiveApiMode } from '../fixtures/env'
import {
  E2E_IDS,
  makeCourse,
  makeProblemDetails,
  makeUser,
  makeUuid,
  type MockCourse,
  type MockUser,
} from '../helpers/mock-data'

test.describe('Area 2 — Admin Portal Completion (R1–R5 & Academic Programs)', () => {
  test('A2-ADM-01 @tier1 Admin Overview and DLQ include IN_CLASS_EXAM (MF-05) while isolating OFFICIAL_EXAM (MF-04)', async ({
    page,
    authSession,
    apiMocks,
  }) => {
    await authSession.seedSession('Admin')

    const practiceTaskId = makeUuid('0091', 1)
    const inClassTaskId = makeUuid('0091', 2)
    const officialTaskId = makeUuid('0091', 3)
    let replayedTaskId: string | null = null

    if (!isLiveApiMode()) {
      await apiMocks.mockJson('GET', '/api/v1/admin/diagnostics/runtime', 200, {
        timestamp: '2026-11-19T08:00:00.000Z',
        uptime: '04:12:00',
        boundedChannels: { gradingQueue: { count: 2, capacity: 1000 } },
        deadLetterQueue: {
          totalPending: 4,
          byFlowType: { practice: 1, mockExam: 1, inClassExam: 2 },
        },
        abuseMetrics: { requests: 250, rejected: 1, loginLockouts: 0 },
        performance: {
          totalRequests: 250,
          serverErrors5xx: 0,
          serverErrorRatePercent: 0,
          averageLatencyMs: 18.4,
          p95LatencyMs: 52.1,
        },
        system: { workingSetMb: 132.5, gcTotalMemoryMb: 44.2, threadCount: 18 },
      })

      const allTasks = [
        {
          id: practiceTaskId,
          taskType: 'PracticeGrading',
          flowType: 'PRACTICE',
          status: 'pending',
          retryCount: 3,
          errorMessage: 'Gemini timeout on MF-01 practice answer',
          payloadJson: '{}',
          managedByRole: 'admin',
          createdAt: '2026-11-19T07:30:00.000Z',
        },
        {
          id: inClassTaskId,
          taskType: 'InClassGrading',
          flowType: 'IN_CLASS_EXAM',
          status: 'pending',
          retryCount: 2,
          errorMessage: 'Whisper STT transient failure on MF-05 in-class submission',
          payloadJson: '{}',
          managedByRole: 'admin',
          createdAt: '2026-11-19T07:45:00.000Z',
        },
        {
          id: officialTaskId,
          taskType: 'OfficialExamGrading',
          flowType: 'OFFICIAL_EXAM',
          status: 'pending',
          retryCount: 1,
          errorMessage: 'SECRET MF-04 official grading row must stay hidden from Admin',
          payloadJson: '{}',
          managedByRole: 'examination_officer',
          createdAt: '2026-11-19T07:50:00.000Z',
        },
      ]

      await page.route('**/api/v1/admin/dlq/tasks**', async (route) => {
        const req = route.request()
        const method = req.method().toUpperCase()
        const url = new URL(req.url())

        if (method === 'GET' && url.pathname.endsWith('/api/v1/admin/dlq/tasks')) {
          const flow = url.searchParams.get('flowType')
          const visible = allTasks
            .filter((item) => item.id !== replayedTaskId)
            .filter((item) => !flow || item.flowType === flow)
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(visible),
          })
          return
        }

        if (method === 'POST' && url.pathname.endsWith(`/${inClassTaskId}/replay`)) {
          replayedTaskId = inClassTaskId
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
              id: inClassTaskId,
              dlqId: inClassTaskId,
              status: 'requeued',
              message: 'Tác vụ DLQ MF-05 đã được đưa lại vào hàng đợi chấm.',
            }),
          })
          return
        }

        return route.fallback()
      })
    }

    // 1. Overview KPI + Runtime Diagnostics + Sidebar consistency for MF-01/02/05
    await page.goto('/admin/dashboard', { waitUntil: 'domcontentloaded' })
    await expect(page.getByText('DLQ chờ xử lý (MF-01/02/05)')).toBeVisible()
    await expect(page.getByRole('link', { name: 'Lỗi chấm MF-01/02/05' })).toBeVisible()
    await expect(page.getByRole('link', { name: 'Mở hàng đợi lỗi MF-01/02/05' })).toBeVisible()
    if (!isLiveApiMode()) {
      await expect(page.getByText('MF-01: 1 · MF-02: 1 · MF-05: 2')).toBeVisible()
    }

    // 2. DLQ page shows PRACTICE + IN_CLASS_EXAM and filters out OFFICIAL_EXAM
    await page.goto('/admin/dlq', { waitUntil: 'domcontentloaded' })
    await expect(
      page.getByText('Chỉ hiển thị các luồng MF-01 (PRACTICE), MF-02 (MOCK_EXAM) và MF-05 (IN_CLASS_EXAM)'),
    ).toBeVisible()

    if (!isLiveApiMode()) {
      await expect(page.getByText('Gemini timeout on MF-01 practice answer')).toBeVisible()
      await expect(
        page.getByText('Whisper STT transient failure on MF-05 in-class submission'),
      ).toBeVisible()
      await expect(
        page.getByText('SECRET MF-04 official grading row must stay hidden from Admin'),
      ).toHaveCount(0)

      // 3. Filter explicitly to IN_CLASS_EXAM (MF-05) and replay
      await page.getByRole('button', { name: /Luồng nghiệp vụ/i }).click()
      await page.getByRole('option', { name: 'Ca thi trong lớp (MF-05)' }).click()
      await expect(page).toHaveURL(/flow=IN_CLASS_EXAM/)
      await expect(page.getByText('Gemini timeout on MF-01 practice answer')).toHaveCount(0)

      const inClassCard = page
        .locator('article')
        .filter({ hasText: 'Whisper STT transient failure on MF-05 in-class submission' })
      await expect(inClassCard).toBeVisible()
      await inClassCard.getByRole('button', { name: /Chạy lại|Replay/i }).click()

      await expect(
        page.getByText('Whisper STT transient failure on MF-05 in-class submission'),
      ).toHaveCount(0)
      expect(
        apiMocks
          .getRequests('/api/v1/admin/dlq/tasks/*/replay')
          .some((r) => r.method === 'POST' && r.pathname.includes(inClassTaskId)),
      ).toBe(true)
    }
  })

  test('A2-ADM-02 @tier1 Admin creates non-student with normalized username and edits user profile via PUT /admin/users/{id} with 409 handling', async ({
    page,
    authSession,
    apiMocks,
  }) => {
    await authSession.seedSession('Admin')

    const createdId = makeUuid('0092', 1)
    type AdminUserRecord = Omit<MockUser, 'role' | 'username'> & {
      role: string
      username: string | null
    }
    let users: AdminUserRecord[] = [
      { ...makeUser('Admin'), role: 'admin', username: 'admin.root' },
      { ...makeUser('Instructor'), role: 'lecturer', username: 'giangvien.se' },
    ]
    let failFirstPutWithConflict = true

    if (!isLiveApiMode()) {
      await page.route('**/api/v1/admin/users**', async (route) => {
        const req = route.request()
        const method = req.method().toUpperCase()
        const url = new URL(req.url())

        if (method === 'GET' && url.pathname.endsWith('/api/v1/admin/users')) {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
              items: users,
              totalCount: users.length,
              page: 1,
              pageSize: 15,
              totalPages: 1,
            }),
          })
          return
        }

        if (method === 'POST' && url.pathname.endsWith('/api/v1/admin/users')) {
          const body = req.postDataJSON() as {
            email: string
            fullName: string
            role: string
            username?: string | null
            studentCode?: string | null
          }
          const created: AdminUserRecord = {
            id: createdId,
            email: body.email,
            fullName: body.fullName,
            role: body.role,
            username: body.username ?? null,
            studentCode: body.studentCode ?? null,
            department: null,
            isActive: true,
            mustChangePassword: true,
            createdAt: '2026-11-19T08:00:00.000Z',
            updatedAt: '2026-11-19T08:00:00.000Z',
          }
          users = [created, ...users]
          await route.fulfill({
            status: 201,
            contentType: 'application/json',
            body: JSON.stringify(created),
          })
          return
        }

        if (method === 'PUT' && url.pathname.endsWith(`/api/v1/admin/users/${createdId}`)) {
          if (failFirstPutWithConflict) {
            failFirstPutWithConflict = false
            await route.fulfill({
              status: 409,
              contentType: 'application/problem+json',
              body: JSON.stringify(
                makeProblemDetails(
                  409,
                  'Xung đột nghiệp vụ quản trị.',
                  'CONFLICT: Tên đăng nhập đã tồn tại trong hệ thống.',
                  { instance: url.pathname },
                ),
              ),
            })
            return
          }

          const body = req.postDataJSON() as {
            email: string
            fullName: string
            username?: string | null
            studentCode?: string | null
          }
          const existing = users.find((u) => u.id === createdId)!
          const updated = {
            ...existing,
            email: body.email,
            fullName: body.fullName,
            username: body.username ?? null,
            studentCode: body.studentCode ?? null,
            updatedAt: '2026-11-19T08:15:00.000Z',
          }
          users = users.map((u) => (u.id === createdId ? updated : u))
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(updated),
          })
          return
        }

        return route.fallback()
      })
    }

    await page.goto('/admin/users', { waitUntil: 'domcontentloaded' })

    // 1. Create non-student (lecturer) with uppercase input -> normalized lowercase username
    await page.getByRole('button', { name: 'Tạo tài khoản mới' }).click()
    const createDialog = page.getByRole('dialog', { name: 'Tạo tài khoản người dùng mới' })
    await createDialog.locator('#create-user-email').fill('Lecturer.ENT1@fe.edu.vn')
    await createDialog.locator('#create-user-fullname').fill('Giảng Viên ENT 1')
    await createDialog.locator('#create-user-role').selectOption('lecturer')
    await createDialog.locator('#create-user-password').fill('TempPass!123')
    await createDialog.locator('#create-user-username').fill('Lecturer-ENT1')
    await createDialog.getByRole('button', { name: 'Tạo tài khoản' }).click()
    await expect(createDialog).toBeHidden()

    const createdRow = page.locator('tr').filter({ hasText: 'lecturer.ent1@fe.edu.vn' })
    await expect(createdRow.getByText('@lecturer-ent1')).toBeVisible()

    if (!isLiveApiMode()) {
      const createReq = apiMocks
        .getRequests('/api/v1/admin/users')
        .find((r) => r.method === 'POST')
      expect(createReq?.body).toMatchObject({
        email: 'lecturer.ent1@fe.edu.vn',
        role: 'lecturer',
        username: 'lecturer-ent1',
      })
    }

    // 2. Edit profile -> 409 conflict stays open with alert -> retry succeeds
    await createdRow.getByRole('button', { name: 'Sửa' }).click()
    const editDialog = page.getByRole('dialog', { name: 'Chỉnh sửa thông tin tài khoản' })
    await expect(editDialog).toBeVisible()
    await editDialog.locator('#edit-user-fullname').fill('Giảng Viên ENT 1 Updated')
    await editDialog.locator('#edit-user-username').fill('lecturer-ent1-new')
    await editDialog.getByRole('button', { name: 'Lưu thay đổi' }).click()

    if (!isLiveApiMode()) {
      await expect(
        editDialog.getByRole('alert').filter({ hasText: /Tên đăng nhập đã tồn tại/i }),
      ).toBeVisible()
      await editDialog.getByRole('button', { name: 'Lưu thay đổi' }).click()
    }
    await expect(editDialog).toBeHidden()

    const updatedRow = page.locator('tr').filter({ hasText: 'Giảng Viên ENT 1 Updated' })
    await expect(updatedRow.getByText('@lecturer-ent1-new')).toBeVisible()

    if (!isLiveApiMode()) {
      const putReq = apiMocks
        .getRequests(`/api/v1/admin/users/${createdId}`)
        .find((r) => r.method === 'PUT')
      expect(putReq?.body).toMatchObject({
        email: 'lecturer.ent1@fe.edu.vn',
        fullName: 'Giảng Viên ENT 1 Updated',
        username: 'lecturer-ent1-new',
      })
    }
  })

  test('A2-ADM-03 @tier1 Admin Audit Logs action presets include all new academic program and USER_UPDATED actions', async ({
    page,
    authSession,
    apiMocks,
  }) => {
    await authSession.seedSession('Admin')

    const auditLogId = makeUuid('0093', 1)
    if (!isLiveApiMode()) {
      await page.route('**/api/v1/admin/audit-logs**', async (route) => {
        const url = new URL(route.request().url())
        const action = url.searchParams.get('action')
        const items =
          action === 'USER_UPDATED'
            ? [
                {
                  id: auditLogId,
                  userId: E2E_IDS.users.Admin,
                  userEmail: 'admin.e2e@fe.edu.vn',
                  userFullName: 'Quản Trị Hệ Thống',
                  action: 'USER_UPDATED',
                  entityName: 'users',
                  entityId: E2E_IDS.users.Instructor,
                  oldValues: 'username=old.lec',
                  newValues: 'username=new.lec',
                  ipAddress: '127.0.0.1',
                  createdAt: '2026-11-19T08:10:00.000Z',
                },
              ]
            : []
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items,
            totalCount: items.length,
            page: 1,
            pageSize: 15,
            totalPages: items.length > 0 ? 1 : 0,
          }),
        })
      })
    }

    await page.goto('/admin/audit-logs', { waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: /Hành động \(action\)/i }).click()

    for (const expectedAction of [
      'USER_UPDATED',
      'ACADEMIC_PROGRAM_CREATED',
      'ACADEMIC_PROGRAM_UPDATED',
      'ACADEMIC_PROGRAM_DEPARTMENT_HEAD_ASSIGNED',
      'SEMESTER_COURSE_OFFERINGS_SYNCED',
    ]) {
      await expect(page.getByRole('option', { name: new RegExp(expectedAction) })).toBeVisible()
    }

    await page.getByRole('option', { name: /USER_UPDATED/ }).click()
    await expect(page).toHaveURL(/action=USER_UPDATED/)
    if (!isLiveApiMode()) {
      await expect(page.getByText('USER_UPDATED', { exact: true }).first()).toBeVisible()
      expect(
        apiMocks
          .getRequests('/api/v1/admin/audit-logs*')
          .some((r) => r.search.includes('action=USER_UPDATED')),
      ).toBe(true)
    }
  })

  test('A2-ADM-04 @tier1 Admin MF-01 Configs initializes, validates bounds [1..50], and updates MaxPracticeQuestionsPerSession', async ({
    page,
    authSession,
    apiMocks,
  }) => {
    await authSession.seedSession('Admin')

    let configs = [
      {
        id: makeUuid('0094', 1),
        key: 'MaxPracticeFollowUpQuestions',
        value: '2',
        description: 'Số câu hỏi phụ tối đa',
        updatedAt: '2026-11-19T08:00:00.000Z',
      },
      {
        id: makeUuid('0094', 2),
        key: 'MinMixedPracticeQuestions',
        value: '3',
        description: 'Số câu tối thiểu Progressive',
        updatedAt: '2026-11-19T08:00:00.000Z',
      },
      {
        id: makeUuid('0094', 3),
        key: 'MaxMixedPracticeQuestions',
        value: '10',
        description: 'Số câu tối đa Progressive',
        updatedAt: '2026-11-19T08:00:00.000Z',
      },
      {
        id: makeUuid('0094', 4),
        key: 'TranscriptBufferSeconds',
        value: '60',
        description: 'Thời gian đệm transcript',
        updatedAt: '2026-11-19T08:00:00.000Z',
      },
      {
        id: makeUuid('0094', 5),
        key: 'SessionInactivityTimeoutMinutes',
        value: '5',
        description: 'Timeout phiên luyện tập',
        updatedAt: '2026-11-19T08:00:00.000Z',
      },
    ]

    if (!isLiveApiMode()) {
      await page.route('**/api/v1/admin/system-configs**', async (route) => {
        const req = route.request()
        const method = req.method().toUpperCase()
        const url = new URL(req.url())

        if (method === 'GET' && url.pathname.endsWith('/api/v1/admin/system-configs')) {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(configs),
          })
          return
        }

        if (method === 'PUT' && url.pathname.endsWith('/MaxPracticeQuestionsPerSession')) {
          const body = req.postDataJSON() as { value: string; description?: string }
          const existingIdx = configs.findIndex((c) => c.key === 'MaxPracticeQuestionsPerSession')
          const updatedItem = {
            id: makeUuid('0094', 6),
            key: 'MaxPracticeQuestionsPerSession',
            value: body.value,
            description: body.description ?? 'Số lượng câu hỏi luyện tập tối đa',
            updatedAt: '2026-11-19T08:20:00.000Z',
          }
          if (existingIdx >= 0) {
            configs = configs.map((c, idx) => (idx === existingIdx ? updatedItem : c))
          } else {
            configs = [...configs, updatedItem]
          }
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(updatedItem),
          })
          return
        }

        return route.fallback()
      })
    }

    await page.goto('/admin/configs', { waitUntil: 'domcontentloaded' })
    const initBtn = page.getByRole('button', { name: 'Khởi tạo 1 tham số mặc định còn thiếu' })
    if (!isLiveApiMode()) {
      await initBtn.click()
    }

    const maxPracticeRow = page.locator('tr').filter({ hasText: 'MaxPracticeQuestionsPerSession' })
    await expect(maxPracticeRow).toBeVisible()
    if (!isLiveApiMode()) {
      await expect(maxPracticeRow.getByText('10', { exact: true })).toBeVisible()
    }

    // Edit with out-of-range value 51 -> blocked client-side
    await maxPracticeRow.getByRole('button', { name: 'Chỉnh sửa' }).click()
    const dialog = page.getByRole('dialog', { name: 'Chỉnh sửa tham số hệ thống' })
    await dialog.locator('input[type="number"]').fill('51')
    await dialog.getByRole('button', { name: 'Lưu cấu hình' }).click()
    await expect(dialog.getByText(/từ 1 đến 50/i)).toBeVisible()

    // Valid value 15 -> saves cleanly
    await dialog.locator('input[type="number"]').fill('15')
    await dialog.getByRole('button', { name: 'Lưu cấu hình' }).click()
    await expect(dialog).toBeHidden()
    await expect(maxPracticeRow.getByText('15', { exact: true })).toBeVisible()

    if (!isLiveApiMode()) {
      const putCalls = apiMocks.getRequests('/api/v1/admin/system-configs/MaxPracticeQuestionsPerSession')
      expect(putCalls.filter((r) => r.method === 'PUT')).toHaveLength(2)
      expect(putCalls[1]?.body).toMatchObject({ value: '15' })
    }
  })

  test('A2-ADM-05 @tier1 Admin Academic Program CRUD and Course academicProgramId assignment work without regression', async ({
    page,
    authSession,
    apiMocks,
  }) => {
    await authSession.seedSession('Admin')

    const seProgramId = makeUuid('0014', 1)
    const entProgramId = makeUuid('0014', 2)
    const deptHead = makeUser('DepartmentHead')
    interface AdminProgramRecord {
      id: string
      code: string
      name: string
      departmentHeadUserId: string | null
      departmentHeadName: string | null
      isActive: boolean
      coursesCount: number
    }
    type AdminCourseRecord = MockCourse & {
      academicProgramId: string | null
      academicProgramCode: string | null
    }
    let programs: AdminProgramRecord[] = [
      {
        id: seProgramId,
        code: 'SE',
        name: 'Software Engineering',
        departmentHeadUserId: deptHead.id,
        departmentHeadName: deptHead.fullName,
        isActive: true,
        coursesCount: 1,
      },
    ]
    let courses: AdminCourseRecord[] = [
      {
        ...makeCourse(),
        academicProgramId: seProgramId,
        academicProgramCode: 'SE',
      },
    ]

    if (!isLiveApiMode()) {
      await page.route('**/api/v1/academic-programs**', async (route) => {
        const req = route.request()
        const method = req.method().toUpperCase()
        const url = new URL(req.url())

        if (method === 'GET' && url.pathname.endsWith('/api/v1/academic-programs')) {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(programs),
          })
          return
        }
        if (method === 'POST' && url.pathname.endsWith('/api/v1/academic-programs')) {
          const body = req.postDataJSON() as {
            code: string
            name: string
            departmentHeadUserId?: string | null
            isActive?: boolean
          }
          const created = {
            id: entProgramId,
            code: body.code.toUpperCase(),
            name: body.name,
            departmentHeadUserId: body.departmentHeadUserId ?? null,
            departmentHeadName: body.departmentHeadUserId === deptHead.id ? deptHead.fullName : null,
            isActive: body.isActive !== false,
            coursesCount: 0,
          }
          programs = [...programs, created]
          await route.fulfill({
            status: 201,
            contentType: 'application/json',
            body: JSON.stringify(created),
          })
          return
        }
        if (method === 'PUT' && url.pathname.endsWith(`/api/v1/academic-programs/${entProgramId}`)) {
          const body = req.postDataJSON() as {
            code: string
            name: string
            departmentHeadUserId?: string | null
            isActive?: boolean
          }
          const updated = {
            id: entProgramId,
            code: body.code.toUpperCase(),
            name: body.name,
            departmentHeadUserId: null,
            departmentHeadName: null,
            isActive: body.isActive !== false,
            coursesCount: 0,
          }
          programs = programs.map((p) => (p.id === entProgramId ? updated : p))
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(updated),
          })
          return
        }
        return route.fallback()
      })

      await page.route('**/api/v1/admin/courses**', async (route) => {
        const req = route.request()
        const method = req.method().toUpperCase()
        if (method === 'GET') {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(courses),
          })
          return
        }
        if (method === 'POST') {
          const body = req.postDataJSON() as {
            code: string
            name: string
            academicProgramId?: string | null
          }
          const created = {
            ...makeCourse({
              id: E2E_IDS.secondCourse,
              code: body.code.toUpperCase(),
              name: body.name,
            }),
            academicProgramId: body.academicProgramId ?? null,
            academicProgramCode: body.academicProgramId === entProgramId ? 'ENT' : 'SE',
          }
          courses = [...courses, created]
          await route.fulfill({
            status: 201,
            contentType: 'application/json',
            body: JSON.stringify(created),
          })
          return
        }
        return route.fallback()
      })
    }

    await page.goto('/admin/courses', { waitUntil: 'domcontentloaded' })

    // 1. Create ENT Program
    await page.getByRole('button', { name: /Thêm chương trình ĐT/i }).click()
    const createProgDialog = page.getByRole('dialog')
    await createProgDialog.getByLabel(/Mã chương trình ĐT/i).fill('ent')
    await createProgDialog.getByLabel(/Tên chương trình đào tạo/i).fill('Tiếng Anh Chuyên Ngành')
    await createProgDialog.getByRole('button', { name: 'Tạo chương trình ĐT' }).click()
    await expect(createProgDialog).toBeHidden()
    await expect(page.getByText('Tiếng Anh Chuyên Ngành')).toBeVisible()

    // 2. Edit ENT Program
    await page.getByRole('button', { name: 'Sửa chương trình ĐT ENT' }).click()
    const editProgDialog = page.getByRole('dialog')
    await editProgDialog.getByLabel(/Tên chương trình đào tạo/i).fill('Bộ môn Tiếng Anh (ENT)')
    await editProgDialog.getByRole('button', { name: 'Cập nhật chương trình ĐT' }).click()
    await expect(editProgDialog).toBeHidden()
    await expect(page.getByText('Bộ môn Tiếng Anh (ENT)')).toBeVisible()

    // 3. Create Course attached to ENT Program
    await page.getByRole('button', { name: /Thêm môn học mới/i }).click()
    const courseDialog = page.getByRole('dialog', { name: 'Tạo môn học mới' })
    await courseDialog.getByLabel(/^Mã môn$/i).fill('ENT104')
    await courseDialog.getByLabel(/Tên môn học/i).fill('English Communication 1')
    await courseDialog.locator('#course-semester').selectOption(E2E_IDS.semester)
    await courseDialog.getByLabel(/^Chương trình đào tạo$/i).selectOption(entProgramId)
    await courseDialog.getByRole('button', { name: 'Tạo môn học' }).click()
    await expect(courseDialog).toBeHidden()

    if (!isLiveApiMode()) {
      const createCourseReq = apiMocks
        .getRequests('/api/v1/admin/courses')
        .find((r) => r.method === 'POST')
      expect(createCourseReq?.body).toMatchObject({
        code: 'ENT104',
        academicProgramId: entProgramId,
      })
    }
  })
})
