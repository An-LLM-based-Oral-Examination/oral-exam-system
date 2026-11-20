import { expect, test } from '../fixtures/test'
import { isLiveApiMode, type CanonicalRole } from '../fixtures/env'
import {
  E2E_IDS,
  makeClass,
  makeClo,
  makeCourse,
  makeNotification,
  makeSemester,
  makeUser,
  PORTAL_HOME_BY_ROLE,
} from '../helpers/mock-data'

const ALL_ROLES: readonly CanonicalRole[] = [
  'Student',
  'Instructor',
  'DepartmentHead',
  'ExaminationOfficer',
  'Proctor',
  'Admin',
]

const FORBIDDEN_ROUTE_CASES: ReadonlyArray<{ role: CanonicalRole; path: string }> = [
  { role: 'Instructor', path: '/admin/dashboard' },
  { role: 'Instructor', path: '/lecturer/dlq' },
  { role: 'Instructor', path: '/dlq' },
  { role: 'Instructor', path: '/department-head/dashboard' },
  { role: 'Instructor', path: '/department-head/question-review' },
  { role: 'Instructor', path: '/department-head/appeals' },
  { role: 'Instructor', path: '/official-exams/manage' },
  { role: 'DepartmentHead', path: '/lecturer/dashboard' },
  { role: 'DepartmentHead', path: '/question-bank/create' },
  { role: 'DepartmentHead', path: '/dlq' },
  { role: 'DepartmentHead', path: '/admin/dashboard' },
  { role: 'DepartmentHead', path: '/department-head/appeals' },
  { role: 'DepartmentHead', path: '/appeals' },
  { role: 'ExaminationOfficer', path: '/admin/dashboard' },
  { role: 'ExaminationOfficer', path: '/lecturer/dashboard' },
  { role: 'ExaminationOfficer', path: '/department-head/dashboard' },
  { role: 'ExaminationOfficer', path: '/lecturer/classes' },
  { role: 'ExaminationOfficer', path: '/department-head/question-review' },
  { role: 'ExaminationOfficer', path: '/department-head/appeals' },
  { role: 'Admin', path: '/examination-officer/dashboard' },
  { role: 'Admin', path: '/appeals' },
  { role: 'Admin', path: '/lecturer/classes' },
  { role: 'Admin', path: '/lecturer/audit' },
  { role: 'Admin', path: '/lecturer/appeals' },
  { role: 'Admin', path: '/department-head/appeals' },
  { role: 'Admin', path: '/examination-officer/exams' },
  { role: 'Proctor', path: '/student/overview' },
  { role: 'Proctor', path: '/lecturer/dashboard' },
  { role: 'Proctor', path: '/lecturer/classes' },
  { role: 'Proctor', path: '/lecturer/question-bank' },
  { role: 'Proctor', path: '/lecturer/audit' },
  { role: 'Proctor', path: '/lecturer/appeals' },
  { role: 'Proctor', path: '/department-head/dashboard' },
  { role: 'Proctor', path: '/department-head/question-review' },
  { role: 'Proctor', path: '/department-head/appeals' },
  { role: 'Proctor', path: '/examination-officer/dashboard' },
  { role: 'Proctor', path: '/examination-officer/exams' },
  { role: 'Proctor', path: '/examination-officer/tickets/assign' },
  { role: 'Proctor', path: '/examination-officer/appeals' },
  { role: 'Proctor', path: '/examination-officer/dlq' },
  { role: 'Proctor', path: '/admin/dashboard' },
  { role: 'Proctor', path: '/admin/dlq' },
  { role: 'Student', path: '/lecturer/dashboard' },
  { role: 'Student', path: '/lecturer/classes' },
  { role: 'Student', path: '/lecturer/question-bank' },
  { role: 'Student', path: '/lecturer/audit' },
  { role: 'Student', path: '/lecturer/appeals' },
  { role: 'Student', path: '/department-head/dashboard' },
  { role: 'Student', path: '/department-head/question-review' },
  { role: 'Student', path: '/department-head/appeals' },
  { role: 'Student', path: '/examination-officer/dashboard' },
  { role: 'Student', path: '/examination-officer/exams' },
  { role: 'Student', path: '/examination-officer/tickets/assign' },
  { role: 'Student', path: '/examination-officer/appeals' },
  { role: 'Student', path: '/examination-officer/dlq' },
  { role: 'Student', path: '/proctor/attendance' },
  { role: 'Student', path: '/admin/dashboard' },
  { role: 'Student', path: '/admin/dlq' },
]

test.describe('Area 2 — Academic Foundation, RBAC & Notifications (WAVE-01)', () => {
  test('A2-T1-01 @tier1 @wave01 Admin creates and updates Semester (no delete action) and syncs CourseOfferings', async ({
    page,
    authSession,
    apiMocks,
  }) => {
    await authSession.seedSession('Admin')

    const initialSemester = makeSemester()
    const createdSemester = makeSemester({
      id: 'a0000002-0000-4000-8000-000000000002',
      code: 'SP27',
      name: 'Spring 2027',
      startDate: '2027-01-05T00:00:00.000Z',
      endDate: '2027-04-30T00:00:00.000Z',
    })
    let semesters = [initialSemester]

    if (!isLiveApiMode()) {
      await page.route('**/api/v1/semesters', async (route) => {
        if (route.request().method().toUpperCase() === 'GET') {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(semesters),
          })
          return
        }
        return route.fallback()
      })
      await page.route('**/api/v1/admin/semesters', async (route) => {
        if (route.request().method().toUpperCase() === 'POST') {
          semesters = [...semesters, createdSemester]
          await route.fulfill({
            status: 201,
            contentType: 'application/json',
            body: JSON.stringify(createdSemester),
          })
          return
        }
        return route.fallback()
      })
      await page.route(`**/api/v1/admin/semesters/${createdSemester.id}`, async (route) => {
        if (route.request().method().toUpperCase() === 'PUT') {
          const updated = { ...createdSemester, name: 'Spring 2027 Updated' }
          semesters = semesters.map((s) => (s.id === createdSemester.id ? updated : s))
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(updated),
          })
          return
        }
        return route.fallback()
      })
      await apiMocks.mockJson(
        'PUT',
        `/api/v1/semesters/${initialSemester.id}/course-offerings`,
        200,
        {
          semesterId: initialSemester.id,
          createdCount: 0,
          enabledCount: 1,
          disabledCount: 0,
          courses: [],
        },
      )
    }

    await page.goto('/admin/dashboard?tab=semesters', { waitUntil: 'domcontentloaded' })
    await expect(page.getByText('CẤM XÓA (ON DELETE RESTRICT)')).toBeVisible()
    await expect(page.getByRole('button', { name: /Xóa/ })).toHaveCount(0)

    await page.getByRole('button', { name: 'Thêm học kỳ mới' }).click()
    const createDialog = page.getByRole('dialog', { name: 'Tạo học kỳ mới' })
    await createDialog.getByPlaceholder('ví dụ: FA26, SP26, SU26').fill('SP27')
    await createDialog.getByPlaceholder('ví dụ: Fall 2026, Spring 2026').fill('Spring 2027')
    await createDialog.locator('input[type="date"]').nth(0).fill('2027-01-05')
    await createDialog.locator('input[type="date"]').nth(1).fill('2027-04-30')
    await createDialog.getByRole('button', { name: 'Tạo học kỳ' }).click()

    const sp27Row = page.locator('tr').filter({ hasText: 'SP27' })
    await expect(sp27Row.getByText('Spring 2027', { exact: true })).toBeVisible()

    await sp27Row.getByRole('button', { name: 'Sửa' }).click()
    const editDialog = page.getByRole('dialog', { name: 'Chỉnh sửa học kỳ' })
    await editDialog.getByPlaceholder('ví dụ: Fall 2026, Spring 2026').fill('Spring 2027 Updated')
    await editDialog.getByRole('button', { name: 'Cập nhật học kỳ' }).click()
    await expect(sp27Row.getByText('Spring 2027 Updated', { exact: true })).toBeVisible()

    const fa26Row = page.locator('tr').filter({ hasText: 'FA26' })
    await fa26Row.getByRole('button', { name: 'Môn mở trong kỳ' }).click()
    const offeringsDialog = page.getByRole('dialog', { name: 'Danh mục môn mở trong học kỳ (FA26)' })
    await expect(offeringsDialog).toBeVisible()
    await offeringsDialog.getByRole('button', { name: 'Lưu danh mục môn mở' }).click()

    if (!isLiveApiMode()) {
      expect(
        apiMocks.getRequests('/api/v1/semesters/*/course-offerings').some((r) => r.method === 'PUT'),
      ).toBe(true)
    }
  })

  test('A2-T1-02 @tier1 @wave01 Admin creates Course, configures maxMockExamsPerDay and assigns DepartmentHead', async ({
    page,
    authSession,
    apiMocks,
  }) => {
    await authSession.seedSession('Admin')

    const existingCourse = makeCourse()
    const newCourse = makeCourse({
      id: E2E_IDS.secondCourse,
      code: 'SWD392',
      name: 'Software Architecture and Design',
      maxMockExamsPerDay: 5,
      departmentHeadUserId: null,
      departmentHeadName: null,
    })
    let courses = [existingCourse]

    if (!isLiveApiMode()) {
      await page.route('**/api/v1/admin/courses**', async (route) => {
        const method = route.request().method().toUpperCase()
        if (method === 'GET') {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(courses),
          })
          return
        }
        if (method === 'POST') {
          courses = [...courses, newCourse]
          await route.fulfill({
            status: 201,
            contentType: 'application/json',
            body: JSON.stringify(newCourse),
          })
          return
        }
        return route.fallback()
      })
      await page.route(
        `**/api/v1/admin/courses/${newCourse.id}/assign-department-head`,
        async (route) => {
          const deptHead = makeUser('DepartmentHead')
          courses = courses.map((c) =>
            c.id === newCourse.id
              ? { ...c, departmentHeadUserId: deptHead.id, departmentHeadName: deptHead.fullName }
              : c,
          )
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
              courseId: newCourse.id,
              departmentHeadUserId: deptHead.id,
            }),
          })
        },
      )
    }

    await page.goto('/admin/dashboard?tab=courses', { waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: 'Thêm môn học mới' }).click()
    const courseDialog = page.getByRole('dialog', { name: 'Tạo môn học mới' })
    await courseDialog.getByPlaceholder('ví dụ: PRN231').fill('SWD392')
    await courseDialog.getByPlaceholder('ví dụ: Cross-Platform .NET').fill('Software Architecture and Design')
    await courseDialog.locator('#course-semester').selectOption(E2E_IDS.semester)
    await courseDialog.locator('#course-max-mock').fill('5')
    await courseDialog.getByRole('button', { name: 'Tạo môn học' }).click()

    const swdRow = page.locator('tr').filter({ hasText: 'SWD392' })
    await expect(swdRow.getByText('SWD392', { exact: true })).toBeVisible()
    await swdRow.getByRole('button', { name: 'Gán Trưởng BM' }).click()
    const assignDialog = page.getByRole('dialog', { name: 'Gán Trưởng bộ môn' })
    await assignDialog.locator('select').selectOption(E2E_IDS.users.DepartmentHead)
    await assignDialog.getByRole('button', { name: 'Lưu phân công' }).click()

    await expect(swdRow.getByText(makeUser('DepartmentHead').fullName)).toBeVisible()

    if (!isLiveApiMode()) {
      const createCall = apiMocks
        .getRequests('/api/v1/admin/courses')
        .find((r) => r.method === 'POST')
      expect(createCall?.body).toMatchObject({
        code: 'SWD392',
        maxMockExamsPerDay: 5,
      })
    }
  })

  test('A2-T1-03 @tier1 @wave01 DepartmentHead creates, edits, deactivates and deletes unused Course CLO 3NF', async ({
    page,
    authSession,
    apiMocks,
  }) => {
    await authSession.seedSession('DepartmentHead')

    const clo1 = makeClo()
    const clo2 = makeClo({
      id: E2E_IDS.clo2,
      cloCode: 'CLO2',
      description: 'Thiết kế API RESTful chuẩn RFC 7807.',
      bloomLevel: 'Apply',
      orderIndex: 2,
      isActive: true,
    })

    if (!isLiveApiMode()) {
      let clos = [clo1]
      await page.route(`**/api/v1/courses/${E2E_IDS.course}/clos**`, async (route) => {
        const method = route.request().method().toUpperCase()
        const urlPath = new URL(route.request().url()).pathname
        if (method === 'GET') {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(clos),
          })
          return
        }
        if (method === 'POST') {
          clos = [...clos, clo2]
          await route.fulfill({
            status: 201,
            contentType: 'application/json',
            body: JSON.stringify(clo2),
          })
          return
        }
        if (method === 'PUT' && urlPath.endsWith(`/${E2E_IDS.clo2}`)) {
          const body = route.request().postDataJSON() as Partial<typeof clo2>
          const updated = { ...clo2, ...body }
          clos = clos.map((item) => (item.id === E2E_IDS.clo2 ? updated : item))
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(updated),
          })
          return
        }
        if (method === 'DELETE' && urlPath.endsWith(`/${E2E_IDS.clo2}`)) {
          clos = clos.filter((item) => item.id !== E2E_IDS.clo2)
          await route.fulfill({ status: 204, body: '' })
          return
        }
        return route.fallback()
      })
    }

    await page.goto('/department-head/clos', { waitUntil: 'domcontentloaded' })
    await expect(page.getByText('CLO1', { exact: true })).toBeVisible()

    await page.getByRole('button', { name: 'Thêm CLO' }).click()
    const createDialog = page.getByRole('dialog', { name: 'Thêm chuẩn đầu ra' })
    await createDialog.getByLabel('Mã CLO').fill('CLO2')
    await createDialog
      .getByLabel('Tên / mô tả chuẩn đầu ra')
      .fill('Thiết kế API RESTful chuẩn RFC 7807.')
    await createDialog.getByRole('button', { name: 'Bloom mục tiêu' }).click()
    await page.getByRole('option', { name: 'Apply' }).click()
    await createDialog.getByRole('button', { name: 'Lưu CLO' }).click()

    await expect(page.getByText('CLO2', { exact: true })).toBeVisible()

    await page.getByRole('button', { name: 'Sửa CLO2' }).click()
    const editDialog = page.getByRole('dialog', { name: 'Sửa CLO2' })
    await editDialog.getByRole('switch', { name: 'Hoạt động' }).click()
    await editDialog.getByRole('button', { name: 'Lưu CLO' }).click()
    await expect(page.getByRole('listitem').filter({ hasText: 'CLO2' }).getByText('Ngừng sử dụng')).toBeVisible()

    await page.getByRole('button', { name: 'Xóa CLO2' }).click()
    const deleteDialog = page.getByRole('alertdialog', { name: 'Xác nhận xóa CLO2' })
    await deleteDialog.getByRole('button', { name: 'Xác nhận xóa' }).click()
    await expect(page.getByRole('listitem').filter({ hasText: 'CLO2' })).toHaveCount(0)

    if (!isLiveApiMode()) {
      expect(
        apiMocks.getRequests(`/api/v1/courses/${E2E_IDS.course}/clos**`).length,
      ).toBeGreaterThanOrEqual(4)
    }
  })

  test('A2-T1-04 @tier1 @wave01 DepartmentHead assigns Lecturers and assigned Lecturer reads active CLOs (AZ-B05)', async ({
    page,
    authSession,
    apiMocks,
  }) => {
    await authSession.seedSession('DepartmentHead')
    const lecturer = makeUser('Instructor')

    if (!isLiveApiMode()) {
      await apiMocks.mockJson('PUT', `/api/v1/course-offerings/${E2E_IDS.offering}/lecturers`, 200, {
        courseOfferingId: E2E_IDS.offering,
        assignedCount: 1,
        disabledCount: 0,
        lecturers: [
          {
            lecturerId: lecturer.id,
            fullName: lecturer.fullName,
            email: lecturer.email,
            isActive: true,
            semesterId: E2E_IDS.semester,
            semesterCode: 'FA26',
            courseOfferingId: E2E_IDS.offering,
          },
        ],
      })
    }

    await page.goto('/department-head/lecturer-assignments', { waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: 'Thêm phân công' }).first().click()
    const assignDialog = page.getByRole('dialog', { name: 'Thêm phân công' })
    await assignDialog.getByLabel('Tìm giảng viên').fill('Trần')
    await assignDialog.getByRole('option', { name: `${lecturer.fullName} — ${lecturer.email}` }).click()
    await assignDialog.getByRole('button', { name: 'Thêm giảng viên' }).click()
    await page.getByRole('alertdialog').getByRole('button', { name: 'Xác nhận' }).click()

    await expect(page.getByText(`Đã phân công ${lecturer.fullName}.`)).toBeVisible()
    await expect(page.getByRole('button', { name: `Gỡ phân công ${lecturer.fullName}` })).toBeVisible()

    // Assigned Lecturer reads active CLOs (AZ-B05)
    await authSession.seedSession('Instructor')
    await page.goto('/lecturer/dashboard', { waitUntil: 'domcontentloaded' })
    const cloCheck = await page.evaluate(async (courseId) => {
      const token = sessionStorage.getItem('access_token')
      const response = await fetch(`/api/v1/courses/${courseId}/clos?activeOnly=true`, {
        headers: { Authorization: `Bearer ${token ?? ''}` },
      })
      return {
        status: response.status,
        data: (await response.json()) as unknown,
      }
    }, E2E_IDS.course)

    expect(cloCheck.status).toBe(200)
    expect(Array.isArray(cloCheck.data)).toBe(true)
  })

  test('A2-T1-05 @tier1 @wave01 Lecturer creates Class (no joinPasswordPlain AZ-B19), adds and removes student from roster', async ({
    page,
    authSession,
    apiMocks,
  }) => {
    await authSession.seedSession('Instructor')

    const secondStudent = makeUser('Student', {
      id: E2E_IDS.users.secondStudent,
      email: 'student2.e2e@fe.edu.vn',
      fullName: 'Phạm Thị Sinh Viên Hai',
      studentCode: 'SE180002',
    })
    let classDetail = makeClass()

    if (!isLiveApiMode()) {
      await apiMocks.mockJson(
        'POST',
        `/api/v1/lecturer/course-offerings/${E2E_IDS.offering}/classes`,
        201,
        {
          id: E2E_IDS.class1,
          code: 'SE1802-PRN231',
          name: 'Lớp SE1802',
          joinCode: 'PRN1802',
          courseId: E2E_IDS.course,
          semesterId: E2E_IDS.semester,
          lecturerId: E2E_IDS.users.Instructor,
          hasPassword: true,
          isJoinEnabled: true,
          allowSelfEnrollment: true,
          createdAt: '2026-11-17T08:00:00.000Z',
        },
      )
      await page.route(`**/api/v1/lecturer/classes/${E2E_IDS.class1}`, async (route) => {
        if (route.request().method().toUpperCase() === 'GET') {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(classDetail),
          })
          return
        }
        return route.fallback()
      })
      await apiMocks.mockJson('GET', '/api/v1/lecturer/classes/students/lookup', 200, {
        studentId: secondStudent.id,
        fullName: secondStudent.fullName,
        email: secondStudent.email,
        studentCode: secondStudent.studentCode,
      })
      await page.route(`**/api/v1/lecturer/classes/${E2E_IDS.class1}/students**`, async (route) => {
        const method = route.request().method().toUpperCase()
        if (method === 'POST') {
          classDetail = {
            ...classDetail,
            enrolledCount: classDetail.enrolledStudents.length + 1,
            enrolledStudents: [
              ...classDetail.enrolledStudents,
              {
                studentId: secondStudent.id,
                fullName: secondStudent.fullName,
                email: secondStudent.email,
                studentCode: secondStudent.studentCode ?? 'SE180002',
                enrolledAt: '2026-11-17T08:10:00.000Z',
                status: 'enrolled',
                joinMethod: 'assigned',
              },
            ],
          }
          await route.fulfill({ status: 201, contentType: 'application/json', body: '{}' })
          return
        }
        if (method === 'DELETE') {
          classDetail = {
            ...classDetail,
            enrolledCount: Math.max(0, classDetail.enrolledStudents.length - 1),
            enrolledStudents: classDetail.enrolledStudents.filter(
              (s) => s.studentId !== secondStudent.id,
            ),
          }
          await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' })
          return
        }
        return route.fallback()
      })
    }

    await page.goto('/lecturer/classes', { waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: 'Tạo lớp' }).click()
    const createDialog = page.getByRole('dialog')
    await createDialog.getByLabel('Mã lớp').fill('SE1802-PRN231')
    await createDialog.getByLabel('Tên lớp').fill('Lớp SE1802')
    await createDialog.getByLabel('Mật khẩu tham gia').fill('ClassPass!123')
    await createDialog.getByLabel('Sĩ số tối đa').fill('30')
    await createDialog.getByRole('button', { name: 'Tạo lớp' }).click()

    await expect(page.getByText('Đã tạo lớp “Lớp SE1802”.')).toBeVisible()

    // AZ-B19: verify joinPasswordPlain is absent on class listing/detail API
    const classApiPayload = await page.evaluate(async (classId) => {
      const token = sessionStorage.getItem('access_token')
      const response = await fetch(`/api/v1/lecturer/classes/${classId}`, {
        headers: { Authorization: `Bearer ${token ?? ''}` },
      })
      return (await response.json()) as Record<string, unknown>
    }, E2E_IDS.class1)
    expect('joinPasswordPlain' in classApiPayload).toBe(false)

    await page.goto(`/lecturer/classes/${E2E_IDS.class1}?tab=students`, {
      waitUntil: 'domcontentloaded',
    })
    await expect(page.getByText('••••••••')).toBeVisible()

    await page.getByRole('button', { name: 'Thêm sinh viên' }).click()
    const addDialog = page.getByRole('dialog')
    await addDialog.getByLabel('Mã hoặc email').fill('SE180002')
    await addDialog.getByLabel('Mã hoặc email').press('Enter')
    await expect(addDialog.getByText(secondStudent.fullName)).toBeVisible()
    await addDialog.getByRole('button', { name: 'Thêm vào lớp' }).click()

    const addedRow = page.locator('li').filter({ hasText: secondStudent.fullName })
    await expect(addedRow).toBeVisible()
    await addedRow.getByRole('button', { name: 'Xóa' }).click()

    await page.getByRole('alertdialog').getByRole('button', { name: 'Xóa' }).click()
    await expect(page.locator('li').filter({ hasText: secondStudent.fullName })).toHaveCount(0)
  })

  test('A2-T1-06 @tier1 @wave01 Student searches and joins Class via password into class_enrollments', async ({
    page,
    authSession,
  }) => {
    await authSession.seedSession('Student')

    const openClass = makeClass({
      id: 'a0000006-0000-4000-8000-000000000002',
      classId: 'a0000006-0000-4000-8000-000000000002',
      code: 'SE1809-PRN231',
      classCode: 'SE1809-PRN231',
      name: 'Lớp SE1809 Mở Tự Tham Gia',
      className: 'Lớp SE1809 Mở Tự Tham Gia',
      isAlreadyEnrolled: false,
      canJoin: true,
      requiresPassword: true,
    })

    if (!isLiveApiMode()) {
      let myClasses = [makeClass()]
      await page.route('**/api/v1/student/classes/my-classes**', async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(myClasses),
        })
      })
      await page.route('**/api/v1/student/classes/search**', async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([openClass]),
        })
      })
      await page.route(`**/api/v1/student/classes/${openClass.classId}/join`, async (route) => {
        myClasses = [...myClasses, { ...openClass, isAlreadyEnrolled: true, canJoin: false }]
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ joined: true, classId: openClass.classId }),
        })
      })
    }

    await page.goto('/classes', { waitUntil: 'domcontentloaded' })
    const searchInput = page.getByPlaceholder('Tìm tên lớp hoặc mã môn')
    await searchInput.fill('SE1809')
    await searchInput.press('Enter')

    const results = page.locator('#class-search-results')
    await expect(results.getByText(openClass.className)).toBeVisible()
    await results.getByRole('button', { name: 'Tham gia lớp' }).click()

    const joinDialog = page.getByRole('dialog')
    await joinDialog.getByPlaceholder('Nhập mật khẩu được giảng viên cấp').fill('ClassPass!123')
    await joinDialog.getByRole('button', { name: 'Tham gia', exact: true }).click()

    await expect(page.getByText(`Đã tham gia ${openClass.className}`)).toBeVisible()
  })

  test('A2-T1-07 @tier1 @wave01 Notifications 3NF (FE-11) works across all 6 roles (AZ-C07) plus Admin User and Config management', async ({
    page,
    authSession,
    apiMocks,
  }) => {
    for (const role of ALL_ROLES) {
      await authSession.seedSession(role)

      const roleNotif = makeNotification({
        id: E2E_IDS.notification,
        userId: E2E_IDS.users[role],
        type: role === 'Student' ? 'OFFICIAL_GRADE_PUBLISHED' : 'SYSTEM_ANNOUNCEMENT',
        title: `Thông báo dành cho ${role}`,
        message: 'Mở chi tiết thông báo để kiểm tra.',
        relatedEntityId: role === 'Student' ? E2E_IDS.ticket : null,
        metadata: role === 'Student' ? { ticketId: E2E_IDS.ticket } : null,
        isRead: false,
      })
      const secondaryNotif = makeNotification({
        id: 'a0000011-0000-4000-8000-000000000002',
        userId: E2E_IDS.users[role],
        type: 'SYSTEM_ANNOUNCEMENT',
        title: `Thông báo thứ hai cho ${role}`,
        message: 'Kiểm tra nút Đọc tất cả.',
        isRead: false,
      })

      if (!isLiveApiMode()) {
        let currentItems = role === 'Student' ? [roleNotif, secondaryNotif] : [roleNotif]
        await page.route('**/api/v1/notifications**', async (route) => {
          const url = route.request().url()
          const method = route.request().method().toUpperCase()
          if (url.endsWith('/read-all') && (method === 'POST' || method === 'PUT')) {
            currentItems = currentItems.map((n) => ({ ...n, isRead: true }))
            await route.fulfill({
              status: 200,
              contentType: 'application/json',
              body: JSON.stringify({ isReadAll: true, updatedCount: 1, message: 'OK' }),
            })
            return
          }
          if (url.endsWith('/read') && (method === 'POST' || method === 'PUT')) {
            const parts = new URL(url).pathname.split('/')
            const targetId = parts[4]
            currentItems = currentItems.map((n) => (n.id === targetId ? { ...n, isRead: true } : n))
            await route.fulfill({
              status: 200,
              contentType: 'application/json',
              body: JSON.stringify({
                id: targetId ?? roleNotif.id,
                notificationId: targetId ?? roleNotif.id,
                isRead: true,
                readAt: '2026-11-17T08:10:00.000Z',
              }),
            })
            return
          }
          const unreadCount = currentItems.filter((n) => !n.isRead).length
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
              items: currentItems,
              unreadCount,
              totalCount: currentItems.length,
              page: 1,
              pageSize: 20,
            }),
          })
        })
      }

      await page.goto(PORTAL_HOME_BY_ROLE[role], { waitUntil: 'domcontentloaded' })
      const bellBtn = page.getByRole('button', { name: 'Thông báo', exact: true })
      await expect(bellBtn, `AZ-C07: Bell button must be visible for ${role}`).toBeVisible()
      await expect(bellBtn.getByText(role === 'Student' ? '2' : '1', { exact: true })).toBeVisible()
      await bellBtn.click()

      const drawer = page.getByRole('dialog', { name: 'Thông báo' })
      await expect(drawer).toBeVisible()
      await expect(drawer.getByText(roleNotif.title)).toBeVisible()

      if (role === 'Student') {
        await drawer.getByRole('button', { name: new RegExp(roleNotif.title) }).click()
        await expect(page).toHaveURL(new RegExp(`/results\\?type=official&id=${E2E_IDS.ticket}`))
        await page.goto(PORTAL_HOME_BY_ROLE[role], { waitUntil: 'domcontentloaded' })
        await bellBtn.click()
        await expect(drawer).toBeVisible()
        await drawer.getByRole('button', { name: 'Đọc tất cả', exact: true }).click()
        await drawer.getByRole('button', { name: 'Đóng', exact: true }).click()
      } else {
        await drawer.getByRole('button', { name: 'Đọc tất cả', exact: true }).click()
        await expect(drawer.getByText('Student Portal')).toHaveCount(0)
        await drawer.getByRole('button', { name: 'Đóng', exact: true }).click()
      }

      if (role !== 'Student') {
        await expect(page.getByRole('link', { name: 'Mở hồ sơ cá nhân' })).toHaveCount(0)
        await expect(page.getByRole('button', { name: 'Mở hồ sơ cá nhân' })).toHaveCount(0)
      }
    }

    // Admin User Creation (mustChangePassword: true) & System Configs
    await authSession.seedSession('Admin')
    if (!isLiveApiMode()) {
      await apiMocks.mockJson('POST', '/api/v1/admin/users', 201, {
        ...makeUser('Student', {
          id: E2E_IDS.users.secondStudent,
          email: 'created.stu@fe.edu.vn',
          fullName: 'Sinh Viên Mới Tạo',
          studentCode: 'SE190999',
          mustChangePassword: true,
        }),
        role: 'student',
      })
      await apiMocks.mockJson(
        'PUT',
        '/api/v1/admin/system-configs/MaxPracticeFollowUpQuestions',
        200,
        {
          id: E2E_IDS.course,
          key: 'MaxPracticeFollowUpQuestions',
          value: '3',
          description: 'Số câu hỏi phụ tối đa trong MF-01',
          updatedAt: '2026-11-17T08:15:00.000Z',
        },
      )
    }

    await page.goto('/admin/users', { waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: 'Tạo tài khoản mới' }).click()
    const userDialog = page.getByRole('dialog', { name: 'Tạo tài khoản người dùng mới' })
    await userDialog.locator('input[type="email"]').fill('created.stu@fe.edu.vn')
    await userDialog.getByPlaceholder('Nguyễn Văn A').fill('Sinh Viên Mới Tạo')
    await userDialog.locator('input[type="password"]').fill('TempPass!123')
    await userDialog.getByPlaceholder('VD: SE180101').fill('SE190999')
    await userDialog.getByRole('button', { name: 'Tạo tài khoản' }).click()
    await expect(userDialog).toBeHidden()

    await page.goto('/admin/configs', { waitUntil: 'domcontentloaded' })
    const followUpRow = page.locator('tr').filter({ hasText: 'MaxPracticeFollowUpQuestions' })
    await followUpRow.getByRole('button', { name: 'Chỉnh sửa' }).click()
    const configDialog = page.getByRole('dialog', { name: 'Chỉnh sửa tham số hệ thống' })
    await configDialog.locator('input[type="number"]').fill('3')
    await configDialog.getByRole('button', { name: 'Lưu cấu hình' }).click()
    await expect(followUpRow.getByText('3', { exact: true })).toBeVisible()
  })

  test('A2-T2-01 @tier2 @wave01 Deleting referenced CLO returns 409 Conflict and guides deactivating via isActive=false (AZ-C08)', async ({
    page,
    authSession,
    apiMocks,
  }) => {
    await authSession.seedSession('DepartmentHead')
    if (!isLiveApiMode()) {
      await apiMocks.mockProblem(
        'DELETE',
        `/api/v1/courses/${E2E_IDS.course}/clos/${E2E_IDS.clo1}`,
        409,
        'CLO đang được câu hỏi hoặc cấu trúc đề tham chiếu.',
        { title: 'Conflict', code: 'CLO_REFERENCED' },
      )
    }

    await page.goto('/department-head/clos', { waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: 'Xóa CLO1' }).click()
    await page.getByRole('alertdialog', { name: 'Xác nhận xóa CLO1' }).getByRole('button', { name: 'Xác nhận xóa' }).click()

    const alert = page.getByRole('alert')
    await expect(alert).toBeVisible()
    await expect(alert).toContainText(/tham chiếu|câu hỏi|cấu trúc đề/i)
    await expect(alert).toContainText(/ngừng sử dụng|is_active|isActive|tạm ngừng|vô hiệu/i)
  })

  test('A2-T2-02 @tier2 @wave01 Unassigned Lecturer receives 403 Forbidden reading GET /courses/{id}/clos (AZ-B05)', async ({
    page,
    authSession,
    apiMocks,
  }) => {
    await authSession.seedSession('Instructor')
    if (!isLiveApiMode()) {
      await apiMocks.mockProblem(
        'GET',
        `/api/v1/courses/${E2E_IDS.secondCourse}/clos`,
        403,
        'Giảng viên không được phân công phụ trách môn học này.',
        { title: 'Forbidden', code: 'COURSE_NOT_ASSIGNED' },
      )
    }

    await page.goto('/lecturer/dashboard', { waitUntil: 'domcontentloaded' })
    const result = await page.evaluate(async (courseId) => {
      const token = sessionStorage.getItem('access_token')
      const res = await fetch(`/api/v1/courses/${courseId}/clos?activeOnly=true`, {
        headers: { Authorization: `Bearer ${token ?? ''}` },
      })
      return {
        status: res.status,
        body: (await res.json()) as { detail?: string; status?: number },
      }
    }, E2E_IDS.secondCourse)

    expect(result.status).toBe(403)
    expect(result.body.status).toBe(403)
  })

  test('A2-T2-03 @tier2 @wave01 Sidebar hygiene (AZ-B11), role-neutral UnauthorizedPage (AZ-B18 #3), invalid role, dynamic deep-link and legacy redirects', async ({
    page,
    authSession,
  }) => {
    // 1. Instructor Sidebar hygiene (AZ-B11): Tổng quan first, NO /lecturer/dlq
    await authSession.seedSession('Instructor')
    await page.goto('/lecturer/dashboard', { waitUntil: 'domcontentloaded' })
    const instructorNav = page.getByRole('navigation', { name: 'Các phân hệ portal' }).getByRole('link')
    await expect(instructorNav.first()).toContainText('Tổng quan')
    await expect(page.locator('a[href="/lecturer/dlq"]')).toHaveCount(0)

    // 2. DepartmentHead Sidebar hygiene (AZ-B11): Tổng quan first, NO /department-head/appeals
    await authSession.seedSession('DepartmentHead')
    await page.goto('/department-head/dashboard', { waitUntil: 'domcontentloaded' })
    const headNav = page.getByRole('navigation', { name: 'Các phân hệ portal' }).getByRole('link')
    await expect(headNav.first()).toContainText('Tổng quan')
    await expect(page.locator('a[href="/department-head/appeals"]')).toHaveCount(0)

    // 3. Student accessing Admin -> /unauthorized with role-neutral copy (AZ-B18 #3)
    await authSession.seedSession('Student')
    await page.goto('/admin/dashboard', { waitUntil: 'domcontentloaded' })
    await expect(page).toHaveURL(/\/unauthorized$/)
    await expect(page.getByText('403 — Không Có Quyền Truy Cập')).toBeVisible()
    await expect(page.getByText(/Yêu cầu vai trò Giảng viên hoặc Quản trị viên/)).toHaveCount(0)

    // 4. Dynamic WAVE-01 deep-link preserves query + hash
    await authSession.seedSession('Instructor')
    await page.goto(`/lecturer/classes/${E2E_IDS.class1}?tab=students#roster`, {
      waitUntil: 'domcontentloaded',
    })
    await expect(page).toHaveURL(
      new RegExp(`/lecturer/classes/${E2E_IDS.class1}\\?tab=students#roster$`),
    )

    // 5. Legacy WAVE-01 redirects
    await page.goto('/question-bank', { waitUntil: 'domcontentloaded' })
    await expect(page).toHaveURL(/\/lecturer\/question-bank$/)
    await page.goto('/question-bank/create', { waitUntil: 'domcontentloaded' })
    await expect(page).toHaveURL(/\/lecturer\/question-bank\/create$/)
    await page.goto('/dlq', { waitUntil: 'domcontentloaded' })
    await expect(page).toHaveURL(/\/unauthorized$/)

    // 6. Invalid role in sessionStorage -> /login
    await page.addInitScript(() => {
      sessionStorage.setItem('access_token', 'e2e-invalid-role-token')
      sessionStorage.setItem(
        'user_profile',
        JSON.stringify({
          id: 'a0000001-0000-4000-8000-000000000099',
          email: 'invalid@fe.edu.vn',
          fullName: 'Invalid Role User',
          role: 'UnknownRole',
          mustChangePassword: false,
        }),
      )
    })
    await page.goto('/admin/dashboard', { waitUntil: 'domcontentloaded' })
    await expect(page).toHaveURL(/\/login$/)
  })

  test('A2-T2-04 @tier2 @wave01 Class Join negative cases (wrong password, join disabled, capacity full) return 400 RFC 7807 and impersonation returns 403', async ({
    page,
    authSession,
    apiMocks,
  }) => {
    await authSession.seedSession('Student')

    const wrongPassClass = makeClass({
      id: 'a0000006-0000-4000-8000-000000000003',
      classId: 'a0000006-0000-4000-8000-000000000003',
      code: 'SE1803-PRN231',
      classCode: 'SE1803-PRN231',
      name: 'Lớp Sai Mật Khẩu',
      className: 'Lớp Sai Mật Khẩu',
      isAlreadyEnrolled: false,
      canJoin: true,
      requiresPassword: true,
    })
    const joinDisabledClass = makeClass({
      id: 'a0000006-0000-4000-8000-000000000004',
      classId: 'a0000006-0000-4000-8000-000000000004',
      code: 'SE1804-PRN231',
      classCode: 'SE1804-PRN231',
      name: 'Lớp Đã Tắt Tự Tham Gia',
      className: 'Lớp Đã Tắt Tự Tham Gia',
      isJoinEnabled: false,
      allowSelfEnrollment: false,
      isAlreadyEnrolled: false,
      canJoin: true,
      requiresPassword: true,
    })
    const capacityFullCardClass = makeClass({
      id: 'a0000006-0000-4000-8000-000000000005',
      classId: 'a0000006-0000-4000-8000-000000000005',
      code: 'SE1805-PRN231',
      classCode: 'SE1805-PRN231',
      name: 'Lớp Đủ Sĩ Số Hiển Thị',
      className: 'Lớp Đủ Sĩ Số Hiển Thị',
      enrolledCount: 35,
      maxStudents: 35,
      isAlreadyEnrolled: false,
      canJoin: false,
      requiresPassword: true,
    })
    const capacityFullRaceClass = makeClass({
      id: 'a0000006-0000-4000-8000-000000000006',
      classId: 'a0000006-0000-4000-8000-000000000006',
      code: 'SE1806-PRN231',
      classCode: 'SE1806-PRN231',
      name: 'Lớp Đủ Sĩ Số Khi Nộp',
      className: 'Lớp Đủ Sĩ Số Khi Nộp',
      enrolledCount: 34,
      maxStudents: 35,
      isAlreadyEnrolled: false,
      canJoin: true,
      requiresPassword: true,
    })

    if (!isLiveApiMode()) {
      await apiMocks.mockJson('GET', '/api/v1/student/classes/search', 200, [
        wrongPassClass,
        joinDisabledClass,
        capacityFullCardClass,
        capacityFullRaceClass,
      ])
      await apiMocks.mockProblem(
        'POST',
        `/api/v1/student/classes/${wrongPassClass.classId}/join`,
        400,
        'Mật khẩu tham gia lớp không chính xác.',
        { title: 'Bad Request', code: 'INVALID_PASSWORD' },
      )
      await apiMocks.mockProblem(
        'POST',
        `/api/v1/student/classes/${joinDisabledClass.classId}/join`,
        400,
        'Lớp học hiện đang tắt tự tham gia.',
        { title: 'Bad Request', code: 'JOIN_DISABLED' },
      )
      await apiMocks.mockProblem(
        'POST',
        `/api/v1/student/classes/${capacityFullRaceClass.classId}/join`,
        400,
        'Lớp học đã đạt sĩ số tối đa.',
        { title: 'Bad Request', code: 'CLASS_FULL' },
      )
    }

    await page.goto('/classes', { waitUntil: 'domcontentloaded' })
    const searchInput = page.getByPlaceholder('Tìm tên lớp hoặc mã môn')
    await searchInput.fill('PRN231')
    await searchInput.press('Enter')

    const results = page.locator('#class-search-results')
    await expect(results).toBeVisible()

    // 1. Wrong password -> 400 INVALID_PASSWORD alert
    const wrongPassCard = results.locator('li').filter({ hasText: wrongPassClass.className })
    await wrongPassCard.getByRole('button', { name: 'Tham gia lớp' }).click()
    let joinDialog = page.getByRole('dialog')
    await joinDialog.getByPlaceholder('Nhập mật khẩu được giảng viên cấp').fill('WrongPass')
    await joinDialog.getByRole('button', { name: 'Tham gia', exact: true }).click()
    await expect(
      page.getByRole('alert').filter({ hasText: 'Mật khẩu tham gia lớp không chính xác' }),
    ).toBeVisible()
    await joinDialog.getByRole('button', { name: 'Hủy', exact: true }).click()

    // 2. Join disabled -> 400 JOIN_DISABLED alert
    const disabledCard = results.locator('li').filter({ hasText: joinDisabledClass.className })
    await disabledCard.getByRole('button', { name: 'Tham gia lớp' }).click()
    joinDialog = page.getByRole('dialog')
    await joinDialog.getByPlaceholder('Nhập mật khẩu được giảng viên cấp').fill('ClassPass!123')
    await joinDialog.getByRole('button', { name: 'Tham gia', exact: true }).click()
    await expect(
      page.getByRole('alert').filter({ hasText: 'Lớp học hiện đang tắt tự tham gia' }),
    ).toBeVisible()
    await joinDialog.getByRole('button', { name: 'Hủy', exact: true }).click()

    // 3. Capacity full -> disabled card button AND 400 CLASS_FULL alert on join submit
    const fullCard = results.locator('li').filter({ hasText: capacityFullCardClass.className })
    await expect(fullCard.getByRole('button', { name: 'Lớp đã đầy' })).toBeDisabled()

    const raceFullCard = results.locator('li').filter({ hasText: capacityFullRaceClass.className })
    await raceFullCard.getByRole('button', { name: 'Tham gia lớp' }).click()
    joinDialog = page.getByRole('dialog')
    await joinDialog.getByPlaceholder('Nhập mật khẩu được giảng viên cấp').fill('ClassPass!123')
    await joinDialog.getByRole('button', { name: 'Tham gia', exact: true }).click()
    await expect(
      page.getByRole('alert').filter({ hasText: 'Lớp học đã đạt sĩ số tối đa' }),
    ).toBeVisible()

    // 4. Live-API lecturer impersonation check (real backend RBAC check, no mocked self-echo)
    if (isLiveApiMode()) {
      await authSession.seedSession('Instructor')
      await page.goto('/lecturer/classes', { waitUntil: 'domcontentloaded' })
      const impersonateStatus = await page.evaluate(async (offeringId) => {
        const token = sessionStorage.getItem('access_token')
        const res = await fetch(`/api/v1/lecturer/course-offerings/${offeringId}/classes`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer ${token ?? ''}`,
          },
          body: JSON.stringify({
            code: 'IMP-01',
            name: 'Impersonation Class',
            password: 'ClassPass!123',
            maxStudents: 30,
            lecturerId: 'a0000001-0000-4000-8000-000000000099',
          }),
        })
        return res.status
      }, E2E_IDS.offering)
      expect(impersonateStatus).toBe(403)
    }
  })

  test('A2-T2-05 @tier2 @wave01 Lecturer and DepartmentHead are blocked 403 from DLQ (AZ-B11) and DeptHead blocked from maxMockExamsPerDay', async ({
    page,
    authSession,
    apiMocks,
  }) => {
    await authSession.seedSession('Instructor')
    if (!isLiveApiMode()) {
      await apiMocks.mockProblem('GET', '/api/v1/dlq/tasks', 403, 'Chỉ Quản trị viên được phép truy cập DLQ.', {
        title: 'Forbidden',
        code: 'FORBIDDEN',
      })
    }

    await page.goto('/lecturer/dlq', { waitUntil: 'domcontentloaded' })
    await expect(page).toHaveURL(/\/unauthorized$/)

    const lecturerDlqStatus = await page.evaluate(async () => {
      const token = sessionStorage.getItem('access_token')
      const res = await fetch('/api/v1/dlq/tasks?flowType=IN_CLASS_EXAM', {
        headers: { Authorization: `Bearer ${token ?? ''}` },
      })
      return res.status
    })
    expect(lecturerDlqStatus).toBe(403)

    await authSession.seedSession('DepartmentHead')
    if (!isLiveApiMode()) {
      await apiMocks.mockProblem('GET', '/api/v1/dlq/tasks', 403, 'Chỉ Quản trị viên được phép truy cập DLQ.', {
        title: 'Forbidden',
        code: 'FORBIDDEN',
      })
      await apiMocks.mockProblem(
        'PUT',
        `/api/v1/courses/${E2E_IDS.course}/configurations`,
        403,
        'Trưởng bộ môn không có quyền sửa maxMockExamsPerDay.',
        { title: 'Forbidden', code: 'ADMIN_ONLY_QUOTA' },
      )
    }

    await page.goto('/department-head/dashboard', { waitUntil: 'domcontentloaded' })
    const headChecks = await page.evaluate(async (courseId) => {
      const token = sessionStorage.getItem('access_token')
      const dlqRes = await fetch('/api/v1/dlq/tasks', {
        headers: { Authorization: `Bearer ${token ?? ''}` },
      })
      const quotaRes = await fetch(`/api/v1/courses/${courseId}/configurations`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${token ?? ''}`,
        },
        body: JSON.stringify({ maxMockExamsPerDay: 10 }),
      })
      return { dlqStatus: dlqRes.status, quotaStatus: quotaRes.status }
    }, E2E_IDS.course)

    expect(headChecks.dlqStatus).toBe(403)
    expect(headChecks.quotaStatus).toBe(403)
  })

  test('A2-T2-06 @tier2 @wave01 Admin duplicate email/studentCode returns 409 Conflict and weak password returns 400', async ({
    page,
    authSession,
    apiMocks,
  }) => {
    await authSession.seedSession('Admin')
    if (!isLiveApiMode()) {
      await apiMocks.mockProblem(
        'POST',
        '/api/v1/admin/users',
        409,
        'Email hoặc mã sinh viên đã tồn tại (không phân biệt hoa thường).',
        { title: 'Conflict', code: 'DUPLICATE_USER' },
      )
    }

    await page.goto('/admin/users', { waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: 'Tạo tài khoản mới' }).click()
    const dialog = page.getByRole('dialog', { name: 'Tạo tài khoản người dùng mới' })
    await dialog.locator('input[type="email"]').fill('STUDENT.E2E@FE.EDU.VN')
    await dialog.getByPlaceholder('Nguyễn Văn A').fill('Trùng Email')
    await dialog.locator('input[type="password"]').fill('ValidPass!123')
    await dialog.getByPlaceholder('VD: SE180101').fill('se180001')
    await dialog.getByRole('button', { name: 'Tạo tài khoản' }).click()

    await expect(dialog.getByRole('alert').filter({ hasText: 'đã tồn tại' })).toBeVisible()

    if (!isLiveApiMode()) {
      await apiMocks.mockProblem('POST', '/api/v1/admin/users', 400, 'Mật khẩu phải đủ độ mạnh (chữ hoa, thường, số, ký tự đặc biệt).', {
        title: 'Bad Request',
        code: 'WEAK_PASSWORD',
      })
      await dialog.locator('input[type="email"]').fill('weak.pwd@fe.edu.vn')
      await dialog.locator('input[type="password"]').fill('12345678')
      await dialog.getByRole('button', { name: 'Tạo tài khoản' }).click()
      await expect(dialog.getByRole('alert').filter({ hasText: 'Mật khẩu phải đủ độ mạnh' })).toBeVisible()
    }
  })

  test('A2-T2-07 @tier2 @wave01 Speech /tts and /stt enforce [Authorize] 401 and RFC 7807 ProblemDetails while /voices stays AllowAnonymous (AZ-B12 R2)', async ({
    page,
    authSession,
    apiMocks,
  }) => {
    if (!isLiveApiMode()) {
      await apiMocks.mockJson('GET', '/api/v1/speech/voices', 200, [
        { name: 'vi-VN-HoaiMyNeural', locale: 'vi-VN' },
      ])
      await apiMocks.mockProblem('POST', '/api/v1/speech/tts', 401, 'Yêu cầu đăng nhập để sử dụng TTS.', {
        title: 'Unauthorized',
      })
      await apiMocks.mockProblem('POST', '/api/v1/speech/stt', 401, 'Yêu cầu đăng nhập để sử dụng STT.', {
        title: 'Unauthorized',
      })
    }

    await page.goto('/login', { waitUntil: 'domcontentloaded' })
    const unauthResults = await page.evaluate(async () => {
      const voicesRes = await fetch('/api/v1/speech/voices')
      const ttsRes = await fetch('/api/v1/speech/tts', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ text: 'Xin chào' }),
      })
      const sttForm = new FormData()
      sttForm.append('file', new Blob(['dummy'], { type: 'audio/webm' }), 'sample.webm')
      const sttRes = await fetch('/api/v1/speech/stt', { method: 'POST', body: sttForm })
      return {
        voicesStatus: voicesRes.status,
        ttsStatus: ttsRes.status,
        sttStatus: sttRes.status,
      }
    })

    expect(unauthResults.voicesStatus).toBe(200)
    expect(unauthResults.ttsStatus).toBe(401)
    expect(unauthResults.sttStatus).toBe(401)

    await authSession.seedSession('Student')
    if (!isLiveApiMode()) {
      await apiMocks.mockProblem('POST', '/api/v1/speech/tts', 400, 'Nội dung văn bản không được để trống.', {
        title: 'Bad Request',
        code: 'EMPTY_TTS_TEXT',
      })
      await apiMocks.mockProblem('POST', '/api/v1/speech/stt', 400, 'Tệp âm thanh trống hoặc vượt quá 25MB.', {
        title: 'Bad Request',
        code: 'INVALID_AUDIO_FILE',
      })
    }

    await page.goto('/student/overview', { waitUntil: 'domcontentloaded' })
    const badPayloads = await page.evaluate(async () => {
      const token = sessionStorage.getItem('access_token')
      const ttsRes = await fetch('/api/v1/speech/tts', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${token ?? ''}`,
        },
        body: JSON.stringify({ text: '' }),
      })
      const ttsBody = (await ttsRes.json()) as { title?: string; status?: number; detail?: string }

      const emptyForm = new FormData()
      emptyForm.append('file', new Blob([], { type: 'audio/webm' }), 'empty.webm')
      const sttRes = await fetch('/api/v1/speech/stt', {
        method: 'POST',
        headers: { Authorization: `Bearer ${token ?? ''}` },
        body: emptyForm,
      })
      const sttBody = (await sttRes.json()) as { title?: string; status?: number; detail?: string }

      return {
        ttsStatus: ttsRes.status,
        ttsBody,
        sttStatus: sttRes.status,
        sttBody,
      }
    })

    expect(badPayloads.ttsStatus).toBe(400)
    expect(badPayloads.ttsBody.status).toBe(400)
    expect(badPayloads.ttsBody.detail).toBeTruthy()
    expect(badPayloads.sttStatus).toBe(400)
    expect(badPayloads.sttBody.status).toBe(400)
    expect(badPayloads.sttBody.detail).toBeTruthy()
  })
})

test.describe('RBAC 6-role route matrix @tier2 @wave01', () => {
  for (const role of ALL_ROLES) {
    test(`${role} @tier2 @wave01 can open canonical home ${PORTAL_HOME_BY_ROLE[role]}`, async ({
      page,
      authSession,
    }) => {
      const homePath = PORTAL_HOME_BY_ROLE[role]
      await authSession.seedSession(role)
      await page.goto(homePath, { waitUntil: 'domcontentloaded' })

      await expect(page).toHaveURL(new RegExp(`${homePath.replaceAll('/', '\\/')}$`))
      await expect(page).not.toHaveURL(/\/unauthorized$/)
    })
  }

  for (const routeCase of FORBIDDEN_ROUTE_CASES) {
    test(`${routeCase.role} @tier2 @wave01 cannot open ${routeCase.path}`, async ({
      page,
      authSession,
      apiMocks,
    }) => {
      await authSession.seedSession(routeCase.role)
      await page.goto(routeCase.path, { waitUntil: 'domcontentloaded' })

      await expect(page).toHaveURL(/\/unauthorized$/)
      const nonAuthMeRequests = apiMocks
        .getRequests()
        .filter((req) => !req.pathname.endsWith('/api/v1/auth/me'))
      expect(nonAuthMeRequests).toEqual([])
    })
  }
})
