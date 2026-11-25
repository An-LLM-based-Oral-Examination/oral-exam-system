interface WaitingTicketScreenProps {
  studentName: string
  busy: boolean
  onRetry: () => Promise<void>
}

export function WaitingTicketScreen({ studentName, busy, onRetry }: WaitingTicketScreenProps) {
  return (
    <section className="card state-card" aria-labelledby="waiting-heading">
      <div className="state-icon state-icon-warning" aria-hidden="true">
        ⏳
      </div>
      <div>
        <span className="badge badge-warning">Đang chờ cấp vé thi</span>
      </div>
      <h2 id="waiting-heading">Chưa có vé thi khả dụng</h2>
      <p>
        Xin chào <strong>{studentName || 'Sinh viên'}</strong>, tài khoản của bạn đã đăng nhập thành công
        nhưng hiện chưa có vé thi được phân bổ hoặc ca thi chưa đến giờ mở đề.
      </p>
      <div className="state-actions">
        <button className="btn btn-primary" type="button" onClick={onRetry} disabled={busy}>
          {busy ? 'Đang kiểm tra…' : 'Kiểm tra lại vé thi'}
        </button>
      </div>
    </section>
  )
}
