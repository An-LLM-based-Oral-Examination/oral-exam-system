import { coloredCardTones, selectedCardBackgrounds } from '@/design/coloredCardTones'
import { MicrophoneCheckPanel } from '@/components/common/MicrophoneCheckPanel'
import { useMicrophoneCheck } from '@/hooks/useMicrophoneCheck'
import { lazy, Suspense, useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import {
  ArrowRight,
  AudioLines,
  Award,
  ChartNoAxesCombined,
  Check,
  Clock3,
  FileCheck2,
  Eye,
  History,
  Info,
  Lightbulb,
  LoaderCircle,
  Mic2,
  Minus,
  Plus,
  Radio,
  Settings2,
  ShieldCheck,
  Volume2,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import { userMessage } from '@/lib/userMessage'
import { DialogFooter, FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'
import { useAuth } from '@/hooks/useAuth'
import { courseService } from '@/services/course.service'
import { practiceService } from '@/services/practice.service'
import {
  getTtsVoiceForCourse,
  isEnglishCourse,
  isSpeechPlaybackCancellation,
  playNaturalSpeech,
  stopSpeechPlayback,
} from '@/services/speech.service'
import { savePracticeRuntime } from '@/utils/practiceSessionStorage'
import { FilterDropMenu } from '@/components/common/FilterDropMenu'
import { fieldLabelClass } from '@/components/common/formFieldClasses'
import { LoadingState } from '@/components/common/LoadingState'
import { EmptyState } from '@/components/common/EmptyState'
import { toast } from '@/components/common/toastStore'
import { PracticeSessionScorecard } from '@/components/practice/PracticeSessionScorecard'
import { BannerFact, PageBanner } from '@/design/ui'
import type {
  CourseDto,
  PracticeDifficulty,
  PracticeHistoryItem,
  PracticeMode,
  PracticeSessionConfig,
  PracticeSessionDetail,
} from '@/types/practice.types'



const PracticeHeroThreeScene = lazy(() =>
  import('./PracticeHeroThreeScene').then((module) => ({
    default: module.PracticeHeroThreeScene,
  })),
)

const FULL_SESSION_MIN_QUESTIONS = 3
const FULL_SESSION_MAX_QUESTIONS = 10

const COURSE_CODE_TONES = [
  'border border-[#4988C4]/35 bg-[#4988C4]/16 text-[#1C4D8D]',
  'border border-[#16836a]/30 bg-[#e8f8f3] text-[#16836a]',
  'border border-[#c26a12]/30 bg-[#fff4e8] text-[#c26a12]',
  'border border-[#1C4D8D]/30 bg-[#f3eefe] text-[#1C4D8D]',
  'border border-[#0e7490]/30 bg-[#e7f6fb] text-[#0e7490]',
  'border border-[#c2415b]/30 bg-[#fdecef] text-[#c2415b]',
  'border border-[#3d7a2a]/30 bg-[#eef8e8] text-[#3d7a2a]',
]

function courseCodeTone(code?: string) {
  if (!code) return COURSE_CODE_TONES[0]
  const hash = Array.from(code).reduce((sum, char) => sum + char.charCodeAt(0), 0)
  return COURSE_CODE_TONES[hash % COURSE_CODE_TONES.length]
}

const DIFFICULTY_OPTIONS: Array<{
  value: PracticeDifficulty
  label: string
  helper: string
  selectedClassName: string
  badgeClassName: string
}> = [
  {
    value: 'easy',
    selectedClassName: cn(coloredCardTones.green, selectedCardBackgrounds.green),
    badgeClassName: 'bg-[#16836a] text-white',
    label: 'Dễ',
    helper: 'Củng cố nền tảng',
  },
  {
    value: 'medium',
    selectedClassName: cn(coloredCardTones.amber, selectedCardBackgrounds.amber),
    badgeClassName: 'bg-[#c26a12] text-white',
    label: 'Trung bình',
    helper: 'Vận dụng kiến thức',
  },
  {
    value: 'hard',
    selectedClassName: cn(coloredCardTones.rose, selectedCardBackgrounds.rose),
    badgeClassName: 'bg-[#c2415b] text-white',
    label: 'Khó',
    helper: 'Phân tích chuyên sâu',
  },
]


function formatPracticeDate(value: string) {
  return new Intl.DateTimeFormat('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
    timeZone: 'Asia/Ho_Chi_Minh',
  }).format(new Date(value))
}

function getPracticeModeLabel(mode: PracticeHistoryItem['practiceMode']) {
  return mode === 'per_question' ? 'Luyện từng câu' : 'Luyện trọn gói'
}

export function PracticePage() {
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const { user } = useAuth()
  const [courses, setCourses] = useState<CourseDto[]>([])
  const [practiceHistory, setPracticeHistory] = useState<PracticeHistoryItem[]>([])
  const [selectedCourseId, setSelectedCourseId] = useState('')
  const [feedbackMode, setFeedbackMode] = useState<PracticeMode>('PER_QUESTION')
  const [selectedDifficulties, setSelectedDifficulties] = useState<PracticeDifficulty[]>(['medium'])
  const [questionCount, setQuestionCount] = useState(5)
  const microphoneCheck = useMicrophoneCheck()
  const { deviceState: microphoneState, deviceNames: microphoneName } = microphoneCheck
  const setupSectionRef = useRef<HTMLElement>(null)
  const historyColumnRef = useRef<HTMLElement>(null)

  useEffect(() => {
    // Freeze the history column while the microphone temporarily adds its meter.
    if (microphoneState === 'checking' || microphoneState === 'testing') return
    const section = setupSectionRef.current
    const column = historyColumnRef.current
    if (!section || !column) return
    const syncHeight = () => {
      column.style.setProperty('--practice-column-height', `${section.getBoundingClientRect().height}px`)
    }
    syncHeight()
    const observer = typeof ResizeObserver !== 'undefined' ? new ResizeObserver(syncHeight) : null
    observer?.observe(section)
    window.addEventListener('resize', syncHeight)
    return () => {
      observer?.disconnect()
      window.removeEventListener('resize', syncHeight)
    }
  }, [microphoneState])

  const [isVoicePreviewPlaying, setIsVoicePreviewPlaying] = useState(false)
  const [voicePreviewMessage, setVoicePreviewMessage] = useState('')
  const speechRequestRef = useRef(0)
  const [showAllHistoryModal, setShowAllHistoryModal] = useState(false)
  const [selectedScorecardId, setSelectedScorecardId] = useState<string | null>(null)
  const [scorecardMap, setScorecardMap] = useState<Record<string, PracticeSessionDetail>>({})
  const [isLoadingScorecard, setIsLoadingScorecard] = useState(false)
  const [isLoadingData, setIsLoadingData] = useState(true)
  const [isStarting, setIsStarting] = useState(false)
  const recentPracticeHistory = practiceHistory.slice(0, 6)
  const completedRecentSessions = recentPracticeHistory.filter((item) => item.status === 'completed')
  const scoredRecentSessions = completedRecentSessions.filter((item) => item.averageScore !== null)
  const recentAverageScore = scoredRecentSessions.length > 0
    ? scoredRecentSessions.reduce((sum, item) => sum + (item.averageScore ?? 0), 0) / scoredRecentSessions.length
    : null
  const recentAnsweredQuestions = recentPracticeHistory.reduce((sum, item) => sum + item.answeredQuestions, 0)

  const selectedCourse = useMemo(
    () => courses.find((course) => course.id === selectedCourseId) ?? null,
    [courses, selectedCourseId],
  )
  const estimatedMinutes = questionCount * 3

  const openPracticeScorecard = useCallback(
    async (sessionId: string) => {
      setSelectedScorecardId(sessionId)
      if (scorecardMap[sessionId]) return

      setIsLoadingScorecard(true)
      try {
        const detail = await practiceService.getSession(sessionId)
        setScorecardMap((prev) => ({ ...prev, [sessionId]: detail }))
      } catch (err) {
        toast.error(userMessage(err, 'Không tải được chi tiết câu hỏi và nhận xét phiên luyện tập.'))
      } finally {
        setIsLoadingScorecard(false)
      }
    },
    [scorecardMap],
  )

  const loadPracticeData = useCallback(async () => {
    if (!user) return
    setIsLoadingData(true)

    try {
      const [coursesResult, historyResult] = await Promise.allSettled([
        courseService.getEnrolledCourses(),
        practiceService.getStudentHistory(user.id),
      ])
      if (coursesResult.status === 'rejected') throw coursesResult.reason

      const availableCourses = coursesResult.value
      setCourses(availableCourses)
      if (historyResult.status === 'fulfilled') {
        setPracticeHistory(historyResult.value)
      } else {
        setPracticeHistory([])
        toast.error(
          userMessage(
            historyResult.reason,
            'Đã tải danh sách môn học nhưng chưa tải được lịch sử luyện tập.',
          ),
        )
      }
      const requestedCourse = searchParams.get('course')?.trim().toLowerCase()
      const requestedCourseMatch = requestedCourse
        ? availableCourses.find(
            (course) =>
              course.id.toLowerCase() === requestedCourse ||
              course.code.trim().toLowerCase() === requestedCourse,
          )
        : undefined
      setSelectedCourseId((current) =>
        availableCourses.some((course) => course.id === current)
          ? current
          : (requestedCourseMatch?.id ?? availableCourses[0]?.id ?? ''),
      )
    } catch (loadError) {
      toast.error(userMessage(loadError, 'Không tải được dữ liệu luyện tập. Vui lòng thử lại.'))
    } finally {
      setIsLoadingData(false)
    }
  }, [searchParams, user])

  useEffect(() => {
    const loadTimer = window.setTimeout(() => void loadPracticeData(), 0)
    return () => window.clearTimeout(loadTimer)
  }, [loadPracticeData])

  useEffect(() => {
    return () => {
      speechRequestRef.current += 1
      stopSpeechPlayback()
    }
  }, [])

  const handleVoicePreview = () => {
    if (!selectedCourse) return

    if (isVoicePreviewPlaying) {
      speechRequestRef.current += 1
      stopSpeechPlayback()
      setIsVoicePreviewPlaying(false)
      setVoicePreviewMessage('Đã dừng nghe thử giọng đọc đề.')
      return
    }

    const requestId = ++speechRequestRef.current
    const isEnglish = isEnglishCourse(selectedCourse.code, selectedCourse.name)
    const previewText = `Xin chào. Phiên luyện tập ${selectedCourse.name} đã sẵn sàng. Hãy trả lời rõ ràng và tự tin.`
    setIsVoicePreviewPlaying(true)
    setVoicePreviewMessage('Đang phát thử giọng đọc đề...')

    void playNaturalSpeech(
      isEnglish
        ? `Hello. The ${selectedCourse.name} practice session is ready. Please answer clearly and confidently.`
        : previewText,
      getTtsVoiceForCourse(selectedCourse.code, selectedCourse.name),
      0.94,
    )
      .then(() => {
        if (speechRequestRef.current === requestId) setVoicePreviewMessage('Đã phát xong câu chào mẫu.')
      })
      .catch((error: unknown) => {
        if (speechRequestRef.current !== requestId || isSpeechPlaybackCancellation(error)) return
        setVoicePreviewMessage('Không thể phát giọng đọc đề. Vui lòng thử lại.')
      })
      .finally(() => {
        if (speechRequestRef.current === requestId) setIsVoicePreviewPlaying(false)
      })
  }


  const toggleDifficulty = (difficulty: PracticeDifficulty) => {
    setSelectedDifficulties((current) => {
      if (current.includes(difficulty)) {
        return current.length === 1 ? current : current.filter((item) => item !== difficulty)
      }
      return DIFFICULTY_OPTIONS.map(({ value }) => value).filter(
        (value) => current.includes(value) || value === difficulty,
      )
    })
  }

  const updateFullSessionQuestionCount = (nextValue: number) => {
    if (!Number.isFinite(nextValue)) return
    setQuestionCount(
      Math.min(
        FULL_SESSION_MAX_QUESTIONS,
        Math.max(FULL_SESSION_MIN_QUESTIONS, Math.round(nextValue)),
      ),
    )
  }

  const selectFeedbackMode = (mode: PracticeMode) => {
    setFeedbackMode(mode)
    if (mode === 'FULL_SESSION') {
      setQuestionCount((current) =>
        Math.min(FULL_SESSION_MAX_QUESTIONS, Math.max(FULL_SESSION_MIN_QUESTIONS, current)),
      )
    }
  }

  const handleStartPractice = async () => {
    if (!selectedCourse || !user || isStarting) return

    setIsStarting(true)
    const config: PracticeSessionConfig = {
      courseId: selectedCourse.id,
      courseName: selectedCourse.name,
      courseCode: selectedCourse.code,
      mode: feedbackMode,
      selectedDifficulties: feedbackMode === 'PER_QUESTION' ? selectedDifficulties : [],
      questionCount: feedbackMode === 'FULL_SESSION' ? questionCount : null,
      microphoneName,
    }

    try {
      const session = await practiceService.startSession({
        courseId: selectedCourse.id,
        studentId: user.id,
        difficulty: feedbackMode === 'FULL_SESSION' ? 'progressive' : null,
        difficulties: feedbackMode === 'PER_QUESTION' ? selectedDifficulties : null,
        ...(feedbackMode === 'FULL_SESSION' ? { questionCount } : {}),
        isFullSession: feedbackMode === 'FULL_SESSION',
      })

      if (session.questions.length === 0) {
        throw new Error('Backend không trả về câu hỏi nào cho phiên luyện tập.')
      }

      savePracticeRuntime({
        version: 1,
        sessionId: session.sessionId,
        studentId: user.id,
        transcriptBufferSeconds: session.transcriptBufferSeconds,
        config,
        questions: session.questions,
        drafts: [],
        currentQuestionIndex: 0,
        mainQuestionsCompleted: 0,
        questionDifficulties: {},
        activeFollowUp: null,
        pendingSubmission: null,
        pendingAnswerId: null,
        resolvedAnswer: null,
        submittedAnswerIdsByQuestion: {},
        fullSessionSubmitted: false,
        draftAnswerText: '',
        draftAnswerMode: 'voice',
        showBuffer: false,
        bufferDeadlineTimestamp: null,
      })

      navigate(`/practice/session?session=${session.sessionId}`)
    } catch (startError) {
      toast.error(userMessage(startError, 'Không bắt đầu được phiên luyện tập. Vui lòng thử lại.'))
    } finally {
      setIsStarting(false)
    }
  }

  return (
    <div className={ds.page}>
      <PageBanner
        sceneClassName="lg:h-0"
        icon={Mic2}
        eyebrow="Luyện tập tương tác theo học phần"
        title="Phòng Luyện tập Vấn đáp Chuyên ngành"
        description="Luyện phản xạ trả lời bằng giọng nói hoặc văn bản, nhận chấm điểm Rubric và câu hỏi đào sâu ngay trong phiên."
        extras={
          <>
            <BannerFact icon={ShieldCheck}>Chấm theo Rubric</BannerFact>
            <BannerFact icon={AudioLines} tone="green">
              Giọng nói hoặc văn bản
            </BannerFact>
            <BannerFact icon={Lightbulb} tone="violet">
              Câu hỏi đào sâu trong phiên
            </BannerFact>
          </>
        }
        scene={
          <Suspense
            fallback={
              <div className="flex size-full items-center justify-center" aria-hidden="true">
                <div className="size-20 animate-pulse rounded-full border border-[#7ce8f4]/30 bg-[#3dd5ee]/10 motion-reduce:animate-none" />
              </div>
            }
          >
            <PracticeHeroThreeScene active={isVoicePreviewPlaying} />
          </Suspense>
        }
      />

      <div className="mb-0! grid items-start gap-4 lg:grid-cols-[minmax(0,3fr)_minmax(0,2fr)]">
        <section ref={setupSectionRef} className={cn(ds.section, 'min-w-0 overflow-hidden')}>
          <div className={cn(ds.sectionHeader, 'flex flex-wrap items-center justify-between gap-3')}>
            <div className={ds.sectionHeading}>
              <div className={ds.sectionTitleRow}>
                <span className={ds.sectionIconWrap}>
                  <Settings2 className={ds.sectionIcon} />
                </span>
                <h2 className={ds.sectionTitle}>Thiết lập luyện tập</h2>
              </div>
              <p className={ds.sectionHint}>Chọn môn, chế độ và độ khó</p>
            </div>
            <button type="button" onClick={handleVoicePreview} className={ds.outlineButton}>
              {isVoicePreviewPlaying ? (
                <AudioLines className="size-4 animate-pulse motion-reduce:animate-none" />
              ) : (
                <Volume2 className="size-4" />
              )}
              {isVoicePreviewPlaying ? 'Dừng nghe thử' : 'Nghe thử giọng'}
            </button>
          </div>

          <div className={cn(ds.sectionBody, 'space-y-4')}>
            {isLoadingData ? (
              <LoadingState label="Đang tải môn học…" className="min-h-10" />
            ) : courses.length === 0 ? (
              <div role="status" className="flex items-start gap-2.5 rounded-xl border border-[#f3ddb0] bg-[#fff8eb] px-3.5 py-3 text-sm leading-relaxed text-[#8a641e]">
                <Info className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
                <p>
                  <span className="font-bold">Chưa có môn để luyện tập.</span>{' '}
                  Hãy tham gia lớp tại{' '}
                  <Link to="/classes" className="inline-flex items-center gap-1 whitespace-nowrap border-b border-current leading-tight font-bold text-[#1C4D8D] hover:text-[#0F2854]">Quản lý lớp học<ArrowRight className="size-3.5" aria-hidden="true" /></Link>
                </p>
              </div>
            ) : (
              <div>
                <span className={fieldLabelClass()}>Môn học</span>
                <div className="mt-1.5 flex min-w-0 flex-wrap items-center gap-3">
                  <FilterDropMenu
                    label="Chọn môn học"
                    value={selectedCourseId}
                    onChange={setSelectedCourseId}
                    disabled={isLoadingData || courses.length === 0}
                    options={courses.map((course) => ({ value: course.id, label: `${course.code} · ${course.name}` }))}
                    emptyText={isLoadingData ? 'Đang tải môn học…' : 'Chưa có môn từ lớp đã tham gia'}
                    className="min-w-48 flex-1"
                    wrapOptions
                  />
                  <div className="flex min-w-0 max-w-full items-center gap-2 rounded-xl bg-[#E7F3FB] px-3 py-2.5 text-sm shadow-sm shadow-[#0F2854]/10 leading-relaxed text-[#3d5270]">
                    <Info className="mt-0.5 size-4 shrink-0 text-[#1C4D8D]" aria-hidden="true" />
                    <p className="truncate" title="Tham gia thêm lớp để luyện tập các môn khác.">
                      <Link to="/classes" className="font-bold text-[#1C4D8D] underline underline-offset-2 hover:text-[#0F2854]">Tham gia thêm lớp</Link>{' '}
                      để luyện tập các môn khác.
                    </p>
                  </div>
                </div>
              </div>
            )}

            <fieldset>
              <legend className={fieldLabelClass()}>
                Chế độ luyện tập
              </legend>
              <div className="mt-2 grid gap-3 sm:grid-cols-2">
                <label
                  className={cn(
                    ds.panelShadow,
                    'group relative cursor-pointer rounded-xl border p-3 transition has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-[#1C4D8D] motion-reduce:transition-none',
                    feedbackMode === 'PER_QUESTION'
                      ? cn(coloredCardTones.blue, selectedCardBackgrounds.blue)
                      : 'border-[#82aedb]/55 bg-white',
                  )}
                >
                  <input
                    type="radio"
                    name="feedback-mode"
                    value="PER_QUESTION"
                    checked={feedbackMode === 'PER_QUESTION'}
                    onChange={() => selectFeedbackMode('PER_QUESTION')}
                    className="sr-only"
                  />
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex size-9 items-center justify-center rounded-xl bg-[#1C4D8D] text-white">
                      <Radio className="size-4" />
                    </div>
                    <span
                      className={cn(
                        'flex size-5 items-center justify-center rounded-full border transition',
                        feedbackMode === 'PER_QUESTION'
                          ? 'border-[#1C4D8D] bg-[#1C4D8D] text-white'
                          : 'border-[#82aedb]/55 bg-white text-transparent',
                      )}
                    >
                      <Check className="size-3" />
                    </span>
                  </div>
                  <h3 className="mt-2 text-sm font-bold text-[#0F2854]">Luyện từng câu</h3>
                  <p className="mt-1 text-xs leading-relaxed text-[#5b6b84]">
                    Nhận điểm và nhận xét sau mỗi câu trả lời.
                  </p>
                  <span className={cn(ds.infoChip, 'mt-2')}>
                    <Radio className="size-3" /> Không giới hạn câu · Có câu hỏi phụ
                  </span>
                </label>

                <label
                  className={cn(
                    ds.panelShadow,
                    'group relative cursor-pointer rounded-xl border p-3 transition has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-[#1C4D8D] motion-reduce:transition-none',
                    feedbackMode === 'FULL_SESSION'
                      ? cn(coloredCardTones.violet, selectedCardBackgrounds.violet)
                      : 'border-[#82aedb]/55 bg-white',
                  )}
                >
                  <input
                    type="radio"
                    name="feedback-mode"
                    value="FULL_SESSION"
                    checked={feedbackMode === 'FULL_SESSION'}
                    onChange={() => selectFeedbackMode('FULL_SESSION')}
                    className="sr-only"
                  />
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex size-9 items-center justify-center rounded-xl bg-[#6d4cc4] text-white">
                      <ShieldCheck className="size-4" />
                    </div>
                    <span
                      className={cn(
                        'flex size-5 items-center justify-center rounded-full border transition',
                        feedbackMode === 'FULL_SESSION'
                          ? 'border-[#6d4cc4] bg-[#6d4cc4] text-white'
                          : 'border-[#82aedb]/55 bg-white text-transparent',
                      )}
                    >
                      <Check className="size-3" />
                    </span>
                  </div>
                  <h3 className="mt-2 text-sm font-bold text-[#0F2854]">Luyện trọn gói</h3>
                  <p className="mt-1 text-xs leading-relaxed text-[#5b6b84]">
                    Nhận điểm và nhận xét khi hoàn thành cả phiên.
                  </p>
                  <span className={cn(ds.infoChip, 'mt-2 border-[#d4c4f5] bg-[#F7F3FD] text-[#6d4cc4]')}>
                    <Clock3 className="size-3" /> 3–10 câu · Không có câu hỏi phụ
                  </span>
                </label>
              </div>
            </fieldset>

            {feedbackMode === 'PER_QUESTION' ? (
              <section className={cn('flex flex-col justify-between gap-2.5 rounded-xl border border-[#82aedb]/55 bg-[#F4FBFD] p-3 sm:min-h-[132px]', ds.panelShadow)}>
                <div className="flex flex-col gap-1 sm:flex-row sm:items-center sm:justify-between">
                  <div>
                    <h3 className={fieldLabelClass()}>
                      Chọn mức độ câu hỏi
                    </h3>
                    <p className="mt-0.5 text-xs text-[#5b6b84]">
                      Chọn ít nhất một mức; có thể kết hợp nhiều mức trong cùng phiên luyện tập.
                    </p>
                  </div>
                  <span className={cn(ds.sectionBadge, 'shrink-0 whitespace-nowrap')}>
                    Đã chọn {selectedDifficulties.length}/3 mức
                  </span>
                </div>

                <div className="grid gap-2.5 sm:grid-cols-3" aria-label="Các mức độ luyện tập">
                  {DIFFICULTY_OPTIONS.map((option) => {
                    const selected = selectedDifficulties.includes(option.value)
                    const isOnlySelection = selected && selectedDifficulties.length === 1

                    return (
                      <button
                        key={option.value}
                        type="button"
                        aria-pressed={selected}
                        aria-disabled={isOnlySelection}
                        onClick={() => toggleDifficulty(option.value)}
                        className={cn(
                          'flex min-w-0 items-center gap-2 rounded-xl border px-3 py-2.5 text-left shadow-sm shadow-[#0F2854]/10 transition focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none',
                          selected
                            ? option.selectedClassName
                            : 'border-[#82aedb]/55 bg-white hover:border-[#82aedb]/55',
                        )}
                        title={
                          isOnlySelection ? 'Phiên luyện tập cần ít nhất một mức độ.' : undefined
                        }
                      >
                        <span
                          className={cn(
                            'flex size-8 shrink-0 items-center justify-center rounded-lg text-xs font-black',
                            selected ? option.badgeClassName : 'bg-[#E7F3FB] text-[#5b6b84]',
                          )}
                        >
                          {option.label.charAt(0)}
                        </span>
                        <span className="min-w-0 flex-1">
                          <span className="block text-sm font-bold text-[#0F2854]">
                            {option.label}
                          </span>
                          <span className="mt-0.5 block text-xs text-[#5b6b84]">
                            {option.helper}
                          </span>
                        </span>
                        <span
                          className={cn(
                            'flex size-4 shrink-0 items-center justify-center rounded-full border',
                            selected
                              ? 'border-[#1C4D8D] bg-[#1C4D8D] text-white'
                              : 'border-[#82aedb]/55 text-transparent',
                          )}
                        >
                          <Check className="size-2.5" />
                        </span>
                      </button>
                    )
                  })}
                </div>
              </section>
            ) : (
              <section className={cn('flex flex-col justify-between gap-5 rounded-xl border border-[#82aedb]/55 bg-[#F4FBFD] px-3 py-5 sm:min-h-[132px]', ds.panelShadow)}>
                <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                  <div className="flex items-center gap-3">
                    <div className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-[#1C4D8D] text-white shadow-xs">
                      <Clock3 className="size-4" />
                    </div>
                    <div>
                      <p className="text-xs font-bold tracking-[0.06em] text-[#1C4D8D] uppercase">
                        Lộ trình tăng dần · Dễ → Trung bình → Khó
                      </p>
                      <p className="mt-0.5 text-sm font-bold text-[#0F2854]">
                        {questionCount} câu hỏi · khoảng {estimatedMinutes} phút
                      </p>
                    </div>
                  </div>

                  <div className="inline-flex h-10 shrink-0 items-center gap-1 rounded-xl border border-[#82aedb]/70 bg-white px-1 shadow-sm shadow-[#0F2854]/10">
                    <button
                      type="button"
                      onClick={() => updateFullSessionQuestionCount(questionCount - 1)}
                      disabled={questionCount <= FULL_SESSION_MIN_QUESTIONS}
                      className="flex size-8 items-center justify-center rounded-lg text-[#1C4D8D] transition hover:bg-[#E7F3FB] hover:text-[#1C4D8D] focus-visible:outline-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-35"
                      aria-label="Giảm một câu"
                    >
                      <Minus className="size-3.5" />
                    </button>
                    <label className="flex items-center gap-1 pr-1">
                      <span className="sr-only">Số câu hỏi luyện trọn gói</span>
                      <input
                        type="number"
                        min={FULL_SESSION_MIN_QUESTIONS}
                        max={FULL_SESSION_MAX_QUESTIONS}
                        value={questionCount}
                        onChange={(event) =>
                          updateFullSessionQuestionCount(Number(event.target.value))
                        }
                        className="h-8 w-10 appearance-none border-0 bg-transparent text-center text-sm font-bold text-[#0F2854] tabular-nums outline-none focus:outline-none focus:ring-0 [appearance:textfield] [&::-webkit-inner-spin-button]:appearance-none [&::-webkit-outer-spin-button]:appearance-none"
                      />
                      <span className="text-xs font-semibold text-[#5b6b84]">câu</span>
                    </label>
                    <button
                      type="button"
                      onClick={() => updateFullSessionQuestionCount(questionCount + 1)}
                      disabled={questionCount >= FULL_SESSION_MAX_QUESTIONS}
                      className="flex size-8 items-center justify-center rounded-lg text-[#1C4D8D] transition hover:bg-[#E7F3FB] hover:text-[#1C4D8D] focus-visible:outline-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-35"
                      aria-label="Tăng một câu"
                    >
                      <Plus className="size-3.5" />
                    </button>
                  </div>
                </div>

                <div className="space-y-2">
                  <input
                    type="range"
                    min={FULL_SESSION_MIN_QUESTIONS}
                    max={FULL_SESSION_MAX_QUESTIONS}
                    value={questionCount}
                    onChange={(event) => updateFullSessionQuestionCount(Number(event.target.value))}
                    className="block h-1.5 w-full cursor-pointer accent-[#1C4D8D]"
                    aria-label="Chọn số câu hỏi từ 3 đến 10"
                  />
                  <div className="flex justify-between font-mono text-xs font-semibold text-[#1C4D8D]">
                    <span>{FULL_SESSION_MIN_QUESTIONS} câu</span>
                    <span>{FULL_SESSION_MAX_QUESTIONS} câu</span>
                  </div>
                </div>
              </section>
            )}

            <MicrophoneCheckPanel check={microphoneCheck} />

            <button
              type="button"
              onClick={() => void handleStartPractice()}
              disabled={isStarting || isLoadingData || !selectedCourse}
              className={cn(ds.primaryButton, 'group w-full')}
            >
              {isStarting ? (
                <LoaderCircle className="size-4 animate-spin" />
              ) : (
                <Mic2 className="size-4" />
              )}
              {isStarting
                ? 'Đang khởi tạo phiên luyện tập…'
                : feedbackMode === 'PER_QUESTION'
                  ? 'Bắt đầu luyện từng câu'
                  : `Bắt đầu gói ${questionCount} câu hỏi`}
              <ArrowRight className="size-4 transition-transform group-hover:translate-x-0.5 motion-reduce:transform-none" />
            </button>
          </div>
        </section>

        <aside ref={historyColumnRef} className="flex min-w-0 flex-col gap-4 lg:h-[var(--practice-column-height,auto)]">
          <section className={cn(ds.section, 'shrink-0')}>
            <div className={ds.sectionHeader}>
              <div className={ds.sectionTitleRow}>
                <span className={ds.sectionIconWrap}><ChartNoAxesCombined className={ds.sectionIcon} aria-hidden="true" /></span>
                <h2 className={ds.sectionTitle}>Tổng quan luyện tập</h2>
              </div>
              <p className={ds.sectionHint}>{isLoadingData ? 'Đang tải thống kê…' : recentPracticeHistory.length > 0 ? `Dựa trên ${recentPracticeHistory.length} phiên gần nhất` : 'Theo dõi kết quả sau mỗi phiên luyện tập'}</p>
            </div>
            <dl className="grid grid-cols-3 gap-3 p-4 sm:p-5">
              <div className={cn('rounded-xl border px-2 py-3 text-center', coloredCardTones.blue)}>
                <dd className="text-2xl font-bold text-[#1C4D8D] tabular-nums">{isLoadingData ? '—' : completedRecentSessions.length}</dd>
                <dt className="mt-1 text-xs font-semibold text-[#5b6b84]">Phiên hoàn thành</dt>
              </div>
              <div className={cn('rounded-xl border px-2 py-3 text-center', coloredCardTones.green)}>
                <dd className="text-2xl font-bold text-[#16836a] tabular-nums">{isLoadingData ? '—' : recentAnsweredQuestions}</dd>
                <dt className="mt-1 text-xs font-semibold text-[#5b6b84]">Câu đã trả lời</dt>
              </div>
              <div className={cn('rounded-xl border px-2 py-3 text-center', coloredCardTones.violet)}>
                <dd className="text-2xl font-bold text-[#6d4cc4] tabular-nums">{recentAverageScore === null ? '—' : recentAverageScore.toFixed(1)}</dd>
                <dt className="mt-1 text-xs font-semibold text-[#5b6b84]">Điểm TB / 10</dt>
              </div>
            </dl>
            <p className="px-4 pb-4 text-xs text-[#5b6b84] sm:px-5">{recentPracticeHistory.length === 0 ? 'Hoàn thành phiên đầu tiên để xem thống kê tại đây.' : 'Điểm trung bình chỉ tính các phiên đã hoàn thành và có điểm.'}</p>
          </section>

          <section className={cn(ds.section, 'flex min-h-0 min-w-0 flex-1 flex-col')}>
            <div className={cn(ds.sectionHeader, 'shrink-0')}>
              <div className={ds.sectionHeading}>
                <div className={ds.sectionTitleRow}>
                  <span className={ds.sectionIconWrap}><History className={ds.sectionIcon} aria-hidden="true" /></span>
                  <h2 className={ds.sectionTitle}>Lịch sử</h2>
                  <span className={ds.sectionBadge}>{practiceHistory.length > 4 ? `4/${practiceHistory.length}` : `${practiceHistory.length} phiên`}</span>
                </div>
                <p className={ds.sectionHint}>{practiceHistory.length > 4 ? `4 phiên gần nhất trong ${practiceHistory.length} lượt` : 'Các phiên luyện tập gần nhất'}</p>
              </div>
            </div>
            <div className={cn(ds.sectionBody, 'flex min-h-0 flex-1 flex-col gap-4', !isLoadingData && practiceHistory.length > 0 && 'gap-2.5 pt-2 pb-3 sm:pt-2 sm:pb-3')}>
              {isLoadingData ? (
                <LoadingState label="Đang tải lịch sử…" className="flex-1" />
              ) : practiceHistory.length === 0 ? (
                <EmptyState className="flex-1 justify-center" icon={History} title="Chưa có phiên luyện tập nào" description="Lịch sử và điểm sẽ hiện sau khi luyện tập." />
              ) : (
                <ul className="grid min-h-0 flex-1 grid-rows-4 overflow-y-auto overscroll-contain">
                  {recentPracticeHistory.slice(0, 4).map((item) => (
                    <li key={item.sessionId} className="relative grid grid-cols-[auto_minmax(0,1fr)] items-center gap-x-3 gap-y-2 rounded-b-lg border-b border-[#b7cce1] px-3 py-2 transition-shadow hover:z-10 hover:shadow-[0_5px_5px_-5px_rgba(15,40,84,0.35)] sm:grid-cols-[auto_minmax(0,1fr)_auto]">
                      <span className="grid size-10 place-items-center rounded-lg bg-[#E7F3FB] text-[#1C4D8D]"><Mic2 className="size-5" aria-hidden="true" /></span>
                      <div className="min-w-0">
                        <h3 className="break-words text-sm leading-snug font-bold text-[#0F2854]">{item.courseName}</h3>
                        <div className="mt-1.5 flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-[#5b6b84]">
                          <span className={cn('rounded-full border px-2 py-0.5 font-bold',
                            item.averageScore !== null ? 'border-[#b7e4cf] bg-[#e8f8f1] text-[#16836a]'
                              : item.status === 'abandoned' ? 'border-[#f3c2c8] bg-[#fdecec] text-[#c2415b]'
                                : item.status === 'in_progress' ? 'border-[#82aedb]/55 bg-[#F4FBFD] text-[#1C4D8D]'
                                  : 'border-[#f3ddb0] bg-[#fff8eb] text-[#9a6414]')}>
                            {item.averageScore !== null ? `${item.averageScore.toFixed(2)}/10` : item.status === 'in_progress' ? 'Đang làm' : item.status === 'abandoned' ? 'Đã bỏ' : 'Đang chấm'}
                          </span>
                          <time dateTime={item.startedAt}>{formatPracticeDate(item.startedAt)}</time>
                          <span>{item.answeredQuestions}/{item.totalQuestions} câu</span>
                        </div>
                      </div>
                      <div className="col-start-2 min-w-0 sm:col-start-3">
                        {item.status === 'in_progress' ? (
                          <Link to={`/practice/session?session=${encodeURIComponent(item.sessionId)}`} className={cn(ds.outlineButton, 'h-9 min-h-9 rounded-lg px-3 text-xs')}>
                            <ArrowRight className="size-3" /> Tiếp tục
                          </Link>
                        ) : (
                          <button type="button" onClick={() => void openPracticeScorecard(item.sessionId)} className={cn(ds.outlineButton, 'h-9 min-h-9 rounded-lg px-3 text-xs')}>
                            <Eye className="size-3" /> Xem điểm
                          </button>
                        )}
                      </div>
                    </li>
                  ))}
                </ul>
              )}
              {!isLoadingData && practiceHistory.length > 0 && (
                <div className="flex h-7 shrink-0 items-center justify-end text-xs">
                  <button type="button" onClick={() => setShowAllHistoryModal(true)} className="ml-auto inline-flex items-center gap-1 rounded-md px-1 py-0.5 font-bold text-[#1C4D8D] transition hover:bg-[#E7F3FB] hover:text-[#0F2854] hover:underline focus-visible:outline-2 focus-visible:outline-[#1C4D8D]">
                    Xem tất cả <ArrowRight className="size-3.5" aria-hidden="true" />
                  </button>
                </div>
              )}
            </div>
          </section>

        </aside>
      </div>

      {showAllHistoryModal && (
        <FormDialog
          titleId="practice-history-title"
          eyebrow="Luyện tập"
          title="Lịch sử luyện tập"
          headerExtra={<span className={ds.sectionBadge}>{practiceHistory.length} phiên</span>}
          bodyClassName="bg-white p-0"
          icon={History}
          onClose={() => setShowAllHistoryModal(false)}
          size="xl"
          align="center"
          maxHeight="max-h-[min(88vh,720px)]"
          footer={(
            <DialogFooter className="bg-white sm:items-center sm:justify-between">
              <Link
                to="/results?type=practice"
                className="inline-flex items-center gap-1 text-xs font-bold text-[#1C4D8D] hover:underline"
              >
                Mở trang Lịch sử & Kết quả đầy đủ
                <ArrowRight className="size-3.5" />
              </Link>
              <button
                type="button"
                onClick={() => setShowAllHistoryModal(false)}
                className={ds.primaryButton}
              >
                Đóng
              </button>
            </DialogFooter>
          )}
        >
          <div className="divide-y divide-[#82aedb]/55 px-5">
            {practiceHistory.map((item) => (
              <article
                key={item.sessionId}
                className="flex min-w-0 flex-col justify-between gap-3 bg-white py-4 sm:flex-row sm:items-center"
              >
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <span
                      className={cn(
                        'rounded-md px-2 py-0.5 text-xs font-black',
                        courseCodeTone(item.courseCode),
                      )}
                    >
                      {item.courseCode}
                    </span>
                    <span className={ds.infoChip}>
                      {getPracticeModeLabel(item.practiceMode)}
                    </span>
                    <span className="text-sm text-[#5b6b84]">
                      {formatPracticeDate(item.startedAt)}
                    </span>
                  </div>
                  <h3 className="mt-1.5 text-sm font-bold text-[#0F2854]">{item.courseName}</h3>
                  <p className="mt-0.5 text-xs text-[#5b6b84]">
                    Đã trả lời {item.answeredQuestions}/{item.totalQuestions} câu chính
                  </p>
                </div>
                <div className="flex flex-wrap items-center gap-2.5">
                  <span className={item.averageScore === null ? ds.infoChip : ds.successChip}>
                    {item.averageScore === null
                      ? 'Đang chấm'
                      : `${item.averageScore.toFixed(1)}/10`}
                  </span>
                  <button
                    type="button"
                    onClick={() => {
                      setShowAllHistoryModal(false)
                      void openPracticeScorecard(item.sessionId)
                    }}
                    className={cn(ds.outlineButton, 'shrink-0')}
                  >
                    <FileCheck2 className="size-3.5" />
                    Xem câu hỏi & nhận xét
                  </button>
                </div>
              </article>
            ))}
          </div>
        </FormDialog>
      )}

      {selectedScorecardId && (
        <FormDialog
          titleId="practice-scorecard-title"
          eyebrow="Luyện tập"
          title="Chi tiết phiên luyện tập"
          icon={Award}
          onClose={() => setSelectedScorecardId(null)}
          size="2xl"
          align="center"
          maxHeight="max-h-[min(90vh,820px)]"
          footer={(
            <DialogFooter className="bg-white">
              <button
                type="button"
                onClick={() => setSelectedScorecardId(null)}
                className={ds.primaryButton}
              >
                Đóng
              </button>
            </DialogFooter>
          )}
        >
          {isLoadingScorecard ? (
            <LoadingState label="Đang tải chi tiết phiên luyện tập…" />
          ) : scorecardMap[selectedScorecardId] ? (
            <PracticeSessionScorecard session={scorecardMap[selectedScorecardId]} />
          ) : null}
        </FormDialog>
      )}

      <div className="sr-only" aria-live="polite">
        {voicePreviewMessage}
        {microphoneState === 'ready' && ` Microphone ${microphoneName} đã sẵn sàng.`}
      </div>
    </div>
  )
}

export { PracticePage as PracticeSetupPage }
