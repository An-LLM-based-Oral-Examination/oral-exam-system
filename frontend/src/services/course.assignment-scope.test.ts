import { beforeEach, describe, expect, it, vi } from 'vitest'
import { courseService } from './course.service'

const get = vi.hoisted(() => vi.fn())
vi.mock('./api.client', () => ({ default: { get } }))
beforeEach(() => { get.mockReset() })

describe('Lecturer course scope', () => {
  it('keeps an empty assignment list instead of falling back to all courses', async () => {
    get.mockResolvedValue({ data: [] })
    expect(await courseService.getLecturerCourses()).toEqual([])
    expect(get.mock.calls).toEqual([['/lecturer/courses']])
  })
  it('propagates a forbidden assignment request without a global course fallback', async () => {
    const forbidden = { status: 403, detail: 'Không có quyền.' }
    get.mockRejectedValue(forbidden)
    await expect(courseService.getLecturerCourses()).rejects.toBe(forbidden)
    expect(get).toHaveBeenCalledTimes(1)
  })
})
