import { useEffect } from 'react'
import type { ActiveFollowUpState, StudentWorkstationTicket } from '../types/mf04.types'
import { formatDuration } from '../utils/formatters'
import { evaluateTranscriptEdit } from '../utils/transcript-diff'

interface ExamScreenProps {
  ticket: StudentWorkstationTicket
  question: StudentWorkstationTicket['questions'][number]
  questionIndex: number
  completedMainCount: number
  activeFollowUp: ActiveFollowUpState | null
  generatingFollowUp: boolean
  initialTranscript: string
  transcript: string
  recording: boolean
  recordingElapsedSeconds: number
  transcribing: boolean
  busy: boolean
  blurCount: number
  remainingSeconds: number
  transcriptReviewPending: boolean
  transcriptBufferRemaining: number
  onToggleRecording: () => void
  onTranscript: (value: string) => void
  onRestoreOriginalTranscript: () => void
  onComplete: () => Promise<void>
  onSubmit: (allowIncomplete?: boolean) => Promise<void>
}

const DIFFICULTY_LABELS: Record<string, { label: string; toneClass: string }> = {
  easy: { label: 'Dễ', toneClass: 'is-easy' },
  medium: { label: 'Trung bình', toneClass: 'is-medium' },
  hard: { label: 'Khó', toneClass: 'is-hard' },
}

function formatRecordingTimer(totalSeconds: number): string {
  const safe = Math.max(0, Math.floor(totalSeconds))
  const minutes = Math.floor(safe / 60)
  const seconds = safe % 60
  return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`
}

export function ExamScreen({
  ticket,
  question,
  questionIndex,
  completedMainCount,
  activeFollowUp,
  generatingFollowUp,
  initialTranscript,
  transcript,
  recording,
  recordingElapsedSeconds,
  transcribing,
  busy,
  blurCount,
  remainingSeconds,
  transcriptReviewPending,
  transcriptBufferRemaining,
  onToggleRecording,
  onTranscript,
  onRestoreOriginalTranscript,
  onComplete,
  onSubmit,
}: ExamScreenProps) {
  const totalQuestions = ticket.questions.length
  const progressPercent =
    totalQuestions > 0 ? Math.round((Math.min(completedMainCount, totalQuestions) / totalQuestions) * 100) : 0
  const allMainCompleted = completedMainCount >= totalQuestions && !activeFollowUp && !generatingFollowUp
  const allowsTranscriptEdit = ticket.examInputMode === 'VoiceWithTranscriptEdit'
  const inputModeLabel = allowsTranscriptEdit ? 'Ghi âm & rà soát transcript' : 'Chỉ ghi âm giọng nói'
  const timerUrgencyClass =
    remainingSeconds <= 60 ? 'is-danger' : remainingSeconds <= 300 ? 'is-warning' : ''

  const difficultyKey = (question.difficulty ?? '').trim().toLowerCase()
  const difficultyMeta = DIFFICULTY_LABELS[difficultyKey] ?? {
    label: question.difficulty || 'Chuẩn',
    toneClass: 'is-neutral',
  }

  const editEval =
    allowsTranscriptEdit && transcriptReviewPending
      ? evaluateTranscriptEdit(initialTranscript, transcript)
      : null
  const isEditExceeded = editEval?.isExceeded === true

  const safeBufferTotal = Math.max(10, Math.min(300, ticket.transcriptBufferSeconds || 60))
  const bufferProgressPercent = Math.max(0, Math.min(100, (transcriptBufferRemaining / safeBufferTotal) * 100))

  const canUseSpaceShortcut =
    !allMainCompleted && !transcriptReviewPending && !generatingFollowUp && !busy && !transcribing

  useEffect(() => {
    if (!canUseSpaceShortcut) return
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.code !== 'Space' || event.repeat || event.ctrlKey || event.altKey || event.metaKey) return
      const target = event.target as HTMLElement | null
      if (target) {
        const tag = target.tagName
        if (tag === 'TEXTAREA' || tag === 'INPUT' || tag === 'SELECT' || tag === 'BUTTON' || target.isContentEditable) {
          return
        }
      }
      event.preventDefault()
      onToggleRecording()
    }
    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [canUseSpaceShortcut, onToggleRecording])

  const hasQuestionImage = Boolean(question.imageUrl) && !activeFollowUp

  return (
    <section className="practice-exam-shell" aria-labelledby="exam-heading">
      {/* ─── Deep Navy Academic Header (matches PracticeSessionPage) ─── */}
      <header className="practice-exam-banner">
        <div className="practice-exam-banner-grid" aria-hidden="true" />
        <div className="practice-exam-banner-inner">
          <div className="practice-exam-banner-top">
            <div className="practice-exam-title-group">
              <p className="practice-exam-eyebrow">{ticket.sessionTitle}</p>
              <h1 id="exam-heading" className="practice-exam-title">
                {ticket.shiftName} · Phòng {ticket.roomLab} · Ghế #{ticket.seatNumber}
              </h1>
            </div>

            <div className="practice-exam-banner-meta">
              <span
                className={`practice-focus-pill ${blurCount > 0 ? 'is-alert' : ''}`}
                title="Số lần mất tiêu điểm cửa sổ"
              >
                Mất tiêu điểm: {blurCount}/3
              </span>
              <span
                className={`practice-timer-pill ${timerUrgencyClass}`}
                role="timer"
                aria-label="Thời gian còn lại của ca thi"
              >
                Còn {formatDuration(remainingSeconds)}
              </span>
            </div>
          </div>

          <div className="practice-exam-progress-row">
            <span className="practice-exam-progress-label">
              Câu {Math.min(questionIndex + 1, Math.max(1, totalQuestions))}/{totalQuestions}
              {activeFollowUp
                ? ` · Đào sâu ${activeFollowUp.followUpIndex}/${activeFollowUp.maxFollowUpQuestions}`
                : ''}
            </span>
            <div
              className="practice-progress-segments"
              role="progressbar"
              aria-label="Tiến độ bài thi chính thức"
              aria-valuemin={0}
              aria-valuemax={totalQuestions}
              aria-valuenow={completedMainCount}
            >
              {Array.from({ length: Math.max(totalQuestions, 1) }).map((_, index) => {
                const stateClass =
                  index < completedMainCount
                    ? 'is-done'
                    : index === questionIndex
                      ? 'is-current'
                      : ''
                return <span key={index} className={`practice-progress-segment ${stateClass}`} />
              })}
            </div>
            <span className="practice-exam-progress-pct">{progressPercent}%</span>
          </div>
        </div>
      </header>

      {/* ─── Clean White Main Stage ─── */}
      <div className="practice-exam-body">
        {/* Question Block */}
        <article className={`practice-question-section ${hasQuestionImage ? 'has-image' : ''}`}>
          {hasQuestionImage && question.imageUrl ? (
            <figure className="practice-question-figure">
              <img
                src={question.imageUrl}
                alt={question.imageDescription ?? `Hình minh họa cho ${question.title}`}
                loading="lazy"
                referrerPolicy="no-referrer"
                onError={(event) => {
                  event.currentTarget.style.display = 'none'
                }}
              />
              {question.imageDescription ? <figcaption>{question.imageDescription}</figcaption> : null}
            </figure>
          ) : null}

          <div className="practice-question-content-wrap">
            {activeFollowUp ? (
              <div className="practice-parent-context">
                <span className="practice-parent-context-label">
                  Ngữ cảnh câu hỏi chính #{questionIndex + 1}
                </span>
                <strong>{question.title}</strong>
                <p>{question.content}</p>
              </div>
            ) : null}

            <div className="practice-question-meta">
              <span className="practice-meta-eyebrow">
                <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
                  <path d="M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z" />
                  <path d="M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z" />
                </svg>
                {activeFollowUp
                  ? `Câu hỏi đào sâu của câu ${questionIndex + 1}`
                  : 'Thi chính thức'}
              </span>

              {!activeFollowUp ? (
                <span className={`practice-diff-pill ${difficultyMeta.toneClass}`}>
                  {difficultyMeta.label}
                </span>
              ) : (
                <span className="practice-followup-pill">
                  Đào sâu {activeFollowUp.followUpIndex}/{activeFollowUp.maxFollowUpQuestions}
                </span>
              )}

              {!activeFollowUp && question.bloomLevel ? (
                <span className="practice-neutral-pill">{question.bloomLevel}</span>
              ) : null}

              <span className="practice-mode-pill">{inputModeLabel}</span>
            </div>

            {activeFollowUp ? (
              <>
                <h2 className="practice-question-heading">{activeFollowUp.prompt}</h2>
              </>
            ) : (
              <>
                <h2 className="practice-question-heading">{question.title}</h2>
                <p className="practice-question-prompt">{question.content}</p>
                {!question.imageUrl && question.imageDescription ? (
                  <p className="field-hint" style={{ marginTop: '0.6rem' }}>
                    Mô tả hình: {question.imageDescription}
                  </p>
                ) : null}
              </>
            )}
          </div>
        </article>

        {/* ─── Answer Interaction Area ─── */}
        <div className="practice-answer-stage">
          {generatingFollowUp ? (
            <div className="practice-status-card" role="status" aria-live="polite">
              <span className="practice-status-spinner" aria-hidden="true" />
              <div>
                <p className="practice-status-eyebrow">Hệ thống vấn đáp</p>
                <h3>Đang phân tích câu trả lời &amp; chuẩn bị câu hỏi tiếp theo…</h3>
                <p>Vui lòng giữ nguyên cửa sổ thi trong giây lát.</p>
              </div>
            </div>
          ) : allMainCompleted ? (
            <div className="practice-complete-card" role="status">
              <div className="practice-complete-icon" aria-hidden="true">
                ✓
              </div>
              <h3>Đã hoàn thành toàn bộ {totalQuestions} câu hỏi</h3>
              <p>
                Tất cả lượt trả lời đã được tải lên kho lưu trữ và niêm phong SHA-256. Nhấn nút bên dưới để
                nộp bài chính thức và khóa trạm thi.
              </p>
              <button
                className="practice-primary-btn"
                type="button"
                disabled={busy}
                onClick={() => onSubmit(false)}
              >
                {busy ? 'Đang nộp bài…' : 'Nộp bài và khóa kiosk →'}
              </button>
            </div>
          ) : allowsTranscriptEdit && transcriptReviewPending && editEval ? (
            <div className="practice-buffer-panel" role="region" aria-label="Hiệu đính bản trả lời">
              <div className="practice-buffer-header">
                <div>
                  <p className="practice-buffer-eyebrow">Rà soát trước khi niêm phong</p>
                  <h3>Hiệu đính bản trả lời</h3>
                  <p className="practice-buffer-desc">
                    Kiểm tra và chỉnh sửa thuật ngữ kỹ thuật (tối đa 35%). Hệ thống sẽ tự động niêm phong khi hết giờ.
                  </p>
                </div>
                <div
                  className={`practice-buffer-timer ${
                    transcriptBufferRemaining <= 10
                      ? 'is-danger'
                      : transcriptBufferRemaining <= safeBufferTotal / 3
                        ? 'is-warning'
                        : ''
                  }`}
                  role="timer"
                  aria-label={`Còn ${transcriptBufferRemaining} giây để hiệu đính`}
                >
                  Còn {formatRecordingTimer(transcriptBufferRemaining)}
                </div>
              </div>

              <div className="practice-buffer-track" aria-hidden="true">
                <div
                  className={`practice-buffer-fill ${
                    transcriptBufferRemaining <= 10
                      ? 'is-danger'
                      : transcriptBufferRemaining <= safeBufferTotal / 3
                        ? 'is-warning'
                        : ''
                  }`}
                  style={{ width: `${bufferProgressPercent}%` }}
                />
              </div>

              <div className="practice-buffer-toolbar">
                <span className={`badge ${isEditExceeded ? 'badge-danger' : 'badge-warning'}`}>
                  Đã sửa: {editEval.percent.toFixed(1)}% / 35%
                </span>
                {editEval.isManuallyEdited ? (
                  <button
                    className="btn btn-ghost"
                    type="button"
                    onClick={onRestoreOriginalTranscript}
                    disabled={busy}
                  >
                    Khôi phục bản gốc
                  </button>
                ) : null}
              </div>

              {isEditExceeded ? (
                <div className="banner banner-error" role="alert" style={{ marginBottom: '0.75rem' }}>
                  <span className="banner-badge" aria-hidden="true">
                    !
                  </span>
                  <span>
                    Mức chỉnh sửa ({editEval.percent.toFixed(1)}%) vượt giới hạn tối đa 35.0%. Vui lòng giảm chỉnh sửa
                    hoặc bấm &ldquo;Khôi phục bản gốc&rdquo;.
                  </span>
                </div>
              ) : null}

              <textarea
                id="transcript"
                className="practice-buffer-textarea"
                value={transcript}
                onChange={(event) => onTranscript(event.target.value)}
                rows={5}
                disabled={busy}
                placeholder="Nội dung nhận diện giọng nói…"
                aria-label="Transcript câu trả lời"
              />

              <div className="practice-buffer-footer">
                <span className="practice-buffer-hint">
                  Âm thanh gốc đã được giữ lại để niêm phong SHA-256 cùng bản chữ đã hiệu đính.
                </span>
                <button
                  className="practice-primary-btn"
                  type="button"
                  disabled={busy || isEditExceeded}
                  onClick={onComplete}
                >
                  {busy ? 'Đang niêm phong & lưu…' : 'Xác nhận và lưu câu trả lời →'}
                </button>
              </div>
            </div>
          ) : (
            <div className="practice-voice-stage">
              <button
                type="button"
                className={`practice-mic-orb ${recording ? 'is-recording' : ''} ${transcribing || busy ? 'is-busy' : ''}`}
                aria-pressed={recording}
                aria-label={recording ? 'Dừng ghi âm' : 'Bắt đầu ghi âm'}
                onClick={onToggleRecording}
                disabled={busy || transcribing || generatingFollowUp}
              >
                {transcribing || (busy && !recording) ? (
                  <span className="practice-mic-spinner" aria-hidden="true" />
                ) : (
                  <svg
                    viewBox="0 0 24 24"
                    width="30"
                    height="30"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    className={recording ? 'is-pulsing' : ''}
                    aria-hidden="true"
                  >
                    <path d="M12 2a3 3 0 0 0-3 3v7a3 3 0 0 0 6 0V5a3 3 0 0 0-3-3Z" />
                    <path d="M19 10v2a7 7 0 0 1-14 0v-2" />
                    <line x1="12" x2="12" y1="19" y2="22" />
                  </svg>
                )}
                <span className="practice-mic-label">
                  {transcribing
                    ? 'Đang nhận diện'
                    : busy && !recording
                      ? 'Đang lưu…'
                      : recording
                        ? formatRecordingTimer(recordingElapsedSeconds)
                        : 'Ghi âm'}
                </span>
              </button>

              <p className="practice-mic-status" aria-live="polite">
                {recording
                  ? 'Nhấn để dừng ghi âm'
                  : transcribing
                    ? 'Đang nhận diện nội dung…'
                    : busy
                      ? 'Đang tải audio và niêm phong SHA-256…'
                      : 'Nhấn để bắt đầu ghi âm'}
              </p>

              <p className="practice-mic-hint">
                Nhấn nút hoặc phím <kbd>Space</kbd> để bật/tắt.{' '}
                {allowsTranscriptEdit
                  ? 'Dừng để rà soát transcript trước khi lưu.'
                  : 'Dừng để niêm phong và chuyển câu tiếp theo.'}
              </p>
            </div>
          )}
        </div>
      </div>
    </section>
  )
}
