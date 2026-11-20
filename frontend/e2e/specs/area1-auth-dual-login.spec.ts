import { expect, test } from '../fixtures/test'
import { isLiveApiMode } from '../fixtures/env'
import { makeTestJwt } from '../fixtures/auth.fixture'
import {
  BACKEND_ROLE_BY_CANONICAL,
  E2E_IDS,
  makeCourse,
  makeUser,
} from '../helpers/mock-data'

interface MockGoogleWindow {
  google?: {
    accounts: {
      id: {
        initialize: (config: {
          client_id: string
          callback: (response: { credential: string }) => void
          nonce?: string
        }) => void
        renderButton: (container: HTMLElement) => void
        prompt: () => void
      }
    }
  }
  __e2eTriggerGoogleLogin?: (idToken?: string) => void
}

test.describe('Area 1 — Auth & Dual Login (WAVE-01)', () => {
  test('A1-T1-01 @tier1 @wave01 School password login redirects to canonical portal home without storing tokens in localStorage', async ({
    page,
    authSession,
    apiMocks,
  }) => {
    const user = await authSession.loginAs('Student')
    await expect(page).toHaveURL(/\/student\/overview$/)

    const storageSnapshot = await page.evaluate(() => ({
      sessionAccessToken: sessionStorage.getItem('access_token'),
      sessionRefreshToken: sessionStorage.getItem('refresh_token'),
      sessionUserProfile: sessionStorage.getItem('user_profile'),
      localAccessToken: localStorage.getItem('access_token'),
      localRefreshToken: localStorage.getItem('refresh_token'),
    }))

    expect(storageSnapshot.sessionAccessToken).toBeTruthy()
    expect(storageSnapshot.sessionRefreshToken).toBeNull()
    expect(storageSnapshot.sessionUserProfile).toContain(user.email)
    expect(storageSnapshot.localAccessToken).toBeNull()
    expect(storageSnapshot.localRefreshToken).toBeNull()

    const loginCalls = apiMocks.getRequests('/api/v1/auth/password-login')
    expect(loginCalls.length).toBeGreaterThanOrEqual(1)
  })

  test('A1-T1-02 @tier1 @wave01 Google OAuth challenge and login auto-creates Student account (AZ-C04 R2)', async ({
    page,
    apiMocks,
  }) => {
    if (isLiveApiMode()) {
      await page.goto('/login', { waitUntil: 'domcontentloaded' })
      const challengeRes = await page.evaluate(async () => {
        const response = await fetch('/api/v1/auth/google-challenge', {
          method: 'POST',
          credentials: 'include',
        })
        return {
          status: response.status,
          body: (await response.json()) as { nonce?: string; expiresIn?: number },
        }
      })
      expect(challengeRes.status).toBe(200)
      expect(challengeRes.body.nonce).toMatch(/^[A-Za-z0-9_-]{43}$/)
      return
    }

    const googleStudent = makeUser('Student', {
      email: 'new.google.student@fe.edu.vn',
      fullName: 'Sinh Viên Google Mới',
      studentCode: 'SE190123',
      mustChangePassword: false,
    })
    const googleJwt = makeTestJwt(googleStudent)
    const validNonce = 'A'.repeat(43)

    await apiMocks.mockJson('POST', '/api/v1/auth/google-challenge', 200, {
      nonce: validNonce,
      expiresIn: 120,
    })
    await apiMocks.mockJson('POST', '/api/v1/auth/google-login', 200, {
      accessToken: googleJwt,
      tokenType: 'Bearer',
      expiresIn: 900,
      mustChangePassword: false,
      user: {
        id: googleStudent.id,
        email: googleStudent.email,
        fullName: googleStudent.fullName,
        studentCode: googleStudent.studentCode,
        role: BACKEND_ROLE_BY_CANONICAL.Student,
        mustChangePassword: false,
      },
    })
    await apiMocks.mockJson('GET', '/api/v1/auth/me', 200, googleStudent)

    await page.route('https://accounts.google.com/**', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/javascript',
        body: '/* mocked GIS script */',
      })
    })

    await page.addInitScript(() => {
      let capturedCallback: ((response: { credential: string }) => void) | null = null
      const win = window as unknown as MockGoogleWindow
      win.google = {
        accounts: {
          id: {
            initialize(config) {
              capturedCallback = config.callback
            },
            renderButton(container) {
              container.innerHTML = ''
              const btn = document.createElement('button')
              btn.type = 'button'
              btn.textContent = 'Google Sign-In Mock'
              btn.addEventListener('click', () => {
                capturedCallback?.({ credential: 'e2e-google-id-token' })
              })
              container.appendChild(btn)
            },
            prompt() {},
          },
        },
      }
      win.__e2eTriggerGoogleLogin = (idToken = 'e2e-google-id-token') => {
        capturedCallback?.({ credential: idToken })
      }
    })

    await page.goto('/login', { waitUntil: 'domcontentloaded' })
    const mockGoogleBtn = page.getByRole('button', { name: 'Google Sign-In Mock' })
    await expect(mockGoogleBtn).toBeVisible()
    await mockGoogleBtn.click()

    await expect(page).toHaveURL(/\/student\/overview$/)
    const googleLoginRequests = apiMocks.getRequests('/api/v1/auth/google-login')
    expect(googleLoginRequests).toHaveLength(1)
    expect(googleLoginRequests[0]?.body).toEqual({ idToken: 'e2e-google-id-token' })
  })

  test('A1-T1-03 @tier1 @wave01 First-time login with mustChangePassword=true forces password update before portal access', async ({
    page,
    authSession,
    apiMocks,
  }) => {
    const user = await authSession.loginAs('Student', { mustChangePassword: true })
    await expect(page).toHaveURL(/\/change-password$/)
    await expect(page.getByRole('heading', { name: 'Đổi Mật Khẩu Lần Đầu' })).toBeVisible()
    await expect(page.getByText('Yêu cầu bảo mật bắt buộc')).toBeVisible()
    await expect(page.getByRole('link', { name: /Quay lại/ })).toHaveCount(0)

    const updatedUser = { ...user, mustChangePassword: false }
    const updatedJwt = makeTestJwt(updatedUser)

    if (!isLiveApiMode()) {
      await apiMocks.mockJson('POST', '/api/v1/auth/change-password', 200, {
        accessToken: updatedJwt,
        tokenType: 'Bearer',
        expiresIn: 900,
        mustChangePassword: false,
      })
      await apiMocks.mockJson('GET', '/api/v1/auth/me', 200, updatedUser)
    }

    await page.locator('#current-password').fill('E2ePassword!123')
    await page.locator('#new-password').fill('NewStrongPass!123')
    await page.locator('#confirm-password').fill('NewStrongPass!123')
    await page.getByRole('button', { name: 'Xác Nhận Đổi Mật Khẩu' }).click()

    await expect(page).toHaveURL(/\/student\/overview$/)
    const profileJson = await page.evaluate(() => sessionStorage.getItem('user_profile'))
    expect(profileJson).toBeTruthy()
    expect(JSON.parse(profileJson ?? '{}')).toMatchObject({ mustChangePassword: false })
  })

  test('A1-T1-04 @tier1 @wave01 Restores session via refresh-token when sessionStorage is empty and redirects unauthenticated user to /login', async ({
    page,
    apiMocks,
  }) => {
    await page.goto('/admin/dashboard', { waitUntil: 'domcontentloaded' })
    await expect(page).toHaveURL(/\/login$/)

    if (isLiveApiMode()) return

    const student = makeUser('Student')
    const restoredJwt = makeTestJwt(student)
    await apiMocks.mockJson('POST', '/api/v1/auth/refresh-token', 200, {
      accessToken: restoredJwt,
      tokenType: 'Bearer',
      expiresIn: 900,
    })
    await apiMocks.mockJson('GET', '/api/v1/auth/me', 200, student)

    await page.goto('/student/overview', { waitUntil: 'domcontentloaded' })
    await expect(page).toHaveURL(/\/student\/overview$/)
    await expect(page.getByRole('button', { name: 'Đăng xuất', exact: true })).toBeVisible()
    await expect
      .poll(() => apiMocks.getRequests('/api/v1/auth/refresh-token').length)
      .toBeGreaterThanOrEqual(1)
    await expect
      .poll(() => apiMocks.getRequests('/api/v1/auth/me').length)
      .toBeGreaterThanOrEqual(1)
  })

  test('A1-T1-05 @tier1 @wave01 Collapses concurrent 401 responses into a single refresh-token call and replays queued requests (AZ-B02)', async ({
    page,
    authSession,
    apiMocks,
  }) => {
    const instructor = await authSession.seedSession('Instructor')
    if (isLiveApiMode()) {
      await page.goto('/lecturer/dashboard', { waitUntil: 'domcontentloaded' })
      await expect(page).toHaveURL(/\/lecturer\/dashboard$/)
      return
    }

    const refreshedJwt = makeTestJwt(instructor, 900)
    let refreshCalls = 0
    const expiredSet = new Set<string>()

    await page.route('**/api/v1/auth/refresh-token', async (route) => {
      refreshCalls += 1
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          accessToken: refreshedJwt,
          tokenType: 'Bearer',
          expiresIn: 900,
        }),
      })
    })

    for (const endpoint of [
      '/api/v1/lecturer/course-offerings',
      '/api/v1/semesters',
      '/api/v1/lecturer/classes',
      '/api/v1/questions',
    ]) {
      await page.route(`**${endpoint}**`, async (route) => {
        if (!expiredSet.has(endpoint)) {
          expiredSet.add(endpoint)
          await route.fulfill({
            status: 401,
            contentType: 'application/problem+json',
            body: JSON.stringify({
              type: 'https://tools.ietf.org/html/rfc7807',
              title: 'Unauthorized',
              status: 401,
              detail: 'Access token expired.',
            }),
          })
          return
        }
        return route.fallback()
      })
    }

    await page.goto('/lecturer/dashboard', { waitUntil: 'domcontentloaded' })
    await expect(page).toHaveURL(/\/lecturer\/dashboard$/)
    await expect.poll(() => refreshCalls, { timeout: 15_000 }).toBe(1)
    expect(apiMocks.getRequests('/api/v1/auth/refresh-token')).toHaveLength(1)

    const tokenAfterRefresh = await page.evaluate(() => sessionStorage.getItem('access_token'))
    expect(tokenAfterRefresh).toBe(refreshedJwt)
  })

  test('A1-T1-06 @tier1 @wave01 Clean sign-out clears sessionStorage and subsequent refresh returns 401 keeping user on /login', async ({
    page,
    authSession,
    apiMocks,
  }) => {
    await authSession.seedSession('Student')
    await page.goto('/student/overview', { waitUntil: 'domcontentloaded' })
    await expect(page).toHaveURL(/\/student\/overview$/)

    await page.getByRole('button', { name: 'Đăng xuất', exact: true }).click()
    await expect(page).toHaveURL(/\/login$/)

    const cleared = await page.evaluate(() => ({
      accessToken: sessionStorage.getItem('access_token'),
      refreshToken: sessionStorage.getItem('refresh_token'),
      userProfile: sessionStorage.getItem('user_profile'),
    }))
    expect(cleared).toEqual({
      accessToken: null,
      refreshToken: null,
      userProfile: null,
    })

    if (!isLiveApiMode()) {
      await expect.poll(() => apiMocks.getRequests('/api/v1/auth/sign-out').length).toBe(1)
    }

    await page.goto('/student/overview', { waitUntil: 'domcontentloaded' })
    await expect(page).toHaveURL(/\/login$/)
  })

  test('A1-T1-07 @tier1 @wave01 Authenticated profile displays user fields and allows periodic password change', async ({
    page,
    authSession,
    apiMocks,
  }) => {
    const student = await authSession.seedSession('Student')
    await page.goto('/profile', { waitUntil: 'domcontentloaded' })

    await expect(page.getByRole('heading', { name: student.fullName })).toBeVisible()
    await expect(page.getByText(student.email)).toBeVisible()
    await expect(page.getByRole('main').getByText(student.studentCode ?? 'SE180001')).toBeVisible()
    await expect(page.getByText('Sinh viên', { exact: true }).first()).toBeVisible()
    await expect(page.getByRole('switch').first()).toBeVisible()

    if (!isLiveApiMode()) {
      const rotatedToken = makeTestJwt(student)
      await apiMocks.mockJson('POST', '/api/v1/auth/change-password', 200, {
        accessToken: rotatedToken,
        tokenType: 'Bearer',
        expiresIn: 900,
        mustChangePassword: false,
      })
    }

    await page.getByRole('button', { name: 'Đổi mật khẩu' }).click()
    const dialog = page.getByRole('dialog')
    await expect(dialog).toBeVisible()

    await dialog.locator('#current-password').fill('E2ePassword!123')
    await dialog.locator('#new-password').fill('RotatedPass!123')
    await dialog.locator('#confirm-password').fill('RotatedPass!123')
    await dialog.getByRole('button', { name: 'Xác Nhận Đổi Mật Khẩu' }).click()

    await expect(page.getByRole('status').filter({ hasText: 'Đổi mật khẩu thành công' })).toBeVisible()
    await expect(page).toHaveURL(/\/profile$/)
  })

  test('A1-T2-01 @tier2 @wave01 Empty login validation, 401 INVALID_CREDENTIALS and 403 ACCOUNT_LOCKED show alerts without storing tokens', async ({
    page,
    apiMocks,
  }) => {
    await page.goto('/login', { waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click()
    await expect(page.getByText('Vui lòng nhập username.')).toBeVisible()
    await expect(page.getByText('Vui lòng nhập mật khẩu.')).toBeVisible()

    if (!isLiveApiMode()) {
      await apiMocks.mockProblem('POST', '/api/v1/auth/password-login', 401, 'Tên đăng nhập hoặc mật khẩu không đúng.', {
        title: 'Unauthorized',
        code: 'INVALID_CREDENTIALS',
      })
    }

    await page.locator('#login-username').fill('unknown.user@fe.edu.vn')
    await page.locator('#login-password').fill('WrongPassword!123')
    await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click()
    await expect(page.getByRole('alert').last()).toBeVisible()

    if (!isLiveApiMode()) {
      await apiMocks.mockProblem('POST', '/api/v1/auth/password-login', 403, 'Tài khoản đã bị khóa bởi quản trị viên.', {
        title: 'Forbidden',
        code: 'ACCOUNT_LOCKED',
      })
      await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click()
      await expect(page.getByRole('alert').filter({ hasText: 'bị khóa' })).toBeVisible()
    }

    const token = await page.evaluate(() => sessionStorage.getItem('access_token'))
    expect(token).toBeNull()
  })

  test('A1-T2-02 @tier2 @wave01 auth_version revocation or failed refresh evicts stale session to /login and clears sessionStorage', async ({
    page,
    authSession,
    apiMocks,
  }) => {
    await authSession.seedSession('Instructor', { skipAuthMeMock: true })
    if (!isLiveApiMode()) {
      await apiMocks.mockProblem('GET', '/api/v1/auth/me', 401, 'Token đã bị thu hồi do thay đổi auth_version.', {
        title: 'Unauthorized',
        code: 'TOKEN_REVOKED',
      })
      await apiMocks.mockProblem('POST', '/api/v1/auth/refresh-token', 401, 'Refresh token đã bị thu hồi.', {
        title: 'Unauthorized',
        code: 'REFRESH_TOKEN_REVOKED',
      })
    } else {
      await authSession.clearSession()
    }

    await page.goto('/lecturer/dashboard', { waitUntil: 'domcontentloaded' })
    await expect(page).toHaveURL(/\/login$/)

    const storageAfter = await page.evaluate(() => ({
      accessToken: sessionStorage.getItem('access_token'),
      refreshToken: sessionStorage.getItem('refresh_token'),
      userProfile: sessionStorage.getItem('user_profile'),
    }))
    expect(storageAfter).toEqual({
      accessToken: null,
      refreshToken: null,
      userProfile: null,
    })
  })

  test('A1-T2-03 @tier2 @wave01 mustChangePassword=true takes precedence over permitted routes and 403 MUST_CHANGE_PASSWORD redirects mid-session', async ({
    page,
    authSession,
    apiMocks,
  }) => {
    await authSession.seedSession('Instructor', { mustChangePassword: true })
    await page.goto('/lecturer/dashboard', { waitUntil: 'domcontentloaded' })
    await expect(page).toHaveURL(/\/change-password$/)

    if (isLiveApiMode()) return

    await authSession.seedSession('Student', { mustChangePassword: false })
    await apiMocks.mockProblem('GET', '/api/v1/courses', 403, 'Bạn phải đổi mật khẩu trước khi tiếp tục.', {
      title: 'Cần đổi mật khẩu',
      code: 'MUST_CHANGE_PASSWORD',
      errorCode: 'MUST_CHANGE_PASSWORD',
    })

    await page.goto('/practice', { waitUntil: 'domcontentloaded' })
    await expect(page).toHaveURL(/\/change-password$/)
  })

  test('A1-T2-04 @tier2 @wave01 Expired Bearer JWT behind NAT returns 401 before IP bucket and login 429 renders Retry-After banner cleanly (AZ-B02)', async ({
    page,
    authSession,
    apiMocks,
  }) => {
    if (!isLiveApiMode()) {
      await apiMocks.mockProblem(
        'POST',
        '/api/v1/auth/password-login',
        429,
        'Đăng nhập sai quá nhiều lần. Vui lòng thử lại sau 60 giây.',
        {
          title: 'Too Many Requests',
          code: 'LOGIN_TEMPORARILY_LOCKED',
          headers: { 'Retry-After': '60' },
        },
      )

      await page.goto('/login', { waitUntil: 'domcontentloaded' })
      await page.locator('#login-username').fill('student.e2e@fe.edu.vn')
      await page.locator('#login-password').fill('WrongPass!123')
      await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click()
      await expect(page.getByRole('alert').filter({ hasText: '60 giây' })).toBeVisible()
    }

    const student = await authSession.seedSession('Student', { expiresInSeconds: -60 })
    if (!isLiveApiMode()) {
      const freshToken = makeTestJwt(student, 900)
      let firstCoursesCall = true
      await page.route('**/api/v1/courses**', async (route) => {
        if (firstCoursesCall) {
          firstCoursesCall = false
          await route.fulfill({
            status: 401,
            contentType: 'application/problem+json',
            body: JSON.stringify({
              type: 'https://tools.ietf.org/html/rfc7807',
              title: 'Unauthorized',
              status: 401,
              detail: 'Expired JWT behind NAT IP.',
            }),
          })
          return
        }
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([makeCourse({ id: E2E_IDS.course })]),
        })
      })
      await apiMocks.mockJson('POST', '/api/v1/auth/refresh-token', 200, {
        accessToken: freshToken,
        tokenType: 'Bearer',
        expiresIn: 900,
      })
    }

    await page.goto('/student/overview', { waitUntil: 'domcontentloaded' })
    await expect(page).toHaveURL(/\/student\/overview$/)
  })

  test('A1-T2-05 @tier2 @wave01 Cross-tab logout via BroadcastChannel invalidates sibling tab session', async ({
    page,
    context,
    authSession,
  }) => {
    const student = await authSession.seedSession('Student')
    await page.goto('/student/overview', { waitUntil: 'domcontentloaded' })
    await expect(page).toHaveURL(/\/student\/overview$/)

    const pageASession = await page.evaluate(() => ({
      accessToken: sessionStorage.getItem('access_token'),
      userProfile: sessionStorage.getItem('user_profile'),
    }))
    expect(pageASession.accessToken).toBeTruthy()
    expect(pageASession.userProfile).toBeTruthy()

    const pageB = await context.newPage()
    if (!isLiveApiMode()) {
      await pageB.route('**/api/v1/**', async (route) => {
        const url = route.request().url()
        if (new URL(url).pathname.endsWith('/auth/me')) {
          return route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(student),
          })
        }
        if (url.includes('/auth/refresh-token')) {
          return route.fulfill({ status: 401, contentType: 'application/problem+json', body: '{}' })
        }
        if (url.includes('/notifications')) {
          return route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({ items: [], unreadCount: 0, totalCount: 0, page: 1, pageSize: 20 }),
          })
        }
        if (url.includes('/mock-exams/history')) {
          return route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({ items: [], totalCount: 0, pageIndex: 1, pageSize: 10, totalPages: 1 }),
          })
        }
        if (url.includes('/student/dashboard')) {
          return route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({ officialExamTickets: [] }),
          })
        }
        if (url.includes('/mock-exams/quota')) {
          return route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
              courseId: E2E_IDS.course,
              studentId: student.id,
              quotaDate: '2026-11-17',
              usedCount: 0,
              remainingCount: 3,
              maxDailyQuota: 3,
              canStartExam: true,
            }),
          })
        }
        return route.fulfill({ status: 200, contentType: 'application/json', body: '[]' })
      })
    }

    // Seed before the app starts so an anonymous refresh cannot race with the sibling session.
    await pageB.addInitScript(
      ({ accessToken, userProfile }) => {
        if (accessToken) {
          sessionStorage.setItem('access_token', accessToken)
        }
        if (userProfile) {
          sessionStorage.setItem('user_profile', userProfile)
        }
      },
      pageASession,
    )

    await pageB.goto('/student/overview', { waitUntil: 'domcontentloaded' })
    await expect(pageB).toHaveURL(/\/student\/overview$/)
    await expect(pageB.getByRole('button', { name: 'Đăng xuất', exact: true })).toBeVisible()

    await page.getByRole('button', { name: 'Đăng xuất', exact: true }).click()
    await expect(page).toHaveURL(/\/login$/)
    await expect(pageB).toHaveURL(/\/login$/)
    await pageB.close()
  })

  test('A1-T2-06 @tier2 @wave01 Invalid Google challenge/nonce 401 and non-ASCII X-Correlation-ID headers are handled cleanly (AZ-C04)', async ({
    page,
    apiMocks,
  }) => {
    if (isLiveApiMode()) {
      await page.goto('/login', { waitUntil: 'domcontentloaded' })
      const res = await page.evaluate(async () => {
        const response = await fetch('/api/v1/auth/google-challenge', {
          method: 'POST',
          headers: { 'X-Correlation-ID': 'tên-không-hợp-lệ-' + 'x'.repeat(80) },
          credentials: 'include',
        })
        return response.status
      })
      expect(res).toBeLessThan(500)
      return
    }

    let challengeAttempt = 0
    await page.route('**/api/v1/auth/google-challenge', async (route) => {
      challengeAttempt += 1
      if (challengeAttempt === 1) {
        await route.fulfill({
          status: 500,
          contentType: 'application/problem+json',
          headers: { 'X-Correlation-ID': 'e2e-safe-fallback' },
          body: JSON.stringify({
            type: 'https://tools.ietf.org/html/rfc7807',
            title: 'Challenge Failed',
            status: 500,
            detail: 'Cannot issue Google challenge.',
          }),
        })
        return
      }
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        headers: { 'X-Correlation-ID': 'corr-safe-123' },
        body: JSON.stringify({ nonce: 'B'.repeat(43), expiresIn: 120 }),
      })
    })

    await apiMocks.mockProblem(
      'POST',
      '/api/v1/auth/google-login',
      401,
      'Nonce Google không hợp lệ hoặc đã được sử dụng.',
      {
        title: 'Unauthorized',
        code: 'GOOGLE_NONCE_INVALID',
      },
    )

    await page.route('https://accounts.google.com/**', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/javascript',
        body: '/* mocked GIS script */',
      })
    })

    await page.addInitScript(() => {
      let capturedCallback: ((response: { credential: string }) => void) | null = null
      const win = window as unknown as MockGoogleWindow
      win.google = {
        accounts: {
          id: {
            initialize(config) {
              capturedCallback = config.callback
            },
            renderButton(container) {
              container.innerHTML = ''
              const btn = document.createElement('button')
              btn.type = 'button'
              btn.textContent = 'Google Sign-In Mock'
              btn.addEventListener('click', () => {
                capturedCallback?.({ credential: 'replayed-google-token' })
              })
              container.appendChild(btn)
            },
            prompt() {},
          },
        },
      }
    })

    await page.goto('/login', { waitUntil: 'domcontentloaded' })
    await expect(
      page.getByRole('alert').filter({ hasText: 'Không tạo được phiên đăng nhập Google' }),
    ).toBeVisible()

    await page.getByRole('button', { name: 'Thử lại Google' }).click()
    const mockGoogleBtn = page.getByRole('button', { name: 'Google Sign-In Mock' })
    await expect(mockGoogleBtn).toBeVisible()
    await mockGoogleBtn.click()

    await expect(page.getByRole('alert').filter({ hasText: 'Nonce Google không hợp lệ' })).toBeVisible()
    await expect(page).toHaveURL(/\/login$/)
  })
})
