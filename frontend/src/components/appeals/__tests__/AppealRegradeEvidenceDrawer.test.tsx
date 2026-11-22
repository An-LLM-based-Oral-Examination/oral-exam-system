import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AppealRegradeEvidenceDrawer } from '../AppealRegradeEvidenceDrawer'
import { appealsService } from '@/services/appeals.service'

vi.mock('@/services/appeals.service', () => ({
  appealsService: {
    getEvidence: vi.fn(),
  },
}))

describe('AppealRegradeEvidenceDrawer (Single-Ticket BOLA Evidence + Follow-ups)', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('loads single-ticket evidence via GET /appeals/:id/evidence and renders SHA-256, transcript, CoT, and followUpItems', async () => {
    vi.mocked(appealsService.getEvidence).mockResolvedValue({
      ticketId: 'ticket-1',
      studentCode: 'SE170001',
      fullName: 'Nguyễn Văn A',
      seatNumber: 4,
      status: 'PUBLISHED',
      audioR2Url: 'https://r2.example.com/04_SE170001.webm',
      audioHashSha256: 'a'.repeat(64),
      transcriptWhisper: 'Thí sinh trình bày chính xác về Dependency Inversion Principle.',
      aiScore: 6.5,
      confidenceScore: 0.82,
      isSuspicious: false,
      suspiciousReason: null,
      cotTrace: 'Ý chính đúng, cần kiểm tra thêm ví dụ thực tế ở lượt câu phụ.',
      finalScore: 7.0,
      isLocked: true,
      questionTitle: 'SOLID Principles',
      questionContent: 'Giải thích Dependency Inversion Principle trong Clean Architecture.',
      followUpItems: [
        {
          submissionId: 'sub-fu-1',
          followUpIndex: 1,
          followUpPrompt: 'Hãy nêu ví dụ cụ thể về interface repository trong tầng Application.',
          transcriptWhisper: 'Tầng Application định nghĩa IOrderRepository, Infrastructure cài đặt.',
          audioR2Url: 'https://r2.example.com/04_SE170001_Q01_FU1.webm',
          audioHashSha256: 'b'.repeat(64),
          aiScore: 8.5,
          finalScore: 8.5,
          cotTrace: 'Ví dụ chuẩn xác theo Clean Architecture.',
        },
      ],
    })

    const onClose = vi.fn()
    render(
      <AppealRegradeEvidenceDrawer
        appealId="appeal-1"
        studentCode="SE170001"
        studentName="Nguyễn Văn A"
        open
        onClose={onClose}
      />,
    )

    await waitFor(() => expect(appealsService.getEvidence).toHaveBeenCalledWith('appeal-1'))
    expect(
      await screen.findByText(/Thí sinh trình bày chính xác về Dependency Inversion Principle/),
    ).toBeInTheDocument()
    expect(screen.getAllByText('a'.repeat(64)).length).toBeGreaterThan(0)
    expect(screen.getByText(/Câu phụ #1/)).toBeInTheDocument()
    expect(
      screen.getByText(/Hãy nêu ví dụ cụ thể về interface repository trong tầng Application/),
    ).toBeInTheDocument()
    expect(
      screen.getByText(/Tầng Application định nghĩa IOrderRepository/),
    ).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Đóng ngăn bằng chứng' }))
    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('shows inline error with retry when loading single-ticket evidence fails', async () => {
    vi.mocked(appealsService.getEvidence).mockRejectedValueOnce({
      status: 403,
      detail: 'Bạn không có quyền xem bằng chứng của đơn phúc khảo này.',
    })

    render(<AppealRegradeEvidenceDrawer appealId="appeal-403" open onClose={vi.fn()} />)

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Bạn không có quyền xem bằng chứng của đơn phúc khảo này.',
    )
    expect(screen.getByRole('button', { name: /Thử lại/ })).toBeInTheDocument()
  })
})
