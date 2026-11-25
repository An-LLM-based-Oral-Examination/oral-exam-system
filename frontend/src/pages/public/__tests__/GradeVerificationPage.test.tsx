import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { GradeVerificationPage } from '@/pages/public/GradeVerificationPage'

const VALID_HASH = 'a'.repeat(64)

describe('GradeVerificationPage (Public MF-04 QR PKI Verification)', () => {
  const originalFetch = globalThis.fetch

  beforeEach(() => {
    vi.restoreAllMocks()
  })

  afterEach(() => {
    globalThis.fetch = originalFetch
  })

  it('auto-verifies a 64-char SHA-256 hash from route params and renders sealed scores', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          status: 'VALID',
          university: 'Trường Đại học FPT - Phân hiệu TP.HCM',
          digitalSignatureIssuer: 'FPTU Examination Board PAdES-B-B X.509 & SHA-256 Cryptographic Seal',
          certificateHash: VALID_HASH,
          certificateThumbprint: 'THUMB123456',
          certificateSignedAt: '2026-11-25T08:00:00Z',
          isTampered: false,
          shiftId: 'shift-1',
          shiftName: 'Ca 1 - 402',
          roomLab: '402',
          sessionTitle: 'ENT001 · FA26',
          courseCode: 'ENT001',
          courseName: 'Entrepreneurship',
          semesterCode: 'FA26',
          examDate: '2026-11-25',
          shiftStartTime: '2026-11-25T07:00:00Z',
          shiftEndTime: '2026-11-25T08:00:00Z',
          proctorName: 'Hoàng Đức Anh',
          totalStudents: 1,
          lockedTicketsCount: 1,
          isFullyLocked: true,
          verifiedAtUtc: '2026-11-25T08:10:00Z',
          studentScores: [
            {
              seatNumber: 1,
              studentCode: 'SE990001',
              fullName: 'Nguyễn Văn A',
              finalScore: 8.5,
              isLocked: true,
            },
          ],
        }),
        { status: 200, headers: { 'content-type': 'application/json' } },
      ),
    )

    render(
      <MemoryRouter initialEntries={[`/verify/grades/${VALID_HASH}`]}>
        <Routes>
          <Route path="/verify/grades/:certificateHash" element={<GradeVerificationPage />} />
        </Routes>
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(screen.getByText(/Chứng thư hợp lệ & toàn vẹn 100%/)).toBeInTheDocument()
    })
    expect(screen.getByText(/SE990001/)).toBeInTheDocument()
    expect(screen.getByText('8.50')).toBeInTheDocument()
  })

  it('rejects invalid hash format inline without calling backend API', async () => {
    const fetchSpy = vi.fn()
    globalThis.fetch = fetchSpy
    const user = userEvent.setup()

    render(
      <MemoryRouter initialEntries={['/verify/grades']}>
        <Routes>
          <Route path="/verify/grades" element={<GradeVerificationPage />} />
        </Routes>
      </MemoryRouter>,
    )

    const input = screen.getByLabelText(/Mã băm niêm phong SHA-256/i)
    await user.type(input, 'not-a-valid-hash')
    await user.click(screen.getByRole('button', { name: /Xác thực chứng thư/i }))

    expect(screen.getByRole('alert')).toHaveTextContent(/64 ký tự thập lục phân/)
    expect(fetchSpy).not.toHaveBeenCalled()
  })

  it('renders clear error message on 404 or tampered certificate', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          status: 404,
          title: 'Chứng thư bảng điểm không hợp lệ hoặc không tồn tại.',
          detail: 'Không tìm thấy chứng thư bảng điểm hợp lệ.',
        }),
        { status: 404, headers: { 'content-type': 'application/json' } },
      ),
    )

    render(
      <MemoryRouter initialEntries={[`/verify/grades/${VALID_HASH}`]}>
        <Routes>
          <Route path="/verify/grades/:certificateHash" element={<GradeVerificationPage />} />
        </Routes>
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent(/Không tìm thấy chứng thư bảng điểm hợp lệ/)
    })
  })
})
