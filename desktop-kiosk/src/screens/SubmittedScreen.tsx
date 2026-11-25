interface SubmittedScreenProps {
  isExitPending: boolean
  onExit: () => Promise<void>
}

export function SubmittedScreen({ isExitPending, onExit }: SubmittedScreenProps) {
  return (
    <section className="card state-card" aria-labelledby="submitted-heading">
      <div className="state-icon state-icon-success" aria-hidden="true">
        ✓
      </div>
      <div>
        <span className="badge badge-success">Đã nộp bài thành công</span>
      </div>
      <h2 id="submitted-heading">Bài làm đã được lưu an toàn</h2>
      <p>
        Hệ thống đã xác nhận trạng thái SUBMITTED và niêm phong tệp âm thanh. Kiosk không hiển thị điểm
        số và không mở khiếu nại tại phòng thi.
      </p>
      <div className="state-actions">
        <button className="btn btn-primary" type="button" onClick={onExit} disabled={isExitPending}>
          {isExitPending ? 'Đang đóng ứng dụng…' : 'Kết thúc phiên và đóng ứng dụng'}
        </button>
      </div>
    </section>
  )
}
