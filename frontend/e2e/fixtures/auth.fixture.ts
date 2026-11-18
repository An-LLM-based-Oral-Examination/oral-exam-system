import type { Page } from '@playwright/test'
import { getLiveBaseUrl, getLiveCredentials, isLiveApiMode, type CanonicalRole } from './env'
import type { ApiMocksFixture } from '../helpers/api-mocks'
import {
  BACKEND_ROLE_BY_CANONICAL,
  makeUser,
  PORTAL_HOME_BY_ROLE,
  type MockUser,
} from '../helpers/mock-data'

export interface SeedSessionOptions extends Partial<MockUser> {
  accessToken?: string
  expiresInSeconds?: number
  skipAuthMeMock?: boolean
}

export interface AuthSessionFixture {
  loginAs: (role: CanonicalRole, overrides?: SeedSessionOptions) => Promise<MockUser>
  seedSession: (role: CanonicalRole, overrides?: SeedSessionOptions) => Promise<MockUser>
  clearSession: () => Promise<void>
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
    fullName?: string
    role?: string
    studentCode?: string | null
    mustChangePassword?: boolean
  }
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
      ...userOverrides
    } = overrides

    let user = makeUser(role, userOverrides)
    let token = providedToken ?? makeTestJwt(user, expiresInSeconds ?? 900)

    if (isLiveApiMode() && !providedToken) {
      const credentials = getLiveCredentials(role)
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
    const credentials = getLiveCredentials(role)
    const {
      accessToken: providedToken,
      expiresInSeconds = 900,
      skipAuthMeMock = false,
      ...userOverrides
    } = overrides

    const user = makeUser(role, userOverrides)
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
    await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click()

    if (user.mustChangePassword) {
      await page.waitForURL(/\/change-password$/, { waitUntil: 'domcontentloaded' })
    } else {
      const expectedHome = PORTAL_HOME_BY_ROLE[role]
      await page.waitForURL(new RegExp(`${expectedHome.replaceAll('/', '\\/')}`), {
        waitUntil: 'domcontentloaded',
      })
    }

    return user
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
  }
}
