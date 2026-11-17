import { afterEach, describe, expect, it, vi } from 'vitest'
import apiClient from './api.client'
import { courseCloService } from './courseClo.service'

vi.mock('./api.client', () => {
  const client = { get: vi.fn() }
  return { default: client, apiClient: client }
})

afterEach(() => {
  vi.resetAllMocks()
  vi.useRealTimers()
})

describe('current semester department courses', () => {
  it('includes only managed courses with an enabled offering in the current semester', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-11-17T05:00:00Z'))
    const currentCourse = { id: 'current', code: 'C1', name: 'Current' }
    const courses = [
      currentCourse,
      { id: 'old', code: 'C2', name: 'Old' },
      { id: 'disabled', code: 'C3', name: 'Disabled' },
    ]
    vi.mocked(apiClient.get).mockImplementation(async (url) => ({
      data: {
        '/courses/managed-for-clos': courses,
        '/lecturer/course-offerings': [
          { courseId: 'current', semesterId: 'fall', isOfferingEnabled: true },
          { courseId: 'old', semesterId: 'summer', isOfferingEnabled: true },
          { courseId: 'disabled', semesterId: 'fall', isOfferingEnabled: false },
          { courseId: 'outside-department', semesterId: 'fall', isOfferingEnabled: true },
        ],
        '/semesters': [
          {
            id: 'summer',
            code: 'SU26',
            startDate: '2026-05-01',
            endDate: '2026-08-31',
            isActive: false,
          },
          {
            id: 'fall',
            code: 'FA26',
            startDate: '2026-09-01',
            endDate: '2026-12-31',
            isActive: true,
          },
        ],
      }[url as string],
    }))
    expect(await courseCloService.getCurrentSemesterManagedCourses()).toEqual([currentCourse])
  })

  it('does not fall back to a past semester when no current semester exists', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-11-17T05:00:00Z'))
    vi.mocked(apiClient.get).mockImplementation(async url => ({ data: url === '/semesters'
      ? [{ id: 'summer', code: 'SU26', startDate: '2026-05-01', endDate: '2026-08-31', isActive: false }]
      : url === '/lecturer/course-offerings'
        ? [{ courseId: 'old', semesterId: 'summer', isOfferingEnabled: true }]
        : [{ id: 'old', code: 'OLD', name: 'Old' }] }))
    expect(await courseCloService.getCurrentSemesterManagedCourses()).toEqual([])
  })

  it('returns no courses when there is no semester', async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ data: [] })
    expect(await courseCloService.getCurrentSemesterManagedCourses()).toEqual([])
  })
})
