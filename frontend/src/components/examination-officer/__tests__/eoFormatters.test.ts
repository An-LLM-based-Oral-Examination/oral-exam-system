import { describe, expect, it } from 'vitest'
import { buildShiftRoomRecommendation } from '../eoFormatters'

describe('buildShiftRoomRecommendation', () => {
  it('handles 0 students cleanly with default 24 seats', () => {
    const rec = buildShiftRoomRecommendation(0)
    expect(rec.totalStudents).toBe(0)
    expect(rec.recommendedShifts).toBe(0)
    expect(rec.totalRooms).toBe(0)
    expect(rec.suggestedMaxSeats).toBe(24)
    expect(rec.shifts).toEqual([])
  })

  it('splits 100 students with hard cap 24/room into 5 rooms across 2 shifts: Ca 1 (72: 3x24) & Ca 2 (28: 2x14)', () => {
    const rec = buildShiftRoomRecommendation(100)
    expect(rec.recommendedShifts).toBe(2)
    expect(rec.totalRooms).toBe(5)
    expect(rec.roomsPerShiftText).toBe('2–3 phòng/ca')
    expect(rec.suggestedMaxSeats).toBe(24)
    expect(rec.shifts).toHaveLength(2)
    expect(rec.shifts[0]).toMatchObject({
      label: 'Ca 1',
      studentsPerShift: 72,
      roomsCount: 3,
      roomCapacities: [24, 24, 24],
      roomDistributionText: '3 phòng × 24 SV',
    })
    expect(rec.shifts[1]).toMatchObject({
      label: 'Ca 2 (ca cuối)',
      studentsPerShift: 28,
      roomsCount: 2,
      roomCapacities: [14, 14],
      roomDistributionText: '2 phòng × 14 SV',
    })
  })

  it('splits 132 students into 2 shifts of 3 rooms each: Ca 1 (72 SV: 3x24) and Ca 2 (60 SV: 3x20)', () => {
    const rec = buildShiftRoomRecommendation(132)
    expect(rec.recommendedShifts).toBe(2)
    expect(rec.totalRooms).toBe(6)
    expect(rec.roomsPerShiftText).toBe('3 phòng/ca')
    expect(rec.suggestedMaxSeats).toBe(24)
    expect(rec.shifts[0]).toMatchObject({
      label: 'Ca 1',
      studentsPerShift: 72,
      roomsCount: 3,
      roomDistributionText: '3 phòng × 24 SV',
    })
    expect(rec.shifts[1]).toMatchObject({
      label: 'Ca 2 (ca cuối)',
      studentsPerShift: 60,
      roomsCount: 3,
      roomDistributionText: '3 phòng × 20 SV',
    })
  })

  it('splits 120 students into 2 shifts (3 rooms + 2 rooms, 24 SV/room)', () => {
    const rec = buildShiftRoomRecommendation(120)
    expect(rec.recommendedShifts).toBe(2)
    expect(rec.totalRooms).toBe(5)
    expect(rec.roomsPerShiftText).toBe('2–3 phòng/ca')
    expect(rec.shifts[0]).toMatchObject({
      label: 'Ca 1',
      studentsPerShift: 72,
      roomsCount: 3,
      roomDistributionText: '3 phòng × 24 SV',
    })
    expect(rec.shifts[1]).toMatchObject({
      label: 'Ca 2 (ca cuối)',
      studentsPerShift: 48,
      roomsCount: 2,
      roomDistributionText: '2 phòng × 24 SV',
    })
  })

  it('groups identical full shifts cleanly for 200 students (9 rooms across 3 shifts: 3x24, 3x24, 56 SV)', () => {
    const rec = buildShiftRoomRecommendation(200)
    expect(rec.recommendedShifts).toBe(3)
    expect(rec.totalRooms).toBe(9)
    expect(rec.shifts[0]).toMatchObject({
      label: 'Ca 1 – Ca 2',
      shiftCount: 2,
      studentsPerShift: 72,
      roomDistributionText: '3 phòng × 24 SV',
    })
    expect(rec.shifts[1]).toMatchObject({
      label: 'Ca 3 (ca cuối)',
      studentsPerShift: 56,
      roomDistributionText: '2 phòng × 19 SV & 1 phòng × 18 SV',
    })
  })
})
