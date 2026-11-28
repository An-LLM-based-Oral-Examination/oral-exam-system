import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ExamCampaignSetupStep } from '@/components/examination-officer/ExamCampaignSetupStep'
import { CandidateRosterImportPanel } from '@/components/examination-officer/CandidateRosterImportPanel'
import { RoomEstimateBanner } from '@/components/examination-officer/RoomEstimateBanner'
import { BatchShiftCreatorModal } from '@/components/examination-officer/BatchShiftCreatorModal'
import { AutoAllocateWizardDialog } from '@/components/examination-officer/AutoAllocateWizardDialog'
import { ExaminationOfficerShiftMonitorPage } from '../ExaminationOfficerShiftMonitorPage'
import { AuditEvidencePage } from '@/pages/lecturer/AuditEvidencePage'
import { officialExamService } from '@/services/officialExam.service'
import { proctorService } from '@/services/proctor.service'
import { useAuth } from '@/hooks/useAuth'
import { useAntiCheatHub } from '@/hooks/useAntiCheatHub'

vi.mock('@/services/officialExam.service', () => ({
  officialExamService: {
    getEligibleCourses: vi.fn(),
    batchCreateSessions: vi.fn(),
    previewCandidateImport: vi.fn(),
    commitCandidateImport: vi.fn(),
    getSessionCandidates: vi.fn(),
    removeSessionCandidate: vi.fn(),
    downloadCandidateTemplate: vi.fn(),
    batchOpenShifts: vi.fn(),
    autoAllocateStudents: vi.fn(),
    getShifts: vi.fn(),
    getAuditEvidence: vi.fn(),
    getAntiCheatLogs: vi.fn(),
  },
}))

vi.mock('@/services/proctor.service', () => ({
  proctorService: {
    getShiftAttendance: vi.fn(),
  },
}))

vi.mock('@/hooks/useAuth', () => ({
  useAuth: vi.fn(),
}))

vi.mock('@/hooks/useAntiCheatHub', () => ({
  useAntiCheatHub: vi.fn(),
}))

describe('Examination Officer 6-Step MF-04 Flow & FE-3 Monitor/Audit Picker', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(useAuth).mockReturnValue({
      user: {
        id: 'examiner-1',
        email: 'gv@fpt.edu.vn',
        fullName: 'Giám khảo A',
        role: 'Instructor',
      },
      isAuthenticated: true,
    } as never)
    vi.mocked(useAntiCheatHub).mockReturnValue({
      status: 'connected',
      isConnected: true,
      alerts: [],
      latestAlert: null,
      reconnectVersion: 0,
      lastError: null,
      clearAlerts: vi.fn(),
    })
  })

  it('Step 1: loads eligible courses, displays DH read-only Follow-up badge, and batch-creates FE sessions', async () => {
    vi.mocked(officialExamService.getEligibleCourses).mockResolvedValue([
      {
        courseId: 'course-1',
        courseCode: 'PRN231',
        courseName: 'Building Cross-Platform Back-End',
        semesterId: 'sem-1',
        semesterCode: 'FA26',
        activeClassCount: 2,
        eligibleStudentCount: 50,
        maxStudentsPerRoom: 24,
        recommendedRoomCount: 3,
        activeExamStructureId: 'struct-1',
        activeExamStructureName: 'Final Structure',
        handedOverExamSetCount: 25,
        hasFollowUp: true,
        maxFollowUpQuestions: 2,
        existingSessionId: null,
        existingSessionTitle: null,
        existingSessionStatus: null,
        feSession: null,
        reSession: null,
      },
    ])
    vi.mocked(officialExamService.batchCreateSessions).mockResolvedValue({
      semesterId: 'sem-1',
      createdCount: 1,
      sessions: [],
    })
    const onSessionsCreated = vi.fn()

    render(
      <MemoryRouter>
        <ExamCampaignSetupStep
          semesters={[
            {
              id: 'sem-1',
              code: 'FA26',
              name: 'Fall 2026',
              startDate: '2026-01-01',
              endDate: '2026-12-31',
              isActive: true,
            },
          ]}
          selectedSemesterId="sem-1"
          onSemesterChange={vi.fn()}
          onSessionsCreated={onSessionsCreated}
        />
      </MemoryRouter>,
    )

    expect(await screen.findByText('PRN231')).toBeInTheDocument()
    expect(screen.getByText('Bật · tối đa 2 câu')).toBeInTheDocument()
    expect(screen.getByText('Do Trưởng BM cấu hình')).toBeInTheDocument()

    fireEvent.click(screen.getByLabelText('Chọn môn PRN231'))
    fireEvent.click(screen.getByRole('button', { name: /Khởi tạo 1 kỳ thi đợt FE/ }))

    await waitFor(() =>
      expect(officialExamService.batchCreateSessions).toHaveBeenCalledWith(
        'sem-1',
        expect.objectContaining({
          examAttemptType: 'FE',
          courses: [{ courseId: 'course-1', examAttemptType: 'FE' }],
        }),
      ),
    )
    expect(onSessionsCreated).toHaveBeenCalled()
  })

  it('Step 2: rejects >2MB file, blocks commit on invalid preview, commits valid preview via ConfirmDialog, and removes candidate', async () => {
    vi.mocked(officialExamService.getSessionCandidates).mockResolvedValue({
      sessionId: 'session-1',
      examAttemptType: 'FE',
      totalCount: 1,
      allocatedCount: 0,
      unallocatedCount: 1,
      pageNumber: 1,
      pageSize: 100,
      items: [
        {
          studentId: 'stu-1',
          studentCode: 'SE170001',
          fullName: 'Nguyễn Văn A',
          email: 'a@fpt.edu.vn',
          importedAt: '2026-11-20T08:00:00.000Z',
          isAllocated: false,
          ticketId: null,
          shiftId: null,
          roomLab: null,
          seatNumber: null,
          ticketStatus: null,
        },
      ],
    })
    vi.mocked(officialExamService.previewCandidateImport).mockResolvedValue({
      sessionId: 'session-1',
      examAttemptType: 'FE',
      mode: 'REPLACE',
      totalRows: 2,
      validCount: 2,
      invalidCount: 0,
      duplicateCount: 0,
      canCommit: true,
      existingCandidateCount: 1,
      projectedTotalCandidates: 2,
      maxStudentsPerRoom: 24,
      recommendedTotalRooms: 1,
      rows: [
        {
          sourceRow: 2,
          studentCode: 'SE170001',
          studentId: 'stu-1',
          fullName: 'Nguyễn Văn A',
          email: 'a@fpt.edu.vn',
          isEnrolledInSemester: true,
          alreadyInSession: false,
          isValid: true,
          issues: [],
        },
      ],
      issues: [],
    })
    vi.mocked(officialExamService.commitCandidateImport).mockResolvedValue({
      sessionId: 'session-1',
      examAttemptType: 'FE',
      mode: 'REPLACE',
      importedCount: 2,
      totalCandidateCount: 2,
      recommendedTotalRooms: 1,
      importedAt: '2026-11-20T08:05:00.000Z',
    })
    vi.mocked(officialExamService.removeSessionCandidate).mockResolvedValue(undefined)

    const onRosterChanged = vi.fn()
    render(
      <CandidateRosterImportPanel
        sessionId="session-1"
        examAttemptType="FE"
        onRosterChanged={onRosterChanged}
      />,
    )

    expect(await screen.findByText('SE170001')).toBeInTheDocument()

    const fileInput = screen.getByLabelText('Chọn file danh sách thí sinh (Excel/CSV)')
    const oversizedFile = new File([new Uint8Array(2 * 1024 * 1024 + 10)], 'huge.csv', {
      type: 'text/csv',
    })
    fireEvent.change(fileInput, { target: { files: [oversizedFile] } })
    expect(
      await screen.findByText('Dung lượng file danh sách thí sinh không được vượt quá 2 MB.'),
    ).toBeInTheDocument()

    const validFile = new File(['MSSV\nSE170001\nSE170002'], 'roster.csv', { type: 'text/csv' })
    fireEvent.change(fileInput, { target: { files: [validFile] } })
    fireEvent.click(screen.getByRole('button', { name: /Kiểm tra file/ }))

    expect(await screen.findByText('1 phòng')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: /Xác nhận lưu 2 thí sinh/ }))
    expect(await screen.findByRole('alertdialog')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận lưu danh sách' }))

    await waitFor(() =>
      expect(officialExamService.commitCandidateImport).toHaveBeenCalledWith(
        'session-1',
        validFile,
        'REPLACE',
      ),
    )

    fireEvent.click(screen.getByRole('button', { name: 'Gỡ' }))
    expect(await screen.findByRole('alertdialog')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận gỡ' }))
    await waitFor(() =>
      expect(officialExamService.removeSessionCandidate).toHaveBeenCalledWith('session-1', 'stu-1'),
    )
  })

  it('Step 3: RoomEstimateBanner renders ceil(S/24) and red warning when HANDED_OVER exam sets are insufficient', () => {
    render(
      <RoomEstimateBanner
        estimate={{
          sessionId: 'session-1',
          courseId: 'course-1',
          courseCode: 'PRN231',
          courseName: 'Backend',
          semesterId: 'sem-1',
          semesterCode: 'FA26',
          examAttemptType: 'FE',
          candidateSource: 'OFFICIAL_EXAM_CANDIDATES',
          hasImportedCandidates: true,
          importedCandidateCount: 50,
          semesterEnrolledCount: 50,
          eligibleStudentCount: 50,
          allocatedStudentCount: 0,
          unallocatedStudentCount: 50,
          maxStudentsPerRoom: 24,
          currentShiftRoomCount: 0,
          currentTotalCapacity: 0,
          currentAvailableSeats: 0,
          recommendedTotalRooms: 3,
          recommendedAdditionalRooms: 3,
          handedOverExamSetCount: 10,
        }}
      />,
    )

    expect(screen.getByText('3 phòng')).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent(/thiếu bộ đề HANDED_OVER/i)
  })

  it('Step 4: BatchShiftCreatorModal validates room/staff conflicts and calls batchOpenShifts without raw UUID inputs', async () => {
    vi.mocked(officialExamService.batchOpenShifts).mockResolvedValue({
      sessionId: 'session-1',
      shiftGroupName: 'Ca 1 - Sáng',
      createdRoomCount: 2,
      totalCapacityCreated: 48,
      shifts: [],
    })

    const onCreated = vi.fn()
    render(
      <BatchShiftCreatorModal
        open
        sessionId="session-1"
        defaultExamDate="2026-11-20"
        recommendedRooms={2}
        proctors={[
          { userId: 'p1', fullName: 'Giám thị 1' },
          { userId: 'p2', fullName: 'Giám thị 2' },
        ]}
        examiners={[
          { userId: 'e1', fullName: 'Giám khảo 1' },
          { userId: 'e2', fullName: 'Giám khảo 2' },
        ]}
        onClose={vi.fn()}
        onCreated={onCreated}
      />,
    )

    fireEvent.change(screen.getByLabelText('Giám thị phòng 1'), { target: { value: 'p1' } })
    fireEvent.change(screen.getByLabelText('Giám khảo phòng 1'), { target: { value: 'e1' } })
    fireEvent.change(screen.getByLabelText('Giám thị phòng 2'), { target: { value: 'p1' } })
    fireEvent.change(screen.getByLabelText('Giám khảo phòng 2'), { target: { value: 'e2' } })
    fireEvent.click(screen.getByRole('button', { name: /Mở 2 phòng thi/ }))

    expect(await screen.findByRole('alert')).toHaveTextContent(/đã được phân công ở phòng khác/)
    expect(officialExamService.batchOpenShifts).not.toHaveBeenCalled()

    fireEvent.change(screen.getByLabelText('Giám thị phòng 2'), { target: { value: 'p2' } })
    fireEvent.click(screen.getByRole('button', { name: /Mở 2 phòng thi/ }))

    await waitFor(() =>
      expect(officialExamService.batchOpenShifts).toHaveBeenCalledWith(
        'session-1',
        expect.objectContaining({
          rooms: expect.arrayContaining([
            expect.objectContaining({ proctorUserId: 'p1', examinerUserId: 'e1', maxSeats: 24 }),
            expect.objectContaining({ proctorUserId: 'p2', examinerUserId: 'e2', maxSeats: 24 }),
          ]),
        }),
      ),
    )
    expect(onCreated).toHaveBeenCalled()
  })

  it('Step 5: AutoAllocateWizardDialog dry-run highlights balanced final shift (17/17/16) and commits via ConfirmDialog', async () => {
    vi.mocked(officialExamService.autoAllocateStudents)
      .mockResolvedValueOnce({
        sessionId: 'session-1',
        dryRun: true,
        totalEligibleStudents: 50,
        previouslyAllocatedCount: 0,
        newlyAllocatedCount: 50,
        shiftGroups: [
          {
            startTime: '2026-11-20T08:00:00.000Z',
            endTime: '2026-11-20T09:30:00.000Z',
            isFinalBalancedShift: true,
            totalAssignedInGroup: 50,
            rooms: [
              {
                shiftId: 's1',
                roomLab: 'Lab 301',
                maxSeats: 24,
                assignedBefore: 0,
                assignedNow: 17,
                totalAfter: 17,
              },
              {
                shiftId: 's2',
                roomLab: 'Lab 302',
                maxSeats: 24,
                assignedBefore: 0,
                assignedNow: 17,
                totalAfter: 17,
              },
              {
                shiftId: 's3',
                roomLab: 'Lab 303',
                maxSeats: 24,
                assignedBefore: 0,
                assignedNow: 16,
                totalAfter: 16,
              },
            ],
          },
        ],
      })
      .mockResolvedValueOnce({
        sessionId: 'session-1',
        dryRun: false,
        totalEligibleStudents: 50,
        previouslyAllocatedCount: 0,
        newlyAllocatedCount: 50,
        shiftGroups: [],
      })

    render(
      <AutoAllocateWizardDialog
        open
        sessionId="session-1"
        shifts={[
          {
            id: 's1',
            sessionId: 'session-1',
            shiftName: 'Ca 1',
            roomLab: 'Lab 301',
            startTime: '2026-11-20T08:00:00.000Z',
            endTime: '2026-11-20T09:30:00.000Z',
            proctorUserId: 'p1',
            examinerUserId: 'e1',
            proctorName: 'Giám thị 1',
            examinerName: 'Giám khảo 1',
            status: 'scheduled',
            maxSeats: 24,
            assignedTicketsCount: 0,
            isExamPublished: false,
          },
        ]}
        estimate={null}
        onClose={vi.fn()}
        onAllocated={vi.fn()}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: /Chạy thử \(Dry-Run\)/ }))
    expect(await screen.findByText(/Cân bằng đều ca cuối \(chênh ≤ 1 SV\)/)).toBeInTheDocument()
    expect(screen.getByText('Lab 301')).toBeInTheDocument()
    expect(screen.getByText('Lab 303')).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: /Xác nhận phân bổ chính thức/ }))
    expect(await screen.findByRole('alertdialog')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận chia phòng' }))

    await waitFor(() =>
      expect(officialExamService.autoAllocateStudents).toHaveBeenLastCalledWith(
        'session-1',
        expect.objectContaining({ dryRun: false }),
      ),
    )
  })

  it('FE-3: EO Shift Monitor merges realtime SignalR anti-cheat alerts and AuditEvidencePage uses Examiner Shift Picker dropdown', async () => {
    vi.mocked(proctorService.getShiftAttendance).mockResolvedValue({
      shiftId: 'shift-1',
      shiftName: 'Ca 1',
      roomLab: 'Lab 301',
      shiftStatus: 'in_progress',
      startTime: '2026-11-20T08:00:00.000Z',
      endTime: '2026-11-20T09:30:00.000Z',
      totalSeats: 24,
      assignedTicketsCount: 1,
      checkedInCount: 1,
      inProgressCount: 1,
      submittedCount: 0,
      suspendedCount: 0,
      seats: [
        {
          seatNumber: 1,
          isOccupied: true,
          ticketId: 'ticket-1',
          studentId: 'stu-1',
          studentCode: 'SE170001',
          fullName: 'Nguyễn Văn A',
          ipAddress: null,
          deviceId: 'BYOD-MAC-001',
          violationCount: 1,
          setCode: 'SET-01',
          status: 'IN_PROGRESS',
          isCheckedIn: true,
          isSuspended: false,
          suspensionReason: null,
          checkedInAt: '2026-11-20T08:00:00.000Z',
          submittedAt: null,
        },
      ],
    })
    vi.mocked(officialExamService.getAuditEvidence).mockResolvedValue([])
    vi.mocked(officialExamService.getAntiCheatLogs).mockResolvedValue([])
    vi.mocked(useAntiCheatHub).mockReturnValue({
      status: 'connected',
      isConnected: true,
      alerts: [
        {
          id: 'evt-live-1',
          clientEventId: 'client-live-1',
          ticketId: 'ticket-1',
          shiftId: 'shift-1',
          studentId: 'stu-1',
          studentCode: 'SE170001',
          studentName: 'Nguyễn Văn A',
          eventType: 'WINDOW_BLUR',
          severity: 'CRITICAL',
          details: 'Rời cửa sổ thi lần 2',
          violationCount: 2,
          occurredAt: '2026-11-20T08:15:00.000Z',
        },
      ],
      latestAlert: null,
      reconnectVersion: 0,
      lastError: null,
      clearAlerts: vi.fn(),
    })

    const { unmount } = render(
      <MemoryRouter initialEntries={['/examination-officer/monitor/shift-1']}>
        <Routes>
          <Route
            path="/examination-officer/monitor/:shiftId"
            element={<ExaminationOfficerShiftMonitorPage />}
          />
        </Routes>
      </MemoryRouter>,
    )

    expect(await screen.findByText('Rời cửa sổ thi lần 2')).toBeInTheDocument()
    expect(await screen.findByText('BYOD-MAC-001')).toBeInTheDocument()
    expect(screen.getByText('VP: 2')).toBeInTheDocument()
    unmount()

    vi.mocked(officialExamService.getShifts).mockResolvedValue([
      {
        id: '11111111-1111-4111-8111-111111111111',
        sessionId: 'session-1',
        shiftName: 'Ca sáng Lab 301',
        roomLab: 'Lab 301',
        startTime: '2026-11-20T08:00:00.000Z',
        endTime: '2026-11-20T09:30:00.000Z',
        proctorUserId: 'p1',
        examinerUserId: 'examiner-1',
        proctorName: 'Giám thị 1',
        examinerName: 'Giám khảo A',
        status: 'completed',
      },
    ])

    render(
      <MemoryRouter initialEntries={['/lecturer/audit']}>
        <AuditEvidencePage />
      </MemoryRouter>,
    )

    const picker = await screen.findByLabelText('Chọn ca thi hậu kiểm')
    expect(screen.queryByPlaceholderText(/Nhập UUID ca thi/)).not.toBeInTheDocument()
    expect(await screen.findByRole('option', { name: /Ca sáng Lab 301/ })).toBeInTheDocument()
    fireEvent.change(picker, { target: { value: '11111111-1111-4111-8111-111111111111' } })

    await waitFor(() =>
      expect(officialExamService.getAuditEvidence).toHaveBeenCalledWith(
        '11111111-1111-4111-8111-111111111111',
      ),
    )
  })

  it('EO-03 & EO-04: allows unchecking all shifts in AutoAllocateWizardDialog and supports >12 rooms via numeric input in BatchShiftCreatorModal', () => {
    const { unmount } = render(
      <AutoAllocateWizardDialog
        open
        sessionId="session-1"
        shifts={[
          {
            id: 's1',
            sessionId: 'session-1',
            shiftName: 'Ca 1',
            roomLab: 'Lab 301',
            startTime: '2026-11-20T08:00:00.000Z',
            endTime: '2026-11-20T09:30:00.000Z',
            proctorUserId: 'p1',
            examinerUserId: 'e1',
            proctorName: 'Giám thị 1',
            examinerName: 'Giám khảo 1',
            status: 'scheduled',
            maxSeats: 24,
            assignedTicketsCount: 0,
            isExamPublished: false,
          },
        ]}
        estimate={null}
        onClose={vi.fn()}
        onAllocated={vi.fn()}
      />,
    )

    const shiftCheckbox = screen.getByRole('checkbox', { name: /Ca 1 · Lab 301/ })
    expect(shiftCheckbox).toBeChecked()
    fireEvent.click(shiftCheckbox)
    expect(shiftCheckbox).not.toBeChecked()
    expect(screen.getByText(/Phòng đích tham gia phân bổ \(0\/1\)/)).toBeInTheDocument()
    expect(
      screen.getByText(/Vui lòng chọn ít nhất một phòng thi để chạy thử phân bổ\./),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Chạy thử \(Dry-Run\)/ })).toBeDisabled()
    unmount()

    render(
      <BatchShiftCreatorModal
        open
        sessionId="session-1"
        defaultExamDate="2026-11-20"
        recommendedRooms={15}
        proctors={[{ userId: 'p1', fullName: 'Giám thị 1' }]}
        examiners={[{ userId: 'e1', fullName: 'Giám khảo 1' }]}
        onClose={vi.fn()}
        onCreated={vi.fn()}
      />,
    )

    const roomCountInput = screen.getByLabelText('Số lượng phòng K') as HTMLInputElement
    expect(roomCountInput.value).toBe('15')
    fireEvent.change(roomCountInput, { target: { value: '25' } })
    expect(screen.getByRole('button', { name: /Mở 25 phòng thi/ })).toBeInTheDocument()
  })

  it('EO-08: renders pagination and requests next page when CandidateRosterImportPanel has >100 candidates', async () => {
    vi.mocked(officialExamService.getSessionCandidates)
      .mockResolvedValueOnce({
        sessionId: 'session-1',
        examAttemptType: 'FE',
        totalCount: 150,
        allocatedCount: 0,
        unallocatedCount: 150,
        pageNumber: 1,
        pageSize: 100,
        items: [
          {
            studentId: 'stu-1',
            studentCode: 'SE170001',
            fullName: 'Sinh Viên Page 1',
            email: 'p1@fpt.edu.vn',
            importedAt: '2026-11-20T08:00:00.000Z',
            isAllocated: false,
            ticketId: null,
            shiftId: null,
            roomLab: null,
            seatNumber: null,
            ticketStatus: null,
          },
        ],
      })
      .mockResolvedValueOnce({
        sessionId: 'session-1',
        examAttemptType: 'FE',
        totalCount: 150,
        allocatedCount: 0,
        unallocatedCount: 150,
        pageNumber: 2,
        pageSize: 100,
        items: [
          {
            studentId: 'stu-101',
            studentCode: 'SE170101',
            fullName: 'Sinh Viên Page 2',
            email: 'p2@fpt.edu.vn',
            importedAt: '2026-11-20T08:00:00.000Z',
            isAllocated: false,
            ticketId: null,
            shiftId: null,
            roomLab: null,
            seatNumber: null,
            ticketStatus: null,
          },
        ],
      })

    render(
      <CandidateRosterImportPanel
        sessionId="session-1"
        examAttemptType="FE"
        onRosterChanged={vi.fn()}
      />,
    )

    expect(await screen.findByText('SE170001')).toBeInTheDocument()
    expect(screen.getByText(/Hiển thị 1\/150 thí sinh · Trang 1\/2/)).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Trang 2' }))

    await waitFor(() =>
      expect(officialExamService.getSessionCandidates).toHaveBeenLastCalledWith('session-1', {
        search: undefined,
        pageNumber: 2,
        pageSize: 100,
      }),
    )
    expect(await screen.findByText('SE170101')).toBeInTheDocument()
  })
})
