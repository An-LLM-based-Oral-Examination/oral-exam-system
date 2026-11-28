import { DateField, ClockField } from '@/components/common/DateTimeFields'
import { StatusTabs } from '@/components/common/StatusTabs'
import { SearchField } from '@/components/common/SearchField'
import { useSearchInput } from '@/hooks/useSearchInput'
import { TwoStepProgress } from '@/design/TwoStepProgress'
import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { createPortal } from 'react-dom'
import { Link, useSearchParams } from 'react-router-dom'
import {
  AlertCircle,
  CheckCircle2,
  ChevronRight,
  CircleStop,
  ClipboardList,
  Clock3,
  Layers,
  LoaderCircle,
  Lock,
  Plus,
  Presentation,
  RefreshCw,
  Timer,
  UsersRound,
  X,
} from 'lucide-react'
import { EmptyState } from '@/components/common/EmptyState'
import { FieldError } from '@/components/common/formField'
import { fieldInvalidClass, fieldLabelClass } from '@/components/common/formFieldClasses'
import { toast } from '@/components/common/toastStore'
import { FilterDropMenu } from '@/components/common/FilterDropMenu'
import { ConfirmDialog } from '@/design/ConfirmDialog'
import { DialogFooter, FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'
import { scheduleUi } from '@/lib/scheduleUi'
import { cn } from '@/lib/utils'
import { userMessage } from '@/lib/userMessage'
import { inClassShiftService } from '@/services/inClassShift.service'
import { InClassShiftProjectorDialog } from '@/pages/lecturer/InClassShiftProjectorDialog'
import {
  IN_CLASS_DIFFICULTIES,
  evaluateInClassPoolSelection,
  formatDifficultyListLabel,
  formatPoolModeLabel,
  sumCountsForDifficulties,
} from '@/utils/inClassShiftPool'
import type {
  CreateInClassShiftRequest,
  InClassCreatePoolMode,
  InClassDifficulty,
  InClassShift,
  InClassShiftPoolStats,
  InClassShiftStatus,
  LecturerClassOption,
} from '@/types/inClassShift.types'

type StatusFilter = 'ALL' | InClassShiftStatus

const STATUS_FILTERS: Array<{
  value: StatusFilter
  label: string
  countIdleClass: string
  dotClass: string
}> = [
  {
    value: 'ALL',
    label: 'Tất cả',
    countIdleClass: 'bg-[#e8eef6] text-[#1C4D8D]',
    dotClass: 'bg-[#1C4D8D]',
  },
  {
    value: 'SCHEDULED',
    label: 'Chờ kích hoạt',
    countIdleClass: 'bg-[#fff4df] text-[#8d6118]',
    dotClass: 'bg-[#d4a017]',
  },
  {
    value: 'ACTIVE',
    label: 'Đang diễn ra',
    countIdleClass: 'bg-[#e8f7f1] text-[#197158]',
    dotClass: 'bg-[#1a9a72]',
  },
  {
    value: 'CLOSED',
    label: 'Đã kết thúc',
    countIdleClass: 'bg-[#fff4df] text-[#8d6118]',
    dotClass: 'bg-[#d4a017]',
  },
  {
    value: 'FINALIZED',
    label: 'Đã chốt',
    countIdleClass: 'bg-[#eef1f5] text-[#52647d]',
    dotClass: 'bg-[#7a8aa0]',
  },
]

function getErrorMessage(error: unknown) {
  return userMessage(error, 'Không tải được dữ liệu ca thi trên lớp. Vui lòng thử lại.')
}

function statusChip(status: InClassShiftStatus) {
  if (status === 'SCHEDULED') {
    return (
      <span className={ds.infoChip}>
        <Clock3 className="size-3" />
        Chờ kích hoạt
      </span>
    )
  }
  if (status === 'ACTIVE') {
    return (
      <span className={ds.successChip}>
        <span className="size-1.5 rounded-full bg-[#16836a]" />
        Đang mở
      </span>
    )
  }
  if (status === 'FINALIZED') {
    return (
      <span className={cn(ds.infoChip, 'border-[#82aedb]/55 bg-[#f3f6fa] text-[#5b6b84]')}>
        <Lock className="size-3" />
        Đã chốt
      </span>
    )
  }
  return <span className={ds.infoChip}>Đã kết thúc</span>
}

function pad2(value: number) {
  return String(value).padStart(2, '0')
}

function toDateKey(date: Date) {
  return `${date.getFullYear()}-${pad2(date.getMonth() + 1)}-${pad2(date.getDate())}`
}

function toClock(date: Date) {
  return `${pad2(date.getHours())}:${pad2(date.getMinutes())}`
}

/** ISO local có offset — backend so sánh UTC không bị lệch múi giờ. */
function toOffsetIso(dateKey: string, clock: string) {
  const [year, month, day] = dateKey.split('-').map(Number)
  const [hour, minute] = clock.split(':').map(Number)
  const local = new Date(year, (month || 1) - 1, day || 1, hour || 0, minute || 0, 0, 0)
  if (Number.isNaN(local.getTime())) return ''
  const offsetMinutes = -local.getTimezoneOffset()
  const sign = offsetMinutes >= 0 ? '+' : '-'
  const abs = Math.abs(offsetMinutes)
  return `${dateKey}T${pad2(hour || 0)}:${pad2(minute || 0)}:00${sign}${pad2(Math.floor(abs / 60))}:${pad2(abs % 60)}`
}

function formatShiftPoint(value?: string | null) {
  if (!value) return null
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? null : date
}

function formatShiftDate(date: Date) {
  return date.toLocaleDateString('vi-VN')
}

function formatShiftClock(date: Date) {
  return date.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })
}

function formatDuration(minutes: number) {
  if (minutes <= 0) return '0 phút'
  const hours = Math.floor(minutes / 60)
  const rest = minutes % 60
  if (hours === 0) return `${rest} phút`
  if (rest === 0) return `${hours} giờ`
  return `${hours} giờ ${rest} phút`
}

type FieldErrors = Partial<
  Record<
    | 'classId'
    | 'title'
    | 'startDateKey'
    | 'endDateKey'
    | 'time'
    | 'examMinutes'
    | 'questionCount'
    | 'difficulties'
    | 'pool',
    string
  >
>

interface ProblemAlertState {
  title: string
  detail: string
  fieldMessages: string[]
}

function parseProblemAlert(error: unknown): ProblemAlertState {
  if (typeof error === 'object' && error !== null) {
    const rec = error as Record<string, unknown>
    const title =
      typeof rec.title === 'string' && rec.title.trim()
        ? rec.title.trim()
        : 'Không thể tạo ca kiểm tra'
    const detail = userMessage(error, 'Dữ liệu cấu hình ca chưa hợp lệ. Vui lòng kiểm tra lại.')
    const fieldMessages: string[] = []
    if (typeof rec.errors === 'object' && rec.errors !== null && !Array.isArray(rec.errors)) {
      for (const value of Object.values(rec.errors as Record<string, unknown>)) {
        if (Array.isArray(value)) {
          for (const msg of value) {
            if (typeof msg === 'string' && msg.trim()) fieldMessages.push(msg.trim())
          }
        }
      }
    }
    return { title, detail, fieldMessages }
  }
  return {
    title: 'Không thể tạo ca kiểm tra',
    detail: userMessage(error, 'Vui lòng kiểm tra kết nối và thử lại.'),
    fieldMessages: [],
  }
}

function CreateInClassShiftDialog({
  open,
  classes,
  fixedClassId,
  onClose,
  onCreated,
}: {
  open: boolean
  classes: LecturerClassOption[]
  fixedClassId?: string
  onClose: () => void
  onCreated: (shift: InClassShift) => void
}) {
  const [step, setStep] = useState<1 | 2>(1)
  const [classId, setClassId] = useState('')
  const [title, setTitle] = useState('')
  const [startDateKey, setStartDateKey] = useState('')
  const [endDateKey, setEndDateKey] = useState('')
  const [startClock, setStartClock] = useState('')
  const [endClock, setEndClock] = useState('')
  const [examMinutes, setExamMinutes] = useState(20)
  const [description, setDescription] = useState('')
  const [questionCountInput, setQuestionCountInput] = useState('2')
  const [hasFollowUp, setHasFollowUp] = useState(false)
  const [maxFollowUpQuestions, setMaxFollowUpQuestions] = useState(2)
  const [poolMode, setPoolMode] = useState<InClassCreatePoolMode>('ALL_COURSE_BANK')
  const [selectedDifficulties, setSelectedDifficulties] = useState<InClassDifficulty[]>([
    'easy',
    'medium',
    'hard',
  ])
  const [selectedCloIds, setSelectedCloIds] = useState<string[]>([])
  const [cloExpanded, setCloExpanded] = useState(false)
  const [draftCloIds, setDraftCloIds] = useState<string[]>([])
  const [poolStats, setPoolStats] = useState<InClassShiftPoolStats | null>(null)
  const [poolLoading, setPoolLoading] = useState(false)
  const [poolLoadError, setPoolLoadError] = useState<ProblemAlertState | null>(null)
  const [poolReloadTick, setPoolReloadTick] = useState(0)
  const [submitProblem, setSubmitProblem] = useState<ProblemAlertState | null>(null)
  const [busy, setBusy] = useState(false)
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})

  useEffect(() => {
    if (!open) return
    const start = new Date()
    const end = new Date(start.getTime() + 2 * 60 * 60_000)
    return scheduleUi(() => {
      setStep(1)
      setClassId(fixedClassId || classes[0]?.id || '')
      setTitle('')
      setStartDateKey(toDateKey(start))
      setEndDateKey(toDateKey(end))
      setStartClock(toClock(start))
      setEndClock(toClock(end))
      setExamMinutes(20)
      setDescription('')
      setQuestionCountInput('2')
      setHasFollowUp(false)
      setCloExpanded(false)
      setMaxFollowUpQuestions(2)
      setPoolMode('ALL_COURSE_BANK')
      setSelectedDifficulties(['easy', 'medium', 'hard'])
      setSelectedCloIds([])
      setPoolStats(null)
      setPoolLoadError(null)
      setSubmitProblem(null)
      setFieldErrors({})
      setBusy(false)
    })
  }, [open, classes, fixedClassId])

  const selectedClass = useMemo(
    () => classes.find((item) => item.id === classId) ?? null,
    [classes, classId],
  )
  const selectedCourseId = selectedClass?.courseId

  useEffect(() => {
    if (!open || !classId) {
      return scheduleUi(() => {
        setPoolStats(null)
        setPoolLoadError(null)
        setPoolLoading(false)
      })
    }
    let cancelled = false
    const cancelStart = scheduleUi(() => {
      setPoolLoading(true)
      setPoolLoadError(null)
      void inClassShiftService
        .getPoolStats(classId, selectedCourseId)
        .then((stats) => {
          if (cancelled) return
          setPoolStats(stats)
          const validCloSet = new Set(stats.clos.map((item) => item.cloId))
          setSelectedCloIds((current) => current.filter((id) => validCloSet.has(id)))
          const nonZeroDiffs = IN_CLASS_DIFFICULTIES.map((item) => item.value).filter(
            (diff) => (stats.courseBankCounts[diff] || 0) > 0,
          )
          if (nonZeroDiffs.length > 0) {
            setSelectedDifficulties((current) => {
              const filtered = current.filter((diff) => (stats.courseBankCounts[diff] || 0) > 0)
              return filtered.length > 0 ? filtered : nonZeroDiffs
            })
          }
        })
        .catch((loadErr) => {
          if (cancelled) return
          setPoolStats(null)
          setPoolLoadError(parseProblemAlert(loadErr))
        })
        .finally(() => {
          if (!cancelled) setPoolLoading(false)
        })
    })
    return () => {
      cancelled = true
      cancelStart()
    }
  }, [open, classId, selectedCourseId, poolReloadTick])

  const parsedQuestionCount = useMemo(() => {
    const trimmed = questionCountInput.trim()
    if (!trimmed) return Number.NaN
    const num = Number(trimmed)
    return Number.isFinite(num) ? num : Number.NaN
  }, [questionCountInput])

  const poolEvaluation = useMemo(
    () =>
      evaluateInClassPoolSelection({
        poolStats,
        poolMode,
        totalQuestionsPerStudent: parsedQuestionCount,
        selectedDifficulties,
        selectedCloIds,
      }),
    [poolStats, poolMode, parsedQuestionCount, selectedDifficulties, selectedCloIds],
  )

  const difficultySourceCounts = useMemo(() => {
    if (!poolStats) return { easy: 0, medium: 0, hard: 0 }
    if (poolMode === 'ALL_COURSE_BANK') {
      return {
        easy: poolStats.courseBankCounts.easy,
        medium: poolStats.courseBankCounts.medium,
        hard: poolStats.courseBankCounts.hard,
      }
    }
    const chosen = poolStats.clos.filter((clo) => selectedCloIds.includes(clo.cloId))
    return chosen.reduce(
      (acc, clo) => ({
        easy: acc.easy + (clo.counts.easy || 0),
        medium: acc.medium + (clo.counts.medium || 0),
        hard: acc.hard + (clo.counts.hard || 0),
      }),
      { easy: 0, medium: 0, hard: 0 },
    )
  }, [poolStats, poolMode, selectedCloIds])

  if (!open) return null

  const classOptions = classes.map((item) => ({
    value: item.id,
    label: item.courseCode ? `${item.name} (${item.courseCode})` : item.name,
  }))
  const startTime = startDateKey && startClock ? `${startDateKey}T${startClock}` : ''
  const endTime = endDateKey && endClock ? `${endDateKey}T${endClock}` : ''
  const startDate = startTime ? new Date(startTime) : null
  const endDate = endTime ? new Date(endTime) : null
  const timeValid = Boolean(
    startDate &&
      endDate &&
      !Number.isNaN(startDate.getTime()) &&
      !Number.isNaN(endDate.getTime()) &&
      endDate.getTime() > startDate.getTime(),
  )
  const windowMinutes =
    timeValid && startDate && endDate
      ? Math.round((endDate.getTime() - startDate.getTime()) / 60_000)
      : 0
  const examMinutesValid =
    Number.isInteger(examMinutes) && examMinutes >= 1 && examMinutes <= 60 && (windowMinutes === 0 || examMinutes <= windowMinutes)

  const clearFieldError = (...keys: Array<keyof FieldErrors>) => {
    setSubmitProblem(null)
    setFieldErrors((current) => {
      if (!keys.some((key) => current[key])) return current
      const next = { ...current }
      for (const key of keys) delete next[key]
      return next
    })
  }

  const validateStep1 = (): FieldErrors => {
    const next: FieldErrors = {}
    const trimmedTitle = title.trim()
    if (!classId) next.classId = 'Vui lòng chọn lớp học phần.'
    if (!trimmedTitle) next.title = 'Vui lòng nhập tên ca kiểm tra.'
    else if (trimmedTitle.length > 150) next.title = 'Tên ca tối đa 150 ký tự.'
    if (!startDateKey) next.startDateKey = 'Vui lòng chọn ngày mở ca.'
    if (!endDateKey) next.endDateKey = 'Vui lòng chọn ngày đóng ca.'
    if (!startClock || !endClock) next.time = 'Vui lòng chọn giờ mở và giờ đóng ca.'
    else if (startDateKey && endDateKey && !timeValid) {
      next.time = 'Thời điểm đóng ca phải sau thời điểm mở ca.'
    }
    return next
  }

  const validateStep2 = (): FieldErrors => {
    const next: FieldErrors = {}
    if (!Number.isInteger(examMinutes) || examMinutes < 1 || examMinutes > 60) {
      next.examMinutes = 'Thời gian phải từ 1 đến 60 phút.'
    } else if (windowMinutes > 0 && examMinutes > windowMinutes) {
      next.examMinutes = `Thời gian không được dài hơn khoảng ca đang mở (${formatDuration(windowMinutes)}).`
    }
    if (
      !Number.isInteger(parsedQuestionCount) ||
      parsedQuestionCount < 1 ||
      parsedQuestionCount > 10
    ) {
      next.questionCount = 'Số câu mỗi đề phải là số nguyên từ 1 đến 10.'
    }
    if (selectedDifficulties.length === 0) {
      next.difficulties = 'Vui lòng chọn ít nhất một mức độ câu hỏi (Dễ, Trung bình hoặc Khó).'
    }
    if (poolLoadError) {
      next.pool = 'Chưa tải được thống kê ngân hàng câu hỏi của môn. Hãy tải lại trước khi tạo ca.'
    } else if (!poolStats) {
      next.pool = 'Đang kiểm tra ngân hàng câu hỏi hợp lệ. Vui lòng đợi trong giây lát.'
    } else if (!poolEvaluation.isValid && poolEvaluation.errors.length > 0) {
      const poolSpecific = poolEvaluation.errors.find(
        (msg) =>
          !msg.startsWith('Số câu mỗi sinh viên') && !msg.startsWith('Vui lòng chọn ít nhất một mức độ'),
      )
      if (poolSpecific) next.pool = poolSpecific
    }
    return next
  }

  const goNext = () => {
    if (busy) return
    const next = validateStep1()
    setFieldErrors(next)
    if (Object.keys(next).length > 0) return
    setStep(2)
  }

  const goBack = () => {
    if (busy) return
    setFieldErrors({})
    setSubmitProblem(null)
    setStep(1)
  }

  const toggleDifficulty = (diff: InClassDifficulty) => {
    setSelectedDifficulties((current) => {
      const exists = current.includes(diff)
      const updated = exists ? current.filter((item) => item !== diff) : [...current, diff]
      return IN_CLASS_DIFFICULTIES.map((item) => item.value).filter((val) => updated.includes(val))
    })
    clearFieldError('difficulties', 'pool')
  }

  const toggleDraftClo = (cloId: string) => {
    setDraftCloIds((current) => current.includes(cloId) ? current.filter((id) => id !== cloId) : [...current, cloId])
  }
  const openCloPicker = () => {
    setDraftCloIds([...selectedCloIds])
    setCloExpanded(true)
  }
  const confirmCloSelection = () => {
    setSelectedCloIds(poolStats?.clos.filter((clo) => draftCloIds.includes(clo.cloId)).map((clo) => clo.cloId) ?? [])
    clearFieldError('pool')
    setCloExpanded(false)
  }



  const createShift = async () => {
    if (step !== 2 || busy) return
    const step1Errors = validateStep1()
    if (Object.keys(step1Errors).length > 0) {
      setFieldErrors(step1Errors)
      setStep(1)
      return
    }
    const step2Errors = validateStep2()
    if (Object.keys(step2Errors).length > 0) {
      setFieldErrors(step2Errors)
      return
    }
    if (!startDateKey || !endDateKey || !startClock || !endClock) return
    const startIso = toOffsetIso(startDateKey, startClock)
    const endIso = toOffsetIso(endDateKey, endClock)
    const startMs = startIso ? new Date(startIso).getTime() : NaN
    const endMs = endIso ? new Date(endIso).getTime() : NaN
    if (!startIso || !endIso || Number.isNaN(startMs) || Number.isNaN(endMs)) {
      setFieldErrors({ time: 'Khung giờ ca không hợp lệ. Hãy chọn lại ngày và giờ.' })
      setStep(1)
      return
    }
    if (endMs <= Date.now()) {
      setFieldErrors({
        time: 'Giờ đóng ca đã qua. Hãy chọn lại khung giờ (cửa sổ ca phải còn ở tương lai).',
      })
      setStep(1)
      return
    }
    if (endMs - startMs < examMinutes * 60_000) {
      setFieldErrors({
        time: `Khung giờ ca phải dài hơn hoặc bằng thời gian làm bài (${examMinutes} phút).`,
      })
      setStep(1)
      return
    }

    setBusy(true)
    setFieldErrors({})
    setSubmitProblem(null)

    const orderedDifficulties = IN_CLASS_DIFFICULTIES.map((item) => item.value).filter((diff) =>
      selectedDifficulties.includes(diff),
    )

    const payload: CreateInClassShiftRequest = {
      classId,
      shiftName: title.trim(),
      startTime: startIso,
      endTime: endIso,
      description: description.trim(),
      questionPoolMode: poolMode,
      totalQuestionsPerStudent: parsedQuestionCount,
      selectedDifficulties: orderedDifficulties,
      ...(poolMode === 'SELECTED_CLOS' ? { selectedCloIds } : {}),
      hasFollowUp,
      maxFollowUpQuestions: hasFollowUp ? maxFollowUpQuestions : 0,
      isAutoPublishGrades: false,
      examDurationMinutes: examMinutes,
    }

    try {
      const shift = await inClassShiftService.createShift(payload)
      onCreated(shift)
    } catch (createError) {
      const problem = parseProblemAlert(createError)
      setSubmitProblem(problem)
      toast.error(problem.detail)
      setBusy(false)
    }
  }

  const onFormSubmit = (event: FormEvent) => {
    event.preventDefault()
    if (step === 1) {
      goNext()
      return
    }
    void createShift()
  }

  const steps = [
    { id: 1 as const, title: fixedClassId ? 'Thông tin và thời gian' : 'Lớp và thời gian' },
    { id: 2 as const, title: 'Cấu hình đề' },
  ]

  const canSubmitStep2 =
    !busy &&
    !poolLoading &&
    !poolLoadError &&
    Boolean(poolStats) &&
    poolEvaluation.isValid &&
    examMinutesValid

  const questionCountInvalid =
    Boolean(fieldErrors.questionCount) ||
    !Number.isInteger(parsedQuestionCount) ||
    parsedQuestionCount < 1 ||
    parsedQuestionCount > 10

  const draftCloEvaluation = evaluateInClassPoolSelection({
    poolStats, poolMode: 'SELECTED_CLOS', totalQuestionsPerStudent: parsedQuestionCount,
    selectedDifficulties, selectedCloIds: draftCloIds,
  })
  const missingCloDifficulties = draftCloEvaluation.difficultyPreview
    .filter((item) => item.availableCount === 0)
    .map((item) => item.label)
  const emptySelectedClos = draftCloEvaluation.cloPreview
    .filter((item) => item.availableInSelectedDifficulties === 0)
    .map((item) => item.cloCode)
  const uniqueDraftCloCount = new Set(draftCloIds).size
  const cloSelectionWarning = draftCloEvaluation.isValid
    ? ''
    : uniqueDraftCloCount === 0
      ? 'Vui lòng chọn ít nhất một CLO.'
      : selectedDifficulties.length === 0
        ? 'Vui lòng chọn độ khó trong cấu hình đề trước.'
        : questionCountInvalid
          ? 'Số câu mỗi đề phải từ 1 đến 10.'
          : [
              uniqueDraftCloCount > parsedQuestionCount
                ? `Số CLO đã chọn (${uniqueDraftCloCount}) không được lớn hơn số câu hỏi mỗi sinh viên (${parsedQuestionCount})`
                : '',
              draftCloEvaluation.totalAvailable < parsedQuestionCount
                ? `Chỉ có ${draftCloEvaluation.totalAvailable}/${parsedQuestionCount} câu phù hợp`
                : '',
              missingCloDifficulties.length > 0
                ? `thiếu câu mức ${missingCloDifficulties.join(', ')}`
                : emptySelectedClos.length > 0
                  ? `${emptySelectedClos.join(', ')} chưa có câu phù hợp`
                  : '',
            ].filter(Boolean).join('; ').replace(/^./, (letter) => letter.toUpperCase()) + '.'
  const renderCloList = () => (
    <ul className="space-y-2 p-3.5">
      {poolStats?.clos.map((clo) => {
        const checked = draftCloIds.includes(clo.cloId)
        const availableQuestions = sumCountsForDifficulties(clo.counts, selectedDifficulties)
        return (
          <li key={clo.cloId}>
            <label className={cn(
              'relative flex cursor-pointer items-start gap-2.5 overflow-hidden rounded-xl border px-3.5 py-3 transition motion-reduce:transition-none',
              checked
                ? 'border-[#4988C4] bg-[#EDF7FC]'
                : 'border-[#82aedb]/45 bg-white hover:border-[#4988C4] hover:bg-[#F8FBFD]',
            )}>
              <input
                type="checkbox"
                checked={checked}
                onChange={() => toggleDraftClo(clo.cloId)}
                className="mt-1 size-4 shrink-0 rounded border-[#4988C4] accent-[#1C4D8D] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]"
              />
              <div className="min-w-0 flex-1">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <span className="inline-flex rounded-lg border border-[#82aedb]/60 bg-white px-2 py-0.5 font-mono text-xs font-bold text-[#1C4D8D]">
                    {clo.cloCode}
                  </span>
                  <span className={cn(
                    'inline-flex items-center gap-1.5 whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-semibold tabular-nums',
                    availableQuestions > 0 ? 'bg-[#E8F8F3] text-[#16836a]' : 'bg-[#FFF1F2] text-[#b23c50]',
                  )}>
                    <span aria-hidden="true" className="size-1.5 rounded-full bg-current" />
                    {availableQuestions} câu
                  </span>
                </div>
                <p className="mt-1.5 text-sm leading-6 wrap-anywhere text-[#0F2854]">
                  {clo.description || 'Chưa có mô tả chi tiết cho CLO này.'}
                </p>
              </div>
            </label>
          </li>
        )
      })}
    </ul>
  )

  return (
    <FormDialog
      titleId="create-inclass-title"
      eyebrow={selectedClass?.name || 'Lớp học phần'}
      title="Tạo ca kiểm tra"
      icon={ClipboardList}
      size="md"
      onClose={onClose}
      busy={busy}
      beforeBody={
        <TwoStepProgress
          label="Tiến độ tạo ca"
          step={step}
          steps={steps}
          disabled={busy}
          onStepChange={(target) => (target === 1 ? goBack() : goNext())}
        />
      }
      footer={
        <DialogFooter className="bg-white sm:items-center sm:justify-between">
          <button
            type="button"
            disabled={busy}
            onClick={onClose}
            className={cn(ds.outlineButton, 'min-h-10')}
          >
            Hủy
          </button>
          <div className="flex flex-col-reverse gap-2 sm:flex-row">
            {step === 2 && (
              <button
                type="button"
                disabled={busy}
                onClick={goBack}
                className={cn(ds.outlineButton, 'min-h-10')}
              >
                Quay lại
              </button>
            )}
            {step === 1 ? (
              <button
                key="create-shift-next"
                type="button"
                disabled={busy}
                onClick={goNext}
                className={cn(ds.primaryButton, 'min-h-10')}
              >
                Tiếp tục
                <ChevronRight className="size-4" />
              </button>
            ) : (
              <button
                key="create-shift-submit"
                type="button"
                disabled={!canSubmitStep2}
                onClick={() => void createShift()}
                className={cn(ds.primaryButton, 'min-h-10 disabled:cursor-not-allowed disabled:opacity-50')}
              >
                {busy ? (
                  <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" />
                ) : (
                  <Plus className="size-3.5" />
                )}
                {busy ? 'Đang tạo...' : 'Tạo ca'}
              </button>
            )}
          </div>
        </DialogFooter>
      }
    >
      {cloExpanded && createPortal(
        <FormDialog titleId="expanded-clo-title" title="Chọn CLO" eyebrow={selectedClass?.courseCode || 'Cấu hình đề'} icon={Layers}
          size="lg" nested maxHeight="max-h-[min(90dvh,760px)]" bodyClassName="bg-[#F8FBFD] p-0" onClose={() => setCloExpanded(false)}
          headerExtra={<span className={cn(ds.sectionBadge, 'shrink-0 whitespace-nowrap', draftCloIds.length === 0 && 'border-[#efc8cc] bg-[#fff2f3] text-[#a93d48]')}>{draftCloIds.length}/{poolStats?.clos.length ?? 0} CLO</span>}
          beforeBody={<div className="shrink-0 space-y-1 border-b border-[#82aedb]/55 bg-[#F4FBFD] p-3.5">
            <p className="text-sm font-semibold text-[#0F2854]">Chọn CLO để giới hạn phạm vi câu hỏi của đề.</p>
            {cloSelectionWarning && <div role="alert"><FieldError message={cloSelectionWarning} /></div>}
          </div>}
          footer={<DialogFooter className="bg-white">
            <button type="button" className={ds.outlineButton} onClick={() => setCloExpanded(false)}>Hủy</button>
            <button type="button" className={ds.primaryButton} disabled={!draftCloEvaluation.isValid} onClick={confirmCloSelection}>Xác nhận lựa chọn</button>
          </DialogFooter>}>
          {renderCloList()}
        </FormDialog>, document.body)}
      <form id="create-inclass-form" onSubmit={onFormSubmit} className="space-y-3">
        {step === 1 ? (
          <section className="space-y-3.5">
            {fixedClassId ? null : (
              <div>
                <span className={fieldLabelClass(Boolean(fieldErrors.classId))}>Lớp học phần</span>
                <FilterDropMenu
                  label="Chọn lớp"
                  value={classId}
                  onChange={(value) => {
                    setClassId(value)
                    setSelectedCloIds([])
                    clearFieldError('classId', 'pool')
                  }}
                  options={classOptions}
                  emptyText="Chưa có lớp để chọn"
                  className="mt-1.5 w-full"
                  invalid={Boolean(fieldErrors.classId)}
                />
                <FieldError message={fieldErrors.classId} />
              </div>
            )}
            <label className="block">
              <span className={fieldLabelClass(Boolean(fieldErrors.title))}>Tên ca kiểm tra</span>
              <input
                value={title}
                onChange={(event) => {
                  setTitle(event.target.value)
                  clearFieldError('title')
                }}
                maxLength={150}
                placeholder="Ví dụ: Kiểm tra tiến độ 1"
                aria-invalid={Boolean(fieldErrors.title)}
                className={cn(ds.field, 'mt-1.5', fieldErrors.title && fieldInvalidClass)}
                autoFocus
              />
              <FieldError message={fieldErrors.title} />
            </label>
            <label className="block">
              <span className="text-xs font-bold tracking-[0.04em] text-[#5b6b84] uppercase">
                Mô tả ca (tùy chọn)
              </span>
              <textarea
                value={description}
                onChange={(event) => setDescription(event.target.value)}
                rows={2}
                placeholder="Ví dụ: Đánh giá chuẩn đầu ra giai đoạn giữa kỳ"
                className="mt-1.5 min-h-16 w-full resize-y rounded-xl border border-slate-300 bg-white px-3 py-2.5 text-sm font-medium text-[#0F2854] outline-none placeholder:text-slate-400 focus:border-[#1C4D8D] focus:ring-2 focus:ring-[#1C4D8D]/20"
              />
            </label>
            <div className="space-y-3">
              <div className="grid min-w-0 gap-3 sm:grid-cols-[minmax(0,3fr)_minmax(0,2fr)]">
                <div>
                  <DateField
                    label="Ngày mở ca"
                    value={startDateKey}
                    invalid={Boolean(fieldErrors.startDateKey || fieldErrors.time)}
                    onChange={(value) => {
                      setStartDateKey(value)
                      clearFieldError('startDateKey', 'time', 'examMinutes')
                    }}
                  />
                  <FieldError message={fieldErrors.startDateKey} />
                </div>
                <ClockField
                  label="Giờ mở ca"
                  value={startClock}
                  invalid={Boolean(fieldErrors.time)}
                  onChange={(value) => {
                    setStartClock(value)
                    clearFieldError('time', 'examMinutes')
                  }}
                />
              </div>
              <div className="grid min-w-0 gap-3 sm:grid-cols-[minmax(0,3fr)_minmax(0,2fr)]">
                <div>
                  <DateField
                    label="Ngày đóng ca"
                    value={endDateKey}
                    invalid={Boolean(fieldErrors.endDateKey || fieldErrors.time)}
                    onChange={(value) => {
                      setEndDateKey(value)
                      clearFieldError('endDateKey', 'time', 'examMinutes')
                    }}
                  />
                  <FieldError message={fieldErrors.endDateKey} />
                </div>
                <ClockField
                  label="Giờ đóng ca"
                  value={endClock}
                  invalid={Boolean(fieldErrors.time)}
                  onChange={(value) => {
                    setEndClock(value)
                    clearFieldError('time', 'examMinutes')
                  }}
                />
              </div>
              <FieldError message={fieldErrors.time} />
            </div>
          </section>
        ) : (
          <div className="space-y-3">
            <div className="flex items-center gap-3 rounded-2xl border border-[#82aedb]/55 bg-[#F4FBFD] px-3.5 py-2.5">
              <span className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-[#1C4D8D]/10 text-[#1C4D8D]">
                <UsersRound className="size-4" aria-hidden="true" />
              </span>
              <div className="min-w-0">
                <p className="truncate text-sm font-bold text-[#0F2854]">
                  {title.trim() || 'Chưa đặt tên'}
                </p>
                <p className="truncate text-xs font-medium text-slate-600">
                  {selectedClass?.name || 'Chưa chọn lớp'}
                  {selectedClass?.courseCode ? ` · ${selectedClass.courseCode}` : ''}
                  {timeValid
                    ? ` · ${startClock} ${startDateKey.split('-').reverse().join('/')} – ${endClock} ${endDateKey.split('-').reverse().join('/')}`
                    : ''}
                </p>
              </div>
            </div>

            {submitProblem && (
              <div
                role="alert"
                className="rounded-2xl border border-rose-200 bg-rose-50 px-3.5 py-3 text-sm text-rose-900"
              >
                <p className="flex items-center gap-1.5 font-bold text-rose-800">
                  <AlertCircle className="size-4 shrink-0" />
                  {submitProblem.title}
                </p>
                <p className="mt-1 text-xs leading-relaxed text-rose-800">{submitProblem.detail}</p>
                {submitProblem.fieldMessages.length > 0 && (
                  <ul className="mt-1.5 list-disc space-y-0.5 pl-4 text-xs text-rose-800">
                    {submitProblem.fieldMessages.map((msg, idx) => (
                      <li key={idx}>{msg}</li>
                    ))}
                  </ul>
                )}
              </div>
            )}

            <section className="space-y-3 rounded-2xl border border-[#82aedb]/55 bg-white p-4">
              <div className="grid gap-3 sm:grid-cols-2">
                <div>
                  <div className="flex items-center justify-between gap-3">
                  <label
                    htmlFor="shift-exam-minutes"
                    className={fieldLabelClass(Boolean(fieldErrors.examMinutes))}
                  >
                    Thời gian (phút) <span className="text-rose-600">*</span>
                  </label>
                  <input
                    id="shift-exam-minutes"
                    type="number"
                    min={1}
                    max={60}
                    step={1}
                    disabled={busy}
                    value={examMinutes}
                    aria-invalid={Boolean(fieldErrors.examMinutes) || undefined}
                    onChange={(event) => {
                      setExamMinutes(Number(event.target.value))
                      clearFieldError('examMinutes')
                    }}
                    className={cn(ds.field, 'h-10 w-16 shrink-0 px-2 text-center tabular-nums', fieldErrors.examMinutes && fieldInvalidClass)}
                  />
                  </div>
                  <input type="range" min={1} max={60} step={1} disabled={busy}
                    aria-label="Thời gian thanh trượt"
                    value={Math.min(60, Math.max(1, examMinutes || 1))}
                    onChange={(event) => { setExamMinutes(Number(event.target.value)); clearFieldError('examMinutes') }}
                    className="mt-3 h-2 w-full cursor-pointer accent-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-45" />
                  <FieldError message={fieldErrors.examMinutes} />
                </div>
                <div>
                  <div className="flex items-center justify-between gap-3">
                  <label
                    htmlFor="total-questions-per-student"
                    className={fieldLabelClass(questionCountInvalid)}
                  >
                    Số câu mỗi đề <span className="text-rose-600">*</span>
                  </label>
                  <input
                    id="total-questions-per-student"
                    type="number"
                    min={1}
                    max={10}
                    step={1}
                    disabled={busy}
                    value={questionCountInput}
                    aria-label="Số câu mỗi đề"
                    aria-invalid={questionCountInvalid || undefined}
                    onChange={(event) => {
                      setQuestionCountInput(event.target.value)
                      clearFieldError('questionCount', 'pool')
                    }}
                    className={cn(ds.field, 'h-10 w-16 shrink-0 px-2 text-center tabular-nums', questionCountInvalid && fieldInvalidClass)}
                  />
                  </div>
                  <input type="range" min={1} max={10} step={1} disabled={busy}
                    aria-label="Số câu mỗi đề thanh trượt"
                    value={Math.min(10, Math.max(1, Number(questionCountInput) || 1))}
                    onChange={(event) => { setQuestionCountInput(event.target.value); clearFieldError('questionCount', 'pool') }}
                    className="mt-3 h-2 w-full cursor-pointer accent-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-45" />
                  <FieldError
                    message={
                      fieldErrors.questionCount ||
                      (questionCountInvalid
                        ? 'Số câu mỗi đề phải là số nguyên từ 1 đến 10.'
                        : undefined)
                    }
                  />
                </div>
              </div>
              <div className="grid items-center gap-3 border-t border-[#82aedb]/35 pt-4 sm:grid-cols-2">
                <button
                  type="button"
                  role="switch"
                  aria-checked={hasFollowUp}
                  aria-label="Câu hỏi phụ"
                  disabled={busy}
                  onClick={() => {
                    setHasFollowUp((current) => !current)
                    if (!hasFollowUp)
                      setMaxFollowUpQuestions((count) => Math.min(5, Math.max(1, count || 2)))
                  }}
                  className="flex h-10 items-center justify-between gap-3 rounded-xl border border-[#4988C4] bg-white px-3 text-sm font-bold text-[#0F2854] focus-visible:outline-2 focus-visible:outline-[#1C4D8D] disabled:opacity-60"
                >
                  Câu hỏi phụ
                  <span
                    aria-hidden="true"
                    className={cn(
                      'relative h-6 w-11 shrink-0 rounded-full transition-colors',
                      hasFollowUp ? 'bg-[#16836a]' : 'bg-[#cbd5e1]',
                    )}
                  >
                    <span
                      className={cn(
                        'absolute top-0.5 left-0.5 size-5 rounded-full bg-white transition-transform motion-reduce:transition-none',
                        hasFollowUp && 'translate-x-5',
                      )}
                    />
                  </span>
                </button>
                <div>
                  <div className="flex items-center justify-between gap-3">
                  <label htmlFor="shift-followup-limit" className={fieldLabelClass(false)}>Số câu phụ tối đa</label>
                  <input
                    id="shift-followup-limit"
                    type="number"
                    min={1}
                    max={5}
                    step={1}
                    disabled={!hasFollowUp || busy}
                    value={Math.min(5, Math.max(1, maxFollowUpQuestions || 2))}
                    onChange={(event) =>
                      setMaxFollowUpQuestions(
                        Math.min(5, Math.max(1, Math.round(Number(event.target.value)) || 1)),
                      )
                    }
                    className={cn(
                      ds.field,
                      'h-10 w-16 shrink-0 px-2 text-center tabular-nums disabled:cursor-not-allowed disabled:bg-slate-100 disabled:text-slate-400',
                    )}
                  />
                  </div>
                  <input type="range" min={1} max={5} step={1} disabled={!hasFollowUp || busy}
                    aria-label="Số câu phụ tối đa thanh trượt"
                    value={Math.min(5, Math.max(1, maxFollowUpQuestions || 2))}
                    onChange={(event) => setMaxFollowUpQuestions(Number(event.target.value))}
                    className="mt-3 h-2 w-full cursor-pointer accent-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-45" />
                </div>
              </div>
            </section>

            <section className="space-y-3 rounded-2xl border border-[#82aedb]/55 bg-white p-4">
              <div>
                <div className="flex items-center justify-between gap-2">
                  <span className={fieldLabelClass(Boolean(fieldErrors.difficulties))}>
                    Độ khó được sử dụng <span className="text-rose-600">*</span>
                  </span>
                  <span className="text-xs font-medium text-slate-500">Chọn ít nhất một mức</span>
                </div>
                <div
                  role="group"
                  aria-label="Độ khó được sử dụng"
                  className="mt-2 grid grid-cols-1 gap-2 sm:grid-cols-3"
                >
                  {IN_CLASS_DIFFICULTIES.map((item) => {
                    const checked = selectedDifficulties.includes(item.value)
                    const count = difficultySourceCounts[item.value]
                    return (
                      <label
                        key={item.value}
                        className={cn(
                          'flex cursor-pointer items-center justify-between gap-2 rounded-xl border px-3 py-2.5 text-sm transition motion-reduce:transition-none',
                          checked
                            ? 'border-[#1C4D8D] bg-[#1C4D8D]/5 text-[#0F2854]'
                            : 'border-[#82aedb]/55 bg-white text-[#5b6b84] hover:border-[#4988C4]',
                        )}
                      >
                        <span className="flex items-center gap-2 font-semibold">
                          <input
                            type="checkbox"
                            checked={checked}
                            onChange={() => toggleDifficulty(item.value)}
                            className="size-4 rounded accent-[#1C4D8D]"
                          />
                          <span>{item.label}</span>
                        </span>
                        <span className={cn(ds.infoChip, 'shrink-0 whitespace-nowrap bg-[#E7F3FB] px-2 text-[#1C4D8D] tabular-nums')}>
                          {poolStats ? `${count} câu` : '…'}
                        </span>
                      </label>
                    )
                  })}
                </div>
                <FieldError
                  message={
                    fieldErrors.difficulties ||
                    (selectedDifficulties.length === 0
                      ? 'Vui lòng chọn ít nhất một mức độ câu hỏi (Dễ, Trung bình hoặc Khó).'
                      : undefined)
                  }
                />
              </div>

              <div className="border-t border-[#82aedb]/35 pt-3.5">
                <span className={fieldLabelClass(false)}>
                  Nguồn câu hỏi <span className="text-rose-600">*</span>
                </span>
                <div
                  role="radiogroup"
                  aria-label="Nguồn câu hỏi"
                  className="mt-2 grid grid-cols-1 gap-2.5 sm:grid-cols-2"
                >
                  <label
                    className={cn(
                      'flex cursor-pointer items-start gap-2.5 rounded-xl border p-3 transition motion-reduce:transition-none',
                      poolMode === 'ALL_COURSE_BANK'
                        ? 'border-[#1C4D8D] bg-[#1C4D8D]/5'
                        : 'border-[#82aedb]/55 bg-white hover:border-[#4988C4]',
                    )}
                  >
                    <input
                      type="radio"
                      name="questionPoolMode"
                      value="ALL_COURSE_BANK"
                      checked={poolMode === 'ALL_COURSE_BANK'}
                      onChange={() => {
                        setPoolMode('ALL_COURSE_BANK')
                        clearFieldError('pool')
                      }}
                      className="mt-1 size-4 accent-[#1C4D8D]"
                    />
                    <span className="min-w-0">
                      <span className="block text-sm font-bold text-[#0F2854]">
                        Toàn bộ ngân hàng môn
                      </span>
                      <span className="mt-0.5 block text-xs leading-relaxed text-slate-600">
                        Câu luyện tập đã duyệt, đang hoạt động của môn.
                      </span>
                    </span>
                  </label>

                  <label
                    className={cn(
                      'flex cursor-pointer items-start gap-2.5 rounded-xl border p-3 transition motion-reduce:transition-none',
                      poolMode === 'SELECTED_CLOS'
                        ? 'border-[#1C4D8D] bg-[#1C4D8D]/5'
                        : 'border-[#82aedb]/55 bg-white hover:border-[#4988C4]',
                    )}
                  >
                    <input
                      type="radio"
                      name="questionPoolMode"
                      value="SELECTED_CLOS"
                      checked={poolMode === 'SELECTED_CLOS'}
                      onChange={() => {
                        setPoolMode('SELECTED_CLOS')
                        clearFieldError('pool')
                      }}
                      className="mt-1 size-4 accent-[#1C4D8D]"
                    />
                    <span className="min-w-0">
                      <span className="block text-sm font-bold text-[#0F2854]">
                        Theo chuẩn đầu ra (CLO)
                      </span>
                      <span className="mt-0.5 block text-xs leading-relaxed text-slate-600">
                        Lấy câu hỏi trong phạm vi các CLO bạn chọn.
                      </span>
                    </span>
                  </label>
                </div>
              </div>

              {poolLoading ? (
                <div
                  role="status"
                  className="flex items-center gap-2 rounded-xl border border-slate-200 bg-slate-50 px-3.5 py-3 text-xs font-semibold text-slate-600"
                >
                  <LoaderCircle className="size-4 animate-spin text-[#1C4D8D] motion-reduce:animate-none" />
                  Đang tải ngân hàng câu hỏi...
                </div>
              ) : poolLoadError ? (
                <div
                  role="alert"
                  className="rounded-xl border border-rose-200 bg-rose-50 px-3.5 py-3 text-xs text-rose-900"
                >
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <p className="font-bold text-rose-800">{poolLoadError.title}</p>
                    <button
                      type="button"
                      onClick={() => setPoolReloadTick((tick) => tick + 1)}
                      className="inline-flex items-center gap-1 rounded-lg border border-rose-300 bg-white px-2.5 py-1 text-xs font-bold text-rose-800 hover:bg-rose-100"
                    >
                      <RefreshCw className="size-3" />
                      Thử lại
                    </button>
                  </div>
                  <p className="mt-1 leading-relaxed text-rose-800">{poolLoadError.detail}</p>
                </div>
              ) : poolStats ? (
                <div>
                  {poolMode === 'SELECTED_CLOS' && (
                    <section aria-labelledby="shift-clo-heading" aria-describedby={selectedCloIds.length === 0 ? 'shift-pool-errors' : undefined}
                      aria-invalid={selectedCloIds.length === 0 || undefined}
                      className={cn('rounded-xl border bg-[#F4FBFD] p-4', selectedCloIds.length === 0 ? 'border-[#c2415b]' : 'border-[#82aedb]/55')}>
                      <div className="flex flex-wrap items-center justify-between gap-3">
                        <div>
                          <h3 id="shift-clo-heading" className="text-sm font-bold text-[#0F2854]">Đã chọn {selectedCloIds.length} CLO</h3>
                          <p className="mt-1 text-xs text-[#5b6b84]">Giới hạn phạm vi câu hỏi của đề.</p>
                        </div>
                        <button type="button" className={ds.outlineButton} disabled={busy || poolStats.clos.length === 0} aria-expanded={cloExpanded} onClick={openCloPicker}>
                          <Layers className="size-4" aria-hidden="true" /> Chọn CLO
                        </button>
                      </div>
                      {poolStats.clos.length === 0 ? <EmptyState compact icon={Layers} title="Chưa có CLO đang hoạt động"
                        description="Chọn toàn bộ ngân hàng môn hoặc liên hệ Trưởng bộ môn để bổ sung CLO." />
                        : selectedCloIds.length > 0 && <div className="mt-3 flex flex-wrap gap-2">
                          {selectedCloIds.map((id) => <span key={id} className={cn(ds.infoChip, 'gap-1 bg-white pr-1 whitespace-nowrap')}>
                            {poolStats.clos.find((clo) => clo.cloId === id)?.cloCode ?? 'CLO'}
                            <button type="button" aria-label={`Bỏ ${poolStats.clos.find((clo) => clo.cloId === id)?.cloCode ?? 'CLO'}`} disabled={busy}
                              onClick={() => { setSelectedCloIds((current) => current.filter((cloId) => cloId !== id)); clearFieldError('pool') }}
                              className="flex size-6 items-center justify-center rounded-full text-[#5b6b84] hover:bg-[#E7F3FB] hover:text-[#1C4D8D]">
                              <X className="size-3.5" aria-hidden="true" />
                            </button>
                          </span>)}
                        </div>}
                    </section>
                  )}

                  {!poolEvaluation.isValid && poolEvaluation.errors.length > 0 && (
                    <div id="shift-pool-errors" data-testid="pool-allocation-preview" aria-live="polite">
                      {[...new Set(poolEvaluation.errors)].map((error) => <FieldError key={error} message={error} />)}
                    </div>
                  )}
                </div>
              ) : null}

              <FieldError message={fieldErrors.pool && !poolEvaluation.errors.includes(fieldErrors.pool) ? fieldErrors.pool : undefined} />
            </section>
          </div>
        )}
      </form>
    </FormDialog>
  )
}

export function InClassShiftHubPage({
  classId: lockedClassId,
  embedded = false,
}: {
  classId?: string
  embedded?: boolean
} = {}) {
  const [searchParams] = useSearchParams()
  const [classes, setClasses] = useState<LecturerClassOption[]>([])
  const [shifts, setShifts] = useState<InClassShift[]>([])
  const [classFilter, setClassFilter] = useState(() => lockedClassId || searchParams.get('classId') || 'ALL')
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('ALL')
  const searchInput = useSearchInput()
  const search = searchInput.query
  const [loading, setLoading] = useState(true)
  const [createOpen, setCreateOpen] = useState(false)
  const [projectorShiftId, setProjectorShiftId] = useState<string | null>(null)
  const [activatingId, setActivatingId] = useState<string | null>(null)
  const [closingShift, setClosingShift] = useState<InClassShift | null>(null)
  const [closing, setClosing] = useState(false)
  const [reloadKey, setReloadKey] = useState(0)

  useEffect(() => {
    let cancelled = false
    const load = async () => {
      setLoading(true)
      try {
        const [classResult, shiftResult] = await Promise.all([
          inClassShiftService.getLecturerClasses(),
          inClassShiftService.listShifts(lockedClassId ? { classId: lockedClassId } : undefined),
        ])
        if (cancelled) return
        setClasses(lockedClassId ? classResult.filter((item) => item.id === lockedClassId) : classResult)
        setShifts(shiftResult)
      } catch (loadError) {
        if (!cancelled) toast.error(getErrorMessage(loadError))
      } finally {
        if (!cancelled) setLoading(false)
      }
    }
    void load()
    return () => {
      cancelled = true
    }
  }, [reloadKey, lockedClassId])

  useEffect(() => {
    const timer = window.setInterval(() => {
      void inClassShiftService.listShifts(lockedClassId ? { classId: lockedClassId } : undefined).then(setShifts).catch(() => undefined)
    }, 15000)
    return () => window.clearInterval(timer)
  }, [lockedClassId])

  const classOptions = useMemo(() => {
    if (classes.length === 0) return []
    return [
      { value: 'ALL', label: 'Tất cả lớp' },
      ...classes.map((item) => ({ value: item.id, label: item.name })),
    ]
  }, [classes])

  const activeClassId = lockedClassId || classFilter

  const classScopedShifts = useMemo(() => {
    if (activeClassId === 'ALL') return shifts
    return shifts.filter((shift) => shift.classId === activeClassId)
  }, [shifts, activeClassId])

  const statusCounts = useMemo(() => {
    const counts: Record<StatusFilter, number> = {
      ALL: classScopedShifts.length,
      SCHEDULED: 0,
      ACTIVE: 0,
      CLOSED: 0,
      FINALIZED: 0,
    }
    for (const shift of classScopedShifts) {
      counts[shift.status] += 1
    }
    return counts
  }, [classScopedShifts])

  const filtered = useMemo(() => {
    const query = search.trim().toLocaleLowerCase('vi-VN')
    return classScopedShifts.filter((shift) => {
      if (statusFilter !== 'ALL' && shift.status !== statusFilter) return false
      if (!query) return true
      return (
        shift.title.toLocaleLowerCase('vi-VN').includes(query) ||
        shift.className.toLocaleLowerCase('vi-VN').includes(query) ||
        shift.courseCode.toLocaleLowerCase('vi-VN').includes(query)
      )
    })
  }, [classScopedShifts, statusFilter, search])

  const activate = async (shift: InClassShift) => {
    setActivatingId(shift.id)
    try {
      await inClassShiftService.activateShift(shift.id)
      setShifts((current) =>
        current.map((item) => (item.id === shift.id ? { ...item, status: 'ACTIVE' } : item)),
      )
      toast.success(`Đã kích hoạt “${shift.title}”. Có thể chiếu mã OTP cho sinh viên.`)
    } catch (activateError) {
      toast.error(userMessage(activateError, 'Không kích hoạt được ca thi. Vui lòng thử lại.'))
    } finally {
      setActivatingId(null)
    }
  }

  const confirmClose = async () => {
    if (!closingShift) return
    setClosing(true)
    try {
      await inClassShiftService.closeShift(closingShift.id)
      setShifts((current) =>
        current.map((item) => (item.id === closingShift.id ? { ...item, status: 'CLOSED' } : item)),
      )
      if (projectorShiftId === closingShift.id) setProjectorShiftId(null)
      toast.success(`Đã đóng ca “${closingShift.title}”. Sinh viên không vào ca hoặc nộp bài được nữa.`)
      setClosingShift(null)
    } catch (closeError) {
      toast.error(userMessage(closeError, 'Không đóng được ca thi. Vui lòng thử lại.'))
    } finally {
      setClosing(false)
    }
  }

  return (
    <div lang="vi" className={embedded ? 'space-y-3' : cn(ds.page, 'pb-8')}>

      <section className={cn(ds.section, 'overflow-hidden')}>
        <div className={cn(ds.sectionHeader, 'flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between')}>
            <div className={ds.sectionHeading}>
              <div className={ds.sectionTitleRow}>
                <span className={ds.sectionIconWrap}>
                  <ClipboardList className={ds.sectionIcon} />
                </span>
                <h2 className={ds.sectionTitle}>Danh sách ca</h2>
                <span className={ds.sectionBadge}>
                  {loading ? '…' : `${filtered.length} ca`}
                </span>
              </div>
              <p className={ds.sectionHint}>{embedded ? 'Ca kiểm tra của lớp này' : 'Theo dõi các ca kiểm tra trên lớp'}</p>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              {lockedClassId ? null : (
              <FilterDropMenu
                label="Lớp học phần"
                menuMinWidth={ds.filterMenuMinWidth}
                wrapOptions
                value={classFilter}
                onChange={setClassFilter}
                options={classOptions}
                emptyText="Chưa có lớp"
                className={ds.filterClass}
              />
              )}
              <div className="relative shrink-0">
                <SearchField
                  value={searchInput.draft}
                  onChange={(event) => searchInput.change(event.target.value)}
                  onKeyDown={searchInput.onKeyDown}
                  placeholder="Tìm theo tên ca..."
                  className={cn(ds.searchControl, 'h-10 rounded-full border border-[#4988C4] bg-white py-0 pr-4 pl-9 text-sm font-semibold text-[#0F2854] shadow-[0_2px_8px_rgba(23,48,96,0.04)] outline-none placeholder:font-medium placeholder:text-[#8aa0b8] focus:border-[#4988C4] focus:ring-2 focus:ring-[#1C4D8D]/15')}
                />
              </div>
              <button
                type="button"
                onClick={() => setReloadKey((key) => key + 1)}
                disabled={loading}
                className={ds.outlineButton}
              >
                <RefreshCw className={cn('size-3.5', loading && 'animate-spin')} />
                Làm mới
              </button>
              <button
                type="button"
                onClick={() => setCreateOpen(true)}
                className={cn(ds.primaryButton, 'min-h-10')}
              >
                <Plus className="size-3.5" />
                Tạo ca kiểm tra
              </button>
            </div>
        </div>

        <div className="bg-white">
          <div className="px-4 pt-3 sm:px-6">
            <StatusTabs label="Lọc trạng thái ca" value={statusFilter} options={STATUS_FILTERS.map(tab => ({ ...tab, count: statusCounts[tab.value] }))} onChange={setStatusFilter} />
          </div>
        </div>

        <div className={cn(ds.sectionBody, 'border-t-0 pt-4')}>
          {loading ? (
            <div className="flex min-h-40 flex-col items-center justify-center gap-3 text-sm text-[#5b6b84]">
              <LoaderCircle className="size-7 animate-spin text-[#1C4D8D] motion-reduce:animate-none" />
              Đang tải danh sách ca...
            </div>
          ) : filtered.length === 0 ? (
            <EmptyState
              icon={UsersRound}
              title="Chưa có ca phù hợp"
              description="Tạo Progress Test / Quiz cho lớp đang dạy để sinh viên vào bằng mã OTP chiếu trên lớp."
              action={
                <button type="button" onClick={() => setCreateOpen(true)} className={ds.primaryButton}>
                  <Plus className="size-4" />
                  Tạo ca kiểm tra
                </button>
              }
            />
          ) : (
            <ul className="divide-y divide-[#4988C4]/25 overflow-hidden rounded-2xl border border-[#82aedb]/55">
              {filtered.map((shift) => {
                const start = formatShiftPoint(shift.startTime)
                const end = formatShiftPoint(shift.endTime)
                const endLabel = start && end && toDateKey(end) !== toDateKey(start)
                  ? `${formatShiftDate(end)} ${formatShiftClock(end)}`
                  : end
                    ? formatShiftClock(end)
                    : ''
                const timeRange = start && endLabel
                  ? `${formatShiftClock(start)} – ${endLabel}`
                  : start
                    ? formatShiftClock(start)
                    : ''
                const factClass =
                  'inline-flex items-center gap-1.5 rounded-lg bg-[#D1EFF8] px-2.5 py-1.5 text-xs font-semibold text-[#1C4D8D]'
                return (
                <li
                  key={shift.id}
                  className="flex flex-col gap-3 bg-white px-4 py-4 transition hover:bg-[#F8FBFD] sm:flex-row sm:items-center sm:justify-between sm:px-5"
                >
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      {statusChip(shift.status)}
                      <span className="rounded-full border border-[#82aedb]/55 bg-[#F4FBFD] px-2.5 py-0.5 text-[11px] font-bold text-[#1C4D8D]">
                        {shift.className}
                      </span>
                    </div>
                    <h3 className="mt-2 truncate text-xl font-bold tracking-[-0.02em] text-[#0F2854]">
                      {shift.title}
                    </h3>
                    <div className="mt-2 flex flex-wrap items-center gap-1.5">
                      <span className={factClass}>
                        <Clock3 className="size-3.5 shrink-0 text-[#1C4D8D]" />
                        {start ? (
                          <>
                            <span className="font-bold text-[#0F2854]">{formatShiftDate(start)}</span>
                            {timeRange ? <span>{timeRange}</span> : null}
                          </>
                        ) : (
                          'Chưa có giờ'
                        )}
                      </span>
                      {shift.examDurationMinutes != null && (
                        <span className={factClass}>
                          <Timer className="size-3.5 shrink-0 text-[#1C4D8D]" />
                          <span>
                            Làm bài{' '}
                            <span className="font-bold text-[#0F2854]">
                              {formatDuration(shift.examDurationMinutes)}
                            </span>
                          </span>
                        </span>
                      )}
                      <span className={factClass}>
                        <ClipboardList className="size-3.5 shrink-0 text-[#1C4D8D]" />
                        <span>
                          <span className="font-bold text-[#0F2854] tabular-nums">{shift.questionCount}</span> câu/SV
                          {shift.hasFollowUp ? (
                            <>
                              {' · phụ '}
                              <span className="font-bold text-[#0F2854] tabular-nums">
                                {shift.maxFollowUpQuestions || 1}
                              </span>
                            </>
                          ) : null}
                        </span>
                      </span>
                      <span className={factClass}>
                        <Layers className="size-3.5 shrink-0 text-[#1C4D8D]" />
                        <span>
                          {formatPoolModeLabel(
                            shift.questionPoolMode,
                            shift.selectedCloCodes,
                            shift.selectedCloIds.length,
                          )}
                          {' · '}
                          {formatDifficultyListLabel(shift.selectedDifficulties)}
                        </span>
                      </span>
                      <span className={factClass}>
                        <UsersRound className="size-3.5 shrink-0 text-[#1C4D8D]" />
                        <span className="tabular-nums">
                          <span className="font-bold text-[#0F2854]">{shift.joinedCount}</span>
                          {' vào ca · '}
                          <span className="font-bold text-[#0F2854]">{shift.submittedCount}</span>
                          {' đã nộp'}
                        </span>
                      </span>
                    </div>
                  </div>
                  <div className="flex flex-wrap items-center gap-2 sm:shrink-0">
                    {shift.status === 'SCHEDULED' && (
                      <button
                        type="button"
                        disabled={activatingId === shift.id}
                        onClick={() => void activate(shift)}
                        className={cn(ds.primaryButton, 'min-h-10')}
                      >
                        {activatingId === shift.id ? (
                          <LoaderCircle className="size-3.5 animate-spin" />
                        ) : (
                          <CheckCircle2 className="size-3.5" />
                        )}
                        Kích hoạt
                      </button>
                    )}
                    {shift.status === 'ACTIVE' && (
                      <button
                        type="button"
                        onClick={() => setProjectorShiftId(shift.id)}
                        className={cn(ds.primaryButton, 'min-h-10')}
                      >
                        <Presentation className="size-3.5" />
                        Chiếu mã
                      </button>
                    )}
                    {(shift.status === 'SCHEDULED' || shift.status === 'ACTIVE') && (
                      <button
                        type="button"
                        onClick={() => setClosingShift(shift)}
                        className={cn(ds.outlineButton, 'min-h-10')}
                      >
                        <CircleStop className="size-3.5" />
                        Đóng ca
                      </button>
                    )}
                    <Link
                      to={shift.classId ? `/lecturer/classes/${shift.classId}/shifts/${shift.id}/results` : '/lecturer/classes'}
                      className={cn(ds.outlineButton, 'min-h-10')}
                    >
                      {shift.status === 'FINALIZED' ? 'Xem điểm' : 'Bảng điểm'}
                      <ChevronRight className="size-3.5" />
                    </Link>
                  </div>
                </li>
                )
              })}
            </ul>
          )}
        </div>
      </section>

      <CreateInClassShiftDialog
        open={createOpen}
        classes={classes}
        fixedClassId={lockedClassId}
        onClose={() => setCreateOpen(false)}
        onCreated={(shift) => {
          setCreateOpen(false)
          toast.success(`Đã tạo ca “${shift.title}”. Bấm Kích hoạt khi bắt đầu giờ thi, rồi chiếu mã OTP.`)
          setShifts((current) => [shift, ...current.filter((item) => item.id !== shift.id)])
        }}
      />

      <InClassShiftProjectorDialog
        open={Boolean(projectorShiftId)}
        shiftId={projectorShiftId}
        onClose={() => setProjectorShiftId(null)}
      />

      {closingShift && (
        <ConfirmDialog
          tone="warning"
          title="Đóng ca kiểm tra"
          message={`Đóng “${closingShift.title}” ngay. Sinh viên không vào ca hoặc nộp bài được nữa. Bảng điểm vẫn mở để chốt điểm.`}
          confirmLabel="Đóng ca"
          cancelLabel="Hủy"
          pending={closing}
          onConfirm={() => void confirmClose()}
          onCancel={() => {
            if (!closing) setClosingShift(null)
          }}
        />
      )}
    </div>
  )
}
