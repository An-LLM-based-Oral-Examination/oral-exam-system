import type { BlockReason } from '../types/mf04.types'

interface BlockedScreenProps {
  blurCount: number
  reason: BlockReason
  detailMessage?: string
  checkingStatus: boolean
  onCheckStatus: () => Promise<void>
}

export function BlockedScreen({
  blurCount,
  reason,
  detailMessage,
  checkingStatus,
  onCheckStatus,
}: BlockedScreenProps) {
  const badgeText =
    reason === 'BYOD_CONFLICT'
      ? 'Xung đột thiết bị BYOD'
      : reason === 'SERVER_LOCKED'
        ? 'Phiên thi đã được khóa từ hệ thống'
        : `Đình chỉ an ninh · ${blurCount} lần mất tiêu điểm`

  const heading =
    reason === 'BYOD_CONFLICT'
      ? 'Thiết bị không khớp phiên thi'
      : reason === 'SERVER_LOCKED'
        ? 'Phiên thi đã tạm dừng'
        : 'Phiên thi đã bị tạm khóa'

  return (
    <section className="card state-card" aria-labelledby="blocked-heading">
      <div className="state-icon state-icon-danger" aria-hidden="true">
        !
      </div>
      <div>
        <span className="badge badge-danger">{badgeText}</span>
      </div>
      <h2 id="blocked-heading">{heading}</h2>
      <p>
        {detailMessage ||
          'Phiên thi đã bị khóa ở kiosk do vi phạm anti-cheat. Không thể tiếp tục hoặc tự ý nộp bài từ màn hình này. Vui lòng giữ nguyên trạng thái máy và báo ngay cho Giám thị phòng thi.'}
      </p>
      <div className="state-actions">
        <button className="btn btn-secondary" type="button" onClick={onCheckStatus} disabled={checkingStatus}>
          {checkingStatus ? 'Đang kiểm tra trạng thái…' : 'Kiểm tra lại trạng thái từ máy chủ'}
        </button>
      </div>
    </section>
  )
}
