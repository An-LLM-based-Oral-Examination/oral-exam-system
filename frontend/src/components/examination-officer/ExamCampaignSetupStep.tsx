import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  ArrowRight,
  BookOpen,
  CalendarPlus,
  CheckCircle2,
  LoaderCircle,
  RefreshCw,
} from 'lucide-react'
import { officialExamService } from '@/services/officialExam.service'
import type { SemesterOption } from '@/types/class.types'
import type {
  ExamAttemptType,
  SemesterEligibleExamCourse,
} from '@/types/officialExam.types'
import { SectionHeader } from '@/design/ui'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { EmptyState } from '@/components/common/EmptyState'
import { toast } from '@/components/common/toastStore'
import { userMessage } from '@/lib/userMessage'

interface ExamCampaignSetupStepProps {
  semesters: SemesterOption[]
  selectedSemesterId: string
  onSemesterChange: (semesterId: string) => void
  onSessionsCreated: () => Promise<void> | void
}

function getTodayDate() {
  return new Date().toISOString().slice(0, 10)
}

export function ExamCampaignSetupStep({
  semesters,
  selectedSemesterId,
  onSemesterChange,
  onSessionsCreated,
}: ExamCampaignSetupStepProps) {
  const [eligibleCourses, setEligibleCourses] = useState<SemesterEligibleExamCourse[]>([])
  const [selectedCourseIds, setSelectedCourseIds] = useState<string[]>([])
  const [examAttemptType, setExamAttemptType] = useState<ExamAttemptType>('FE')
  const [examDate, setExamDate] = useState(getTodayDate)
  const [skipExisting, setSkipExisting] = useState(true)
  const [loading, setLoading] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')

  const activeSemester = useMemo(
    () => semesters.find((item) => item.id === selectedSemesterId),
    [selectedSemesterId, semesters],
  )

  const loadEligibleCourses = useCallback(async () => {
    if (!selectedSemesterId || typeof officialExamService.getEligibleCourses !== 'function') {
      setEligibleCourses([])
      return
    }
    setLoading(true)
    setError('')
    try {
      const items = await officialExamService.getEligibleCourses(selectedSemesterId)
      setEligibleCourses(items)
      setSelectedCourseIds((current) =>
        current.filter((id) => items.some((course) => course.courseId === id)),
      )
    } catch (loadError) {
      setEligibleCourses([])
      setError(userMessage(loadError, 'Không tải được danh sách môn học mở trong học kỳ.'))
    } finally {
      setLoading(false)
    }
  }, [selectedSemesterId])

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadEligibleCourses()
    }, 0)
    return () => window.clearTimeout(timer)
  }, [loadEligibleCourses])

  const toggleCourse = (courseId: string) => {
    setSelectedCourseIds((current) =>
      current.includes(courseId) ? current.filter((id) => id !== courseId) : [...current, courseId],
    )
  }

  const toggleSelectAllEligible = () => {
    const selectableIds = eligibleCourses
      .filter((course) =>
        examAttemptType === 'FE' ? !course.feSession : !course.reSession,
      )
      .map((course) => course.courseId)
    if (selectableIds.length === 0) return
    const allSelected = selectableIds.every((id) => selectedCourseIds.includes(id))
    setSelectedCourseIds(allSelected ? [] : selectableIds)
  }

  const handleBatchCreate = async () => {
    if (!selectedSemesterId || selectedCourseIds.length === 0 || submitting) return
    if (activeSemester?.startDate && activeSemester?.endDate) {
      const minDate = activeSemester.startDate.slice(0, 10)
      const maxDate = activeSemester.endDate.slice(0, 10)
      if (examDate < minDate || examDate > maxDate) {
        setError(`Ngày thi phải nằm trong học kỳ ${activeSemester.code} (${minDate} đến ${maxDate}).`)
        return
      }
    }

    setSubmitting(true)
    setError('')
    setNotice('')
    try {
      const response = await officialExamService.batchCreateSessions(selectedSemesterId, {
        examDate,
        examAttemptType,
        examInputMode: 'VoiceWithTranscriptEdit',
        transcriptBufferSeconds: 60,
        skipExisting,
        courses: selectedCourseIds.map((courseId) => ({
          courseId,
          examAttemptType,
        })),
      })
      const successMessage = `Đã khởi tạo ${response.createdCount} kỳ thi đợt ${examAttemptType} trong học kỳ ${activeSemester?.code ?? ''}.`
      setNotice(successMessage)
      toast.success(successMessage)
      setSelectedCourseIds([])
      await loadEligibleCourses()
      await Promise.resolve(onSessionsCreated())
    } catch (createError) {
      const message = userMessage(createError, 'Không thể khởi tạo hàng loạt kỳ thi chính thức.')
      setError(message)
      toast.error(message)
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <section className={ds.section} aria-labelledby="eo-step-1-heading">
      <SectionHeader
        icon={CalendarPlus}
        title="Bước 1 · Chọn Đợt thi & Môn thi (FE / RE)"
        badge={`${eligibleCourses.length} môn`}
        hint="Chọn học kỳ và đợt thi (FE / RE), hệ thống tự động lấy danh sách môn học để khởi tạo đợt thi và chuyển sang tạo ca thi."
        action={
          <button
            type="button"
            className={ds.outlineButton}
            onClick={() => void loadEligibleCourses()}
            disabled={loading || !selectedSemesterId}
          >
            <RefreshCw className={cn('size-4', loading && 'animate-spin')} /> Làm mới
          </button>
        }
      />

      <div className="space-y-4 p-5">
        <div className="grid gap-3 md:grid-cols-3">
          <label className="grid gap-1.5 text-xs font-bold text-[#52657d]">
            Học kỳ *
            <select
              aria-label="Học kỳ mở kỳ thi"
              className={ds.field}
              value={selectedSemesterId}
              onChange={(event) => onSemesterChange(event.target.value)}
            >
              <option value="">Chọn học kỳ</option>
              {semesters.map((semester) => (
                <option key={semester.id} value={semester.id}>
                  {semester.code} · {semester.name}
                </option>
              ))}
            </select>
          </label>

          <label className="grid gap-1.5 text-xs font-bold text-[#52657d]">
            Đợt thi *
            <select
              aria-label="Đợt thi FE hoặc RE"
              className={ds.field}
              value={examAttemptType}
              onChange={(event) => {
                setExamAttemptType(event.target.value === 'RE' ? 'RE' : 'FE')
                setSelectedCourseIds([])
              }}
            >
              <option value="FE">FE · Thi lần 1 (Final Exam)</option>
              <option value="RE">RE · Thi lại (Retake Exam)</option>
            </select>
          </label>

          <label className="grid gap-1.5 text-xs font-bold text-[#52657d]">
            Ngày thi dự kiến *
            <input
              type="date"
              aria-label="Ngày thi dự kiến"
              className={ds.field}
              value={examDate}
              onChange={(event) => setExamDate(event.target.value)}
            />
          </label>
        </div>

        {(error || notice) && (
          <div
            role={error ? 'alert' : 'status'}
            className={cn(
              'flex flex-wrap items-center justify-between gap-2 rounded-xl border px-4 py-3 text-sm font-semibold',
              error
                ? 'border-[#f3c2c8] bg-[#fdecec] text-[#a43d4b]'
                : 'border-[#b7e4cf] bg-[#effaf6] text-[#176f58]',
            )}
          >
            <span>{error || notice}</span>
            {error && (
              <button
                type="button"
                className={ds.outlineButton}
                onClick={() => void loadEligibleCourses()}
              >
                Thử lại
              </button>
            )}
          </div>
        )}

        {loading ? (
          <div className="flex items-center justify-center gap-2 py-10 text-sm font-semibold text-[#5b6b84]">
            <LoaderCircle className="size-5 animate-spin text-[#1C4D8D]" />
            Đang tải các môn học mở trong học kỳ…
          </div>
        ) : eligibleCourses.length === 0 ? (
          <EmptyState
            compact
            icon={BookOpen}
            title="Chưa có môn học đủ điều kiện trong học kỳ này"
            description="Chọn học kỳ khác hoặc yêu cầu Quản trị viên mở danh mục môn học trong học kỳ."
          />
        ) : (
          <>
            <div className="overflow-x-auto rounded-xl border border-[#d9e8f2]">
              <table className="w-full min-w-[860px] text-left text-sm">
                <thead className="bg-[#F4FBFD] text-xs font-bold text-[#5b6b84] uppercase">
                  <tr>
                    <th className="w-12 px-4 py-3">
                      <input
                        type="checkbox"
                        aria-label="Chọn tất cả môn chưa mở đợt"
                        checked={
                          eligibleCourses.some((course) =>
                            examAttemptType === 'FE' ? !course.feSession : !course.reSession,
                          ) &&
                          eligibleCourses
                            .filter((course) =>
                              examAttemptType === 'FE' ? !course.feSession : !course.reSession,
                            )
                            .every((course) => selectedCourseIds.includes(course.courseId))
                        }
                        onChange={toggleSelectAllEligible}
                      />
                    </th>
                    <th className="px-4 py-3">Môn học</th>
                    <th className="px-4 py-3">Cấu trúc &amp; Bộ đề</th>
                    <th className="px-4 py-3">Follow-up (Trưởng BM)</th>
                    <th className="px-4 py-3">Đợt FE (Lần 1)</th>
                    <th className="px-4 py-3">Đợt RE (Thi lại)</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[#d9e8f2] bg-white">
                  {eligibleCourses.map((course) => {
                    const alreadyHasTarget =
                      examAttemptType === 'FE'
                        ? Boolean(course.feSession)
                        : Boolean(course.reSession)
                    const checked = selectedCourseIds.includes(course.courseId)
                    return (
                      <tr key={course.courseId} className="hover:bg-[#F8FBFD]">
                        <td className="px-4 py-3">
                          <input
                            type="checkbox"
                            aria-label={`Chọn môn ${course.courseCode}`}
                            checked={checked}
                            disabled={alreadyHasTarget}
                            onChange={() => toggleCourse(course.courseId)}
                          />
                        </td>
                        <td className="px-4 py-3">
                          <strong className="block text-[#0F2854]">{course.courseCode}</strong>
                          <span className="text-xs text-[#5b6b84]">{course.courseName}</span>
                        </td>
                        <td className="px-4 py-3 text-xs text-[#52657d]">
                          <div>
                            {course.activeExamStructureName ? (
                              <span className="font-semibold text-[#0F2854]">
                                {course.activeExamStructureName}
                              </span>
                            ) : (
                              <span className={ds.dangerChip}>Thiếu cấu trúc đề</span>
                            )}
                          </div>
                          <span className="mt-1 block text-[#5b6b84]">
                            Bộ đề HANDED_OVER: <strong>{course.handedOverExamSetCount}</strong>
                          </span>
                        </td>
                        <td className="px-4 py-3">
                          <span className={ds.infoChip}>
                            {course.hasFollowUp
                              ? `Bật · tối đa ${course.maxFollowUpQuestions} câu`
                              : 'Tắt (mặc định)'}
                          </span>
                          <span className="mt-1 block text-[11px] text-[#687a91]">
                            Do Trưởng BM cấu hình
                          </span>
                        </td>
                        <td className="px-4 py-3">
                          {course.feSession ? (
                            <Link
                              to={`/examination-officer/exams/${course.feSession.sessionId}`}
                              className="inline-flex items-center gap-1 rounded-lg border border-[#b7e4cf] bg-[#effaf6] px-2.5 py-1 text-xs font-bold text-[#176f58] hover:bg-[#dff4eb]"
                            >
                              <CheckCircle2 className="size-3.5" /> Tạo ca FE ({course.feSession.candidateCount} SV)
                              <ArrowRight className="size-3" />
                            </Link>
                          ) : (
                            <span className="text-xs text-[#7c8ca1]">Chưa mở</span>
                          )}
                        </td>
                        <td className="px-4 py-3">
                          {course.reSession ? (
                            <Link
                              to={`/examination-officer/exams/${course.reSession.sessionId}`}
                              className="inline-flex items-center gap-1 rounded-lg border border-[#82aedb]/55 bg-[#F4FBFD] px-2.5 py-1 text-xs font-bold text-[#1C4D8D] hover:bg-[#e5f2fb]"
                            >
                              <CheckCircle2 className="size-3.5" /> Tạo ca RE ({course.reSession.candidateCount} SV)
                              <ArrowRight className="size-3" />
                            </Link>
                          ) : (
                            <span className="text-xs text-[#7c8ca1]">Chưa mở</span>
                          )}
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>

            <div className="flex flex-wrap items-center justify-between gap-3">
              <label className="inline-flex items-center gap-2 text-xs font-semibold text-[#52657d]">
                <input
                  type="checkbox"
                  checked={skipExisting}
                  onChange={(event) => setSkipExisting(event.target.checked)}
                />
                Bỏ qua môn đã tồn tại đợt {examAttemptType} (tránh lỗi 409)
              </label>
              <button
                type="button"
                disabled={submitting || selectedCourseIds.length === 0}
                onClick={() => void handleBatchCreate()}
                className={ds.primaryButton}
              >
                {submitting ? (
                  <LoaderCircle className="size-4 animate-spin" />
                ) : (
                  <CalendarPlus className="size-4" />
                )}
                Khởi tạo {selectedCourseIds.length} kỳ thi đợt {examAttemptType}
              </button>
            </div>
          </>
        )}
      </div>
    </section>
  )
}
