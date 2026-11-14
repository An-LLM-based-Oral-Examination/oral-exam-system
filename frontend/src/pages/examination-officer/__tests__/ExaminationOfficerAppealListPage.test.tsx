import { MemoryRouter } from 'react-router-dom'
import { render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ExaminationOfficerAppealListPage } from '../ExaminationOfficerAppealListPage'
import { appealsService } from '@/services/appeals.service'

vi.mock('@/services/appeals.service', () => ({
  appealsService: {
    list: vi.fn(),
  },
}))

const list = vi.mocked(appealsService.list)

describe('ExaminationOfficerAppealListPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    list.mockResolvedValue([
      {
        id: 'appeal-1',
        ticketId: 'ticket-1',
        studentId: 'student-1',
        studentCode: 'SE170001',
        studentName: 'Le Vu Hoang',
        sessionId: 'session-1',
        sessionTitle: 'PRN231 Oral Examination',
        submissionId: null,
        reason: 'Please review the recorded answer.',
        status: 'PENDING',
        assignedTo: undefined,
        assignedToName: '',
        reviewedBy: null,
        reviewedByName: null,
        decision: null,
        originalScore: 7.5,
        proposedScore: null,
        reviewNotes: null,
        createdAt: '2026-11-14T01:00:00.000Z',
        updatedAt: '2026-11-14T01:00:00.000Z',
        resolvedAt: null,
      },
    ])
  })

  it('renders backend appeals and keeps the officer detail link', async () => {
    render(
      <MemoryRouter>
        <ExaminationOfficerAppealListPage />
      </MemoryRouter>,
    )

    await waitFor(() => expect(screen.getByText('SE170001')).toBeInTheDocument())
    expect(screen.getByText('PRN231 Oral Examination')).toBeInTheDocument()
    expect(screen.getAllByText('PENDING')).toHaveLength(2)
    expect(screen.getByRole('link', { name: /M.*ở/ })).toHaveAttribute(
      'href',
      '/examination-officer/appeals/appeal-1',
    )
  })

  it('shows the API error without replacing the page shell', async () => {
    list.mockRejectedValueOnce({ detail: 'Appeals service unavailable.' })
    render(
      <MemoryRouter>
        <ExaminationOfficerAppealListPage />
      </MemoryRouter>,
    )

    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('Appeals service unavailable.'))
    expect(screen.queryByText('SE170001')).not.toBeInTheDocument()
  })
})
