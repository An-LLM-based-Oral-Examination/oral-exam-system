import { cleanup, render, renderHook, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { createInClassShiftHubConnection, createPracticeHubConnection } from '@/services/signalr.service'
import { useSignalR } from '@/hooks/useSignalR'
import { InClassShiftResultsPage } from '@/pages/lecturer/InClassShiftResultsPage'
import { StudentInClassExamPage } from '@/pages/student/StudentInClassExamPage'
import { ensureFreshAccessToken } from '@/services/api.client'
import { inClassShiftService } from '@/services/inClassShift.service'

const signalrMockState = vi.hoisted(() => ({
  capturedFactories: [] as Array<{ hubUrl: string; accessTokenFactory?: () => string | Promise<string> }>,
  startImpl: null as (() => Promise<void>) | null,
  invokeSpy: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@microsoft/signalr', () => {
  class MockHubConnectionBuilder {
    private hubUrl = ''
    private accessTokenFactory?: () => string | Promise<string>

    withUrl(hubUrl: string, options?: { accessTokenFactory?: () => string | Promise<string> }) {
      this.hubUrl = hubUrl
      this.accessTokenFactory = options?.accessTokenFactory
      signalrMockState.capturedFactories.push({
        hubUrl,
        accessTokenFactory: options?.accessTokenFactory,
      })
      return this
    }

    withAutomaticReconnect() {
      return this
    }

    configureLogging() {
      return this
    }

    build() {
      return {
        state: 'Disconnected',
        hubUrl: this.hubUrl,
        accessTokenFactory: this.accessTokenFactory,
        on: vi.fn(),
        off: vi.fn(),
        onreconnecting: vi.fn(),
        onreconnected: vi.fn(),
        onclose: vi.fn(),
        start: vi.fn(() => (signalrMockState.startImpl ? signalrMockState.startImpl() : Promise.resolve())),
        stop: vi.fn().mockResolvedValue(undefined),
        invoke: signalrMockState.invokeSpy,
      }
    }
  }

  return {
    HubConnectionBuilder: MockHubConnectionBuilder,
    HubConnectionState: {
      Disconnected: 'Disconnected',
      Connecting: 'Connecting',
      Connected: 'Connected',
      Disconnecting: 'Disconnecting',
      Reconnecting: 'Reconnecting',
    },
    LogLevel: { Warning: 3 },
  }
})

vi.mock('@/services/api.client', () => ({
  ensureFreshAccessToken: vi.fn(),
  apiClient: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
  },
}))

vi.mock('@/services/class.service', () => ({
  classService: {
    listMyClasses: vi.fn().mockResolvedValue([]),
  },
}))

vi.mock('@/services/inClassShift.service', () => ({
  inClassShiftService: {
    getShift: vi.fn(),
    listSubmissions: vi.fn(),
    listStudentShifts: vi.fn(),
    listStudentResults: vi.fn(),
  },
}))

describe('SignalR async accessTokenFactory token freshness (M2 Item 7)', () => {
  beforeEach(() => {
    signalrMockState.capturedFactories = []
    signalrMockState.startImpl = null
    signalrMockState.invokeSpy = vi.fn().mockResolvedValue(undefined)
    vi.mocked(ensureFreshAccessToken).mockResolvedValue('fresh-jwt-after-15m')
  })

  afterEach(() => {
    cleanup()
    vi.restoreAllMocks()
  })

  it('supports async accessTokenFactory in createPracticeHubConnection and createInClassShiftHubConnection', async () => {
    const asyncPracticeFactory = vi.fn(async () => 'practice-fresh-jwt')
    const asyncShiftFactory = vi.fn(async () => 'shift-fresh-jwt')

    createPracticeHubConnection('/hubs/practice', asyncPracticeFactory)
    createInClassShiftHubConnection('/hubs/in-class-shift', asyncShiftFactory)

    expect(signalrMockState.capturedFactories).toHaveLength(2)
    expect(await signalrMockState.capturedFactories[0].accessTokenFactory?.()).toBe('practice-fresh-jwt')
    expect(await signalrMockState.capturedFactories[1].accessTokenFactory?.()).toBe('shift-fresh-jwt')
  })

  it('useSignalR passes () => ensureFreshAccessToken(90) for negotiate and reconnects', async () => {
    const { unmount } = renderHook(() => useSignalR('practice-session-1', '/hubs/practice'))

    await waitFor(() => {
      expect(signalrMockState.capturedFactories.length).toBeGreaterThanOrEqual(1)
    })

    const factory = signalrMockState.capturedFactories[0].accessTokenFactory
    expect(factory).toBeTypeOf('function')

    const token = await factory?.()
    expect(ensureFreshAccessToken).toHaveBeenCalledWith(90)
    expect(token).toBe('fresh-jwt-after-15m')
    unmount()
  })

  it('InClassShiftResultsPage passes () => ensureFreshAccessToken(90) to createInClassShiftHubConnection', async () => {
    vi.mocked(inClassShiftService.getShift).mockResolvedValue({
      id: 'shift-res-1',
      classId: 'cls-1',
      className: 'PRN231',
      courseCode: 'PRN231',
      title: 'Ca thi thực hành',
      description: '',
      status: 'ACTIVE',
      isLocked: false,
      startTime: '2026-11-18T05:00:00Z',
      durationMinutes: 60,
      examDurationMinutes: 20,
      questionCount: 2,
      hasFollowUp: false,
      maxFollowUpQuestions: 0,
      isAutoPublishGrades: false,
      questionPoolMode: 'ALL_COURSE_BANK',
      selectedDifficulties: ['easy'],
      selectedCloIds: [],
      passwordRotationSeconds: 300,
      gracePeriodSeconds: 60,
      joinedCount: 1,
      submittedCount: 0,
      createdAt: '2026-11-18T04:50:00Z',
    })
    vi.mocked(inClassShiftService.listSubmissions).mockResolvedValue([])

    render(
      <MemoryRouter initialEntries={['/lecturer/classes/cls-1/in-class-shifts/shift-res-1/results']}>
        <Routes>
          <Route
            path="/lecturer/classes/:classId/in-class-shifts/:shiftId/results"
            element={<InClassShiftResultsPage />}
          />
        </Routes>
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(signalrMockState.capturedFactories.length).toBeGreaterThanOrEqual(1)
    })

    const token = await signalrMockState.capturedFactories[0].accessTokenFactory?.()
    expect(ensureFreshAccessToken).toHaveBeenCalledWith(90)
    expect(token).toBe('fresh-jwt-after-15m')
  })

  it('StudentInClassExamPage passes () => ensureFreshAccessToken(90) when opening finalized results', async () => {
    vi.mocked(inClassShiftService.listStudentShifts).mockResolvedValue([
      {
        id: 'shift-stu-fin',
        classId: 'cls-1',
        className: 'PRN231',
        courseCode: 'PRN231',
        title: 'Ca Đã Chốt Điểm',
        description: '',
        status: 'FINALIZED',
        isLocked: true,
        startTime: '2026-11-18T05:00:00Z',
        durationMinutes: 60,
        examDurationMinutes: 20,
        questionCount: 1,
        hasFollowUp: false,
        maxFollowUpQuestions: 0,
        isAutoPublishGrades: false,
        questionPoolMode: 'ALL_COURSE_BANK',
        selectedDifficulties: ['easy'],
        selectedCloIds: [],
        passwordRotationSeconds: 300,
        gracePeriodSeconds: 60,
        joinedCount: 1,
        submittedCount: 1,
        createdAt: '2026-11-18T04:50:00Z',
        myParticipationStatus: 'FINALIZED',
      },
    ])
    vi.mocked(inClassShiftService.listStudentResults).mockResolvedValue([])

    render(
      <MemoryRouter initialEntries={['/in-class-exam?result=shift-stu-fin']}>
        <StudentInClassExamPage />
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(signalrMockState.capturedFactories.length).toBeGreaterThanOrEqual(1)
    })

    const token = await signalrMockState.capturedFactories[0].accessTokenFactory?.()
    expect(ensureFreshAccessToken).toHaveBeenCalledWith(90)
    expect(token).toBe('fresh-jwt-after-15m')
  })

  it('InClassShiftResultsPage skips JoinShift when unmounted before connection.start() resolves', async () => {
    let resolveStart!: () => void
    signalrMockState.startImpl = () =>
      new Promise<void>((resolve) => {
        resolveStart = resolve
      })

    vi.mocked(inClassShiftService.getShift).mockResolvedValue({
      id: 'shift-res-dispose',
      classId: 'cls-1',
      className: 'PRN231',
      courseCode: 'PRN231',
      title: 'Ca thi unmount',
      description: '',
      status: 'ACTIVE',
      isLocked: false,
      startTime: '2026-11-18T05:00:00Z',
      durationMinutes: 60,
      examDurationMinutes: 20,
      questionCount: 2,
      hasFollowUp: false,
      maxFollowUpQuestions: 0,
      isAutoPublishGrades: false,
      questionPoolMode: 'ALL_COURSE_BANK',
      selectedDifficulties: ['easy'],
      selectedCloIds: [],
      passwordRotationSeconds: 300,
      gracePeriodSeconds: 60,
      joinedCount: 1,
      submittedCount: 0,
      createdAt: '2026-11-18T04:50:00Z',
    })
    vi.mocked(inClassShiftService.listSubmissions).mockResolvedValue([])

    const view = render(
      <MemoryRouter initialEntries={['/lecturer/classes/cls-1/in-class-shifts/shift-res-dispose/results']}>
        <Routes>
          <Route
            path="/lecturer/classes/:classId/in-class-shifts/:shiftId/results"
            element={<InClassShiftResultsPage />}
          />
        </Routes>
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(signalrMockState.capturedFactories.length).toBeGreaterThanOrEqual(1)
    })

    view.unmount()
    resolveStart()
    await Promise.resolve()
    await Promise.resolve()

    expect(signalrMockState.invokeSpy).not.toHaveBeenCalledWith('JoinShift', 'shift-res-dispose')
  })
})
