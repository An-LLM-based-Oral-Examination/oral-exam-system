import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { ShiftTimeRangePicker } from '../ShiftTimeRangePicker'
import { CreateSingleShiftDialog } from '../SingleShiftDialog'
import {
  createInitialShiftForm,
  createShiftRoomRow,
  formatShiftTimeSummaryVi,
  getDefaultShiftWindow,
  getLocalTodayDate,
  resolveShiftStaffPools,
  shiftEndPreservingDuration,
} from '../shiftStaffHelpers'
import { officialExamService } from '@/services/officialExam.service'

describe('EO Shift Staff & Time UX Helpers', () => {
  it('computes local today date and clamps default shift window to semester bounds', () => {
    const morningLocal = new Date(2026, 10, 28, 0, 15, 0)
    expect(getLocalTodayDate(morningLocal)).toBe('2026-11-28')

    const inside = getDefaultShiftWindow(
      { startDate: '2026-09-01', endDate: '2026-12-31' },
      60,
      new Date(2026, 10, 28, 12, 51, 0),
    )
    expect(inside).toEqual({
      startTime: '2026-11-28T13:00',
      endTime: '2026-11-28T14:00',
    })

    const beforeSemester = getDefaultShiftWindow(
      { startDate: '2027-01-10', endDate: '2027-04-30' },
      60,
      new Date(2026, 10, 28, 12, 51, 0),
    )
    expect(beforeSemester).toEqual({
      startTime: '2027-01-10T08:00',
      endTime: '2027-01-10T09:00',
    })

    const afterSemester = getDefaultShiftWindow(
      { startDate: '2026-01-01', endDate: '2026-04-30' },
      60,
      new Date(2026, 10, 28, 12, 51, 0),
    )
    expect(afterSemester).toEqual({
      startTime: '2026-04-30T08:00',
      endTime: '2026-04-30T09:00',
    })
  })

  it('auto-preserves shift duration when startTime changes and renders 24h Vietnamese summary', () => {
    expect(
      shiftEndPreservingDuration('2026-11-28T08:00', '2026-11-28T09:30', '2026-11-28T13:30'),
    ).toBe('2026-11-28T15:00')

    const summary = formatShiftTimeSummaryVi('2026-11-28T13:30', '2026-11-28T15:00')
    expect(summary).toContain('28/11/2026')
    expect(summary).toContain('13:30 → 15:00')
    expect(summary).toContain('(90 phút)')
  })

  it('resolves staff pools strictly from officialExamService.getShiftStaff without leaking lecturers into proctors', async () => {
    const spy = vi.spyOn(officialExamService, 'getShiftStaff').mockResolvedValueOnce({
      proctors: [
        {
          userId: 'proctor-real-1',
          fullName: 'Giám thị Hải',
          email: 'haind@fpt.edu.vn',
          username: 'haind',
          role: 'proctor',
          detail: 'haind@fpt.edu.vn',
          isCourseAssigned: false,
        },
      ],
      examiners: [
        {
          userId: 'lec-swd-1',
          fullName: 'Nguyễn Trọng Tốt',
          email: 'totnt@fpt.edu.vn',
          username: 'totnt',
          role: 'lecturer',
          detail: 'GV môn SWD392 · totnt@fpt.edu.vn',
          isCourseAssigned: true,
        },
      ],
    })

    const pools = await resolveShiftStaffPools('course-swd', 'sem-fa26', [])
    expect(spy).toHaveBeenCalledWith({ courseId: 'course-swd', semesterId: 'sem-fa26' })
    expect(pools.proctors).toEqual([
      { userId: 'proctor-real-1', fullName: 'Giám thị Hải', detail: 'haind@fpt.edu.vn' },
    ])
    expect(pools.examiners).toEqual([
      {
        userId: 'lec-swd-1',
        fullName: 'Nguyễn Trọng Tốt',
        detail: 'GV môn SWD392 · totnt@fpt.edu.vn',
      },
    ])
    spy.mockRestore()
  })

  it('ShiftTimeRangePicker supports quick presets, duration pills, and auto-shifts endTime', () => {
    const onChange = vi.fn()
    render(
      <ShiftTimeRangePicker
        startId="test-start"
        endId="test-end"
        startTime="2026-11-28T08:00"
        endTime="2026-11-28T09:00"
        mode="create"
        onChange={onChange}
      />,
    )

    // Đổi giờ bắt đầu -> tự giữ thời lượng 60p
    fireEvent.change(screen.getByLabelText('Bắt đầu *'), {
      target: { value: '2026-11-28T14:00' },
    })
    expect(onChange).toHaveBeenLastCalledWith('2026-11-28T14:00', '2026-11-28T15:00')

    // Chọn nhanh khung giờ 09:30 -> giữ ngày & thời lượng
    fireEvent.click(screen.getByRole('button', { name: '09:30' }))
    expect(onChange).toHaveBeenLastCalledWith('2026-11-28T09:30', '2026-11-28T10:30')

    // Chọn nhanh thời lượng 90p
    fireEvent.click(screen.getByRole('button', { name: '90p' }))
    expect(onChange).toHaveBeenLastCalledWith('2026-11-28T08:00', '2026-11-28T09:30')

    // Chọn nhanh "Thi ngay"
    fireEvent.click(screen.getByRole('button', { name: /Thi ngay/i }))
    const [nowStart, nowEnd] = onChange.mock.calls.at(-1) as [string, string]
    expect(new Date(nowEnd).getTime() - new Date(nowStart).getTime()).toBe(60 * 60_000)
  })

  it('ShiftTimeRangePicker edit mode jumps stale multi-day past dates to a future slot and warns on semester/end overflow', () => {
    const onChange = vi.fn()
    render(
      <ShiftTimeRangePicker
        startId="edit-start"
        endId="edit-end"
        startTime="2020-01-01T08:00"
        endTime="2020-01-01T09:00"
        mode="edit"
        semesterBounds={{ startDate: '2026-09-01', endDate: '2026-12-31', code: 'FA26' }}
        onChange={onChange}
      />,
    )

    expect(
      screen.getByText(/Khi cập nhật ca đã lên lịch, giờ bắt đầu mới cần ở trong tương lai/i),
    ).toBeInTheDocument()
    expect(screen.getByText(/đang nằm ngoài học kỳ FA26/i)).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: '09:30' }))
    const [nextStart, nextEnd] = onChange.mock.calls.at(-1) as [string, string]
    expect(new Date(nextStart).getTime()).toBeGreaterThan(Date.now())
    expect(new Date(nextEnd).getTime() - new Date(nextStart).getTime()).toBe(60 * 60_000)
  })

  it('SingleShiftDialog blocks duplicate examiner across parallel rooms in the same shift', () => {
    const baseForm = createInitialShiftForm({ startDate: '2026-09-01', endDate: '2026-12-31' })
    baseForm.semesterId = 'sem-1'
    baseForm.courseId = 'course-1'
    baseForm.shifts[0].shiftName = 'Ca 1'
    baseForm.shifts[0].startTime = '2026-11-29T09:30'
    baseForm.shifts[0].endTime = '2026-11-29T10:30'
    baseForm.shifts[0].rooms = [
      createShiftRoomRow({
        roomLab: 'A1',
        maxSeats: '24',
        proctorUserId: 'p1',
        examinerUserId: 'e1',
      }),
      createShiftRoomRow({
        roomLab: 'A2',
        maxSeats: '24',
        proctorUserId: 'p2',
        examinerUserId: 'e1',
      }),
    ]

    render(
      <CreateSingleShiftDialog
        open
        busy={false}
        isLoading={false}
        isLoadingOfferings={false}
        isLoadingLecturers={false}
        semesters={[
          {
            id: 'sem-1',
            code: 'FA26',
            name: 'Fall 2026',
            startDate: '2026-09-01',
            endDate: '2026-12-31',
            isActive: true,
          },
        ]}
        offerings={[
          {
            courseOfferingId: 'off-1',
            courseId: 'course-1',
            courseCode: 'SWD392',
            courseName: 'Software Architecture',
            description: null,
            credits: 3,
            academicProgramId: null,
            academicProgramCode: null,
            isCourseActive: true,
            isSelected: true,
          },
        ]}
        selectedSemester={{
          id: 'sem-1',
          code: 'FA26',
          name: 'Fall 2026',
          startDate: '2026-09-01',
          endDate: '2026-12-31',
          isActive: true,
        }}
        selectedCourse={{
          courseOfferingId: 'off-1',
          courseId: 'course-1',
          courseCode: 'SWD392',
          courseName: 'Software Architecture',
          description: null,
          credits: 3,
          academicProgramId: null,
          academicProgramCode: null,
          isCourseActive: true,
          isSelected: true,
        }}
        selectedCourseSessions={[]}
        proctorOptions={[
          { userId: 'p1', fullName: 'Giám thị 1' },
          { userId: 'p2', fullName: 'Giám thị 2' },
        ]}
        examinerOptions={[
          { userId: 'e1', fullName: 'Giám khảo 1' },
          { userId: 'e2', fullName: 'Giám khảo 2' },
        ]}
        shiftForm={baseForm}
        error=""
        offeringsError=""
        lecturersError=""
        onClose={vi.fn()}
        onSubmit={vi.fn()}
        onSemesterChange={vi.fn()}
        onCourseChange={vi.fn()}
        onChangeField={vi.fn()}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: /Tiếp tục: Cấu hình ca & phòng/i }))

    expect(
      screen.getByText(/Một Giám khảo không thể chấm đồng thời 2 phòng trong cùng khung giờ/i),
    ).toBeInTheDocument()
    expect(screen.getAllByText('Giám khảo trùng phòng khác')).toHaveLength(2)
    expect(screen.getByRole('button', { name: /Tiếp tục: Xem lại/i })).toBeDisabled()
  })
})
