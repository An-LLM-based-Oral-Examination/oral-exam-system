import { useMemo, useState } from 'react'
import { AlertTriangle, CheckCircle2, LoaderCircle, Shuffle } from 'lucide-react'
import { officialExamService } from '@/services/officialExam.service'
import type {
  AutoAllocateStudentsResponse,
  ExamShift,
  OfficialExamEnrollmentEstimate,
} from '@/types/officialExam.types'
import { ConfirmDialog } from '@/design/ConfirmDialog'
import { DialogFooter, FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'
import { toast } from '@/components/common/toastStore'
import { userMessage } from '@/lib/userMessage'

interface AutoAllocateWizardDialogProps {
  open: boolean
  sessionId: string
  shifts: ExamShift[]
  estimate: OfficialExamEnrollmentEstimate | null
  onClose: () => void
  onAllocated: () => Promise<void> | void
}

export function AutoAllocateWizardDialog({
  open,
  sessionId,
  shifts,
  estimate,
  onClose,
  onAllocated,
}: AutoAllocateWizardDialogProps) {
  const scheduledShifts = useMemo(
    () =>
      shifts.filter(
        (shift) => shift.status.toLowerCase() === 'scheduled' && !shift.isExamPublished,
      ),
    [shifts],
  )
  const [selectedShiftIds, setSelectedShiftIds] = useState<string[]>(() =>
    scheduledShifts.map((shift) => shift.id),
  )
  const [assignHandedOverExamSets, setAssignHandedOverExamSets] = useState(true)
  const [dryRunResult, setDryRunResult] = useState<AutoAllocateStudentsResponse | null>(null)
  const [runningDryRun, setRunningDryRun] = useState(false)
  const [committing, setCommitting] = useState(false)
  const [showCommitConfirm, setShowCommitConfirm] = useState(false)
  const [error, setError] = useState('')

  const effectiveShiftIds =
    selectedShiftIds.length > 0 ? selectedShiftIds : scheduledShifts.map((shift) => shift.id)

  const toggleShift = (shiftId: string) => {
    setSelectedShiftIds((current) => {
      const base = current.length > 0 ? current : scheduledShifts.map((shift) => shift.id)
      return base.includes(shiftId) ? base.filter((id) => id !== shiftId) : [...base, shiftId]
    })
    setDryRunResult(null)
  }

  const runDryRun = async () => {
    if (runningDryRun || committing) return
    setRunningDryRun(true)
    setError('')
    try {
      const result = await officialExamService.autoAllocateStudents(sessionId, {
        shiftIds: effectiveShiftIds.length > 0 ? effectiveShiftIds : null,
        assignHandedOverExamSets,
        dryRun: true,
      })
      setDryRunResult(result)
    } catch (dryRunError) {
      setDryRunResult(null)
      setError(userMessage(dryRunError, 'Không thể chạy thử phân bổ phòng thi.'))
    } finally {
      setRunningDryRun(false)
    }
  }

  const executeCommit = async () => {
    if (committing) return
    setCommitting(true)
    setError('')
    try {
      const result = await officialExamService.autoAllocateStudents(sessionId, {
        shiftIds: effectiveShiftIds.length > 0 ? effectiveShiftIds : null,
        assignHandedOverExamSets,
        dryRun: false,
      })
      setShowCommitConfirm(false)
      toast.success(
        `Đã phân bổ ngẫu nhiên ${result.newlyAllocatedCount} thí sinh vào các phòng thi.`,
      )
      await Promise.resolve(onAllocated())
      onClose()
    } catch (commitError) {
      setShowCommitConfirm(false)
      const message = userMessage(commitError, 'Không thể xác nhận chia phòng ngẫu nhiên.')
      setError(message)
      toast.error(message)
    } finally {
      setCommitting(false)
    }
  }

  const insufficientSetsWarning =
    assignHandedOverExamSets &&
    estimate !== null &&
    estimate.unallocatedStudentCount > 0 &&
    estimate.handedOverExamSetCount <
      Math.min(estimate.maxStudentsPerRoom || 24, estimate.unallocatedStudentCount)

  return (
    <>
      <FormDialog
        open={open}
        align="center"
        size="xl"
        variant="calm"
        icon={Shuffle}
        eyebrow="BƯỚC 5 · CHIA PHÒNG NGẪU NHIÊN (CSPRNG)"
        title="Phân bổ ngẫu nhiên DSSV & Cân bằng ca cuối"
        busy={runningDryRun || committing}
        onClose={() => !(runningDryRun || committing) && onClose()}
        footer={
          <DialogFooter variant="calm">
            <button
              type="button"
              className={ds.outlineButton}
              disabled={runningDryRun || committing}
              onClick={onClose}
            >
              Đóng
            </button>
            <button
              type="button"
              className={ds.outlineButton}
              disabled={runningDryRun || committing || scheduledShifts.length === 0}
              onClick={() => void runDryRun()}
            >
              {runningDryRun ? (
                <LoaderCircle className="size-4 animate-spin" />
              ) : (
                <Shuffle className="size-4" />
              )}
              Chạy thử (Dry-Run)
            </button>
            <button
              type="button"
              className={ds.primaryButton}
              disabled={
                runningDryRun ||
                committing ||
                !dryRunResult ||
                dryRunResult.newlyAllocatedCount === 0
              }
              onClick={() => setShowCommitConfirm(true)}
            >
              <CheckCircle2 className="size-4" /> Xác nhận phân bổ chính thức
            </button>
          </DialogFooter>
        }
      >
        <div className="space-y-3">
          {error ? (
            <div
              role="alert"
              className="rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-4 py-3 text-sm font-semibold text-[#a43d4b]"
            >
              {error}
            </div>
          ) : null}

          {insufficientSetsWarning ? (
            <div
              role="alert"
              className="flex items-start gap-2 rounded-xl border border-[#f3c2c8] bg-[#fff5f6] px-4 py-3 text-xs font-semibold text-[#a43d4b]"
            >
              <AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
              <span>
                Số bộ đề HANDED_OVER ({estimate?.handedOverExamSetCount}) nhỏ hơn sĩ số phòng cần
                phát đề không trùng. Tắt tùy chọn phát đề đồng thời hoặc bổ sung bộ đề HANDED_OVER.
              </span>
            </div>
          ) : null}

          <div className="space-y-2 rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] p-4">
            <p className="text-xs font-bold text-[#0F2854] uppercase">
              Phòng đích tham gia phân bổ ({effectiveShiftIds.length}/{scheduledShifts.length})
            </p>
            {scheduledShifts.length === 0 ? (
              <p className="text-xs text-[#687a91]">
                Chưa có phòng thi nào ở trạng thái scheduled chưa xuất bản đề. Hãy mở thêm phòng ở
                Bước 4.
              </p>
            ) : (
              <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
                {scheduledShifts.map((shift) => {
                  const checked = effectiveShiftIds.includes(shift.id)
                  return (
                    <label
                      key={shift.id}
                      className="flex cursor-pointer items-center gap-2 rounded-lg border border-[#d9e8f2] bg-white px-3 py-2 text-xs font-semibold text-[#0F2854]"
                    >
                      <input
                        type="checkbox"
                        checked={checked}
                        onChange={() => toggleShift(shift.id)}
                      />
                      <span className="truncate">
                        {shift.shiftName} · {shift.roomLab} ({shift.assignedTicketsCount ?? 0}/
                        {shift.maxSeats ?? 24})
                      </span>
                    </label>
                  )
                })}
              </div>
            )}

            <label className="mt-2 inline-flex items-center gap-2 pt-2 text-xs font-semibold text-[#1C4D8D]">
              <input
                type="checkbox"
                checked={assignHandedOverExamSets}
                onChange={(event) => {
                  setAssignHandedOverExamSets(event.target.checked)
                  setDryRunResult(null)
                }}
              />
              Đồng thời phát bộ đề HANDED_OVER không trùng trong từng phòng
            </label>
          </div>

          {dryRunResult ? (
            <div className="space-y-3 rounded-2xl border border-[#82aedb]/55 bg-white p-4">
              <div className="flex flex-wrap items-center justify-between gap-2 text-xs font-semibold text-[#0F2854]">
                <span>
                  Tổng thí sinh hợp lệ: <strong>{dryRunResult.totalEligibleStudents}</strong> · Đã
                  xếp trước đó: <strong>{dryRunResult.previouslyAllocatedCount}</strong> · Dự kiến
                  xếp mới: <strong className="text-[#1C4D8D]">{dryRunResult.newlyAllocatedCount}</strong>
                </span>
                <span className={ds.infoChip}>Bản xem trước Dry-Run</span>
              </div>

              <div className="space-y-3">
                {dryRunResult.shiftGroups.map((group, idx) => (
                  <div
                    key={`${group.startTime}-${idx}`}
                    className="rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] p-3"
                  >
                    <div className="mb-2 flex flex-wrap items-center justify-between gap-2">
                      <span className="text-xs font-bold text-[#0F2854]">
                        Khung ca #{idx + 1} · {group.totalAssignedInGroup} thí sinh
                      </span>
                      {group.isFinalBalancedShift ? (
                        <span className={ds.successChip}>
                          Cân bằng đều ca cuối (chênh &le; 1 SV)
                        </span>
                      ) : null}
                    </div>
                    <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
                      {group.rooms.map((room) => (
                        <div
                          key={room.shiftId}
                          className="rounded-lg border border-[#d6e4f2] bg-white p-2.5 text-xs"
                        >
                          <div className="flex items-center justify-between font-bold text-[#0F2854]">
                            <span>{room.roomLab}</span>
                            <span className="font-mono text-[#1C4D8D]">
                              {room.totalAfter}/{room.maxSeats}
                            </span>
                          </div>
                          <p className="mt-1 text-[11px] text-[#5b6b84]">
                            Trước: {room.assignedBefore} · Xếp mới: +{room.assignedNow}
                          </p>
                        </div>
                      ))}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          ) : null}
        </div>
      </FormDialog>

      {showCommitConfirm && dryRunResult ? (
        <ConfirmDialog
          variant="calm"
          tone="warning"
          eyebrow="XÁC NHẬN CHIA PHÒNG NGẪU NHIÊN"
          title={`Phân bổ ${dryRunResult.newlyAllocatedCount} thí sinh vào phòng thi?`}
          message="Hệ thống sẽ cấp vé SCHEDULED ngẫu nhiên bằng CSPRNG, tự đánh số ghế 1..24 và bảo toàn toàn bộ vé đã phân bổ trước đó."
          confirmLabel="Xác nhận chia phòng"
          cancelLabel="Quay lại"
          pending={committing}
          onConfirm={() => void executeCommit()}
          onCancel={() => !committing && setShowCommitConfirm(false)}
        />
      ) : null}
    </>
  )
}
