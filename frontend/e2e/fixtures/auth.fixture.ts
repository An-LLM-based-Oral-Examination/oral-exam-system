import { randomInt } from 'node:crypto'
import { request as playwrightRequest, type Page } from '@playwright/test'
import { getLiveBaseUrl, getLiveCredentials, isLiveApiMode, type CanonicalRole } from './env'
import type { ApiMocksFixture } from '../helpers/api-mocks'
import {
  BACKEND_ROLE_BY_CANONICAL,
  makeUser,
  PORTAL_HOME_BY_ROLE,
  type MockUser,
} from '../helpers/mock-data'

export interface LiveCredentialsOverride {
  username: string
  password: string
}

export interface SeedSessionOptions extends Partial<MockUser> {
  accessToken?: string
  expiresInSeconds?: number
  skipAuthMeMock?: boolean
  credentials?: LiveCredentialsOverride
}

export interface ProvisionedLiveUser {
  user: MockUser
  credentials: LiveCredentialsOverride
  cleanup: () => Promise<void>
}

export interface AuthSessionFixture {
  loginAs: (role: CanonicalRole, overrides?: SeedSessionOptions) => Promise<MockUser>
  seedSession: (role: CanonicalRole, overrides?: SeedSessionOptions) => Promise<MockUser>
  clearSession: () => Promise<void>
  provisionLiveTempUser: (
    role: Extract<CanonicalRole, 'Student' | 'Instructor'>,
  ) => Promise<ProvisionedLiveUser>
}

function toBase64Url(value: string): string {
  return Buffer.from(value, 'utf8')
    .toString('base64')
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/g, '')
}

export function makeTestJwt(user: MockUser, expiresInSeconds = 900): string {
  const header = toBase64Url(JSON.stringify({ alg: 'HS256', typ: 'JWT' }))
  const nowSec = Math.floor(Date.now() / 1000)
  const payload = toBase64Url(
    JSON.stringify({
      sub: user.id,
      email: user.email,
      role: BACKEND_ROLE_BY_CANONICAL[user.role],
      must_change_password: user.mustChangePassword,
      iat: nowSec,
      exp: nowSec + expiresInSeconds,
    }),
  )
  return `${header}.${payload}.e2e-signature`
}

let seedSequence = 0

const CANONICAL_ROLE_BY_BACKEND: Record<string, CanonicalRole> = {
  student: 'Student',
  Student: 'Student',
  lecturer: 'Instructor',
  instructor: 'Instructor',
  Instructor: 'Instructor',
  department_head: 'DepartmentHead',
  DepartmentHead: 'DepartmentHead',
  examination_officer: 'ExaminationOfficer',
  ExaminationOfficer: 'ExaminationOfficer',
  proctor: 'Proctor',
  Proctor: 'Proctor',
  admin: 'Admin',
  Admin: 'Admin',
}

interface LiveLoginPayload {
  accessToken?: string
  mustChangePassword?: boolean
  user?: {
    id?: string
    email?: string
    username?: string | null
    fullName?: string
    role?: string
    studentCode?: string | null
    mustChangePassword?: boolean
  }
}

interface LiveAdminUserPayload {
  id: string
  email: string
  username?: string | null
  fullName: string
  studentCode?: string | null
  role: string
  isActive: boolean
  mustChangePassword: boolean
}

export function createAuthSessionFixture(
  page: Page,
  apiMocks: ApiMocksFixture,
): AuthSessionFixture {
  const seedSession = async (
    role: CanonicalRole,
    overrides: SeedSessionOptions = {},
  ): Promise<MockUser> => {
    const {
      accessToken: providedToken,
      expiresInSeconds,
      skipAuthMeMock = false,
      credentials: providedCredentials,
      ...userOverrides
    } = overrides

    let user = makeUser(role, userOverrides)
    let token = providedToken ?? makeTestJwt(user, expiresInSeconds ?? 900)

    if (isLiveApiMode() && !providedToken) {
      const credentials = providedCredentials ?? getLiveCredentials(role)
      const loginResponse = await page.request.post(`${getLiveBaseUrl()}/api/v1/auth/password-login`, {
        data: {
          username: credentials.username,
          password: credentials.password,
        },
      })
      if (!loginResponse.ok()) {
        throw new Error(
          `Live API seedSession(${role}) failed: HTTP ${loginResponse.status()} ${await loginResponse.text()}`,
        )
      }
      const loginPayload = (await loginResponse.json()) as LiveLoginPayload
      if (!loginPayload.accessToken || !loginPayload.user?.id || !loginPayload.user.email) {
        throw new Error(`Live API seedSession(${role}) returned incomplete login payload.`)
      }
      const resolvedRole =
        (loginPayload.user.role ? CANONICAL_ROLE_BY_BACKEND[loginPayload.user.role] : undefined) ?? role
      user = makeUser(resolvedRole, {
        id: loginPayload.user.id,
        email: loginPayload.user.email,
        username: loginPayload.user.username ?? credentials.username,
        fullName: loginPayload.user.fullName ?? makeUser(resolvedRole).fullName,
        studentCode: loginPayload.user.studentCode ?? null,
        mustChangePassword:
          userOverrides.mustChangePassword ??
          loginPayload.mustChangePassword ??
          loginPayload.user.mustChangePassword ??
          false,
        ...userOverrides,
      })
      token =
        expiresInSeconds !== undefined && expiresInSeconds <= 0
          ? makeTestJwt(user, expiresInSeconds)
          : loginPayload.accessToken
    }

    const profilePayload = {
      id: user.id,
      email: user.email,
      fullName: user.fullName,
      role: user.role,
      mustChangePassword: user.mustChangePassword,
      ...(user.studentCode ? { studentCode: user.studentCode } : {}),
    }

    if (!isLiveApiMode() && !skipAuthMeMock) {
      await apiMocks.mockJson('GET', '/api/v1/auth/me', 200, user)
    }

    seedSequence += 1
    const targetSeq = seedSequence
    const seedSeqPrefix = '__e2e_seed_seq:'

    await page.addInitScript(
      ({ seededToken, seededProfile, seq, prefix }) => {
        const win = window as Window & { __e2eSeedSeq?: number }
        const rawName = typeof window.name === 'string' ? window.name : ''
        const nameSeq = rawName.startsWith(prefix) ? Number(rawName.slice(prefix.length)) : 0
        const appliedSeq = Math.max(
          Number.isFinite(nameSeq) ? nameSeq : 0,
          typeof win.__e2eSeedSeq === 'number' ? win.__e2eSeedSeq : 0,
        )
        if (appliedSeq >= seq) return

        window.name = `${prefix}${seq}`
        win.__e2eSeedSeq = seq
        sessionStorage.setItem('access_token', seededToken)
        sessionStorage.removeItem('refresh_token')
        sessionStorage.setItem('user_profile', JSON.stringify(seededProfile))
        localStorage.setItem(
          'oes_active_identity',
          JSON.stringify({
            eventId: `e2e-evt-${Date.now()}`,
            type: 'LOGIN',
            userId: seededProfile.id,
            sourceTabId: 'e2e-seed-tab',
            occurredAt: Date.now(),
          }),
        )
      },
      { seededToken: token, seededProfile: profilePayload, seq: targetSeq, prefix: seedSeqPrefix },
    )

    const currentUrl = page.url()
    if (currentUrl.startsWith('http://') || currentUrl.startsWith('https://')) {
      await page.evaluate(
        ({ seededToken, seededProfile, seq, prefix }) => {
          const win = window as Window & { __e2eSeedSeq?: number }
          window.name = `${prefix}${seq}`
          win.__e2eSeedSeq = seq
          sessionStorage.setItem('access_token', seededToken)
          sessionStorage.removeItem('refresh_token')
          sessionStorage.setItem('user_profile', JSON.stringify(seededProfile))
          localStorage.setItem(
            'oes_active_identity',
            JSON.stringify({
              eventId: `e2e-evt-${Date.now()}`,
              type: 'LOGIN',
              userId: seededProfile.id,
              sourceTabId: 'e2e-seed-tab',
              occurredAt: Date.now(),
            }),
          )
        },
        { seededToken: token, seededProfile: profilePayload, seq: targetSeq, prefix: seedSeqPrefix },
      )
    }

    return user
  }

  const loginAs = async (
    role: CanonicalRole,
    overrides: SeedSessionOptions = {},
  ): Promise<MockUser> => {
    const {
      accessToken: providedToken,
      expiresInSeconds = 900,
      skipAuthMeMock = false,
      credentials: providedCredentials,
      ...userOverrides
    } = overrides
    const credentials = providedCredentials ?? getLiveCredentials(role)

    let user = makeUser(role, userOverrides)
    const token = providedToken ?? makeTestJwt(user, expiresInSeconds)

    if (!isLiveApiMode()) {
      await apiMocks.mockJson('POST', '/api/v1/auth/password-login', 200, {
        accessToken: token,
        tokenType: 'Bearer',
        expiresIn: expiresInSeconds,
        mustChangePassword: user.mustChangePassword,
        user: {
          id: user.id,
          email: user.email,
          fullName: user.fullName,
          studentCode: user.studentCode,
          role: BACKEND_ROLE_BY_CANONICAL[user.role],
          mustChangePassword: user.mustChangePassword,
        },
      })
      if (!skipAuthMeMock) {
        await apiMocks.mockJson('GET', '/api/v1/auth/me', 200, user)
      }
    }

    await page.goto('/login', { waitUntil: 'domcontentloaded' })
    await page.locator('#login-username').fill(credentials.username)
    await page.locator('#login-password').fill(credentials.password)

    let resolvedRole = role
    if (isLiveApiMode()) {
      const [loginResponse] = await Promise.all([
        page.waitForResponse(
          (res) =>
            res.url().includes('/api/v1/auth/password-login') &&
            res.request().method() === 'POST',
        ),
        page.getByRole('button', { name: 'Đăng nhập', exact: true }).click(),
      ])
      if (!loginResponse.ok()) {
        throw new Error(
          `Live API loginAs(${role}) failed: HTTP ${loginResponse.status()} ${await loginResponse.text()}`,
        )
      }
      const loginPayload = (await loginResponse.json()) as LiveLoginPayload
      if (!loginPayload.accessToken || !loginPayload.user?.id || !loginPayload.user.email) {
        throw new Error(`Live API loginAs(${role}) returned incomplete login payload.`)
      }
      resolvedRole =
        (loginPayload.user.role ? CANONICAL_ROLE_BY_BACKEND[loginPayload.user.role] : undefined) ??
        role
      user = makeUser(resolvedRole, {
        id: loginPayload.user.id,
        email: loginPayload.user.email,
        username: loginPayload.user.username ?? credentials.username,
        fullName: loginPayload.user.fullName ?? makeUser(resolvedRole).fullName,
        studentCode: loginPayload.user.studentCode ?? null,
        mustChangePassword: Boolean(
          loginPayload.mustChangePassword ?? loginPayload.user.mustChangePassword ?? false,
        ),
      })
    } else {
      await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click()
    }

    if (user.mustChangePassword) {
      await page.waitForURL(/\/change-password$/, { waitUntil: 'domcontentloaded' })
    } else {
      const expectedHome = PORTAL_HOME_BY_ROLE[resolvedRole]
      await page.waitForURL(new RegExp(`${expectedHome.replaceAll('/', '\\/')}`), {
        waitUntil: 'domcontentloaded',
      })
    }

    return user
  }

  const provisionLiveTempUser = async (
    role: Extract<CanonicalRole, 'Student' | 'Instructor'>,
  ): Promise<ProvisionedLiveUser> => {
    if (!isLiveApiMode()) {
      throw new Error('provisionLiveTempUser can only be called when E2E_LIVE_API=1.')
    }

    const adminCredentials = getLiveCredentials('Admin')
    const adminContext = await playwrightRequest.newContext({ baseURL: getLiveBaseUrl() })

    try {
      const adminLoginRes = await adminContext.post('/api/v1/auth/password-login', {
        data: {
          username: adminCredentials.username,
          password: adminCredentials.password,
        },
      })
      if (!adminLoginRes.ok()) {
        throw new Error(
          `Live API Admin login for provisioning failed: HTTP ${adminLoginRes.status()} ${await adminLoginRes.text()}`,
        )
      }

      const adminLoginPayload = (await adminLoginRes.json()) as LiveLoginPayload
      const adminToken = adminLoginPayload.accessToken
      if (!adminToken) {
        throw new Error('Live API Admin login did not return an accessToken.')
      }

      const uniqueDigits = String(randomInt(100000, 999999))
      const uniqueTag = `${Date.now().toString(36)}.${uniqueDigits}`
      const tempPassword = `TempPass!${uniqueDigits}`
      const backendRole = BACKEND_ROLE_BY_CANONICAL[role]
      const studentCode = role === 'Student' ? `ET${uniqueDigits}` : null
      const username = role === 'Student' ? null : `e2e.lec.${uniqueTag}`
      const email =
        role === 'Student' ? `e2e.stu.${uniqueTag}@fpt.edu.vn` : `e2e.lec.${uniqueTag}@fe.edu.vn`
      const fullName = role === 'Student' ? 'Ephemeral Student' : 'Ephemeral Lecturer'

      const createRes = await adminContext.post('/api/v1/admin/users', {
        headers: { Authorization: `Bearer ${adminToken}` },
        data: {
          email,
          fullName,
          password: tempPassword,
          role: backendRole,
          ...(studentCode ? { studentCode } : { username }),
        },
      })
      if (!createRes.ok()) {
        throw new Error(
          `Live API provisionLiveTempUser(${role}) failed: HTTP ${createRes.status()} ${await createRes.text()}`,
        )
      }

      const created = (await createRes.json()) as LiveAdminUserPayload
      const resolvedUsername = created.username ?? username ?? created.email
      const resolvedRole = CANONICAL_ROLE_BY_BACKEND[created.role] ?? role
      const user = makeUser(resolvedRole, {
        id: created.id,
        email: created.email,
        username: resolvedUsername,
        fullName: created.fullName,
        studentCode: created.studentCode ?? null,
        isActive: created.isActive,
        mustChangePassword: created.mustChangePassword,
      })

      let cleanedUp = false
      const cleanup = async (): Promise<void> => {
        if (cleanedUp) return
        cleanedUp = true
        try {
          await adminContext.put(`/api/v1/admin/users/${created.id}/status`, {
            headers: { Authorization: `Bearer ${adminToken}` },
            data: { isActive: false, reason: 'E2E ephemeral user cleanup' },
          })
        } finally {
          await adminContext.dispose()
        }
      }

      return {
        user,
        credentials: {
          username: resolvedUsername,
          password: tempPassword,
        },
        cleanup,
      }
    } catch (error) {
      await adminContext.dispose()
      throw error
    }
  }

  const clearSession = async (): Promise<void> => {
    seedSequence += 1
    const clearSeq = seedSequence
    const seedSeqPrefix = '__e2e_seed_seq:'

    await page.addInitScript(
      ({ seq, prefix }) => {
        const win = window as Window & { __e2eSeedSeq?: number }
        const rawName = typeof window.name === 'string' ? window.name : ''
        const nameSeq = rawName.startsWith(prefix) ? Number(rawName.slice(prefix.length)) : 0
        const appliedSeq = Math.max(
          Number.isFinite(nameSeq) ? nameSeq : 0,
          typeof win.__e2eSeedSeq === 'number' ? win.__e2eSeedSeq : 0,
        )
        if (appliedSeq >= seq) return

        window.name = `${prefix}${seq}`
        win.__e2eSeedSeq = seq
        sessionStorage.clear()
        localStorage.removeItem('oes_active_identity')
        localStorage.removeItem('oes_auth_refresh_lock')
      },
      { seq: clearSeq, prefix: seedSeqPrefix },
    )

    const currentUrl = page.url()
    if (!currentUrl.startsWith('http://') && !currentUrl.startsWith('https://')) {
      return
    }
    await page.evaluate(
      ({ seq, prefix }) => {
        const win = window as Window & { __e2eSeedSeq?: number }
        window.name = `${prefix}${seq}`
        win.__e2eSeedSeq = seq
        sessionStorage.clear()
        localStorage.removeItem('oes_active_identity')
        localStorage.removeItem('oes_auth_refresh_lock')
      },
      { seq: clearSeq, prefix: seedSeqPrefix },
    )
  }

  return {
    loginAs,
    seedSession,
    clearSession,
    provisionLiveTempUser,
  }
}
