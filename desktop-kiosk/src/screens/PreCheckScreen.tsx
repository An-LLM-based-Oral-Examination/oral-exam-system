interface PreCheckScreenProps {
  microphoneReady: boolean
  micTestSeconds: number
  micLevel: number
  displayCount: number
  isKioskMode: boolean
  altTabTestMode: boolean
  securitySnapshot: KioskSecuritySnapshot | null
  scanningEnvironment: boolean
  busy: boolean
  onTest: () => Promise<void>
  onRescanEnvironment: () => Promise<void>
  onCheckIn: () => Promise<void>
}

export function PreCheckScreen({
  microphoneReady,
  micTestSeconds,
  micLevel,
  displayCount,
  isKioskMode,
  altTabTestMode,
  securitySnapshot,
  scanningEnvironment,
  busy,
  onTest,
  onRescanEnvironment,
  onCheckIn,
}: PreCheckScreenProps) {
  const clampedDb = Math.max(0, Math.min(100, micLevel))
  const timerPercent = Math.max(0, Math.min(100, Math.round((micTestSeconds / 30) * 100)))
  const isPassingLevel = clampedDb >= 60

  const effectiveDisplayCount = Math.max(displayCount, securitySnapshot?.displayCount ?? 1)
  const agent = securitySnapshot?.windowsAgent ?? null
  const envIssues: string[] = []
  if (effectiveDisplayCount > 1) {
    envIssues.push(`Đang kết nối ${effectiveDisplayCount} màn hình (yêu cầu chỉ dùng 1 màn hình).`)
  }
  if (agent?.remoteControlDetected && agent.remoteControlProcesses.length > 0) {
    envIssues.push(`Đang chạy phần mềm điều khiển từ xa: ${agent.remoteControlProcesses.join(', ')}.`)
  }
  if (agent?.vmDetected && agent.vmIndicators.length > 0) {
    envIssues.push(`Phát hiện môi trường máy ảo: ${agent.vmIndicators.join(', ')}.`)
  }
  if (agent && agent.disallowedProcesses.length > 0) {
    envIssues.push(`Đang mở ứng dụng không được phép: ${agent.disallowedProcesses.join(', ')}.`)
  }

  const enforceStrictEnv = isKioskMode && !altTabTestMode
  const blockedByEnvironment = enforceStrictEnv && envIssues.length > 0

  return (
    <section className="card" aria-labelledby="precheck-heading">
      <div className="screen-header">
        <div>
          <h2 className="screen-title" id="precheck-heading">
            Kiểm tra thiết bị trước giờ thi
          </h2>
          <p className="screen-subtitle">
            Hãy nói rõ ràng vào microphone trong 30 giây để hệ thống xác nhận âm lượng đạt chuẩn.
          </p>
        </div>
        <span className={`badge ${microphoneReady ? 'badge-success' : 'badge-warning'}`}>
          {microphoneReady ? 'Micro đạt chuẩn' : 'Cần kiểm tra micro'}
        </span>
      </div>

      <div className="mic-panel">
        <div className="mic-header-row">
          <div className="mic-metric-group">
            <span className={`mic-db-value ${isPassingLevel ? 'is-passing' : ''}`}>{clampedDb}</span>
            <span className="mic-db-unit">dB (yêu cầu ≥ 60 dB)</span>
          </div>
          <button className="btn btn-secondary" type="button" onClick={onTest} disabled={busy}>
            {busy ? `Đang đo (${micTestSeconds}/30s)…` : microphoneReady ? 'Kiểm tra lại micro' : 'Bắt đầu kiểm tra (30s)'}
          </button>
        </div>

        <div className="meter-block">
          <div className="meter-labels">
            <span>Cường độ âm thanh trực tiếp</span>
            <span>Mốc đạt: 60 dB</span>
          </div>
          <div className="meter-track" role="progressbar" aria-valuenow={clampedDb} aria-valuemin={0} aria-valuemax={100}>
            <div className={`meter-fill ${isPassingLevel ? 'is-passing' : ''}`} style={{ width: `${clampedDb}%` }} />
            <div className="meter-threshold" title="Ngưỡng tối thiểu 60 dB" />
          </div>
        </div>

        <div className="meter-block">
          <div className="meter-labels">
            <span>Tiến độ đo độ ổn định</span>
            <span>{micTestSeconds} / 30 giây</span>
          </div>
          <div className="meter-track" role="progressbar" aria-valuenow={micTestSeconds} aria-valuemin={0} aria-valuemax={30}>
            <div className="meter-fill is-timer" style={{ width: `${timerPercent}%` }} />
          </div>
        </div>
      </div>

      <div className="env-strip" aria-label="Trạng thái môi trường thi">
        <span className="env-item">
          <span className="env-check">✓</span> Kết nối máy chủ sẵn sàng
        </span>
        <span className="env-item">
          <span className={isKioskMode ? 'env-check' : 'env-warn'}>{isKioskMode ? '✓' : '!'}</span>
          {isKioskMode ? 'Bảo vệ cửa sổ Kiosk đang bật' : 'Đang chạy chế độ cửa sổ thử nghiệm'}
        </span>
        <span className="env-item">
          <span className={effectiveDisplayCount <= 1 ? 'env-check' : 'env-warn'}>
            {effectiveDisplayCount <= 1 ? '✓' : '!'}
          </span>
          {effectiveDisplayCount <= 1 ? '1 màn hình (Hợp lệ)' : `Phát hiện ${effectiveDisplayCount} màn hình`}
        </span>
        <span className="env-item">
          <span className={envIssues.length === 0 ? 'env-check' : 'env-warn'}>
            {envIssues.length === 0 ? '✓' : '!'}
          </span>
          {agent
            ? envIssues.length === 0
              ? 'Môi trường Windows sạch'
              : `Cảnh báo an ninh (${envIssues.length})`
            : 'Giám sát cấp ứng dụng'}
        </span>
      </div>

      {envIssues.length > 0 ? (
        <div className="env-alert" role="alert">
          <div className="env-alert-head">
            <strong>
              {enforceStrictEnv
                ? 'Vui lòng xử lý các cảnh báo môi trường trước khi điểm danh:'
                : 'Cảnh báo môi trường (chế độ thử nghiệm vẫn cho phép tiếp tục):'}
            </strong>
            <button
              className="btn btn-ghost"
              type="button"
              onClick={onRescanEnvironment}
              disabled={scanningEnvironment || busy}
            >
              {scanningEnvironment ? 'Đang quét…' : 'Quét lại môi trường'}
            </button>
          </div>
          <ul className="env-alert-list">
            {envIssues.map((issue) => (
              <li key={issue}>{issue}</li>
            ))}
          </ul>
        </div>
      ) : null}

      <div className="action-bar">
        <button
          className="btn btn-primary"
          type="button"
          disabled={!microphoneReady || busy || blockedByEnvironment}
          onClick={onCheckIn}
        >
          Điểm danh và vào phòng chờ →
        </button>
      </div>
    </section>
  )
}
