import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import {
  AlertCircle,
  ArrowRight,
  AudioLines,
  Check,
  ChevronRight,
  Headphones,
  History,
  Lightbulb,
  LoaderCircle,
  Mic2,
  RefreshCw,
  Search,
  ShieldCheck,
  Volume2,
} from 'lucide-react'
import { OverlayDrawer, OverlayModal } from '@/components/ui/OverlayDrawer'
import { useAuth } from '@/hooks/useAuth'
import { useSpeechRecognition } from '@/hooks/useSpeechRecognition'
import { cn } from '@/lib/utils'
import { courseService } from '@/services/course.service'
import { practiceService } from '@/services/practice.service'
import {
  speechService,
  TTS_RATE_OPTIONS,
  TTS_VOICE_OPTIONS,
  type NeuralVoiceId,
} from '@/services/speech.service'
import type {
  CourseDto,
  PracticeDifficulty,
  PracticeHistoryItem,
  PracticeMode,
  PracticeSessionConfig,
} from '@/types/practice.types'
import {
  loadPracticeRuntime,
  savePracticeRuntime,
  type StoredPracticeRuntime,
} from '@/utils/practiceSessionStorage'

type MicrophoneState = 'idle' | 'checking' | 'ready' | 'error' | 'unsupported'

const FULL_SESSION_MIN_QUESTIONS = 3
const FULL_SESSION_MAX_QUESTIONS = 10

const DIFFICULTY_OPTIONS: Array<{
  value: PracticeDifficulty
  label: string
  subtitle: string
  dotClass: string
  activeClass: string
  inactiveClass: string
  badgeClass: string
}> = [
  {
    value: 'easy',
    label: 'Dễ',
    subtitle: 'Đơn giản · Nền tảng',
    dotClass: 'bg-emerald-500',
    activeClass: 'border-emerald-400 bg-emerald-50 text-emerald-800 shadow-xs ring-1 ring-emerald-400/30',
    inactiveClass: 'border-emerald-200/80 bg-emerald-50/30 text-emerald-700 hover:bg-emerald-50/70',
    badgeClass: 'border-emerald-200 bg-emerald-50 text-emerald-700',
  },
  {
    value: 'medium',
    label: 'Trung bình',
    subtitle: 'Đáng chú ý · Vận dụng',
    dotClass: 'bg-amber-500',
    activeClass: 'border-amber-400 bg-amber-50 text-amber-900 shadow-xs ring-1 ring-amber-400/30',
    inactiveClass: 'border-amber-200/80 bg-amber-50/30 text-amber-800 hover:bg-amber-50/70',
    badgeClass: 'border-amber-200 bg-amber-50 text-amber-800',
  },
  {
    value: 'hard',
    label: 'Khó',
    subtitle: 'Thử thách · Chuyên sâu',
    dotClass: 'bg-rose-500',
    activeClass: 'border-rose-400 bg-rose-50 text-rose-800 shadow-xs ring-1 ring-rose-400/30',
    inactiveClass: 'border-rose-200/80 bg-rose-50/30 text-rose-700 hover:bg-rose-50/70',
    badgeClass: 'border-rose-200 bg-rose-50 text-rose-700',
  },
]

const FALLBACK_COURSES: CourseDto[] = [
  {
    id: '00000000-0000-0000-0000-000000000008',
    code: 'PRN231',
    name: 'Building Cross-Platform Back-End Applications with .NET',
    credits: 3,
    isActive: true,
    transcriptBufferSeconds: 60,
    maxFollowUpQuestions: 2,
    hasFollowUp: true,
    examInputMode: 'VoiceWithTranscriptEdit',
  },
  {
    id: '00000000-0000-0000-0000-000000000009',
    code: 'SWD392',
    name: 'Software Architecture and Design',
    credits: 3,
    isActive: true,
    transcriptBufferSeconds: 60,
    maxFollowUpQuestions: 2,
    hasFollowUp: true,
    examInputMode: 'VoiceWithTranscriptEdit',
  },
  {
    id: '00000000-0000-0000-0000-00000000000a',
    code: 'SWR302',
    name: 'Software Requirements',
    credits: 3,
    isActive: true,
    transcriptBufferSeconds: 60,
    maxFollowUpQuestions: 2,
    hasFollowUp: true,
    examInputMode: 'VoiceWithTranscriptEdit',
  },
]

const PRACTICE_TIPS = [
  'Hãy giữ tốc độ nói ổn định từ 110–130 từ/phút và trả lời theo cấu trúc: định nghĩa – cơ chế – ví dụ thực tiễn.',
  'Dành 5 giây đầu để sắp xếp ý trong đầu, sau đó nói câu kết luận trực diện trước khi đi vào giải thích chi tiết.',
  'Khi gặp thuật ngữ tiếng Anh chuyên ngành (DI, OOP, ACID, CAP...), hãy phát âm chuẩn và bổ sung 1 câu tiếng Việt làm rõ.',
  'Nếu điểm số câu hỏi đạt từ 4.0 đến 8.0, AI sẽ hỏi thêm câu phụ đào sâu — hãy sẵn sàng trả lời bản chất vấn đề.',
]

const ANSWER_STRUCTURE_STEPS = [
  {
    step: '1',
    title: 'Định nghĩa cốt lõi',
    detail: 'Nêu trực diện khái niệm hoặc nguyên lý chính trong 1–2 câu ngắn gọn, súc tích.',
  },
  {
    step: '2',
    title: 'Cơ chế vận hành',
    detail: 'Giải thích luồng hoạt động, ưu/nhược điểm và các ràng buộc khi triển khai.',
  },
  {
    step: '3',
    title: 'Ví dụ thực tiễn',
    detail: 'Liên hệ tình huống dự án thực tế (.NET, React, SQL, Microservices).',
  },
]

function getMicrophoneStatus(state: MicrophoneState) {
  switch (state) {
    case 'checking':
      return {
        label: 'Đang kiểm tra micro…',
        helper: 'Trình duyệt đang yêu cầu quyền truy cập micro.',
        dotClassName: 'bg-amber-500 animate-pulse',
        badgeClassName: 'border-amber-200 bg-amber-50 text-amber-800',
      }
    case 'ready':
      return {
        label: 'Microphone sẵn sàng',
        helper: 'Thiết bị thu âm hoạt động tốt và sẵn sàng vào phòng.',
        dotClassName: 'bg-emerald-500',
        badgeClassName: 'border-emerald-200 bg-emerald-50 text-emerald-700',
      }
    case 'error':
      return {
        label: 'Cảnh báo: Chưa cấp quyền micro',
        helper: 'Hãy cấp quyền truy cập micro trong trình duyệt để thu âm.',
        dotClassName: 'bg-rose-500',
        badgeClassName: 'border-rose-200 bg-rose-50 text-rose-700',
      }
    case 'unsupported':
      return {
        label: 'Trình duyệt chưa hỗ trợ thu âm',
        helper: 'Hãy mở trên Google Chrome hoặc Microsoft Edge phiên bản mới.',
        dotClassName: 'bg-rose-500',
        badgeClassName: 'border-rose-200 bg-rose-50 text-rose-700',
      }
    default:
      return {
        label: 'Đáng chú ý: Chưa kiểm tra micro',
        helper: 'Nên bấm kiểm tra thử giọng và loa AI trước khi vào phòng.',
        dotClassName: 'bg-amber-500',
        badgeClassName: 'border-amber-200 bg-amber-50 text-amber-800',
      }
  }
}

function formatPracticeDate(value: string) {
  return new Intl.DateTimeFormat('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(value))
}

function getPracticeModeLabel(mode: PracticeHistoryItem['practiceMode']) {
  return mode === 'per_question' ? 'Luyện từng câu' : 'Lộ trình bậc thang'
}

function findActivePracticeRuntime(studentId?: string): StoredPracticeRuntime | null {
  try {
    const keys = Object.keys(window.sessionStorage).filter((key) =>
      key.startsWith('mf01_practice_session:'),
    )
    for (const key of keys) {
      const sessionId = key.replace('mf01_practice_session:', '')
      const runtime = loadPracticeRuntime(sessionId)
      if (runtime && (!studentId || runtime.studentId === studentId)) {
        return runtime
      }
    }
    return null
  } catch {
    return null
  }
}

export function PracticePage() {
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const courseParam = searchParams.get('course')
  const { user } = useAuth()
  const [courses, setCourses] = useState<CourseDto[]>([])
  const [courseSearch, setCourseSearch] = useState('')
  const [practiceHistory, setPracticeHistory] = useState<PracticeHistoryItem[]>([])
  const [selectedCourseId, setSelectedCourseId] = useState('')
  const [isConfigModalOpen, setIsConfigModalOpen] = useState(false)
  const [feedbackMode, setFeedbackMode] = useState<PracticeMode>('PER_QUESTION')
  const [selectedDifficulties, setSelectedDifficulties] = useState<PracticeDifficulty[]>(['medium'])
  const [questionCount, setQuestionCount] = useState(5)
  const [microphoneState, setMicrophoneState] = useState<MicrophoneState>('idle')
  const [microphoneName, setMicrophoneName] = useState('')
  const [isTestingMic, setIsTestingMic] = useState(false)
  const [micVolume, setMicVolume] = useState(0)
  const [micPeakVolume, setMicPeakVolume] = useState(0)
  const [micTestSecondsLeft, setMicTestSecondsLeft] = useState(0)
  const [hasCompletedMicTest, setHasCompletedMicTest] = useState(false)
  const audioContextRef = useRef<AudioContext | null>(null)
  const analyserRef = useRef<AnalyserNode | null>(null)
  const micStreamRef = useRef<MediaStream | null>(null)
  const animFrameRef = useRef<number | null>(null)
  const micTestTimeoutRef = useRef<number | null>(null)
  const micTestIntervalRef = useRef<number | null>(null)

  const [speechPrefs, setSpeechPrefs] = useState(() => speechService.getPreferences())
  const [isVoicePreviewPlaying, setIsVoicePreviewPlaying] = useState(false)
  const [voicePreviewMessage, setVoicePreviewMessage] = useState('')
  const [voicePreviewTone, setVoicePreviewTone] = useState<'idle' | 'loading' | 'success' | 'error'>('idle')
  const [expandedHistoryId, setExpandedHistoryId] = useState<string | null>(null)
  const [tipIndex, setTipIndex] = useState(0)
  const [isLoadingData, setIsLoadingData] = useState(true)
  const [isStarting, setIsStarting] = useState(false)
  const [pageError, setPageError] = useState('')
  const [errorSource, setErrorSource] = useState<'load' | 'start' | null>(null)
  const [isVoiceModalOpen, setIsVoiceModalOpen] = useState(false)
  const [isHistoryDrawerOpen, setIsHistoryDrawerOpen] = useState(false)
  const [isRubricDrawerOpen, setIsRubricDrawerOpen] = useState(false)

  const activeRuntime = useMemo(() => findActivePracticeRuntime(user?.id), [user?.id])
  const selectedCourse = useMemo(
    () => courses.find((course) => course.id === selectedCourseId) ?? null,
    [courses, selectedCourseId],
  )

  const micSpeech = useSpeechRecognition({ vocabularyHint: selectedCourse?.code })
  const { stopListening: stopMicSpeechListening } = micSpeech

  const progressiveBreakdown = useMemo(() => {
    const count = questionCount
    const base = Math.floor(count / 3)
    const remainder = count % 3
    const easy = base + (remainder >= 1 ? 1 : 0)
    const medium = base + (remainder === 2 ? 1 : 0)
    const hard = base
    return { easy, medium, hard }
  }, [questionCount])

  const filteredCourses = useMemo(() => {
    if (!courseSearch.trim()) return courses
    const q = courseSearch.toLowerCase()
    return courses.filter(
      (course) => course.code.toLowerCase().includes(q) || course.name.toLowerCase().includes(q),
    )
  }, [courses, courseSearch])

  const effectiveMicState: MicrophoneState =
    micSpeech.transcript.trim().length > 0 || micPeakVolume >= 5 ? 'ready' : microphoneState
  const microphoneStatus = getMicrophoneStatus(effectiveMicState)

  const stopMicTest = useCallback(() => {
    if (micTestTimeoutRef.current) {
      window.clearTimeout(micTestTimeoutRef.current)
      micTestTimeoutRef.current = null
    }
    if (micTestIntervalRef.current) {
      window.clearInterval(micTestIntervalRef.current)
      micTestIntervalRef.current = null
    }
    if (animFrameRef.current) {
      cancelAnimationFrame(animFrameRef.current)
      animFrameRef.current = null
    }
    if (micStreamRef.current) {
      micStreamRef.current.getTracks().forEach((track) => track.stop())
      micStreamRef.current = null
    }
    if (audioContextRef.current && audioContextRef.current.state !== 'closed') {
      void audioContextRef.current.close()
      audioContextRef.current = null
    }
    setIsTestingMic(false)
    setMicTestSecondsLeft(0)
    setMicVolume(0)
  }, [])

  useEffect(() => {
    void speechService.getVoices()
  }, [])

  useEffect(() => {
    return () => {
      stopMicTest()
      stopMicSpeechListening()
      speechService.stopSpeaking()
    }
  }, [stopMicTest, stopMicSpeechListening])

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

      const availableCourses =
        coursesResult.status === 'fulfilled' && coursesResult.value.length > 0
          ? coursesResult.value
          : FALLBACK_COURSES
      setCourses(availableCourses)

      if (historyResult.status === 'fulfilled') {
        setPracticeHistory(historyResult.value.slice(0, 10))
      } else {
        setPracticeHistory([])
      }

      const targetCourse = courseParam
        ? availableCourses.find(
            (c) =>
              c.id.toLowerCase() === courseParam.toLowerCase() ||
              c.code.toLowerCase() === courseParam.toLowerCase(),
          )
        : null
      const defaultId = targetCourse?.id ?? availableCourses[0]?.id ?? ''

      setSelectedCourseId((current) => {
        if (targetCourse) return targetCourse.id
        return availableCourses.some((course) => course.id === current) ? current : defaultId
      })
      if (targetCourse) {
        setIsConfigModalOpen(true)
      }
    } catch {
      setCourses(FALLBACK_COURSES)
      setSelectedCourseId(FALLBACK_COURSES[0].id)
    } finally {
      setIsLoadingData(false)
    }
  }, [courseParam, user])

  useEffect(() => {
    const loadTimer = window.setTimeout(() => void loadPracticeData(), 0)
    return () => window.clearTimeout(loadTimer)
  }, [loadPracticeData])

  const handleVoicePreview = () => {
    const courseName = selectedCourse?.name ?? 'Kỹ thuật Phần mềm'

    if (isVoicePreviewPlaying) {
      speechService.stopSpeaking()
      setIsVoicePreviewPlaying(false)
      setVoicePreviewTone('idle')
      setVoicePreviewMessage('Đã dừng nghe thử giọng đọc AI.')
      return
    }

    const label = speechService.getVoiceLabel(speechPrefs.voiceName)
    setIsVoicePreviewPlaying(true)
    setVoicePreviewTone('loading')
    setVoicePreviewMessage(`Đang tổng hợp giọng đọc AI từ Backend (/api/v1/speech/tts): ${label}…`)

    void speechService.speakText(
      `Xin chào bạn. Chào mừng đến với phòng luyện tập vấn đáp ${courseName}. Hãy tự tin trả lời rõ ràng theo cấu trúc định nghĩa, cơ chế và ví dụ.`,
      {
        voiceName: speechPrefs.voiceName,
        speakingRate: speechPrefs.speakingRate,
        onStart: () => {
          setIsVoicePreviewPlaying(true)
          setVoicePreviewTone('success')
          setVoicePreviewMessage(
            `Đang phát giọng AI chuẩn Neural: ${label} (Tốc độ ${speechPrefs.speakingRate}x).`,
          )
        },
        onEnd: () => {
          setIsVoicePreviewPlaying(false)
          setVoicePreviewTone('success')
          setVoicePreviewMessage(`Đã phát thành công câu chào mẫu (${label}) — Loa hoạt động tốt.`)
        },
        onError: () => {
          setIsVoicePreviewPlaying(false)
          setVoicePreviewTone('error')
          setVoicePreviewMessage('Cảnh báo: Không thể phát giọng mẫu trên thiết bị này.')
        },
      },
    )
  }

  const handleSelectVoice = (voiceName: NeuralVoiceId) => {
    const updated = speechService.savePreferences({ voiceName })
    setSpeechPrefs(updated)
    if (isVoicePreviewPlaying) {
      speechService.stopSpeaking()
      setIsVoicePreviewPlaying(false)
    }
    setVoicePreviewTone('idle')
    setVoicePreviewMessage(`Đã chọn giọng đọc: ${speechService.getVoiceLabel(voiceName)}.`)
  }

  const handleSelectRate = (speakingRate: number) => {
    const updated = speechService.savePreferences({ speakingRate })
    setSpeechPrefs(updated)
    if (isVoicePreviewPlaying) {
      speechService.stopSpeaking()
      setIsVoicePreviewPlaying(false)
    }
    setVoicePreviewTone('idle')
    setVoicePreviewMessage(`Đã chọn tốc độ đọc: ${speakingRate}x.`)
  }

  const toggleMicTest = async () => {
    if (isTestingMic) {
      stopMicTest()
      setHasCompletedMicTest(true)
      return
    }

    if (micSpeech.isListening) {
      micSpeech.stopListening()
    }

    if (!navigator.mediaDevices?.getUserMedia) {
      setMicrophoneState('unsupported')
      return
    }

    setMicrophoneState('checking')
    setMicPeakVolume(0)
    setHasCompletedMicTest(false)
    setMicTestSecondsLeft(5)

    try {
      const stream = await navigator.mediaDevices.getUserMedia({
        audio: {
          echoCancellation: true,
          noiseSuppression: true,
        },
      })
      micStreamRef.current = stream
      const audioTrack = stream.getAudioTracks()[0]
      setMicrophoneName(audioTrack?.label || 'Microphone mặc định')
      setMicrophoneState('ready')
      setIsTestingMic(true)

      const AudioCtx =
        window.AudioContext ||
        (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext
      if (AudioCtx) {
        const ctx = new AudioCtx()
        audioContextRef.current = ctx
        if (ctx.state === 'suspended') {
          await ctx.resume()
        }
        const analyser = ctx.createAnalyser()
        analyser.fftSize = 64
        analyserRef.current = analyser
        const source = ctx.createMediaStreamSource(stream)
        source.connect(analyser)

        const dataArray = new Uint8Array(analyser.frequencyBinCount)
        const updateLevel = () => {
          if (!analyserRef.current) return
          analyserRef.current.getByteFrequencyData(dataArray)
          let sum = 0
          for (let i = 0; i < dataArray.length; i++) {
            sum += dataArray[i]
          }
          const avg = sum / dataArray.length
          const normalized = Math.min(100, Math.round((avg / 85) * 100))
          setMicVolume(normalized)
          setMicPeakVolume((prev) => Math.max(prev, normalized))
          animFrameRef.current = requestAnimationFrame(updateLevel)
        }
        updateLevel()
      }

      micTestIntervalRef.current = window.setInterval(() => {
        setMicTestSecondsLeft((prev) => (prev <= 1 ? 0 : prev - 1))
      }, 1000)

      micTestTimeoutRef.current = window.setTimeout(() => {
        stopMicTest()
        setHasCompletedMicTest(true)
      }, 5000)
    } catch {
      setMicrophoneName('')
      setMicrophoneState('error')
      stopMicTest()
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

  const handleSelectCourseCard = (courseId: string) => {
    setSelectedCourseId(courseId)
    setPageError('')
    setIsConfigModalOpen(true)
  }

  const handleStartPractice = async () => {
    if (!selectedCourse || !user || isStarting) return

    stopMicTest()
    speechService.stopSpeaking()
    setIsStarting(true)
    setPageError('')
    setErrorSource(null)
    const config: PracticeSessionConfig = {
      courseId: selectedCourse.id,
      courseName: selectedCourse.name,
      courseCode: selectedCourse.code,
      mode: feedbackMode,
      selectedDifficulties: feedbackMode === 'PER_QUESTION' ? selectedDifficulties : [],
      questionCount,
      microphoneName,
    }

    try {
      const session = await practiceService.startSession({
        courseId: selectedCourse.id,
        studentId: user.id,
        difficulty: feedbackMode === 'FULL_SESSION' ? 'progressive' : null,
        difficulties: feedbackMode === 'PER_QUESTION' ? selectedDifficulties : null,
        questionCount: feedbackMode === 'FULL_SESSION' ? questionCount : null,
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

      setIsConfigModalOpen(false)
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
    <div className="w-full space-y-5 font-sans">
      {/* 1. Thanh tiêu đề chuyển sắc ấm áp & Trạng thái âm thanh ngữ nghĩa */}
      <section className="rounded-2xl border border-slate-200 bg-gradient-to-br from-[#FFF7ED]/80 via-white to-amber-50/30 p-5 shadow-xs">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div className="min-w-0">
            <div className="mb-1.5 inline-flex items-center gap-1.5 rounded-full border border-emerald-200 bg-emerald-50 px-2.5 py-0.5 text-xs font-semibold text-emerald-700">
              <span className="size-1.5 rounded-full bg-emerald-500" />
              Phòng luyện tập AI · Chuẩn Rubric 10.0
            </div>
            <h1 className="text-xl font-semibold text-slate-900">Luyện tập vấn đáp</h1>
          </div>

          <div className="flex flex-wrap items-center gap-2 self-start sm:self-center">
            <button
              type="button"
              onClick={() => setIsVoiceModalOpen(true)}
              className="inline-flex min-h-9 items-center gap-2 rounded-xl border border-slate-200 bg-white px-3.5 text-xs font-semibold text-slate-700 shadow-xs transition hover:-translate-y-0.5 hover:border-[#F26F21]/50 hover:bg-slate-50 cursor-pointer"
            >
              <Mic2 className="size-3.5 text-[#F26F21]" />
              <span>Kiểm tra âm thanh</span>
              <span
                className={cn('size-2.5 rounded-full', microphoneStatus.dotClassName)}
                title={microphoneStatus.label}
              />
            </button>

            <button
              type="button"
              onClick={() => setIsRubricDrawerOpen(true)}
              className="inline-flex min-h-9 items-center gap-1.5 rounded-xl border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 shadow-xs transition hover:-translate-y-0.5 hover:bg-slate-50 cursor-pointer"
            >
              <Lightbulb className="size-3.5 text-amber-600" />
              Tiêu chí chấm điểm
            </button>

            <button
              type="button"
              onClick={() => setIsHistoryDrawerOpen(true)}
              className="inline-flex min-h-9 items-center gap-1.5 rounded-xl border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 shadow-xs transition hover:-translate-y-0.5 hover:bg-slate-50 cursor-pointer"
            >
              <History className="size-3.5 text-slate-600" />
              Nhật ký ({practiceHistory.length})
            </button>
          </div>
        </div>
      </section>

      {/* 2. Thanh nhắc phiên đang làm dở (Màu Vàng đáng chú ý) */}
      {activeRuntime && (
        <div className="flex flex-col gap-3 rounded-2xl border border-amber-200 bg-amber-50/80 px-5 py-3.5 text-amber-900 shadow-xs sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-2.5 text-xs">
            <span className="size-2.5 shrink-0 rounded-full bg-amber-500 animate-pulse" />
            <p>
              <span className="font-semibold text-amber-950">
                Phiên đang mở: {activeRuntime.config.courseCode}
              </span>{' '}
              ·{' '}
              {activeRuntime.config.mode === 'PER_QUESTION'
                ? `Luyện từng câu · Đã làm ${activeRuntime.mainQuestionsCompleted} câu`
                : `Lộ trình bậc thang · ${activeRuntime.mainQuestionsCompleted}/${activeRuntime.config.questionCount} câu`}
            </p>
          </div>
          <button
            type="button"
            onClick={() =>
              navigate(`/practice/session?session=${encodeURIComponent(activeRuntime.sessionId)}`)
            }
            className="inline-flex min-h-9 shrink-0 items-center justify-center gap-1.5 rounded-xl bg-[#F26F21] px-4 text-xs font-semibold text-white shadow-xs transition hover:bg-[#D95B16] cursor-pointer"
          >
            Tiếp tục phiên
            <ArrowRight className="size-3.5" />
          </button>
        </div>
      )}

      {/* Thông báo lỗi tải trang nếu có (Màu Đỏ cảnh báo) */}
      {pageError && errorSource === 'load' && (
        <div
          className="flex items-center justify-between gap-3 rounded-xl border border-rose-200 bg-rose-50 p-4 text-xs font-medium text-rose-700 shadow-xs"
          role="alert"
        >
          <div className="flex items-center gap-2.5">
            <AlertCircle className="size-4 shrink-0 text-rose-600" />
            <span>{pageError}</span>
          </div>
          <button
            type="button"
            onClick={() => void loadPracticeData()}
            className="rounded-lg border border-rose-200 bg-white px-2.5 py-1 text-xs font-semibold text-rose-700 transition hover:bg-rose-100 shrink-0"
          >
            Thử lại
          </button>
        </div>
      )}

      {/* 3. Khối chính — Danh sách chọn học phần trực quan */}
      <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-xs">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <h2 className="text-base font-semibold text-slate-900">Chọn học phần luyện tập</h2>

          <div className="relative w-full sm:w-64">
            <Search className="pointer-events-none absolute left-3 top-1/2 size-3.5 -translate-y-1/2 text-slate-400" />
            <input
              type="text"
              placeholder="Tìm mã hoặc tên học phần..."
              value={courseSearch}
              onChange={(e) => setCourseSearch(e.target.value)}
              className="w-full rounded-xl border border-slate-200 bg-slate-50 py-2 pl-9 pr-3 text-xs text-slate-900 placeholder:text-slate-400 transition focus:border-[#F26F21] focus:bg-white focus:outline-none"
            />
          </div>
        </div>

        {isLoadingData ? (
          <div className="mt-4 flex min-h-40 items-center justify-center gap-2 text-xs text-slate-500">
            <LoaderCircle className="size-4 animate-spin text-[#F26F21]" />
            <span>Đang tải danh sách học phần…</span>
          </div>
        ) : filteredCourses.length === 0 ? (
          <div className="mt-4 rounded-xl border border-dashed border-slate-200 p-8 text-center text-xs text-slate-500">
            Không tìm thấy học phần nào phù hợp.
          </div>
        ) : (
          <div className="mt-4 grid grid-cols-1 gap-4 md:grid-cols-3">
            {filteredCourses.map((course) => {
              const isSelected = course.id === selectedCourseId
              return (
                <button
                  key={course.id}
                  type="button"
                  onClick={() => handleSelectCourseCard(course.id)}
                  className={cn(
                    'group relative flex flex-col justify-between overflow-hidden rounded-2xl border p-4 text-left shadow-xs transition-all hover:-translate-y-0.5 hover:shadow-md cursor-pointer',
                    isSelected
                      ? 'border-[#F26F21] bg-gradient-to-b from-orange-50/70 to-white ring-1 ring-[#F26F21]/25'
                      : 'border-slate-200 bg-white hover:border-[#F26F21]/70',
                  )}
                >
                  <div>
                    <div className="flex items-center justify-between gap-2">
                      <span className="rounded-lg border border-slate-200 bg-slate-100 px-2.5 py-0.5 text-xs font-bold text-slate-900">
                        {course.code}
                      </span>
                      <span className="inline-flex items-center gap-1 rounded-md border border-emerald-200 bg-emerald-50 px-2 py-0.5 text-xs font-semibold text-emerald-700">
                        <span className="size-1.5 rounded-full bg-emerald-500" />
                        {course.credits} TC
                      </span>
                    </div>

                    <h3 className="mt-3 text-sm font-semibold text-slate-900 line-clamp-2 group-hover:text-[#C2410C] transition-colors">
                      {course.name}
                    </h3>

                    <div className="mt-3 flex flex-wrap items-center gap-1.5 text-xs">
                      <span className="rounded-md border border-emerald-200/80 bg-emerald-50/70 px-2 py-0.5 font-medium text-emerald-700">
                        Đệm {course.transcriptBufferSeconds}s
                      </span>
                      <span className="rounded-md border border-amber-200/80 bg-amber-50/70 px-2 py-0.5 font-medium text-amber-800">
                        Phụ tối đa {course.maxFollowUpQuestions} câu
                      </span>
                    </div>
                  </div>

                  <div className="mt-4 space-y-2.5 border-t border-slate-100 pt-3">
                    {/* Thanh 3 cấp độ khó ngữ nghĩa (Xanh - Vàng - Đỏ) */}
                    <div className="flex items-center gap-1" aria-hidden="true">
                      <span className="h-1.5 flex-1 rounded-full bg-emerald-500/85" title="Dễ" />
                      <span
                        className="h-1.5 flex-1 rounded-full bg-amber-500/85"
                        title="Trung bình"
                      />
                      <span className="h-1.5 flex-1 rounded-full bg-rose-500/85" title="Khó" />
                    </div>
                    <div className="flex items-center justify-between text-xs">
                      <span className="font-medium text-slate-500">Rubric 10.0đ</span>
                      <span className="font-semibold text-[#F26F21] group-hover:translate-x-0.5 transition-transform">
                        Bắt đầu →
                      </span>
                    </div>
                  </div>
                </button>
              )
            })}
          </div>
        )}
      </section>

      {/* Màn hình phụ cấu hình các bước khi chọn môn học */}
      <OverlayModal
        open={isConfigModalOpen && Boolean(selectedCourse)}
        onClose={() => setIsConfigModalOpen(false)}
        badge={selectedCourse?.code}
        title={selectedCourse?.name ?? 'Thiết lập phiên luyện tập'}
        size="lg"
        footer={
          <div className="flex flex-wrap items-center justify-between gap-3">
            <button
              type="button"
              onClick={() => setIsVoiceModalOpen(true)}
              className="inline-flex min-h-9 items-center gap-2 rounded-xl border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 transition hover:bg-slate-50 cursor-pointer"
            >
              <Mic2 className="size-3.5 text-[#F26F21]" />
              <span>Kiểm tra Micro & Giọng đọc</span>
              <span className={cn('size-2 rounded-full', microphoneStatus.dotClassName)} />
            </button>

            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={() => setIsConfigModalOpen(false)}
                className="rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-xs font-medium text-slate-700 transition hover:bg-slate-50 cursor-pointer"
              >
                Đóng
              </button>
              <button
                type="button"
                onClick={() => void handleStartPractice()}
                disabled={isStarting || !selectedCourse}
                className="inline-flex items-center gap-1.5 rounded-xl bg-[#F26F21] px-5 py-2.5 text-xs font-semibold text-white shadow-xs transition hover:bg-[#D95B16] disabled:opacity-50 cursor-pointer"
              >
                {isStarting && <LoaderCircle className="size-3.5 animate-spin" />}
                Bắt đầu luyện tập
              </button>
            </div>
          </div>
        }
      >
        <div className="space-y-4">
          {/* Bước 1 — Chọn hình thức luyện tập */}
          <div className="space-y-2">
            <label className="block text-sm font-semibold text-slate-900">
              1. Chọn hình thức luyện tập
            </label>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <button
                type="button"
                onClick={() => selectFeedbackMode('PER_QUESTION')}
                className={cn(
                  'flex items-center justify-between gap-2 rounded-xl border px-3.5 py-3 text-left transition-all cursor-pointer',
                  feedbackMode === 'PER_QUESTION'
                    ? 'border-[#F26F21] bg-orange-50/60 text-[#C2410C] shadow-xs ring-1 ring-[#F26F21]/20'
                    : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50',
                )}
              >
                <span className="text-sm font-semibold text-slate-900">Luyện từng câu</span>
                <span className="rounded-md border border-emerald-200 bg-emerald-50 px-2 py-0.5 text-xs font-semibold text-emerald-700">
                  Linh hoạt
                </span>
              </button>

              <button
                type="button"
                onClick={() => selectFeedbackMode('FULL_SESSION')}
                className={cn(
                  'flex items-center justify-between gap-2 rounded-xl border px-3.5 py-3 text-left transition-all cursor-pointer',
                  feedbackMode === 'FULL_SESSION'
                    ? 'border-[#F26F21] bg-orange-50/60 text-[#C2410C] shadow-xs ring-1 ring-[#F26F21]/20'
                    : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50',
                )}
              >
                <span className="text-sm font-semibold text-slate-900">Lộ trình bậc thang</span>
                <span className="rounded-md border border-amber-200 bg-amber-50 px-2 py-0.5 text-xs font-semibold text-amber-800">
                  3–10 câu
                </span>
              </button>
            </div>
          </div>

          {/* Bước 2 — Tùy chỉnh nhanh theo phân màu ngữ nghĩa */}
          {feedbackMode === 'PER_QUESTION' ? (
            <div className="space-y-3">
              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <label className="text-sm font-semibold text-slate-900">
                    2. Chọn mức độ câu hỏi
                  </label>
                  <span className="text-xs font-medium text-slate-500">
                    {selectedDifficulties.length}/3 mức
                  </span>
                </div>
                <div className="grid grid-cols-3 gap-2.5">
                  {DIFFICULTY_OPTIONS.map((option) => {
                    const selected = selectedDifficulties.includes(option.value)
                    return (
                      <button
                        key={option.value}
                        type="button"
                        onClick={() => toggleDifficulty(option.value)}
                        className={cn(
                          'flex items-center justify-between gap-2 rounded-xl border px-3.5 py-3 text-left transition-all cursor-pointer',
                          selected ? option.activeClass : option.inactiveClass,
                        )}
                      >
                        <span className="flex items-center gap-1.5 text-xs font-bold">
                          <span className={cn('size-2 rounded-full', option.dotClass)} />
                          {option.label}
                        </span>
                        {selected && <Check className="size-3.5 shrink-0" />}
                      </button>
                    )
                  })}
                </div>
              </div>

              <div className="grid grid-cols-1 gap-2.5 sm:grid-cols-2">
                <div className="flex items-center justify-between gap-2 rounded-xl border border-emerald-200 bg-emerald-50/50 px-3.5 py-2.5">
                  <span className="flex items-center gap-1.5 text-xs font-semibold text-emerald-950">
                    <span className="size-2 shrink-0 rounded-full bg-emerald-500" />
                    Làm đến khi dừng / hết kho đề
                  </span>
                  <span className="shrink-0 rounded-md border border-emerald-200 bg-white px-2 py-0.5 text-xs font-semibold text-emerald-700">
                    Tự do
                  </span>
                </div>

                <div className="flex items-center justify-between gap-2 rounded-xl border border-amber-200 bg-amber-50/50 px-3.5 py-2.5">
                  <span className="flex items-center gap-1.5 text-xs font-semibold text-amber-950">
                    <span className="size-2 shrink-0 rounded-full bg-amber-500" />
                    Câu hỏi phụ tự động (4.0–8.0đ)
                  </span>
                  <span className="shrink-0 rounded-md border border-amber-200 bg-white px-2 py-0.5 text-xs font-semibold text-amber-800">
                    Tối đa {selectedCourse?.maxFollowUpQuestions ?? 2} câu
                  </span>
                </div>
              </div>
            </div>
          ) : (
            <div className="space-y-3.5">
              <div className="flex items-center justify-between">
                <label className="text-sm font-semibold text-slate-900">
                  2. Chọn số lượng câu hỏi (3–10 câu)
                </label>
                <span className="text-xs font-semibold text-slate-700">
                  Đang chọn: {questionCount} câu
                </span>
              </div>

              <div className="grid grid-cols-4 gap-2.5">
                {[3, 5, 7, 10].map((count) => (
                  <button
                    key={count}
                    type="button"
                    onClick={() => updateFullSessionQuestionCount(count)}
                    className={cn(
                      'rounded-xl border py-2 text-xs font-semibold transition cursor-pointer',
                      questionCount === count
                        ? 'border-[#F26F21] bg-orange-50/60 text-[#C2410C]'
                        : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50',
                    )}
                  >
                    {count} câu
                  </button>
                ))}
              </div>

              <input
                type="range"
                min={FULL_SESSION_MIN_QUESTIONS}
                max={FULL_SESSION_MAX_QUESTIONS}
                value={questionCount}
                onChange={(e) => updateFullSessionQuestionCount(Number(e.target.value))}
                className="h-2 w-full cursor-pointer accent-[#F26F21]"
                aria-label="Chọn số câu hỏi từ 3 đến 10"
              />

              {/* Thanh phân bổ độ khó 3 đoạn màu trực quan (Xanh - Vàng - Đỏ) */}
              <div className="space-y-2.5 rounded-xl border border-slate-200 bg-slate-50 p-3.5 text-xs">
                <div className="flex h-2.5 w-full overflow-hidden rounded-full bg-slate-200">
                  <div
                    className="bg-emerald-500 transition-all duration-300"
                    style={{ width: `${(progressiveBreakdown.easy / questionCount) * 100}%` }}
                  />
                  <div
                    className="bg-amber-500 transition-all duration-300"
                    style={{ width: `${(progressiveBreakdown.medium / questionCount) * 100}%` }}
                  />
                  <div
                    className="bg-rose-500 transition-all duration-300"
                    style={{ width: `${(progressiveBreakdown.hard / questionCount) * 100}%` }}
                  />
                </div>
                <div className="flex flex-wrap items-center justify-between gap-2 text-xs">
                  <span className="inline-flex items-center gap-1.5 rounded-md border border-emerald-200 bg-emerald-50 px-2.5 py-0.5 font-semibold text-emerald-700">
                    <span className="size-1.5 rounded-full bg-emerald-500" />
                    {progressiveBreakdown.easy} câu Dễ
                  </span>
                  <span className="inline-flex items-center gap-1.5 rounded-md border border-amber-200 bg-amber-50 px-2.5 py-0.5 font-semibold text-amber-800">
                    <span className="size-1.5 rounded-full bg-amber-500" />
                    {progressiveBreakdown.medium} câu Trung bình
                  </span>
                  <span className="inline-flex items-center gap-1.5 rounded-md border border-rose-200 bg-rose-50 px-2.5 py-0.5 font-semibold text-rose-700">
                    <span className="size-1.5 rounded-full bg-rose-500" />
                    {progressiveBreakdown.hard} câu Khó
                  </span>
                </div>
              </div>
            </div>
          )}

          {pageError && errorSource === 'start' && (
            <div
              className="flex items-center gap-2 rounded-xl border border-rose-200 bg-rose-50 p-3 text-xs font-medium text-rose-700"
              role="alert"
            >
              <AlertCircle className="size-4 shrink-0 text-rose-600" />
              <span>{pageError}</span>
            </div>
          )}
        </div>
      </OverlayModal>

      {/* CỬA SỔ MODAL HỖ TRỢ 1: KIỂM TRA MICROPHONE & TÙY CHỈNH GIỌNG ĐỌC AI */}
      <OverlayModal
        open={isVoiceModalOpen}
        onClose={() => {
          stopMicTest()
          stopMicSpeechListening()
          speechService.stopSpeaking()
          setIsVoiceModalOpen(false)
        }}
        badge="Thiết bị & Âm thanh AI"
        title="Kiểm tra Micro & Giọng đọc AI"
        subtitle="Kiểm tra cường độ thu âm 5s, nói thử bóc băng AI (/api/v1/speech/stt) và nghe thử giọng giám khảo (/api/v1/speech/tts)"
        size="xl"
        footer={
          <div className="flex items-center justify-between gap-3">
            <span className="inline-flex items-center gap-2 text-xs text-slate-500">
              <Headphones className="size-4 text-slate-600" />
              Nên dùng tai nghe ở không gian yên tĩnh để AI nhận diện giọng nói chính xác nhất
            </span>
            <button
              type="button"
              onClick={() => {
                stopMicTest()
                stopMicSpeechListening()
                speechService.stopSpeaking()
                setIsVoiceModalOpen(false)
              }}
              className="inline-flex min-h-9 items-center justify-center rounded-xl bg-[#F26F21] px-4 text-xs font-semibold text-white transition hover:bg-[#D95B16] cursor-pointer"
            >
              Xong & Đóng
            </button>
          </div>
        }
      >
        <div className="space-y-5">
          {/* 1. Khối 1 — Kiểm tra Microphone (5s tự động đếm ngược + Đo cường độ đỉnh micPeakVolume) */}
          <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-xs">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <div className="flex items-center gap-3">
                <span
                  className={cn(
                    'flex size-10 items-center justify-center rounded-xl border',
                    effectiveMicState === 'ready'
                      ? 'border-emerald-200 bg-emerald-50 text-emerald-700'
                      : effectiveMicState === 'error' || effectiveMicState === 'unsupported'
                        ? 'border-rose-200 bg-rose-50 text-rose-700'
                        : 'border-amber-200 bg-amber-50 text-amber-800',
                  )}
                >
                  <Mic2 className="size-5" />
                </span>
                <div>
                  <div className="flex items-center gap-2">
                    <h4 className="text-sm font-semibold text-slate-900">
                      1. Kiểm tra Microphone (Đo sóng âm 5s)
                    </h4>
                    <span
                      className={cn(
                        'rounded-md border px-2 py-0.5 text-xs font-semibold',
                        microphoneStatus.badgeClassName,
                      )}
                    >
                      {microphoneStatus.label}
                    </span>
                  </div>
                  <p className="mt-0.5 text-xs text-slate-500">
                    {microphoneName || microphoneStatus.helper}
                  </p>
                </div>
              </div>

              <button
                type="button"
                onClick={() => void toggleMicTest()}
                className={cn(
                  'inline-flex min-h-9 shrink-0 items-center justify-center gap-2 rounded-xl px-4 text-xs font-semibold transition cursor-pointer',
                  isTestingMic
                    ? 'border border-amber-300 bg-amber-50 text-amber-900 hover:bg-amber-100'
                    : 'bg-[#F26F21] text-white hover:bg-[#D95B16]',
                )}
              >
                {isTestingMic ? (
                  <>
                    <RefreshCw className="size-3.5 animate-spin text-amber-700" />
                    Dừng thử giọng ({micTestSecondsLeft}s)
                  </>
                ) : (
                  <>
                    <Mic2 className="size-3.5 text-white" />
                    Thử giọng nói (5s)
                  </>
                )}
              </button>
            </div>

            <div className="mt-3.5 rounded-xl border border-slate-200 bg-slate-50 p-3.5">
              <div className="flex flex-wrap items-center justify-between gap-2 text-xs text-slate-600">
                <span>
                  {isTestingMic
                    ? `Đang đo sóng âm thời gian thực (còn ${micTestSecondsLeft}s)...`
                    : 'Cường độ tín hiệu micro thu được:'}
                </span>
                <div className="flex items-center gap-3">
                  <span>
                    Hiện tại: <strong className="text-slate-900">{micVolume}%</strong>
                  </span>
                  <span>
                    Đỉnh đạt được: <strong className="text-slate-900">{micPeakVolume}%</strong>
                  </span>
                </div>
              </div>

              <div className="mt-2.5 flex h-5 items-center justify-between gap-1">
                {Array.from({ length: 20 }).map((_, idx) => {
                  const threshold = (idx + 1) * 5
                  const displayLevel = isTestingMic ? micVolume : micPeakVolume
                  const isActive = displayLevel >= threshold
                  const activeBarColor =
                    Math.max(micVolume, micPeakVolume) >= 15
                      ? 'bg-emerald-500'
                      : Math.max(micVolume, micPeakVolume) >= 5
                        ? 'bg-amber-500'
                        : 'bg-rose-500'
                  return (
                    <div
                      key={idx}
                      className={cn(
                        'w-full rounded-xs transition-all duration-75',
                        isActive ? cn('h-full', activeBarColor) : 'h-1.5 bg-slate-200',
                      )}
                    />
                  )
                })}
              </div>

              {/* Đánh giá kết quả theo 3 màu ngữ nghĩa */}
              {(microphoneState === 'error' ||
                microphoneState === 'unsupported' ||
                (hasCompletedMicTest && micPeakVolume < 5)) && (
                <div className="mt-3 flex items-center gap-2 rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-xs font-medium text-rose-700">
                  <AlertCircle className="size-4 shrink-0 text-rose-600" />
                  <span>
                    Cảnh báo: Chưa thu được âm thanh hoặc chưa cấp quyền micro. Hãy kiểm tra lại
                    thiết bị đầu vào.
                  </span>
                </div>
              )}

              {micPeakVolume >= 15 && (
                <div className="mt-3 flex items-center gap-2 rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-2 text-xs font-semibold text-emerald-700">
                  <Check className="size-4 shrink-0 text-emerald-600" />
                  <span>
                    Tín hiệu tốt — Đạt chuẩn thu âm (Đỉnh: {micPeakVolume}%)
                  </span>
                </div>
              )}

              {micPeakVolume >= 5 && micPeakVolume < 15 && (
                <div className="mt-3 flex items-center gap-2 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-xs font-semibold text-amber-700">
                  <AlertCircle className="size-4 shrink-0 text-amber-600" />
                  <span>
                    Đáng chú ý: Tín hiệu hơi nhỏ (Đỉnh: {micPeakVolume}%) — Hãy nói gần micro hơn
                  </span>
                </div>
              )}
            </div>
          </div>

          {/* 2. Khối 2 — Nói Thử Kiểm Tra Nhận Diện Giọng Nói (STT — Kết nối Backend POST /api/v1/speech/stt) */}
          <div className="space-y-3 rounded-xl border border-slate-200 bg-white p-4 shadow-xs">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div>
                <h4 className="flex items-center gap-1.5 text-sm font-semibold text-slate-900">
                  <AudioLines className="size-4 text-[#F26F21]" />
                  2. Nói thử kiểm tra nhận diện giọng nói (Backend AI STT)
                </h4>
                <p className="mt-0.5 text-xs text-slate-500">
                  Ghi âm và gửi về Backend (/api/v1/speech/stt) để chuẩn hóa thuật ngữ Kỹ thuật Phần
                  mềm
                </p>
              </div>
              <button
                type="button"
                disabled={micSpeech.isTranscribingBackend}
                onClick={() => {
                  if (micSpeech.isListening) {
                    micSpeech.stopListening()
                  } else {
                    if (isTestingMic) {
                      stopMicTest()
                    }
                    micSpeech.resetTranscript()
                    micSpeech.startListening()
                    setMicrophoneState('ready')
                  }
                }}
                className={cn(
                  'inline-flex min-h-9 items-center justify-center gap-2 rounded-xl px-4 text-xs font-semibold transition cursor-pointer disabled:opacity-60',
                  micSpeech.isListening
                    ? 'border border-amber-300 bg-amber-50 text-amber-900 hover:bg-amber-100'
                    : 'bg-[#F26F21] text-white hover:bg-[#D95B16]',
                )}
              >
                {micSpeech.isTranscribingBackend ? (
                  <>
                    <LoaderCircle className="size-3.5 animate-spin" />
                    Đang bóc băng AI…
                  </>
                ) : micSpeech.isListening ? (
                  <>
                    <span className="size-2 rounded-full bg-amber-500 animate-ping" />
                    Dừng & Bóc băng AI
                  </>
                ) : (
                  <>
                    <Mic2 className="size-3.5 text-white" />
                    Bắt đầu nói thử STT
                  </>
                )}
              </button>
            </div>

            <div className="rounded-lg border border-slate-200 bg-slate-50 p-2.5 text-xs text-slate-600">
              <span className="font-semibold text-slate-800">Câu mẫu gợi ý: </span>
              <em>
                "Chào trợ lý AI, tôi là sinh viên kiểm tra micro môn{' '}
                {selectedCourse?.code || 'PRN231'} với Dependency Injection và RESTful API"
              </em>
            </div>

            {micSpeech.isListening && (
              <div className="flex items-center justify-between gap-3 rounded-xl border border-amber-300 bg-amber-50/70 p-3 text-xs font-medium text-amber-900">
                <div className="flex items-center gap-2">
                  <span className="size-2.5 shrink-0 rounded-full bg-amber-500 animate-ping" />
                  <span>
                    Đang thu âm & nhận diện... Hãy đọc câu mẫu rồi bấm{' '}
                    <strong>"Dừng & Bóc băng AI"</strong>
                  </span>
                </div>
              </div>
            )}

            {micSpeech.isTranscribingBackend && (
              <div className="flex items-center gap-2.5 rounded-xl border border-amber-200 bg-amber-50 p-3 text-xs font-medium text-amber-800">
                <LoaderCircle className="size-4 shrink-0 animate-spin text-amber-600" />
                <span>
                  Đang gửi bản ghi âm về Backend AI (/api/v1/speech/stt) để chuẩn hóa thuật ngữ...
                </span>
              </div>
            )}

            <div className="rounded-xl border border-slate-200 bg-white p-3 text-xs">
              <div className="mb-1.5 flex items-center justify-between text-xs font-medium text-slate-600">
                <span>Văn bản AI nhận diện được:</span>
                {micSpeech.transcript && (
                  <button
                    type="button"
                    onClick={() => micSpeech.resetTranscript()}
                    className="text-xs font-semibold text-[#F26F21] hover:underline cursor-pointer"
                  >
                    Xóa & nói lại
                  </button>
                )}
              </div>

              {micSpeech.transcript ? (
                <div className="space-y-2 rounded-xl border border-emerald-200 bg-emerald-50/70 p-3">
                  <p className="font-medium text-slate-900">"{micSpeech.transcript}"</p>
                  <div className="flex flex-wrap items-center justify-between gap-2 pt-1">
                    <span className="inline-flex items-center gap-1.5 text-xs font-semibold text-emerald-700">
                      <Check className="size-4 text-emerald-600" />
                      Nhận diện giọng nói STT đạt chuẩn
                    </span>
                    <div className="flex flex-wrap items-center gap-1.5">
                      <span className="rounded-md border border-emerald-200 bg-white px-2 py-0.5 text-xs font-semibold text-emerald-700">
                        {micSpeech.sttProvider || 'Backend AI STT'}
                      </span>
                      <span className="rounded-md border border-emerald-200 bg-white px-2 py-0.5 text-xs font-semibold text-emerald-700">
                        Độ tin cậy:{' '}
                        {micSpeech.confidenceScore
                          ? `${Math.round(micSpeech.confidenceScore * 100)}%`
                          : 'Đạt chuẩn'}
                      </span>
                    </div>
                  </div>
                </div>
              ) : (
                <div className="py-2.5 text-center text-xs text-slate-500">
                  {micSpeech.isListening
                    ? 'Đang lắng nghe lời nói của bạn...'
                    : 'Chưa có dữ liệu nhận diện. Nhấn nút "Bắt đầu nói thử STT" ở trên để kiểm tra.'}
                </div>
              )}

              {micSpeech.error && (
                <div className="mt-2 flex items-center gap-2 rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-xs font-medium text-rose-700">
                  <AlertCircle className="size-4 shrink-0 text-rose-600" />
                  <span>Cảnh báo lỗi nhận diện: {micSpeech.error}</span>
                </div>
              )}
            </div>
          </div>

          {/* 3. Khối 3 — Chọn Giọng AI Giám khảo & Tốc độ (Kết nối Backend GET /api/v1/speech/voices & POST /api/v1/speech/tts) */}
          <div className="space-y-3.5 rounded-xl border border-slate-200 bg-white p-4 shadow-xs">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div>
                <h4 className="text-sm font-semibold text-slate-900">
                  3. Giọng đọc AI giám khảo (/api/v1/speech/tts)
                </h4>
                <p className="mt-0.5 text-xs text-slate-500">
                  Trợ lý giọng nói tự nhiên đọc câu hỏi vấn đáp tiếng Việt & tiếng Anh chuyên ngành
                </p>
              </div>
              <button
                type="button"
                onClick={handleVoicePreview}
                className={cn(
                  'inline-flex min-h-9 items-center gap-1.5 rounded-xl border px-3.5 text-xs font-semibold transition cursor-pointer',
                  isVoicePreviewPlaying
                    ? 'border-amber-300 bg-amber-50 text-amber-900 hover:bg-amber-100'
                    : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50',
                )}
              >
                {isVoicePreviewPlaying ? (
                  <AudioLines className="size-4 animate-pulse text-amber-600" />
                ) : (
                  <Volume2 className="size-4 text-[#F26F21]" />
                )}
                {isVoicePreviewPlaying ? 'Dừng nghe thử' : 'Nghe thử câu chào mẫu'}
              </button>
            </div>

            {voicePreviewMessage && (
              <div
                className={cn(
                  'flex items-center gap-2 rounded-lg border px-3 py-2 text-xs font-medium',
                  voicePreviewTone === 'loading'
                    ? 'border-amber-200 bg-amber-50 text-amber-800'
                    : voicePreviewTone === 'success'
                      ? 'border-emerald-200 bg-emerald-50 text-emerald-700'
                      : voicePreviewTone === 'error'
                        ? 'border-rose-200 bg-rose-50 text-rose-700'
                        : 'border-slate-200 bg-slate-50 text-slate-700',
                )}
              >
                {voicePreviewTone === 'loading' && (
                  <LoaderCircle className="size-4 shrink-0 animate-spin text-amber-600" />
                )}
                {voicePreviewTone === 'success' && (
                  <Check className="size-4 shrink-0 text-emerald-600" />
                )}
                {voicePreviewTone === 'error' && (
                  <AlertCircle className="size-4 shrink-0 text-rose-600" />
                )}
                <span>{voicePreviewMessage}</span>
              </div>
            )}

            <div className="grid gap-2.5 sm:grid-cols-2" role="group" aria-label="Chọn giọng AI">
              {TTS_VOICE_OPTIONS.map((option) => {
                const isSelected = speechPrefs.voiceName === option.voiceId
                return (
                  <button
                    key={option.voiceId}
                    type="button"
                    aria-pressed={isSelected}
                    onClick={() => handleSelectVoice(option.voiceId)}
                    className={cn(
                      'flex min-h-11 items-center justify-between gap-2 rounded-xl border px-3.5 py-2.5 text-left text-xs font-semibold transition cursor-pointer',
                      isSelected
                        ? 'border-emerald-400 bg-emerald-50/70 text-emerald-800 ring-1 ring-emerald-400/25'
                        : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50',
                    )}
                  >
                    <span className="flex items-center gap-2">
                      <Volume2
                        className={cn(
                          'size-4 shrink-0',
                          isSelected ? 'text-emerald-600' : 'text-slate-400',
                        )}
                      />
                      <span>{option.shortLabel}</span>
                    </span>
                    {isSelected && <Check className="size-4 text-emerald-600" />}
                  </button>
                )
              })}
            </div>

            <div>
              <span className="block text-xs font-semibold text-slate-900">Tốc độ đọc câu hỏi</span>
              <div
                className="mt-1.5 grid grid-cols-3 gap-2"
                role="group"
                aria-label="Chọn tốc độ đọc"
              >
                {TTS_RATE_OPTIONS.map((rateOption) => {
                  const isSelected = Math.abs(speechPrefs.speakingRate - rateOption.value) < 0.01
                  return (
                    <button
                      key={rateOption.value}
                      type="button"
                      aria-pressed={isSelected}
                      onClick={() => handleSelectRate(rateOption.value)}
                      className={cn(
                        'flex min-h-9 items-center justify-center rounded-xl border px-3 py-2 text-xs font-semibold transition cursor-pointer',
                        isSelected
                          ? 'border-[#F26F21] bg-orange-50/60 text-[#C2410C]'
                          : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50',
                      )}
                    >
                      {rateOption.value}x
                    </button>
                  )
                })}
              </div>
            </div>
          </div>
        </div>
      </OverlayModal>

      {/* CỬA SỔ DRAWER HỖ TRỢ 2: NHẬT KÝ PHIÊN LUYỆN TẬP */}
      <OverlayDrawer
        open={isHistoryDrawerOpen}
        onClose={() => setIsHistoryDrawerOpen(false)}
        badge="Lịch sử & Cẩm nang"
        title={`Lịch sử luyện tập & Hướng dẫn (${practiceHistory.length})`}
        subtitle="Xem lại các phiên luyện tập gần nhất và 3 bước cấu trúc câu trả lời đạt điểm tối đa"
        width="xl"
      >
        <div className="space-y-6">
          <section className="rounded-2xl border border-slate-200 bg-white p-4">
            <h4 className="flex items-center gap-2 text-sm font-semibold text-slate-900">
              <Lightbulb className="size-4 text-slate-700" />
              Cấu trúc trả lời vấn đáp chuẩn CLO
            </h4>
            <ol className="mt-3 space-y-2">
              {ANSWER_STRUCTURE_STEPS.map((item) => (
                <li
                  key={item.step}
                  className="flex items-start gap-2.5 rounded-xl border border-slate-200 bg-slate-50 p-2.5"
                >
                  <span className="flex size-6 shrink-0 items-center justify-center rounded-lg border border-slate-200 bg-white text-xs font-semibold text-slate-700">
                    {item.step}
                  </span>
                  <div className="min-w-0">
                    <p className="text-xs font-semibold text-slate-900">{item.title}</p>
                    <p className="mt-0.5 text-xs leading-relaxed text-slate-500">{item.detail}</p>
                  </div>
                </li>
              ))}
            </ol>
            <div className="mt-3 flex items-center justify-between text-xs font-medium text-slate-600">
              <span>Mẹo: {PRACTICE_TIPS[tipIndex]}</span>
              <button
                type="button"
                onClick={() => setTipIndex((c) => (c + 1) % PRACTICE_TIPS.length)}
                className="shrink-0 font-semibold text-[#F26F21] hover:underline ml-2"
              >
                Đổi mẹo →
              </button>
            </div>
          </section>

          <section className="space-y-3">
            <div className="flex items-center justify-between">
              <h4 className="flex items-center gap-2 text-sm font-semibold text-slate-900">
                <History className="size-4 text-slate-700" />
                Các phiên luyện tập gần đây
              </h4>
              <span className="rounded-full border border-slate-200 bg-slate-100 px-2.5 py-0.5 text-xs font-medium text-slate-700">
                {practiceHistory.length} phiên
              </span>
            </div>

            {isLoadingData ? (
              <div className="flex items-center justify-center gap-2 rounded-xl border border-slate-200 bg-slate-50 py-8 text-xs font-medium text-slate-600">
                <LoaderCircle className="size-4 animate-spin text-[#F26F21]" /> Đang tải lịch sử…
              </div>
            ) : practiceHistory.length === 0 ? (
              <p className="rounded-xl border border-slate-200 bg-slate-50 py-8 text-center text-xs text-slate-500">
                Chưa có phiên luyện tập nào được ghi nhận.
              </p>
            ) : (
              <div className="space-y-2.5">
                {practiceHistory.map((item) => {
                  const isExpanded = expandedHistoryId === item.sessionId
                  return (
                    <article
                      key={item.sessionId}
                      className={cn(
                        'rounded-xl border transition',
                        isExpanded
                          ? 'border-[#F26F21] bg-orange-50/40'
                          : 'border-slate-200 bg-white',
                      )}
                    >
                      <button
                        type="button"
                        onClick={() => setExpandedHistoryId(isExpanded ? null : item.sessionId)}
                        className="w-full p-3.5 text-left"
                        aria-expanded={isExpanded}
                      >
                        <div className="flex items-start justify-between gap-3">
                          <div className="min-w-0">
                            <p className="truncate text-sm font-semibold text-slate-900">
                              {item.courseName} ({item.courseCode})
                            </p>
                            <div className="mt-1.5 flex flex-wrap items-center gap-2 text-xs text-slate-500">
                              <span className="rounded-md border border-slate-200 bg-slate-100 px-2 py-0.5 font-medium text-slate-700">
                                {getPracticeModeLabel(item.practiceMode)}
                              </span>
                              <span>{formatPracticeDate(item.startedAt)}</span>
                            </div>
                          </div>
                          <strong className="shrink-0 text-sm font-semibold text-slate-900">
                            {item.averageScore === null
                              ? 'Đang chấm'
                              : `${item.averageScore.toFixed(1)}/10`}
                          </strong>
                        </div>
                        <div className="mt-2.5 flex items-center justify-between text-xs font-semibold text-[#F26F21]">
                          <span>{isExpanded ? 'Thu gọn chi tiết' : 'Xem chi tiết phiên'}</span>
                          <ChevronRight
                            className={cn(
                              'size-3.5 transition-transform',
                              isExpanded && 'rotate-90',
                            )}
                          />
                        </div>
                      </button>
                      {isExpanded && (
                        <div className="space-y-2 border-t border-slate-200 px-3.5 py-3 text-xs leading-relaxed text-slate-600">
                          <p>
                            Đã trả lời {item.answeredQuestions} lượt cho {item.totalQuestions} câu
                            chính.{' '}
                            {item.status === 'completed'
                              ? 'Phiên đã hoàn tất.'
                              : item.status === 'abandoned'
                                ? 'Phiên đã bị hủy hoặc bỏ dở.'
                                : 'Phiên vẫn đang được thực hiện.'}
                          </p>
                          {item.status !== 'completed' && item.status !== 'abandoned' && (
                            <button
                              type="button"
                              onClick={() => {
                                setIsHistoryDrawerOpen(false)
                                navigate(
                                  `/practice/session?session=${encodeURIComponent(item.sessionId)}`,
                                )
                              }}
                              className="inline-flex items-center gap-1.5 font-semibold text-[#F26F21] hover:underline"
                            >
                              Mở phiên luyện tập <ArrowRight className="size-3.5" />
                            </button>
                          )}
                        </div>
                      )}
                    </article>
                  )
                })}
              </div>
            )}
          </section>
        </div>
      </OverlayDrawer>

      {/* CỬA SỔ DRAWER HỖ TRỢ 3: CẨM NANG TRẢ LỜI VẤN ĐÁP & BAREM RUBRIC CLO */}
      <OverlayDrawer
        open={isRubricDrawerOpen}
        onClose={() => setIsRubricDrawerOpen(false)}
        badge="Chuẩn FPT University"
        title="Tiêu chí chấm điểm & Cẩm nang vấn đáp"
        subtitle="Cấu trúc khung câu trả lời viva-voce đạt điểm tối đa và tiêu chuẩn đánh giá từ Giảng viên & AI"
        width="lg"
        footer={
          <div className="flex w-full items-center justify-between gap-3 text-xs text-slate-600">
            <span className="inline-flex items-center gap-1.5 font-medium">
              <Check className="size-4 text-[#F26F21]" />
              Áp dụng xuyên suốt các môn SE (PRN231, SWD392, SWR302)
            </span>
            <button
              type="button"
              onClick={() => setIsRubricDrawerOpen(false)}
              className="rounded-xl border border-slate-200 bg-white px-4 py-2 text-xs font-semibold text-slate-700 transition hover:bg-slate-50 cursor-pointer"
            >
              Đã hiểu & Đóng
            </button>
          </div>
        }
      >
        <div className="space-y-5">
          <section className="space-y-3">
            <h4 className="flex items-center gap-2 text-sm font-semibold text-slate-900">
              <Lightbulb className="size-4 text-slate-700" />
              Cấu trúc câu trả lời 3 bước chuẩn FPTU
            </h4>
            <div className="space-y-2.5">
              {ANSWER_STRUCTURE_STEPS.map((step) => (
                <div
                  key={step.step}
                  className="rounded-xl border border-slate-200 bg-slate-50 p-4 text-xs"
                >
                  <div className="flex items-center gap-2">
                    <span className="flex size-6 items-center justify-center rounded-md border border-slate-200 bg-white text-xs font-semibold text-slate-700">
                      {step.step}
                    </span>
                    <strong className="text-sm font-semibold text-slate-900">{step.title}</strong>
                  </div>
                  <p className="mt-2 text-xs text-slate-600 leading-relaxed">{step.detail}</p>
                </div>
              ))}
            </div>
          </section>

          <section className="space-y-3">
            <h4 className="flex items-center gap-2 text-sm font-semibold text-slate-900">
              <ShieldCheck className="size-4 text-slate-700" />
              Barem điểm Rubric CLO chi tiết (Thang 10.0đ)
            </h4>
            <div className="space-y-2 text-xs">
              <div className="flex items-start justify-between rounded-xl border border-slate-200 bg-white p-3">
                <div>
                  <strong className="text-slate-900 font-semibold block">
                    1. Khái niệm & Thuật ngữ SE cốt lõi
                  </strong>
                  <span className="text-slate-500">
                    Định nghĩa rõ ràng, chính xác các khái niệm kỹ thuật phần mềm, không nhầm lẫn.
                  </span>
                </div>
                <span className="rounded-md border border-slate-200 bg-slate-100 px-2 py-0.5 font-semibold text-slate-700">
                  2.0đ
                </span>
              </div>

              <div className="flex items-start justify-between rounded-xl border border-slate-200 bg-white p-3">
                <div>
                  <strong className="text-slate-900 font-semibold block">
                    2. Cơ chế hoạt động & Phân tích chuyên sâu
                  </strong>
                  <span className="text-slate-500">
                    Giải thích luồng thực thi, vòng đời, cấu trúc dữ liệu hoặc các design patterns
                    liên quan.
                  </span>
                </div>
                <span className="rounded-md border border-slate-200 bg-slate-100 px-2 py-0.5 font-semibold text-slate-700">
                  4.0đ
                </span>
              </div>

              <div className="flex items-start justify-between rounded-xl border border-slate-200 bg-white p-3">
                <div>
                  <strong className="text-slate-900 font-semibold block">
                    3. Ứng dụng thực tế & Đánh giá Trade-offs
                  </strong>
                  <span className="text-slate-500">
                    Liên hệ dự án thực tế, phân tích ưu/nhược điểm kỹ thuật và xử lý các trường hợp
                    biên.
                  </span>
                </div>
                <span className="rounded-md border border-slate-200 bg-slate-100 px-2 py-0.5 font-semibold text-slate-700">
                  4.0đ
                </span>
              </div>
            </div>
          </section>

          <div className="rounded-xl border border-slate-200 bg-slate-50 p-4 text-xs text-slate-600">
            <strong className="text-slate-900 block font-semibold mb-1">
              Lời khuyên luyện tập với AI:
            </strong>
            <p className="leading-relaxed">
              Hãy giữ bình tĩnh và nói bằng giọng điệu tự tin, rõ ràng. Khi gặp câu hỏi mở, hãy chủ
              động áp dụng cấu trúc 3 bước ở trên. AI sẽ chấm điểm từng tiêu chí và đưa ra gợi ý cải
              thiện câu trả lời của bạn ngay sau mỗi câu.
            </p>
          </div>
        </div>
      </OverlayDrawer>

      <div className="sr-only" aria-live="polite">
        {voicePreviewMessage}
        {microphoneState === 'ready' && ` Microphone ${microphoneName} đã sẵn sàng.`}
      </div>
    </div>
  )
}

export { PracticePage as PracticeSetupPage }
