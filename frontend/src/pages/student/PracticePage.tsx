import { lazy, Suspense, useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import {
  ArrowRight,
  AlertCircle,
  AudioLines,
  Award,
  BookOpenText,
  Check,
  ChevronDown,
  ChevronRight,
  Clock3,
  FileCheck2,
  Headphones,
  History,
  Lightbulb,
  LoaderCircle,
  Mic2,
  Minus,
  Plus,
  Radio,
  RefreshCw,
  Settings2,
  ShieldCheck,
  Volume2,
  X,
} from 'lucide-react'
import { cn } from '@/lib/utils'
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
import { PracticeSessionScorecard } from '@/components/practice/PracticeSessionScorecard'
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

type MicrophoneState = 'idle' | 'checking' | 'ready' | 'error' | 'unsupported'

const FULL_SESSION_MIN_QUESTIONS = 3
const FULL_SESSION_MAX_QUESTIONS = 10

const COURSE_CODE_TONES = [
  'bg-[#4988C4]/16 text-[#1C4D8D]',
  'bg-[#e8f8f3] text-[#16836a]',
  'bg-[#fff4e8] text-[#c26a12]',
  'bg-[#f3eefe] text-[#6d4cc4]',
  'bg-[#e7f6fb] text-[#0e7490]',
  'bg-[#fdecef] text-[#c2415b]',
  'bg-[#eef8e8] text-[#3d7a2a]',
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
    label: 'Dễ',
    helper: 'Củng cố nền tảng',
    selectedClassName: 'border-[#82d8bd] bg-[#f0fbf7] shadow-[0_0_0_2px_rgba(22,131,103,0.08)]',
    badgeClassName: 'bg-[#16836a] text-white',
  },
  {
    value: 'medium',
    label: 'Trung bình',
    helper: 'Vận dụng kiến thức',
    selectedClassName: 'border-[#e8c77f] bg-[#fffaf0] shadow-[0_0_0_2px_rgba(166,108,18,0.08)]',
    badgeClassName: 'bg-[#c26a12] text-white',
  },
  {
    value: 'hard',
    label: 'Khó',
    helper: 'Phân tích chuyên sâu',
    selectedClassName: 'border-[#e9a9b0] bg-[#fff5f6] shadow-[0_0_0_2px_rgba(184,64,75,0.08)]',
    badgeClassName: 'bg-[#c2415b] text-white',
  },
]

const PRACTICE_TIPS = [
  'Hãy giữ tốc độ nói ổn định từ 110–130 từ/phút và trả lời theo cấu trúc: định nghĩa – cơ chế – ví dụ.',
  'Dành 5 giây đầu để sắp xếp ý, sau đó nêu luận điểm cốt lõi trước khi đi vào phần giải thích chi tiết.',
  'Khi gặp thuật ngữ chuyên ngành tiếng Anh, hãy phát âm rõ và giải thích thêm ý nghĩa thực tế trong dự án.',
]

function getMicrophoneStatus(state: MicrophoneState) {
  switch (state) {
    case 'checking':
      return {
        label: 'Đang kiểm tra microphone…',
        helper: 'Trình duyệt có thể yêu cầu quyền sử dụng micro.',
        className: 'text-[#1C4D8D]',
        dotClassName: 'bg-[#1C4D8D] animate-pulse motion-reduce:animate-none',
      }
    case 'ready':
      return {
        label: 'Microphone đã sẵn sàng',
        helper: 'Thiết bị sẽ được dùng cho phiên luyện tập.',
        className: 'text-[#15866a]',
        dotClassName: 'bg-[#37ca98] shadow-[0_0_8px_rgba(55,202,152,0.65)]',
      }
    case 'error':
      return {
        label: 'Chưa thể truy cập microphone',
        helper: 'Hãy cấp quyền micro trong trình duyệt rồi thử lại.',
        className: 'text-[#d34d63]',
        dotClassName: 'bg-[#ef6177]',
      }
    case 'unsupported':
      return {
        label: 'Trình duyệt chưa hỗ trợ kiểm tra micro',
        helper: 'Bạn vẫn có thể tiếp tục và kiểm tra lại trong phiên.',
        className: 'text-[#a16207]',
        dotClassName: 'bg-[#e9a23b]',
      }
    default:
      return {
        label: 'Microphone chưa được kiểm tra',
        helper: 'Kiểm tra trước để phiên luyện tập không bị gián đoạn.',
        className: 'text-[#53647b]',
        dotClassName: 'bg-[#9aa9bc]',
      }
  }
}

function formatPracticeDate(value: string) {
  return new Intl.DateTimeFormat('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  }).format(new Date(value))
}

function getPracticeModeLabel(mode: PracticeHistoryItem['practiceMode']) {
  return mode === 'per_question' ? 'Luyện từng câu' : 'Luyện trọn gói'
}

export function PracticePage() {
  const navigate = useNavigate()
  const { user } = useAuth()
  const [courses, setCourses] = useState<CourseDto[]>([])
  const [practiceHistory, setPracticeHistory] = useState<PracticeHistoryItem[]>([])
  const [selectedCourseId, setSelectedCourseId] = useState('')
  const [feedbackMode, setFeedbackMode] = useState<PracticeMode>('PER_QUESTION')
  const [selectedDifficulties, setSelectedDifficulties] = useState<PracticeDifficulty[]>(['medium'])
  const [questionCount, setQuestionCount] = useState(5)
  const [microphoneState, setMicrophoneState] = useState<MicrophoneState>('idle')
  const [microphoneName, setMicrophoneName] = useState('')
  const [isVoicePreviewPlaying, setIsVoicePreviewPlaying] = useState(false)
  const [voicePreviewMessage, setVoicePreviewMessage] = useState('')
  const speechRequestRef = useRef(0)
  const [expandedHistoryId, setExpandedHistoryId] = useState<string | null>(null)
  const [showAllHistoryModal, setShowAllHistoryModal] = useState(false)
  const [selectedScorecardId, setSelectedScorecardId] = useState<string | null>(null)
  const [scorecardMap, setScorecardMap] = useState<Record<string, PracticeSessionDetail>>({})
  const [isLoadingScorecard, setIsLoadingScorecard] = useState(false)
  const [scorecardError, setScorecardError] = useState('')
  const [tipIndex, setTipIndex] = useState(0)
  const [isLoadingData, setIsLoadingData] = useState(true)
  const [isStarting, setIsStarting] = useState(false)
  const [pageError, setPageError] = useState('')
  const [errorSource, setErrorSource] = useState<'load' | 'start' | null>(null)

  const selectedCourse = useMemo(
    () => courses.find((course) => course.id === selectedCourseId) ?? null,
    [courses, selectedCourseId],
  )
  const estimatedMinutes = questionCount * 3
  const microphoneStatus = getMicrophoneStatus(microphoneState)

  const openPracticeScorecard = useCallback(
    async (sessionId: string) => {
      setSelectedScorecardId(sessionId)
      setScorecardError('')
      if (scorecardMap[sessionId]) return

      setIsLoadingScorecard(true)
      try {
        const detail = await practiceService.getSession(sessionId)
        setScorecardMap((prev) => ({ ...prev, [sessionId]: detail }))
      } catch (err) {
        const problem = err as Partial<{ detail: string; error: string; message: string }>
        setScorecardError(
          problem.detail ||
            problem.error ||
            problem.message ||
            'Không thể tải chi tiết câu hỏi và nhận xét phiên luyện tập.',
        )
      } finally {
        setIsLoadingScorecard(false)
      }
    },
    [scorecardMap],
  )

  const loadPracticeData = useCallback(async () => {
    if (!user) return
    setIsLoadingData(true)
    setPageError('')
    setErrorSource(null)

    try {
      const [coursesResult, historyResult] = await Promise.allSettled([
        courseService.getCourses(),
        practiceService.getStudentHistory(user.id),
      ])
      if (coursesResult.status === 'rejected') throw coursesResult.reason

      const availableCourses = coursesResult.value
      setCourses(availableCourses)
      if (historyResult.status === 'fulfilled') {
        setPracticeHistory(historyResult.value)
      } else {
        setPracticeHistory([])
        const historyProblem = historyResult.reason as Partial<{
          detail: string
          error: string
          message: string
        }>
        setPageError(
          historyProblem.detail ||
            historyProblem.error ||
            historyProblem.message ||
            'Đã tải danh sách môn học nhưng chưa thể tải lịch sử luyện tập.',
        )
        setErrorSource('load')
      }
      setSelectedCourseId((current) =>
        availableCourses.some((course) => course.id === current)
          ? current
          : (availableCourses[0]?.id ?? ''),
      )
    } catch (loadError) {
      const problem = loadError as Partial<{ detail: string; error: string; message: string }>
      setPageError(
        problem.detail || problem.error || problem.message || 'Không thể tải dữ liệu luyện tập.',
      )
      setErrorSource('load')
    } finally {
      setIsLoadingData(false)
    }
  }, [user])

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

  const handleMicrophoneCheck = async () => {
    if (!navigator.mediaDevices?.getUserMedia) {
      setMicrophoneState('unsupported')
      return
    }

    setMicrophoneState('checking')
    let stream: MediaStream | undefined

    try {
      stream = await navigator.mediaDevices.getUserMedia({
        audio: {
          echoCancellation: true,
          noiseSuppression: true,
        },
      })
      const audioTrack = stream.getAudioTracks()[0]
      setMicrophoneName(audioTrack?.label || 'Thiết bị âm thanh mặc định')
      setMicrophoneState('ready')
    } catch {
      setMicrophoneName('')
      setMicrophoneState('error')
    } finally {
      stream?.getTracks().forEach((track) => track.stop())
    }
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
    setPageError('')
    setErrorSource(null)
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
      const problem = startError as Partial<{ detail: string; error: string; message: string }>
      setPageError(
        problem.detail || problem.error || problem.message || 'Không thể bắt đầu phiên luyện tập.',
      )
      setErrorSource('start')
    } finally {
      setIsStarting(false)
    }
  }

  return (
    <div className={ds.page}>
      <section className={cn(ds.banner, 'min-h-[218px] sm:min-h-[206px]')}>
        <div className="pointer-events-none absolute inset-0 [background-image:radial-gradient(rgba(255,255,255,0.35)_0.8px,transparent_0.8px)] [background-size:22px_22px] opacity-40" />
        <div className="pointer-events-none absolute inset-y-0 right-0 w-[78%] bg-[radial-gradient(circle_at_72%_50%,rgba(189,232,245,0.22),transparent_42%)] sm:w-[62%]" />

        <div className="relative z-10 flex min-h-[218px] max-w-[720px] flex-col items-start justify-center px-5 py-6 sm:min-h-[206px] sm:px-7 lg:max-w-[62%] lg:px-8">
          <div className="inline-flex items-center gap-2 rounded-full border border-white/25 bg-white/14 px-3 py-1 text-xs font-bold tracking-[0.06em] text-white uppercase backdrop-blur-sm">
            <Mic2 className="size-3.5 text-[#BDE8F5]" />
            Luyện tập tương tác theo học phần
          </div>
          <h1 className="mt-3 max-w-[620px] text-[clamp(1.55rem,3vw,2.2rem)] leading-[1.08] font-bold tracking-[-0.04em] text-white">
            Phòng Luyện tập Vấn đáp Chuyên ngành
          </h1>
          <p className="mt-2 max-w-[590px] text-sm leading-relaxed text-white/85">
            Chủ động luyện phản xạ trả lời câu hỏi chuyên môn bằng giọng nói hoặc văn bản, nhận
            chấm điểm theo tiêu chí Rubric và câu hỏi đào sâu ngay trong phiên.
          </p>

          <button
            type="button"
            onClick={handleVoicePreview}
            className="mt-4 inline-flex min-h-9 items-center gap-2 rounded-xl border border-white/30 bg-white px-3.5 text-xs font-bold text-[#0F2854] shadow-sm transition hover:-translate-y-0.5 hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white motion-reduce:transform-none"
          >
            {isVoicePreviewPlaying ? (
              <AudioLines className="size-4 animate-pulse text-[#1C4D8D] motion-reduce:animate-none" />
            ) : (
              <Volume2 className="size-4 text-[#1C4D8D]" />
            )}
            {isVoicePreviewPlaying ? 'Dừng nghe thử' : 'Nghe thử giọng đọc đề'}
          </button>
        </div>

        <div className="pointer-events-none absolute inset-y-0 right-[-16%] z-0 w-[82%] opacity-55 sm:right-[-8%] sm:w-[66%] sm:opacity-85 lg:right-0 lg:w-[50%] lg:opacity-100">
          <Suspense
            fallback={
              <div className="flex size-full items-center justify-center" aria-hidden="true">
                <div className="size-20 animate-pulse rounded-full border border-[#7ce8f4]/30 bg-[#3dd5ee]/10 motion-reduce:animate-none" />
              </div>
            }
          >
            <PracticeHeroThreeScene active={isVoicePreviewPlaying} />
          </Suspense>
        </div>
      </section>

      {pageError && (
        <div
          className="flex items-start justify-between gap-4 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-red-700"
          role="alert"
        >
          <div className="flex items-start gap-2 text-xs leading-relaxed font-semibold">
            <AlertCircle className="mt-0.5 size-4 shrink-0" />
            <span>{pageError}</span>
          </div>
          <button
            type="button"
            onClick={() =>
              errorSource === 'start' ? void handleStartPractice() : void loadPracticeData()
            }
            className="shrink-0 rounded-lg border border-red-200 bg-white px-3 py-1.5 text-xs font-bold transition hover:bg-red-100 focus-visible:outline-2 focus-visible:outline-red-500"
          >
            {errorSource === 'start' ? 'Thử bắt đầu lại' : 'Thử tải lại'}
          </button>
        </div>
      )}

      <div className="grid items-start gap-4 lg:grid-cols-[minmax(0,1.7fr)_minmax(290px,0.78fr)]">
        <section className={cn(ds.section, 'overflow-hidden')}>
          <div className={ds.sectionHeader}>
            <div className={ds.sectionTitleRow}>
              <span className={ds.sectionIconWrap}>
                <Settings2 className="size-4 text-[#1C4D8D]" />
              </span>
              <h2 className={ds.sectionTitle}>Thiết lập phiên luyện tập</h2>
            </div>
            <p className={ds.sectionHint}>
              Chọn học phần, hình thức luyện tập và mức độ câu hỏi phù hợp với mục tiêu ôn tập của bạn
            </p>
          </div>

          <div className="space-y-5 p-5 sm:p-6">
            <div>
              <label
                htmlFor="practice-course"
                className="text-xs font-bold tracking-[0.02em] text-[#0F2854]"
              >
                Học phần & chuyên đề vấn đáp
              </label>
              <div className="relative mt-2">
                <BookOpenText className="pointer-events-none absolute top-1/2 left-3.5 size-4 -translate-y-1/2 text-[#1C4D8D]" />
                <select
                  id="practice-course"
                  value={selectedCourseId}
                  onChange={(event) => setSelectedCourseId(event.target.value)}
                  disabled={isLoadingData || courses.length === 0}
                  className="min-h-12 w-full appearance-none rounded-xl border border-[#4988C4] bg-[#F4FBFD] py-2 pr-10 pl-10 text-sm font-bold text-[#0F2854] transition hover:border-[#1C4D8D] focus:border-[#1C4D8D] focus:ring-3 focus:ring-[#1C4D8D]/10 focus:outline-none"
                >
                  {courses.length === 0 && (
                    <option value="">
                      {isLoadingData ? 'Đang tải danh sách môn học…' : 'Chưa có môn học khả dụng'}
                    </option>
                  )}
                  {courses.map((course) => (
                    <option key={course.id} value={course.id}>
                      {course.name} ({course.code}) — {course.credits} tín chỉ
                    </option>
                  ))}
                </select>
                <ChevronDown className="pointer-events-none absolute top-1/2 right-3.5 size-4 -translate-y-1/2 text-[#1C4D8D]" />
              </div>

              {courses.length > 0 && (
                <div className="mt-2.5 flex flex-wrap gap-1.5">
                  {courses.map((course) => {
                    const active = course.id === selectedCourseId
                    return (
                      <button
                        key={course.id}
                        type="button"
                        onClick={() => setSelectedCourseId(course.id)}
                        className={cn(
                          'inline-flex items-center gap-1.5 rounded-lg border px-2.5 py-1 text-xs font-bold transition',
                          active
                            ? 'border-[#1C4D8D] bg-[#1C4D8D] text-white shadow-2xs'
                            : cn(
                                'border-transparent hover:opacity-90',
                                courseCodeTone(course.code),
                              ),
                        )}
                      >
                        <span>{course.code}</span>
                      </button>
                    )
                  })}
                </div>
              )}
            </div>

            <fieldset>
              <legend className="text-xs font-bold tracking-[0.02em] text-[#0F2854]">
                Chế độ luyện tập & chấm điểm
              </legend>
              <div className="mt-2 grid gap-3 sm:grid-cols-2">
                <label
                  className={cn(
                    'group relative cursor-pointer rounded-2xl border p-4 transition duration-200 focus-within:ring-2 focus-within:ring-[#1C4D8D] focus-within:ring-offset-2 hover:-translate-y-0.5 hover:shadow-[0_8px_22px_rgba(31,61,105,0.08)] motion-reduce:transform-none',
                    feedbackMode === 'PER_QUESTION'
                      ? 'border-[#4988C4] bg-[#f3f9fd] shadow-[0_0_0_2px_rgba(28,77,141,0.1)]'
                      : 'border-[#dce4ee] bg-white',
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
                          : 'border-[#cad4e2] bg-white text-transparent',
                      )}
                    >
                      <Check className="size-3" />
                    </span>
                  </div>
                  <h3 className="mt-3 text-sm font-bold text-[#0F2854]">Luyện từng câu</h3>
                  <p className="mt-1 text-xs leading-relaxed text-[#5b6b84]">
                    Chọn một hoặc nhiều mức độ và nhận nhận xét chi tiết ngay sau từng câu trả lời.
                  </p>
                  <span className="mt-3 inline-flex items-center gap-1 rounded-full bg-white px-2.5 py-1 text-xs font-bold text-[#1C4D8D]">
                    <Radio className="size-3" /> Không giới hạn câu · Có hỏi phụ đào sâu
                  </span>
                </label>

                <label
                  className={cn(
                    'group relative cursor-pointer rounded-2xl border p-4 transition duration-200 focus-within:ring-2 focus-within:ring-[#1C4D8D] focus-within:ring-offset-2 hover:-translate-y-0.5 hover:shadow-[0_8px_22px_rgba(31,61,105,0.08)] motion-reduce:transform-none',
                    feedbackMode === 'FULL_SESSION'
                      ? 'border-[#b79df2] bg-[#f7f3fd] shadow-[0_0_0_2px_rgba(109,76,196,0.1)]'
                      : 'border-[#dce4ee] bg-white',
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
                          : 'border-[#cad4e2] bg-white text-transparent',
                      )}
                    >
                      <Check className="size-3" />
                    </span>
                  </div>
                  <h3 className="mt-3 text-sm font-bold text-[#0F2854]">Luyện trọn gói</h3>
                  <p className="mt-1 text-xs leading-relaxed text-[#5b6b84]">
                    Chọn 3–10 câu theo lộ trình tăng dần độ khó và nhận bảng điểm tổng kết cuối phiên.
                  </p>
                  <span className="mt-3 inline-flex items-center gap-1 rounded-full bg-white px-2.5 py-1 text-xs font-bold text-[#6d4cc4]">
                    <Clock3 className="size-3" /> 3–10 câu tăng dần · Không hỏi phụ
                  </span>
                </label>
              </div>
            </fieldset>

            {feedbackMode === 'PER_QUESTION' ? (
              <section className="space-y-3 rounded-2xl border border-[#4988C4]/45 bg-[#F4FBFD] p-4">
                <div className="flex flex-col gap-1 sm:flex-row sm:items-end sm:justify-between">
                  <div>
                    <h3 className="text-xs font-bold tracking-[0.02em] text-[#0F2854]">
                      Chọn mức độ câu hỏi
                    </h3>
                    <p className="mt-0.5 text-xs text-[#5b6b84]">
                      Chọn ít nhất một mức; có thể kết hợp nhiều mức trong cùng phiên luyện tập.
                    </p>
                  </div>
                  <span className="rounded-full bg-white px-2.5 py-0.5 text-xs font-bold text-[#1C4D8D]">
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
                          'flex min-h-16 items-center gap-3 rounded-xl border px-3 text-left transition hover:-translate-y-0.5 hover:shadow-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transform-none',
                          selected
                            ? option.selectedClassName
                            : 'border-[#dce4ee] bg-white hover:border-[#4988C4]',
                        )}
                        title={
                          isOnlySelection ? 'Phiên luyện tập cần ít nhất một mức độ.' : undefined
                        }
                      >
                        <span
                          className={cn(
                            'flex size-8 shrink-0 items-center justify-center rounded-lg text-xs font-black',
                            selected ? option.badgeClassName : 'bg-[#f0f3f7] text-[#78879a]',
                          )}
                        >
                          {option.label.charAt(0)}
                        </span>
                        <span className="min-w-0 flex-1">
                          <span className="block text-xs font-bold text-[#0F2854]">
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
                              : 'border-[#cbd5e2] text-transparent',
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
              <section className="rounded-2xl border border-[#d4c4f5] bg-[#f7f3fd] p-4">
                <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                  <div className="flex items-center gap-3">
                    <div className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-[#6d4cc4] text-white shadow-xs">
                      <Clock3 className="size-4" />
                    </div>
                    <div>
                      <p className="text-xs font-bold tracking-[0.06em] text-[#6d4cc4] uppercase">
                        Lộ trình tăng dần · Dễ → Trung bình → Khó
                      </p>
                      <p className="mt-0.5 text-sm font-bold text-[#0F2854]">
                        {questionCount} câu hỏi · khoảng {estimatedMinutes} phút
                      </p>
                      <p className="mt-0.5 text-xs text-[#5b6b84]">
                        Số lượng câu hỏi mỗi gói: {FULL_SESSION_MIN_QUESTIONS}–
                        {FULL_SESSION_MAX_QUESTIONS} câu.
                      </p>
                    </div>
                  </div>

                  <div className="flex items-center gap-2 rounded-xl border border-[#d4c4f5] bg-white p-1.5">
                    <button
                      type="button"
                      onClick={() => updateFullSessionQuestionCount(questionCount - 1)}
                      disabled={questionCount <= FULL_SESSION_MIN_QUESTIONS}
                      className="flex size-8 items-center justify-center rounded-lg text-[#5e7088] transition hover:bg-[#f3eefe] hover:text-[#6d4cc4] focus-visible:outline-2 focus-visible:outline-[#6d4cc4] disabled:cursor-not-allowed disabled:opacity-35"
                      aria-label="Giảm một câu"
                    >
                      <Minus className="size-3.5" />
                    </button>
                    <label className="flex min-w-16 items-baseline justify-center gap-1">
                      <span className="sr-only">Số câu hỏi luyện trọn gói</span>
                      <input
                        type="number"
                        min={FULL_SESSION_MIN_QUESTIONS}
                        max={FULL_SESSION_MAX_QUESTIONS}
                        value={questionCount}
                        onChange={(event) =>
                          updateFullSessionQuestionCount(Number(event.target.value))
                        }
                        className="w-8 rounded bg-transparent text-center font-mono text-sm font-black text-[#6d4cc4] outline-none focus-visible:ring-2 focus-visible:ring-[#6d4cc4]"
                      />
                      <span className="text-xs font-semibold text-[#5b6b84]">câu</span>
                    </label>
                    <button
                      type="button"
                      onClick={() => updateFullSessionQuestionCount(questionCount + 1)}
                      disabled={questionCount >= FULL_SESSION_MAX_QUESTIONS}
                      className="flex size-8 items-center justify-center rounded-lg text-[#5e7088] transition hover:bg-[#f3eefe] hover:text-[#6d4cc4] focus-visible:outline-2 focus-visible:outline-[#6d4cc4] disabled:cursor-not-allowed disabled:opacity-35"
                      aria-label="Tăng một câu"
                    >
                      <Plus className="size-3.5" />
                    </button>
                  </div>
                </div>

                <input
                  type="range"
                  min={FULL_SESSION_MIN_QUESTIONS}
                  max={FULL_SESSION_MAX_QUESTIONS}
                  value={questionCount}
                  onChange={(event) => updateFullSessionQuestionCount(Number(event.target.value))}
                  className="mt-4 h-1.5 w-full cursor-pointer accent-[#6d4cc4]"
                  aria-label="Chọn số câu hỏi từ 3 đến 10"
                />
                <div className="mt-1.5 flex justify-between font-mono text-xs font-semibold text-[#6d4cc4]">
                  <span>{FULL_SESSION_MIN_QUESTIONS} câu</span>
                  <span>{FULL_SESSION_MAX_QUESTIONS} câu</span>
                </div>
              </section>
            )}

            <div className="flex flex-col gap-3 rounded-2xl border border-[#b7e4cf] bg-[#f3fbf7] p-3.5 sm:flex-row sm:items-center sm:justify-between">
              <div className="flex min-w-0 items-center gap-3">
                <div className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-[#16836a] text-white">
                  <Mic2 className="size-4" />
                </div>
                <div className="min-w-0">
                  <p
                    className={cn(
                      'flex items-center gap-1.5 text-xs font-bold',
                      microphoneStatus.className,
                    )}
                  >
                    <span
                      className={cn(
                        'size-1.5 shrink-0 rounded-full',
                        microphoneStatus.dotClassName,
                      )}
                    />
                    {microphoneStatus.label}
                  </p>
                  <p className="mt-0.5 truncate text-xs text-[#4b675d]">
                    {microphoneName || microphoneStatus.helper}
                  </p>
                </div>
              </div>
              <button
                type="button"
                onClick={handleMicrophoneCheck}
                disabled={microphoneState === 'checking'}
                className="inline-flex min-h-9 shrink-0 items-center justify-center gap-1.5 rounded-xl border border-[#82d8bd] bg-white px-3.5 text-xs font-bold text-[#16836a] transition hover:bg-[#e8f8f3] focus-visible:outline-2 focus-visible:outline-[#16836a] disabled:cursor-wait disabled:opacity-60"
              >
                {microphoneState === 'checking' ? (
                  <LoaderCircle className="size-3.5 animate-spin" />
                ) : (
                  <RefreshCw className="size-3.5" />
                )}
                {microphoneState === 'ready' ? 'Kiểm tra lại' : 'Kiểm tra micro'}
              </button>
            </div>

            <button
              type="button"
              onClick={() => void handleStartPractice()}
              disabled={isStarting || isLoadingData || !selectedCourse}
              className="group inline-flex min-h-12 w-full items-center justify-center gap-2 rounded-xl bg-[#1C4D8D] px-5 text-sm font-bold text-white shadow-[0_12px_28px_rgba(36,94,234,0.24)] transition duration-200 hover:-translate-y-0.5 hover:bg-[#0F2854] hover:shadow-[0_16px_34px_rgba(36,94,234,0.3)] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-60 motion-reduce:transform-none"
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

        <aside className="space-y-4">
          <section className={cn(ds.section, 'overflow-hidden')}>
            <div className={cn(ds.sectionHeader, 'flex items-center justify-between gap-2')}>
              <div className={ds.sectionTitleRow}>
                <span className={ds.sectionIconWrap}>
                  <History className="size-4 text-[#1C4D8D]" />
                </span>
                <h2 className="text-base font-bold text-[#0F2854] sm:text-lg">Lịch sử gần đây</h2>
              </div>
              <span className="text-xs font-semibold text-[#1C4D8D]">
                {practiceHistory.length > 0 ? (
                  <button
                    type="button"
                    onClick={() => setShowAllHistoryModal(true)}
                    className="rounded-lg border border-[#4988C4] bg-white px-2.5 py-1 text-xs font-bold text-[#1C4D8D] transition hover:bg-[#F4FBFD]"
                  >
                    Tất cả ({practiceHistory.length})
                  </button>
                ) : (
                  '0 phiên'
                )}
              </span>
            </div>

            <div className="space-y-2.5 p-3.5">
              {isLoadingData && (
                <div className="flex items-center justify-center gap-2 py-8 text-xs font-semibold text-[#75859a]">
                  <LoaderCircle className="size-4 animate-spin" /> Đang tải lịch sử…
                </div>
              )}
              {!isLoadingData && practiceHistory.length === 0 && (
                <p className="py-8 text-center text-xs leading-relaxed text-[#7b899c]">
                  Chưa có phiên luyện tập nào.
                </p>
              )}
              {!isLoadingData &&
                practiceHistory.slice(0, 4).map((item) => {
                  const isExpanded = expandedHistoryId === item.sessionId

                  return (
                    <article
                      key={item.sessionId}
                      className={cn(
                        'rounded-xl border transition',
                        isExpanded
                          ? 'border-[#4988C4] bg-[#f3f9fd] shadow-xs'
                          : 'border-[#dce4ee] bg-white hover:border-[#4988C4]',
                      )}
                    >
                      <button
                        type="button"
                        onClick={() => setExpandedHistoryId(isExpanded ? null : item.sessionId)}
                        className="w-full p-3 text-left focus-visible:rounded-xl focus-visible:outline-2 focus-visible:outline-[#1C4D8D]"
                        aria-expanded={isExpanded}
                      >
                        <div className="flex items-start justify-between gap-2.5">
                          <div className="min-w-0">
                            <div className="flex flex-wrap items-center gap-1.5">
                              <span
                                className={cn(
                                  'rounded-md px-2 py-0.5 text-[11px] font-black',
                                  courseCodeTone(item.courseCode),
                                )}
                              >
                                {item.courseCode}
                              </span>
                              <span className="rounded-md bg-[#f1f5f9] px-2 py-0.5 text-[11px] font-bold text-[#475569]">
                                {getPracticeModeLabel(item.practiceMode)}
                              </span>
                            </div>
                            <p className="mt-1.5 truncate text-xs font-bold text-[#0F2854]">
                              {item.courseName}
                            </p>
                            <p className="mt-0.5 text-[11px] text-[#64748b]">
                              {formatPracticeDate(item.startedAt)}
                            </p>
                          </div>
                          <strong
                            className={cn(
                              'shrink-0 rounded-lg px-2 py-1 text-xs font-black',
                              item.averageScore === null
                                ? 'bg-[#fff8eb] text-[#9a6414]'
                                : 'bg-[#e8f8f3] text-[#16836a]',
                            )}
                          >
                            {item.averageScore === null
                              ? 'Đang chấm'
                              : `${item.averageScore.toFixed(1)}/10`}
                          </strong>
                        </div>
                        <div className="mt-2 flex items-center justify-between text-xs font-bold text-[#1C4D8D]">
                          <span>{isExpanded ? 'Thu gọn' : 'Chi tiết & nhận xét'}</span>
                          <ChevronRight
                            className={cn('size-3.5 transition-transform', isExpanded && 'rotate-90')}
                          />
                        </div>
                      </button>
                      {isExpanded && (
                        <div className="border-t border-[#dce4ee] px-3 py-2.5 text-xs leading-relaxed text-[#5b6b84]">
                          Đã trả lời {item.answeredQuestions}/{item.totalQuestions} câu chính.{' '}
                          {item.status === 'completed'
                            ? 'Phiên đã hoàn tất.'
                            : item.status === 'abandoned'
                              ? 'Phiên đã kết thúc.'
                              : 'Phiên đang diễn ra.'}
                          <div className="mt-2.5 flex flex-wrap items-center gap-2">
                            <button
                              type="button"
                              onClick={() => void openPracticeScorecard(item.sessionId)}
                              className="inline-flex min-h-8 items-center gap-1.5 rounded-lg bg-[#1C4D8D] px-3 py-1.5 text-xs font-bold text-white shadow-2xs transition hover:bg-[#0F2854]"
                            >
                              <FileCheck2 className="size-3.5" />
                              Xem câu hỏi & nhận xét
                            </button>
                            {item.status === 'in_progress' && (
                              <Link
                                to={`/practice/session?session=${encodeURIComponent(item.sessionId)}`}
                                className="inline-flex min-h-8 items-center gap-1 rounded-lg border border-[#4988C4] bg-white px-2.5 py-1.5 text-xs font-bold text-[#1C4D8D] transition hover:bg-[#4988C4]/16"
                              >
                                Tiếp tục
                                <ArrowRight className="size-3" />
                              </Link>
                            )}
                          </div>
                        </div>
                      )}
                    </article>
                  )
                })}
            </div>
          </section>

          <section className="rounded-2xl border border-[#f5d08a] bg-[#fffaf0] p-4 shadow-xs">
            <div className="flex items-center gap-2 text-[#c26a12]">
              <Lightbulb className="size-4" />
              <h2 className="text-sm font-bold text-[#0F2854]">Kinh nghiệm trả lời vấn đáp</h2>
            </div>
            <p className="mt-2.5 min-h-14 text-xs leading-relaxed text-[#5c4826]">
              {PRACTICE_TIPS[tipIndex]}
            </p>
            <button
              type="button"
              onClick={() => setTipIndex((current) => (current + 1) % PRACTICE_TIPS.length)}
              className="mt-2.5 inline-flex min-h-8 items-center gap-1.5 rounded-lg bg-white px-2.5 text-xs font-bold text-[#c26a12] shadow-2xs transition hover:bg-[#fff3d6]"
            >
              Xem gợi ý tiếp theo
              <ChevronRight className="size-3" />
            </button>
          </section>

          <div className="flex items-center gap-3 rounded-2xl border border-[#b7e4cf] bg-[#f3fbf7] p-3.5 text-xs leading-relaxed text-[#2b5b4c]">
            <Headphones className="size-4 shrink-0 text-[#16836a]" />
            Nên sử dụng tai nghe có micro ở nơi yên tĩnh để bản thu âm rõ ràng và chính xác nhất.
          </div>
        </aside>
      </div>

      {showAllHistoryModal && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-[#0F2854]/60 p-3 backdrop-blur-xs sm:p-5"
          role="dialog"
          aria-modal="true"
          aria-label="Toàn bộ lịch sử luyện tập"
        >
          <div className="flex max-h-[88vh] w-full max-w-3xl flex-col overflow-hidden rounded-2xl border border-[#4988C4]/45 bg-[#F4FBFD] shadow-2xl">
            <div className="flex items-center justify-between gap-3 border-b border-[#4988C4]/45 bg-white px-5 py-3.5">
              <div className="flex items-center gap-2">
                <History className="size-5 text-[#1C4D8D]" />
                <h2 className="text-base font-bold text-[#0F2854]">
                  Toàn bộ lịch sử luyện tập ({practiceHistory.length} phiên)
                </h2>
              </div>
              <button
                type="button"
                onClick={() => setShowAllHistoryModal(false)}
                className="inline-flex size-8 items-center justify-center rounded-lg text-[#5b6b84] transition hover:bg-[#4988C4]/16 hover:text-[#0F2854]"
                aria-label="Đóng lịch sử luyện tập"
              >
                <X className="size-4" />
              </button>
            </div>
            <div className="flex-1 space-y-2.5 overflow-y-auto p-4 sm:p-5">
              {practiceHistory.map((item) => (
                <article
                  key={item.sessionId}
                  className="flex flex-col justify-between gap-3 rounded-2xl border border-[#4988C4]/45 bg-white p-4 sm:flex-row sm:items-center"
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
                      <span className="rounded-md bg-[#4988C4]/16 px-2 py-0.5 text-xs font-bold text-[#1C4D8D]">
                        {getPracticeModeLabel(item.practiceMode)}
                      </span>
                      <span className="text-xs font-medium text-[#5b6b84]">
                        {formatPracticeDate(item.startedAt)}
                      </span>
                    </div>
                    <h3 className="mt-1.5 text-sm font-bold text-[#0F2854]">{item.courseName}</h3>
                    <p className="mt-0.5 text-xs text-[#5b6b84]">
                      Đã trả lời {item.answeredQuestions}/{item.totalQuestions} câu chính
                    </p>
                  </div>
                  <div className="flex flex-wrap items-center gap-2.5">
                    <span className="rounded-xl bg-[#4988C4]/14 px-3 py-1.5 text-sm font-black text-[#0F2854]">
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
                      className="inline-flex min-h-9 items-center gap-1.5 rounded-xl bg-[#1C4D8D] px-3.5 py-2 text-xs font-bold text-white transition hover:bg-[#0F2854]"
                    >
                      <FileCheck2 className="size-3.5" />
                      Xem câu hỏi & nhận xét
                    </button>
                  </div>
                </article>
              ))}
            </div>
            <div className="flex items-center justify-between border-t border-[#4988C4]/45 bg-white px-5 py-3">
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
                className="rounded-xl bg-[#1C4D8D] px-4 py-2 text-xs font-bold text-white transition hover:bg-[#0F2854]"
              >
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}

      {selectedScorecardId && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-[#0F2854]/60 p-3 backdrop-blur-xs sm:p-5"
          role="dialog"
          aria-modal="true"
          aria-label="Bảng điểm và nhận xét phiên luyện tập"
        >
          <div className="flex max-h-[90vh] w-full max-w-4xl flex-col overflow-hidden rounded-2xl border border-[#4988C4]/45 bg-[#F4FBFD] shadow-2xl">
            <div className="flex items-center justify-between gap-3 border-b border-[#4988C4]/45 bg-white px-5 py-3.5">
              <div className="flex items-center gap-2">
                <Award className="size-5 text-[#1C4D8D]" />
                <h2 className="text-base font-bold text-[#0F2854]">
                  Chi tiết Phiên luyện tập · Câu hỏi, Trả lời & Nhận xét
                </h2>
              </div>
              <button
                type="button"
                onClick={() => setSelectedScorecardId(null)}
                className="inline-flex size-8 items-center justify-center rounded-lg text-[#5b6b84] transition hover:bg-[#4988C4]/16 hover:text-[#0F2854]"
                aria-label="Đóng bảng điểm"
              >
                <X className="size-4" />
              </button>
            </div>

            <div className="flex-1 overflow-y-auto p-4 sm:p-5">
              {isLoadingScorecard ? (
                <div className="flex min-h-[240px] flex-col items-center justify-center gap-2 text-sm font-semibold text-[#1C4D8D]">
                  <LoaderCircle className="size-6 animate-spin" />
                  Đang tải câu hỏi, câu trả lời và nhận xét...
                </div>
              ) : scorecardError ? (
                <div className="rounded-2xl border border-[#f3c8cf] bg-[#fff5f6] p-4 text-sm text-[#b33d52]">
                  {scorecardError}
                </div>
              ) : scorecardMap[selectedScorecardId] ? (
                <PracticeSessionScorecard session={scorecardMap[selectedScorecardId]} />
              ) : null}
            </div>

            <div className="flex items-center justify-end gap-2 border-t border-[#4988C4]/45 bg-white px-5 py-3">
              <button
                type="button"
                onClick={() => setSelectedScorecardId(null)}
                className="rounded-xl bg-[#1C4D8D] px-4 py-2 text-xs font-bold text-white transition hover:bg-[#0F2854]"
              >
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}

      <div className="sr-only" aria-live="polite">
        {voicePreviewMessage}
        {microphoneState === 'ready' && ` Microphone ${microphoneName} đã sẵn sàng.`}
      </div>
    </div>
  )
}

export { PracticePage as PracticeSetupPage }
