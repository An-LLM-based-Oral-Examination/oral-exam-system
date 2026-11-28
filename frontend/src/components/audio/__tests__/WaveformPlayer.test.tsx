import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { WaveformPlayer } from '../WaveformPlayer'
import { officialExamService } from '@/services/officialExam.service'

vi.mock('@/services/officialExam.service', () => ({
  officialExamService: {
    getAudioPlaybackUrl: vi.fn(),
  },
}))

describe('WaveformPlayer presigned R2 audio URL refresh', () => {
  const originalFetch = globalThis.fetch

  beforeEach(() => {
    vi.clearAllMocks()
  })

  afterEach(() => {
    cleanup()
    globalThis.fetch = originalFetch
  })

  it('auto-refreshes an expired R2 presigned URL once when waveform fetch returns 403', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(new Response(null, { status: 403 }))
      .mockResolvedValueOnce(new Response(new ArrayBuffer(8), { status: 200 }))
    globalThis.fetch = fetchMock

    vi.mocked(officialExamService.getAudioPlaybackUrl).mockResolvedValue({
      ticketId: 'ticket-1',
      submissionId: 'sub-1',
      downloadUrl: 'https://r2.example.com/fresh-audio.webm?sig=fresh',
      audioHashSha256: 'abc123sha256',
      expiresAt: '2099-01-01T00:15:00Z',
    })

    const { container } = render(
      <WaveformPlayer
        audioUrl="https://r2.example.com/expired-audio.webm?sig=expired"
        hashSha256="abc123sha256"
        studentCode="SE170001"
        seatNumber={3}
        ticketId="ticket-1"
        submissionId="sub-1"
      />,
    )

    await waitFor(() =>
      expect(officialExamService.getAudioPlaybackUrl).toHaveBeenCalledWith('ticket-1', 'sub-1'),
    )
    expect(officialExamService.getAudioPlaybackUrl).toHaveBeenCalledTimes(1)

    await waitFor(() =>
      expect(container.querySelector('audio')).toHaveAttribute(
        'src',
        'https://r2.example.com/fresh-audio.webm?sig=fresh',
      ),
    )
  })

  it('refreshes the R2 presigned URL on <audio> playback error and exposes manual retry when refresh fails', async () => {
    globalThis.fetch = vi
      .fn<typeof fetch>()
      .mockResolvedValue(new Response(new ArrayBuffer(8), { status: 200 }))

    vi.mocked(officialExamService.getAudioPlaybackUrl)
      .mockRejectedValueOnce(new Error('Mất kết nối máy chủ'))
      .mockResolvedValueOnce({
        ticketId: 'ticket-2',
        submissionId: null,
        downloadUrl: 'https://r2.example.com/recovered-audio.webm',
        audioHashSha256: null,
        expiresAt: '2099-01-01T00:15:00Z',
      })

    const { container } = render(
      <WaveformPlayer
        audioUrl="https://r2.example.com/initial-audio.webm"
        hashSha256="hash-2"
        studentCode="SE170002"
        seatNumber={5}
        ticketId="ticket-2"
      />,
    )

    const audio = container.querySelector('audio')
    expect(audio).not.toBeNull()
    fireEvent.error(audio!)

    expect(await screen.findByRole('alert')).toHaveTextContent('Mất kết nối máy chủ')
    const retryButton = screen.getByRole('button', { name: 'Làm mới liên kết âm thanh' })
    fireEvent.click(retryButton)

    await waitFor(() =>
      expect(container.querySelector('audio')).toHaveAttribute(
        'src',
        'https://r2.example.com/recovered-audio.webm',
      ),
    )
    expect(officialExamService.getAudioPlaybackUrl).toHaveBeenCalledTimes(2)
  })
})
