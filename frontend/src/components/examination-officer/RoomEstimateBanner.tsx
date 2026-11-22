import { AlertTriangle, Building2, CheckCircle2, FileCheck2, UsersRound } from 'lucide-react'
import type { OfficialExamEnrollmentEstimate } from '@/types/officialExam.types'
import { cn } from '@/lib/utils'

interface RoomEstimateBannerProps {
  estimate: OfficialExamEnrollmentEstimate | null
  loading?: boolean
}

export function RoomEstimateBanner({ estimate, loading = false }: RoomEstimateBannerProps) {
  if (loading && !estimate) {
    return (
      <section className="rounded-2xl border border-[#d9e8f2] bg-white p-4 text-xs font-semibold text-[#5b6b84]">
        Đang tính ước lượng sĩ số thí sinh và đề xuất số phòng thi (chuẩn 24 SV/phòng)…
      </section>
    )
  }

  if (!estimate) return null

  const maxPerRoom = estimate.maxStudentsPerRoom || 24
  const recommendedTotalRooms =
    estimate.recommendedTotalRooms ||
    Math.ceil(estimate.eligibleStudentCount / Math.max(1, maxPerRoom))
  const requiredSetsForRoom = Math.min(maxPerRoom, estimate.unallocatedStudentCount)
  const hasInsufficientExamSets =
    estimate.unallocatedStudentCount > 0 && estimate.handedOverExamSetCount < requiredSetsForRoom
  const missingCandidates = !estimate.hasImportedCandidates && estimate.eligibleStudentCount === 0

  return (
    <section className="space-y-3" aria-label="Ước tính sĩ số và đề xuất phòng thi">
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
        <article className="rounded-2xl border border-[#d9e8f2] bg-white px-4 py-3.5">
          <p className="flex items-center gap-1.5 text-[11px] font-bold tracking-[0.06em] text-[#5b6b84] uppercase">
            <UsersRound className="size-3.5 text-[#1C4D8D]" aria-hidden="true" />
            Thí sinh dự thi ({estimate.examAttemptType})
          </p>
          <strong className="mt-1 block font-mono text-2xl font-black text-[#0F2854]">
            {estimate.eligibleStudentCount}
          </strong>
          <span className="text-[11px] text-[#687a91]">Nguồn: DSSV đã import</span>
        </article>

        <article className="rounded-2xl border border-[#b7e4cf] bg-[#effaf6] px-4 py-3.5">
          <p className="flex items-center gap-1.5 text-[11px] font-bold tracking-[0.06em] text-[#176f58] uppercase">
            <CheckCircle2 className="size-3.5" aria-hidden="true" />
            Đã xếp phòng
          </p>
          <strong className="mt-1 block font-mono text-2xl font-black text-[#176f58]">
            {estimate.allocatedStudentCount}
          </strong>
          <span className="text-[11px] text-[#357a65]">
            {estimate.currentShiftRoomCount} phòng · {estimate.currentTotalCapacity} ghế
          </span>
        </article>

        <article className="rounded-2xl border border-[#d9e8f2] bg-[#F8FBFD] px-4 py-3.5">
          <p className="text-[11px] font-bold tracking-[0.06em] text-[#5b6b84] uppercase">
            Chưa xếp phòng
          </p>
          <strong className="mt-1 block font-mono text-2xl font-black text-[#1C4D8D]">
            {estimate.unallocatedStudentCount}
          </strong>
          <span className="text-[11px] text-[#687a91]">
            Ghế trống hiện có: {estimate.currentAvailableSeats}
          </span>
        </article>

        <article className="rounded-2xl border border-[#82aedb]/60 bg-[#F4FBFD] px-4 py-3.5">
          <p className="flex items-center gap-1.5 text-[11px] font-bold tracking-[0.06em] text-[#1C4D8D] uppercase">
            <Building2 className="size-3.5" aria-hidden="true" />
            Đề xuất phòng (ceil(S/{maxPerRoom}))
          </p>
          <strong className="mt-1 block font-mono text-2xl font-black text-[#0F2854]">
            {recommendedTotalRooms} phòng
          </strong>
          <span className="text-[11px] text-[#52657d]">
            {estimate.recommendedAdditionalRooms > 0
              ? `Cần mở thêm ${estimate.recommendedAdditionalRooms} phòng`
              : 'Đã đủ sức chứa dự kiến'}
          </span>
        </article>

        <article
          className={cn(
            'rounded-2xl border px-4 py-3.5',
            hasInsufficientExamSets
              ? 'border-[#f3c2c8] bg-[#fff5f6]'
              : 'border-[#d9e8f2] bg-white',
          )}
        >
          <p
            className={cn(
              'flex items-center gap-1.5 text-[11px] font-bold tracking-[0.06em] uppercase',
              hasInsufficientExamSets ? 'text-[#a43d4b]' : 'text-[#5b6b84]',
            )}
          >
            <FileCheck2 className="size-3.5" aria-hidden="true" />
            Bộ đề HANDED_OVER
          </p>
          <strong
            className={cn(
              'mt-1 block font-mono text-2xl font-black',
              hasInsufficientExamSets ? 'text-[#a43d4b]' : 'text-[#0F2854]',
            )}
          >
            {estimate.handedOverExamSetCount}
          </strong>
          <span className="text-[11px] text-[#687a91]">
            Tối thiểu {requiredSetsForRoom} bộ đề / phòng
          </span>
        </article>
      </div>

      {hasInsufficientExamSets ? (
        <div
          role="alert"
          className="flex items-start gap-2.5 rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-4 py-3 text-xs font-semibold text-[#a43d4b]"
        >
          <AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
          <p>
            Cảnh báo thiếu bộ đề HANDED_OVER: hiện chỉ có{' '}
            <strong>{estimate.handedOverExamSetCount}</strong> bộ đề, nhỏ hơn mức tối thiểu{' '}
            <strong>{requiredSetsForRoom}</strong> bộ đề cần thiết để phát không trùng trong mỗi
            phòng (<code>uq_tickets_shift_exam_set</code>). Vui lòng đề nghị Trưởng Bộ môn bàn giao
            thêm bộ đề trước khi phân bổ phát đề tự động.
          </p>
        </div>
      ) : null}

      {missingCandidates ? (
        <div
          role="status"
          className="rounded-xl border border-[#f3d19a] bg-[#fff8ed] px-4 py-3 text-xs font-semibold text-[#8a5511]"
        >
          Chưa có thí sinh nào được import vào đợt <strong>{estimate.examAttemptType}</strong>. Hãy
          tải lên file Excel/CSV danh sách MSSV ở Bước 2 để hệ thống tính chính xác số phòng cần mở.
        </div>
      ) : null}
    </section>
  )
}
