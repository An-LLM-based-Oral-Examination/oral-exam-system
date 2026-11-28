import { useRef, useState } from 'react'
import { BookOpenCheck, Check, LoaderCircle } from 'lucide-react'
import { FormDialog, DialogFooter } from '@/design/overlays'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { examPreparationService as service } from '@/services/examPreparation.service'
import { preparationError, similarityTone } from '@/lib/examPreparation'
import type { BankPromotionCandidate } from '@/types/examPreparation.types'
import { ExamLoadError } from './ExamPreparationShared'
import { toast } from '@/components/common/toastStore'

export function PaperBankPromotionDialog({
  paperId,
  setCode,
  candidates,
  onClose,
}: {
  paperId: string
  setCode: string
  candidates: BankPromotionCandidate[]
  onClose: () => void
}) {
  const [selected, setSelected] = useState(() =>
    candidates.filter((c) => c.similarityPercent < 70).map((c) => c.questionId),
  )
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const pending = useRef(false)
  async function save() {
    if (pending.current || !selected.length) return
    pending.current = true
    setBusy(true)
    setError('')
    try {
      await service.promoteQuestions(paperId, selected)
      toast.success('Đã thêm các câu được chọn vào ngân hàng.')
      onClose()
    } catch (failure) {
      setError(preparationError(failure))
    } finally {
      pending.current = false
      setBusy(false)
    }
  }
  return (
    <FormDialog
      title="Thêm câu vào ngân hàng"
      eyebrow={`${setCode} · Đã duyệt`}
      icon={BookOpenCheck}
      size="md"
      nested
      busy={busy}
      onClose={() => {
        if (!pending.current) onClose()
      }}
      footer={
        <DialogFooter>
          <span className="mr-auto text-sm font-semibold text-[#1C4D8D]">
            Đã chọn {selected.length}/{candidates.length} câu
          </span>
          <button type="button" className={ds.outlineButton} disabled={busy} onClick={onClose}>
            Bỏ qua
          </button>
          <button
            type="button"
            className={ds.primaryButton}
            disabled={busy || !selected.length}
            onClick={() => void save()}
          >
            {busy ? <LoaderCircle className="size-4 animate-spin" /> : <Check className="size-4" />}{' '}
            {busy ? 'Đang thêm…' : 'Thêm vào ngân hàng'}
          </button>
        </DialogFooter>
      }
    >
      <p className="mb-3 text-sm leading-relaxed text-[#5b6b84]">
        Bộ đề đã được duyệt. Chọn câu mới để bổ sung vào ngân hàng chung; bỏ qua không ảnh hưởng kết
        quả duyệt.
      </p>
      <div className="divide-y divide-[#82aedb]/45 rounded-xl border border-[#82aedb]/55 bg-white px-3">
        {candidates.map((c) => (
          <div key={c.questionId} className="py-3">
            <label className="flex cursor-pointer items-start gap-3">
              <input
                type="checkbox"
                className="mt-1 size-4 shrink-0 accent-[#1C4D8D]"
                disabled={busy}
                checked={selected.includes(c.questionId)}
                onChange={(e) =>
                  setSelected((ids) =>
                    e.target.checked
                      ? [...ids, c.questionId]
                      : ids.filter((id) => id !== c.questionId),
                  )
                }
              />
              <div className="min-w-0 flex-1">
                <p className="text-sm font-bold wrap-anywhere text-[#0F2854]">{c.title}</p>
                <div className="mt-2 flex flex-wrap items-center gap-2">
                  <span
                    className={cn(
                      'rounded-full border px-2 py-0.5 text-xs font-bold tabular-nums',
                      similarityTone(c.similarityPercent),
                    )}
                  >
                    {c.similarityPercent}% tương đồng
                  </span>
                  <span className="text-xs font-semibold text-[#5b6b84]">
                    {c.similarityPercent >= 85
                      ? 'Đề xuất không thêm'
                      : c.similarityPercent >= 70
                        ? 'Cân nhắc dùng câu đã có'
                        : c.similarityPercent >= 50
                          ? 'Nên rà soát'
                          : 'Có thể thêm'}
                  </span>
                </div>
              </div>
            </label>
            {c.comparedTitle && (
              <details className="mt-2 ml-7 text-xs text-[#5b6b84]">
                <summary className="cursor-pointer font-semibold text-[#1C4D8D]">
                  Câu tương tự trong ngân hàng
                </summary>
                <p className="mt-2 font-bold wrap-anywhere text-[#0F2854]">{c.comparedTitle}</p>
                <p className="mt-1 leading-relaxed wrap-anywhere whitespace-pre-wrap">
                  {c.comparedContent}
                </p>
              </details>
            )}
          </div>
        ))}
      </div>
      {error && (
        <div className="mt-3">
          <ExamLoadError message={error} />
        </div>
      )}
    </FormDialog>
  )
}
