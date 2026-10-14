import { useState, type FormEvent } from 'react'
import { AlertCircle, CheckCircle2, LockKeyhole, Save } from 'lucide-react'

interface ScoreOverrideFormProps {
  initialScore?: number | null
  isLocked: boolean
  isSaving?: boolean
  persistenceAvailable?: boolean
  onSaveOverride?: (newScore: number, reason: string) => Promise<void> | void
}

export function ScoreOverrideForm({
  initialScore,
  isLocked,
  isSaving = false,
  persistenceAvailable = true,
  onSaveOverride,
}: ScoreOverrideFormProps) {
  const [score, setScore] = useState(initialScore ?? 0)
  const [reason, setReason] = useState('')
  const [error, setError] = useState('')

  const isReadOnly = isLocked || !persistenceAvailable || !onSaveOverride

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (isReadOnly || !onSaveOverride) return

    if (score < 0 || score > 10) {
      setError('Điểm chính thức phải nằm trong khoảng từ 0 đến 10.')
      return
    }
    if (reason.trim().length < 10) {
      setError('Bắt buộc nhập lý do giải trình khi điều chỉnh điểm.')
      return
    }

    setError('')
    await onSaveOverride(score, reason.trim())
  }

  return (
    <form
      onSubmit={handleSubmit}
      className="overflow-hidden rounded-2xl border border-[#dce5f1] bg-white shadow-[0_10px_34px_rgba(31,61,105,0.06)]"
      aria-labelledby="score-review-title"
    >
      <div className="flex flex-col gap-3 border-b border-[#e8edf4] px-4 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-5">
        <div>
          <h3 id="score-review-title" className="text-sm font-bold text-[#13213a]">
            Điểm hậu kiểm của giảng viên
          </h3>
          <p className="mt-1 text-[10px] text-[#728197]">
            Mọi thay đổi phải có giải trình trước khi công bố điểm toàn ca.
          </p>
        </div>
        <span
          className={`inline-flex w-fit items-center gap-1.5 rounded-full px-2.5 py-1 text-[10px] font-bold ${
            isLocked
              ? 'border border-[#f0c5c9] bg-[#fff1f2] text-[#ad3541]'
              : 'border border-[#cbdcf7] bg-[#f0f5ff] text-[#275bc3]'
          }`}
        >
          {isLocked ? (
            <LockKeyhole className="size-3.5" aria-hidden="true" />
          ) : (
            <CheckCircle2 className="size-3.5" aria-hidden="true" />
          )}
          {isLocked ? 'Đã khóa một chiều' : 'Đang hậu kiểm'}
        </span>
      </div>

      <div className="space-y-4 p-4 sm:p-5">
        {!persistenceAvailable && !isLocked && (
          <div className="flex gap-2 rounded-xl border border-[#f0deb9] bg-[#fff9ec] p-3 text-[10px] leading-relaxed text-[#80591d]">
            <AlertCircle className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
            <p>
              Chức năng lưu điểm hậu kiểm chưa được kích hoạt trên hệ thống. Biểu mẫu được khóa để
              tránh hiển thị kết quả chưa được ghi nhận.
            </p>
          </div>
        )}

        <div className="grid gap-4 sm:grid-cols-[150px_minmax(0,1fr)]">
          <label className="block">
            <span className="text-[10px] font-bold tracking-[0.06em] text-[#63758e] uppercase">
              Điểm chính thức
            </span>
            <div className="mt-1.5 flex items-baseline gap-1 rounded-xl border border-[#d5dfec] bg-[#fbfcfe] px-3 py-2.5 focus-within:border-[#6991ee] focus-within:ring-2 focus-within:ring-[#dce8ff]">
              <input
                type="number"
                min="0"
                max="10"
                step="0.1"
                value={score}
                onChange={(event) => setScore(Number(event.target.value))}
                disabled={isReadOnly || isSaving}
                className="min-w-0 flex-1 bg-transparent font-mono text-lg font-black text-[#10203d] outline-none disabled:cursor-not-allowed disabled:opacity-60"
              />
              <span className="text-xs font-bold text-[#7e8ca0]">/10</span>
            </div>
          </label>

          <label className="block">
            <span className="flex items-center justify-between gap-2 text-[10px] font-bold tracking-[0.06em] text-[#63758e] uppercase">
              Lý do điều chỉnh
              <span className="text-[#b33c48] normal-case">Bắt buộc khi override</span>
            </span>
            <textarea
              value={reason}
              onChange={(event) => setReason(event.target.value)}
              disabled={isReadOnly || isSaving}
              minLength={10}
              maxLength={2000}
              aria-invalid={Boolean(error)}
              placeholder="Đối chiếu audio, transcript và từng tiêu chí rubric trước khi giải trình..."
              className="mt-1.5 min-h-24 w-full resize-y rounded-xl border border-[#d5dfec] bg-[#fbfcfe] px-3 py-2.5 text-xs leading-relaxed text-[#263850] transition outline-none placeholder:text-[#9aa8ba] focus:border-[#6991ee] focus:ring-2 focus:ring-[#dce8ff] disabled:cursor-not-allowed disabled:opacity-60"
            />
          </label>
        </div>

        {error && (
          <p
            className="flex items-center gap-1.5 text-[10px] font-semibold text-[#b23441]"
            role="alert"
          >
            <AlertCircle className="size-3.5" aria-hidden="true" />
            {error}
          </p>
        )}
      </div>

      <div className="flex justify-end border-t border-[#e8edf4] bg-[#fbfcfe] px-4 py-3 sm:px-5">
        <button
          type="submit"
          disabled={isReadOnly || isSaving}
          className="inline-flex min-h-10 items-center justify-center gap-2 rounded-xl bg-[#245eea] px-4 text-xs font-bold text-white shadow-[0_8px_20px_rgba(36,94,234,0.2)] transition hover:bg-[#194fcf] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#245eea] disabled:cursor-not-allowed disabled:bg-[#aab8cb] disabled:shadow-none"
        >
          <Save className="size-4" aria-hidden="true" />
          {isSaving ? 'Đang lưu...' : 'Lưu điểm hậu kiểm'}
        </button>
      </div>
    </form>
  )
}
