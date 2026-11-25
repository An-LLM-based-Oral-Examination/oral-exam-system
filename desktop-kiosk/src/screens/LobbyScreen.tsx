import type { StudentWorkstationTicket } from '../types/mf04.types'
import { formatDateTime } from '../utils/formatters'

interface LobbyScreenProps {
  ticket: StudentWorkstationTicket
  offlineDraftAvailable: boolean
  hasCompleteDraft: boolean
  onStart: () => Promise<void>
  onResumeSubmit: () => Promise<void>
}

export function LobbyScreen({
  ticket,
  offlineDraftAvailable,
  hasCompleteDraft,
  onStart,
  onResumeSubmit,
}: LobbyScreenProps) {
  return (
    <section aria-labelledby="lobby-heading">
      <div className="ticket-hero">
        <div className="ticket-hero-copy">
          <span className="ticket-kicker">Phòng chờ thí sinh</span>
          <h2 id="lobby-heading">{ticket.sessionTitle}</h2>
          <p>
            {ticket.shiftName} · {formatDateTime(ticket.startTime)} – {formatDateTime(ticket.endTime)}
          </p>
        </div>
        <div className="seat-badge" aria-label={`Phòng ${ticket.roomLab}, số máy ${ticket.seatNumber}`}>
          <span>Số máy</span>
          <strong>{ticket.seatNumber}</strong>
          <span>Phòng {ticket.roomLab}</span>
        </div>
      </div>

      <div className="ticket-body">
        <div className="meta-grid">
          <div className="meta-tile">
            <span>Mã bộ đề</span>
            <strong>{ticket.examSetCode ?? 'Đã phân bổ'}</strong>
          </div>
          <div className="meta-tile">
            <span>Số lượng câu hỏi</span>
            <strong>{ticket.questions.length} câu hỏi</strong>
          </div>
          <div className="meta-tile">
            <span>Bản sao lưu cục bộ</span>
            <strong>{offlineDraftAvailable ? 'Đã khôi phục bản nháp' : 'Sẵn sàng lưu an toàn'}</strong>
          </div>
        </div>

        <div className="rules-note">
          <strong>Lưu ý phòng thi:</strong> Không chuyển đổi cửa sổ hoặc thoát toàn màn hình trong lúc làm
          bài. Sau khi nộp bài, hệ thống tự niêm phong âm thanh và không hiển thị điểm tại máy thi.
        </div>

        <div className="action-bar">
          <button
            className="btn btn-primary"
            type="button"
            onClick={hasCompleteDraft ? onResumeSubmit : onStart}
          >
            {hasCompleteDraft ? 'Gửi lại bản nháp đã lưu →' : 'Bắt đầu làm bài →'}
          </button>
        </div>
      </div>
    </section>
  )
}
