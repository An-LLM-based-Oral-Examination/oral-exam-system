import { useEffect, useState } from 'react'
import { ClipboardCheck, Gavel, LoaderCircle, RefreshCw, Sparkles, UserRoundCog } from 'lucide-react'
import { appealsService } from '@/services/appeals.service'
import { useAuth } from '@/hooks/useAuth'
import type { Appeal, AppealAllocationProposal } from '@/types/appeal.types'
import { cn } from '@/lib/utils'
import { EmptyState } from '@/components/common/EmptyState'
import { FieldError, fieldInvalidClass, fieldLabelClass } from '@/components/common/formField'
import { BannerFact, LecturerPageBanner, SectionHeader } from '@/design/ui'
import { ds } from '@/design/system'

function getErrorMessage(error: unknown) {
  if (typeof error === 'object' && error !== null && 'detail' in error) {
    const detail = (error as { detail?: unknown }).detail
    if (typeof detail === 'string' && detail.trim()) return detail
  }
  return 'Không thể hoàn tất yêu cầu. Vui lòng thử lại.'
}

export function AppealManagementPage() {
  const { user } = useAuth()
  const [appeals, setAppeals] = useState<Appeal[]>([])
  const [selected, setSelected] = useState<Appeal | null>(null)
  const [lecturerId, setLecturerId] = useState('')
  const [decision, setDecision] = useState('APPROVED')
  const [score, setScore] = useState('')
  const [notes, setNotes] = useState('')
  const [proposal, setProposal] = useState<AppealAllocationProposal | null>(null)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [scoreError, setScoreError] = useState('')
  const [notesError, setNotesError] = useState('')
  const [lecturerIdError, setLecturerIdError] = useState('')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')

  const canManageAppeals = user?.role === 'ExaminationOfficer' || user?.role === 'Admin'
  const canRegradeAppeal = user?.role === 'Instructor'

  const resetFieldErrors = () => {
    setScoreError('')
    setNotesError('')
    setLecturerIdError('')
  }

  /** Trả về true nếu hợp lệ; đồng thời highlight từng field lỗi. */
  const validateReviewFields = (scoreRequired: boolean) => {
    let nextScoreError = ''
    const scoreText = score.trim()
    if (!scoreText) {
      if (scoreRequired) nextScoreError = 'Vui lòng nhập điểm.'
    } else {
      const value = Number(scoreText)
      if (!Number.isFinite(value) || value < 0 || value > 10) nextScoreError = 'Điểm phải nằm trong khoảng 0 – 10.'
    }
    const nextNotesError = notes.trim().length < 10 ? 'Ghi chú cần ít nhất 10 ký tự.' : ''
    setScoreError(nextScoreError)
    setNotesError(nextNotesError)
    return !nextScoreError && !nextNotesError
  }

  const loadAppeals = async () => {
    setLoading(true); setError('')
    try {
      const result = await appealsService.list()
      setAppeals(Array.isArray(result) ? result : [])
      setSelected((current) => current ? (result.find((item) => item.id === current.id) ?? current) : result[0] ?? null)
    } catch (loadError) {
      setError(getErrorMessage(loadError))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    const timer = window.setTimeout(() => { void loadAppeals() }, 0)
    return () => window.clearTimeout(timer)
  }, [])

  const runAction = async (action: () => Promise<unknown>, successMessage: string) => {
    setBusy(true); setError(''); setNotice('')
    try {
      await action()
      setNotice(successMessage)
      await loadAppeals()
    } catch (actionError) {
      setError(getErrorMessage(actionError))
    } finally {
      setBusy(false)
    }
  }

  const review = () => {
    if (!selected) return
    setLecturerIdError('')
    if (!validateReviewFields(false)) return
    void runAction(
      () => appealsService.review(selected.id, { decision, proposedScore: score.trim() ? Number(score) : undefined, reviewNotes: notes.trim() }),
      'Đã ghi nhận quyết định phúc khảo cuối cùng.'
    )
  }

  const assign = () => {
    if (!selected) return
    resetFieldErrors()
    if (!lecturerId.trim()) {
      setLecturerIdError('Vui lòng nhập Lecturer user ID.')
      return
    }
    void runAction(
      () => appealsService.assignLecturer(selected.id, { lecturerId: lecturerId.trim() }),
      'Đã phân công giảng viên chấm lại độc lập.'
    )
  }

  const regrade = () => {
    if (!selected) return
    setLecturerIdError('')
    if (!validateReviewFields(true)) return
    void runAction(
      () => appealsService.regrade(selected.id, { newScore: Number(score), reviewNotes: notes.trim() }),
      'Đã lưu điểm chấm lại phúc khảo.'
    )
  }

  const previewAutoAllocation = async () => {
    setBusy(true); setError(''); setNotice('')
    try {
      const result = await appealsService.getAllocationProposal()
      setProposal(result)
      const assignableCount = (result.items ?? []).filter((item) => Boolean(item.proposedLecturerId)).length
      setNotice(`Đã tính đề xuất phân bổ tự động: ${assignableCount}/${result.totalAppeals ?? 0} đơn đủ điều kiện (định mức <= 24 bài/GV).`)
    } catch (actionError) {
      setError(getErrorMessage(actionError))
    } finally {
      setBusy(false)
    }
  }

  const confirmBatchAssign = () => {
    const validItems = (proposal?.items ?? []).filter(
      (item): item is typeof item & { proposedLecturerId: string } => Boolean(item.proposedLecturerId)
    )
    if (validItems.length === 0) return
    void runAction(
      async () => {
        await appealsService.batchAssign({
          assignments: validItems.map((item) => ({
            appealId: item.appealId,
            lecturerId: item.proposedLecturerId,
          })),
        })
        setProposal(null)
      },
      `Đã phân công đồng loạt ${validItems.length} đơn phúc khảo.`
    )
  }

  const assignableItems = (proposal?.items ?? []).filter((item) => Boolean(item.proposedLecturerId))

  return (
    <div className={ds.page}>
      <LecturerPageBanner
        icon={ClipboardCheck}
        eyebrow="Phúc khảo 5 bước (R2/R7)"
        title="Quản lý & Thẩm định Phúc khảo"
        description="Cán bộ Khảo thí tiếp nhận đơn, phân công Giảng viên chấm lại độc lập (định mức <= 24 bài/GV, loại trừ giám khảo ban đầu) và phê duyệt quyết định cuối cùng. Trưởng bộ môn giám sát chuyên môn học thuật."
        extras={
          <>
            <BannerFact icon={Gavel}>Khảo thí phê duyệt</BannerFact>
            <BannerFact icon={UserRoundCog} tone="green">
              Định mức &le; 24 bài/GV
            </BannerFact>
            <BannerFact icon={ClipboardCheck} tone="violet">
              Chấm lại độc lập
            </BannerFact>
          </>
        }
      />
      {(error || notice) && (
        <div
          role={error ? 'alert' : 'status'}
          className={cn(
            'rounded-xl border px-4 py-3 text-sm font-semibold',
            error ? 'border-[#f0c5c9] bg-[#fff5f6] text-[#a43c47]' : 'border-[#b7e4cf] bg-[#effaf6] text-[#176f58]'
          )}
        >
          {error || notice}
        </div>
      )}

      {canManageAppeals && (
        <section className={cn(ds.section, 'p-4')}>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <h2 className="text-sm font-bold text-[#0F2854]">Phân bổ tự động Giảng viên chấm phúc khảo (Định mức &le; 24 bài/GV)</h2>
              <p className="mt-0.5 text-xs text-[#687a91]">
                Hệ thống tự động cân bằng tải và loại trừ 100% Giám khảo đã chấm ca thi ban đầu của sinh viên.
              </p>
            </div>
            <div className="flex flex-wrap gap-2">
              <button type="button" disabled={busy} onClick={() => void previewAutoAllocation()} className={ds.outlineButton}>
                <Sparkles className="size-4" /> Đề xuất phân bổ tự động
              </button>
              {assignableItems.length > 0 && (
                <button type="button" disabled={busy} onClick={confirmBatchAssign} className={ds.primaryButton}>
                  <UserRoundCog className="size-4" /> Xác nhận phân công ({assignableItems.length} đơn)
                </button>
              )}
            </div>
          </div>
          {proposal && (
            <div className="mt-3 rounded-xl border border-[#82aedb]/55 bg-[#f8fbfd] p-3 text-xs text-[#52657d]">
              <p className="font-semibold text-[#0F2854]">
                Tổng đơn chờ: {proposal.totalAppeals} · Giảng viên khả dụng: {proposal.totalLecturers} · Đủ điều kiện phân bổ: {assignableItems.length}
              </p>
              {proposal.items.length > 0 && (
                <div className="mt-2 grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
                  {proposal.items.map((item) => (
                    <div key={item.appealId} className="rounded-lg border border-[#d6e4f2] bg-white p-2">
                      <p className="font-bold text-[#1C4D8D]">{item.studentCode} · {item.sessionTitle}</p>
                      <p className="mt-0.5 text-[#0F2854]">
                        GV chấm lại: <strong>{item.proposedLecturerName || 'Chưa có GV phù hợp'}</strong>
                      </p>
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}
        </section>
      )}

      <section className="grid gap-4 lg:grid-cols-[minmax(280px,0.8fr)_minmax(0,1.4fr)]">
        <div className={ds.section}>
          <SectionHeader
            icon={Gavel}
            title="Danh sách đơn"
            badge={`${appeals.length} đơn`}
            hint="Theo dõi và xử lý đơn phúc khảo"
            action={
              <button type="button" className={ds.outlineButton} onClick={() => void loadAppeals()}>
                <RefreshCw className="size-4" /> Làm mới
              </button>
            }
          />
          {loading ? (
            <div className="flex justify-center p-10">
              <LoaderCircle className="size-7 animate-spin text-[#1C4D8D]" />
            </div>
          ) : (
            <div className="max-h-[640px] space-y-2 overflow-y-auto p-3">
              {appeals.map((appeal) => (
                <button
                  key={appeal.id}
                  type="button"
                  onClick={() => {
                    setSelected(appeal)
                    setScore(appeal.proposedScore?.toString() ?? appeal.originalScore?.toString() ?? '')
                    setNotes(appeal.reviewNotes ?? '')
                    resetFieldErrors()
                  }}
                  className={cn(
                    'w-full rounded-xl border p-3 text-left transition',
                    selected?.id === appeal.id ? 'border-[#1C4D8D] bg-[#F4FBFD]' : 'border-[#82aedb]/55 bg-white hover:border-[#82aedb]/55'
                  )}
                >
                  <div className="flex items-center justify-between gap-2">
                    <span className="font-mono text-xs font-bold text-[#1C4D8D]">{appeal.studentCode || '—'}</span>
                    <span className={ds.infoChip}>{appeal.status}</span>
                  </div>
                  <p className="mt-2 text-sm font-bold text-[#0F2854]">{appeal.studentName || 'Sinh viên'}</p>
                  <p className="mt-1 truncate text-xs text-[#687a91]">{appeal.sessionTitle || appeal.reason}</p>
                </button>
              ))}
              {appeals.length === 0 && (
                <EmptyState compact icon={Gavel} title="Chưa có đơn phúc khảo" description="Đơn mới sẽ xuất hiện tại đây khi sinh viên gửi." />
              )}
            </div>
          )}
        </div>
        {selected ? (
          <div className="space-y-4">
            <section className={ds.section}>
              <SectionHeader icon={Gavel} title={`Chi tiết đơn #${selected.id.slice(0, 8)}`} hint={`${selected.studentName} · ${selected.studentCode}`} />
              <div className="space-y-4 p-5">
                <div className="rounded-xl border border-[#82aedb]/55 bg-[#f8fbfd] p-4 text-sm leading-relaxed text-[#52657d]">
                  <p><strong>Lý do khiếu nại:</strong> {selected.reason}</p>
                  <p className="mt-2">
                    <strong>Điểm gốc:</strong> {selected.originalScore ?? '—'} · <strong>Điểm đề xuất chấm lại:</strong> {selected.proposedScore ?? '—'} · <strong>Trạng thái:</strong> {selected.status}
                  </p>
                  {selected.assignedToName && (
                    <p className="mt-2"><strong>Giảng viên chấm lại độc lập:</strong> {selected.assignedToName}</p>
                  )}
                </div>

                {(canManageAppeals || canRegradeAppeal) ? (
                  <>
                    <div className="grid gap-3 sm:grid-cols-2">
                      {canManageAppeals && (
                        <label className="text-xs font-bold text-[#63758e]">
                          Quyết định của Khảo thí
                          <select className={cn(ds.field, 'mt-1')} value={decision} onChange={(event) => setDecision(event.target.value)}>
                            <option value="APPROVED">APPROVED (Chấp thuận cập nhật điểm)</option>
                            <option value="REJECTED">REJECTED (Giữ nguyên điểm gốc)</option>
                          </select>
                        </label>
                      )}
                      <label className={fieldLabelClass(Boolean(scoreError))}>
                        Điểm đề xuất / chấm lại
                        <input
                          type="number"
                          min="0"
                          max="10"
                          step="0.1"
                          className={cn(ds.field, 'mt-1', scoreError && fieldInvalidClass)}
                          value={score}
                          onChange={(event) => {
                            setScore(event.target.value)
                            if (scoreError) setScoreError('')
                          }}
                          aria-invalid={Boolean(scoreError)}
                        />
                        <FieldError message={scoreError} />
                      </label>
                    </div>
                    <div>
                      <textarea
                        className={cn(ds.field, 'min-h-24 py-2', notesError && fieldInvalidClass)}
                        placeholder="Biên bản giải trình chấm lại / ghi chú phê duyệt (tối thiểu 10 ký tự)"
                        value={notes}
                        onChange={(event) => {
                          setNotes(event.target.value)
                          if (notesError) setNotesError('')
                        }}
                        aria-label="Ghi chú xử lý"
                        aria-invalid={Boolean(notesError)}
                      />
                      <FieldError message={notesError} />
                    </div>
                    <div className="flex flex-wrap gap-2">
                      {canManageAppeals && (
                        <>
                          <button type="button" disabled={busy} onClick={review} className={ds.primaryButton}>
                            <Gavel className="size-4" /> Phê duyệt quyết định cuối
                          </button>
                          <div className="max-w-[220px]">
                            <input
                              className={cn(ds.field, lecturerIdError && fieldInvalidClass)}
                              placeholder="Lecturer user ID"
                              value={lecturerId}
                              onChange={(event) => {
                                setLecturerId(event.target.value)
                                if (lecturerIdError) setLecturerIdError('')
                              }}
                              aria-label="Lecturer user ID"
                              aria-invalid={Boolean(lecturerIdError)}
                            />
                            <FieldError message={lecturerIdError} />
                          </div>
                          <button type="button" disabled={busy} onClick={assign} className={ds.outlineButton}>
                            <UserRoundCog className="size-4" /> Phân công GV chấm lại
                          </button>
                        </>
                      )}
                      {canRegradeAppeal && (
                        <button type="button" disabled={busy} onClick={regrade} className={ds.primaryButton}>
                          <Gavel className="size-4" /> Gửi biên bản & điểm chấm lại
                        </button>
                      )}
                    </div>
                  </>
                ) : (
                  <div className="rounded-xl border border-[#d6e4f2] bg-[#f4f8fc] p-3 text-xs font-semibold text-[#1C4D8D]">
                    Chế độ giám sát học thuật (Trưởng bộ môn): Theo dõi tiến trình phúc khảo môn học. Quyền phân công và phê duyệt quyết định cuối cùng thuộc Cán bộ Khảo thí.
                  </div>
                )}
              </div>
            </section>
          </div>
        ) : (
          <EmptyState className="min-h-[320px] justify-center" icon={Gavel} title="Chưa chọn đơn" description="Chọn một đơn phúc khảo ở danh sách bên trái để xử lý." />
        )}
      </section>
    </div>
  )
}
