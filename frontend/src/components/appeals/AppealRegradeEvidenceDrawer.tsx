import { useCallback, useEffect, useState } from 'react'
import {
  FileAudio2,
  LoaderCircle,
  MessageSquareQuote,
  RefreshCw,
  ShieldCheck,
  Sparkles,
  X,
} from 'lucide-react'
import { appealsService } from '@/services/appeals.service'
import type { AuditEvidenceItem } from '@/types/officialExam.types'
import { WaveformPlayer } from '@/components/audio/WaveformPlayer'
import { ds } from '@/design/system'
import { userMessage } from '@/lib/userMessage'

interface AppealRegradeEvidenceDrawerProps {
  appealId: string
  studentCode?: string
  studentName?: string
  open: boolean
  onClose: () => void
}

export function AppealRegradeEvidenceDrawer({
  appealId,
  studentCode,
  studentName,
  open,
  onClose,
}: AppealRegradeEvidenceDrawerProps) {
  const [evidence, setEvidence] = useState<AuditEvidenceItem | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  const loadEvidence = useCallback(async () => {
    if (!open || !appealId || typeof appealsService.getEvidence !== 'function') return
    setLoading(true)
    setError('')
    try {
      const item = await appealsService.getEvidence(appealId)
      setEvidence(item)
    } catch (loadError) {
      setEvidence(null)
      setError(
        userMessage(
          loadError,
          'Không thể tải bằng chứng âm thanh và lập luận AI cho đơn phúc khảo này.',
        ),
      )
    } finally {
      setLoading(false)
    }
  }, [appealId, open])

  useEffect(() => {
    if (!open) return
    const timer = window.setTimeout(() => {
      void loadEvidence()
    }, 0)
    return () => window.clearTimeout(timer)
  }, [loadEvidence, open])

  useEffect(() => {
    if (!open) return
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose()
    }
    document.addEventListener('keydown', onKeyDown)
    return () => document.removeEventListener('keydown', onKeyDown)
  }, [onClose, open])

  if (!open) return null

  const followUps = evidence?.followUpItems ?? []

  return (
    <div
      className="fixed inset-0 z-50 flex justify-end bg-[#0F2854]/35 backdrop-blur-[1px]"
      role="presentation"
      onClick={(event) => {
        if (event.target === event.currentTarget) onClose()
      }}
    >
      <aside
        role="dialog"
        aria-modal="true"
        aria-label="Hồ sơ bằng chứng phúc khảo"
        className="flex h-full w-full max-w-2xl flex-col border-l border-[#d9e8f2] bg-white shadow-[-16px_0_40px_rgba(15,40,84,0.18)]"
      >
        <header className="flex items-start justify-between gap-3 border-b border-[#d9e8f2] bg-[#F4FBFD] px-5 py-4">
          <div className="min-w-0">
            <p className="flex items-center gap-1.5 text-[11px] font-bold tracking-[0.08em] text-[#1C4D8D] uppercase">
              <FileAudio2 className="size-3.5" aria-hidden="true" />
              Bằng chứng đơn vé (Single-Ticket BOLA)
            </p>
            <h2 className="mt-1 truncate text-base font-black text-[#0F2854]">
              {evidence?.studentCode || studentCode || 'Thí sinh'}
              {evidence?.fullName || studentName
                ? ` · ${evidence?.fullName || studentName}`
                : ''}
            </h2>
            {evidence ? (
              <p className="mt-0.5 text-xs text-[#5b6b84]">
                Ghế #{String(evidence.seatNumber).padStart(2, '0')} · Trạng thái: {evidence.status}
              </p>
            ) : null}
          </div>
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={() => void loadEvidence()}
              disabled={loading}
              className={ds.outlineButton}
              aria-label="Tải lại bằng chứng phúc khảo"
            >
              <RefreshCw className={loading ? 'size-4 animate-spin' : 'size-4'} />
            </button>
            <button
              type="button"
              onClick={onClose}
              className={ds.outlineButton}
              aria-label="Đóng ngăn bằng chứng"
            >
              <X className="size-4" /> Đóng
            </button>
          </div>
        </header>

        <div className="flex-1 space-y-4 overflow-y-auto p-5">
          {loading ? (
            <div className="flex min-h-[260px] flex-col items-center justify-center gap-2 text-xs font-semibold text-[#5b6b84]">
              <LoaderCircle className="size-7 animate-spin text-[#1C4D8D]" />
              <span>Đang tải âm thanh Cloudflare R2, SHA-256, transcript và AI CoT…</span>
            </div>
          ) : error ? (
            <div
              role="alert"
              className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-4 py-3 text-xs font-semibold text-[#a43d4b]"
            >
              <span>{error}</span>
              <button
                type="button"
                onClick={() => void loadEvidence()}
                className={ds.outlineButton}
              >
                <RefreshCw className="size-3.5" /> Thử lại
              </button>
            </div>
          ) : evidence ? (
            <>
              <div className="grid gap-3 sm:grid-cols-3">
                <div className="rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] p-3">
                  <p className="text-[11px] font-bold text-[#687a91] uppercase">Điểm AI</p>
                  <strong className="mt-1 block font-mono text-xl font-black text-[#0F2854]">
                    {evidence.aiScore === null ? '—' : evidence.aiScore.toFixed(1)}
                  </strong>
                </div>
                <div className="rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] p-3">
                  <p className="text-[11px] font-bold text-[#687a91] uppercase">Điểm hiện tại</p>
                  <strong className="mt-1 block font-mono text-xl font-black text-[#1C4D8D]">
                    {evidence.finalScore === null ? '—' : evidence.finalScore.toFixed(1)}
                  </strong>
                </div>
                <div className="rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] p-3">
                  <p className="text-[11px] font-bold text-[#687a91] uppercase">Độ tin cậy AI</p>
                  <strong className="mt-1 block font-mono text-xl font-black text-[#168367]">
                    {evidence.confidenceScore === null
                      ? '—'
                      : `${Math.round(evidence.confidenceScore * 100)}%`}
                  </strong>
                </div>
              </div>

              <WaveformPlayer
                audioUrl={evidence.audioR2Url}
                hashSha256={evidence.audioHashSha256}
                studentCode={evidence.studentCode}
                seatNumber={evidence.seatNumber}
                ticketId={evidence.ticketId}
              />

              <div className="rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] p-3 text-xs text-[#52657d]">
                <p className="flex items-center gap-1.5 font-bold text-[#0F2854]">
                  <ShieldCheck className="size-4 text-[#168367]" aria-hidden="true" />
                  Mã băm toàn vẹn âm thanh (SHA-256)
                </p>
                <p className="mt-1 break-all font-mono text-[11px] text-[#245997]">
                  {evidence.audioHashSha256 || 'Chưa có mã băm SHA-256.'}
                </p>
              </div>

              {evidence.questionContent ? (
                <section className="rounded-xl border border-[#d9e8f2] bg-white p-4 text-xs text-[#52657d]">
                  <p className="font-bold text-[#0F2854]">
                    Câu hỏi chính{evidence.questionTitle ? `: ${evidence.questionTitle}` : ''}
                  </p>
                  <p className="mt-1.5 whitespace-pre-wrap leading-relaxed">
                    {evidence.questionContent}
                  </p>
                </section>
              ) : null}

              <div className="grid gap-3 sm:grid-cols-2">
                <section className="rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] p-4 text-xs text-[#52657d]">
                  <p className="font-bold text-[#0F2854]">Transcript Whisper (Bài làm gốc)</p>
                  <p className="mt-2 whitespace-pre-wrap leading-relaxed">
                    {evidence.transcriptWhisper || 'Chưa có transcript.'}
                  </p>
                </section>

                <section className="rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] p-4 text-xs text-[#52657d]">
                  <p className="flex items-center gap-1.5 font-bold text-[#0F2854]">
                    <Sparkles className="size-3.5 text-[#1C4D8D]" aria-hidden="true" />
                    Chuỗi lập luận AI (CoT)
                  </p>
                  <p className="mt-2 whitespace-pre-wrap leading-relaxed">
                    {evidence.cotTrace || 'Chưa có chuỗi lập luận AI CoT.'}
                  </p>
                </section>
              </div>

              <section className="space-y-3 rounded-xl border border-[#82aedb]/55 bg-white p-4">
                <div className="flex items-center justify-between gap-2">
                  <h3 className="flex items-center gap-1.5 text-xs font-bold tracking-[0.06em] text-[#1C4D8D] uppercase">
                    <MessageSquareQuote className="size-4" aria-hidden="true" />
                    Các lượt câu hỏi phụ (Follow-up · {followUps.length})
                  </h3>
                </div>

                {followUps.length === 0 ? (
                  <p className="text-xs text-[#687a91]">
                    Bài thi này không có câu hỏi phụ (Follow-up) đi kèm.
                  </p>
                ) : (
                  <div className="space-y-3">
                    {followUps.map((item) => (
                      <article
                        key={item.submissionId}
                        className="space-y-2.5 rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] p-3.5 text-xs text-[#52657d]"
                      >
                        <div className="flex flex-wrap items-center justify-between gap-2">
                          <strong className="text-[#0F2854]">
                            Câu phụ #{item.followUpIndex}
                          </strong>
                          <span className={ds.infoChip}>
                            AI: {item.aiScore === null ? '—' : item.aiScore.toFixed(1)} · Final:{' '}
                            {item.finalScore === null ? '—' : item.finalScore.toFixed(1)}
                          </span>
                        </div>
                        {item.followUpPrompt ? (
                          <p className="font-semibold text-[#1C4D8D]">
                            Câu hỏi phụ: {item.followUpPrompt}
                          </p>
                        ) : null}
                        <p className="whitespace-pre-wrap leading-relaxed">
                          <strong>Transcript:</strong>{' '}
                          {item.transcriptWhisper || 'Chưa có transcript.'}
                        </p>
                        {item.cotTrace ? (
                          <p className="whitespace-pre-wrap leading-relaxed">
                            <strong>AI CoT:</strong> {item.cotTrace}
                          </p>
                        ) : null}
                        {item.audioR2Url ? (
                          <WaveformPlayer
                            audioUrl={item.audioR2Url}
                            hashSha256={item.audioHashSha256}
                            studentCode={`${evidence.studentCode}-FU${item.followUpIndex}`}
                            seatNumber={evidence.seatNumber}
                            ticketId={evidence.ticketId}
                            submissionId={item.submissionId}
                          />
                        ) : null}
                      </article>
                    ))}
                  </div>
                )}
              </section>
            </>
          ) : (
            <p className="text-xs text-[#687a91]">
              Chưa có dữ liệu bằng chứng cho đơn phúc khảo này.
            </p>
          )}
        </div>
      </aside>
    </div>
  )
}
