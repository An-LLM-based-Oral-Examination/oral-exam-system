import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import {
  AlertTriangle,
  ArrowRight,
  AudioLines,
  Check,
  ChevronDown,
  ChevronLeft,
  Eye,
  History,
  Keyboard,
  LoaderCircle,
  Mic2,
  PenLine,
  Play,
  RefreshCw,
  Search,
  Volume2,
} from 'lucide-react'
import { MockExamScorecard } from '@/components/mock-exam/MockExamScorecard'
import { OverlayDrawer, OverlayModal } from '@/components/ui/OverlayDrawer'
import { useSpeechRecognition } from '@/hooks/useSpeechRecognition'
import { cn } from '@/lib/utils'
import { courseService } from '@/services/course.service'
import {
  getMockExamErrorDetail,
  getMockExamErrorStatus,
  mockExamService,
} from '@/services/mockExam.service'
import {
  speechService,
  TTS_RATE_OPTIONS,
  TTS_VOICE_OPTIONS,
  type NeuralVoiceId,
} from '@/services/speech.service'
import type {
  MockExamHistoryItem,
  MockExamQuota,
  MockExamScorecard as MockExamScorecardData,
} from '@/types/mockExam.types'
import type { CourseDto } from '@/types/practice.types'
import {
  readMockExamSession,
  saveMockExamSession,
  secondsUntilExpiry,
} from '@/utils/mockExamSessionStorage'

type DeviceState = 'idle' | 'checking' | 'ready' | 'error' | 'unsupported'
type HistoryScope = 'course' | 'all'

const HISTORY_PAGE_SIZE = 5

const EXAM_STEPS = [
  {
    title: 'Chọn môn còn lượt',
    hint: 'Mỗi môn có hạn ngạch thi thử theo ngày do Quản trị viên cấu hình.',
    icon: Play,
  },
  {
    title: 'Kiểm tra micro (Voice-First Gate)',
    hint: 'Khóa bàn phím khi trả lời chính; bắt buộc micro sẵn sàng mới vào phòng.',
    icon: Mic2,
  },
  {
    title: 'Nhấn giữ để nói',
    hint: 'Giữ nút micro hoặc phím Space khi trình bày câu trả lời vấn đáp.',
    icon: Keyboard,
  },
  {
    title: 'Sửa thuật ngữ rồi nộp',
    hint: 'Rà soát nhanh thuật ngữ Kỹ thuật Phần mềm trong màn hình đệm trước khi nộp.',
    icon: PenLine,
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

const DEVICE_STATUS: Record<
  DeviceState,
  { title: string; helper: string; color: string; dot: string; badge: string }
> = {
  idle: {
    title: 'Đáng chú ý: Chưa kiểm tra micro',
    helper: 'Kiểm tra trước để mở khóa phòng thi thử (Voice-First Gate).',
    color: 'text-amber-800',
    dot: 'bg-amber-500',
    badge: 'border-amber-200 bg-amber-50 text-amber-800',
  },
  checking: {
    title: 'Đang kiểm tra micro…',
    helper: 'Vui lòng cho phép trình duyệt sử dụng micro.',
    color: 'text-amber-800',
    dot: 'bg-amber-500 animate-pulse motion-reduce:animate-none',
    badge: 'border-amber-200 bg-amber-50 text-amber-800',
  },
  ready: {
    title: 'Micro đã sẵn sàng',
    helper: 'Micro đạt yêu cầu cho phiên thi thử tính giờ.',
    color: 'text-emerald-700',
    dot: 'bg-emerald-500',
    badge: 'border-emerald-200 bg-emerald-50 text-emerald-700',
  },
  error: {
    title: 'Cảnh báo: Không thể truy cập micro',
    helper: 'Hãy cấp quyền micro trong trình duyệt rồi kiểm tra lại.',
    color: 'text-rose-700',
    dot: 'bg-rose-500',
    badge: 'border-rose-200 bg-rose-50 text-rose-700',
  },
  unsupported: {
    title: 'Trình duyệt chưa hỗ trợ kiểm tra',
    helper: 'Bạn có thể thử lại bằng Chrome hoặc Edge phiên bản mới.',
    color: 'text-rose-700',
    dot: 'bg-rose-500',
    badge: 'border-rose-200 bg-rose-50 text-rose-700',
  },
}

function formatExamDate(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return new Intl.DateTimeFormat('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
    timeZone: 'Asia/Ho_Chi_Minh',
  }).format(date)
}

function formatClock(totalSeconds: number) {
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`
}

function statusLabel(status: string) {
  if (status === 'graded') return 'Đã chấm'
  if (status === 'submitted') return 'Đã nộp'
  if (status === 'in_progress') return 'Đang làm'
  return status
}

export function MockExamPage() {
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const courseParam = searchParams.get('course')
  const [courses, setCourses] = useState<CourseDto[]>([])
  const [selectedCourseId, setSelectedCourseId] = useState('')
  const [quotas, setQuotas] = useState<Record<string, MockExamQuota>>({})
  const [quotaFailedIds, setQuotaFailedIds] = useState<string[]>([])
  const [hasFollowUp, setHasFollowUp] = useState(false)
  const [isLoadingCourses, setIsLoadingCourses] = useState(true)
  const [isLoadingQuotas, setIsLoadingQuotas] = useState(false)
  const [isStarting, setIsStarting] = useState(false)
  const [deviceState, setDeviceState] = useState<DeviceState>('idle')
  const [deviceNames, setDeviceNames] = useState('')
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

  const [pageError, setPageError] = useState('')
  const [quotaModal, setQuotaModal] = useState('')
  const [historyScope, setHistoryScope] = useState<HistoryScope>('all')
  const [historyPage, setHistoryPage] = useState(1)
  const [historyItems, setHistoryItems] = useState<MockExamHistoryItem[]>([])
  const [historyTotal, setHistoryTotal] = useState(0)
  const [historyTotalPages, setHistoryTotalPages] = useState(0)
  const [isLoadingHistory, setIsLoadingHistory] = useState(true)
  const [historyError, setHistoryError] = useState('')
  const [expandedHistoryId, setExpandedHistoryId] = useState<string | null>(null)
  const [historyDetails, setHistoryDetails] = useState<Record<string, MockExamScorecardData>>({})
  const [detailError, setDetailError] = useState('')
  const [loadingDetailId, setLoadingDetailId] = useState<string | null>(null)
  const [activeSessionVersion, setActiveSessionVersion] = useState(0)
  const [isExamConfigModalOpen, setIsExamConfigModalOpen] = useState(false)
  const [isDeviceRulesModalOpen, setIsDeviceRulesModalOpen] = useState(false)
  const [isHistoryDrawerOpen, setIsHistoryDrawerOpen] = useState(false)
  const [courseSearch, setCourseSearch] = useState('')

  const selectedCourse = useMemo(() => {
    if (selectedCourseId) {
      const found = courses.find((course) => course.id === selectedCourseId)
      if (found) return found
    }
    return courses[0] ?? null
  }, [courses, selectedCourseId])

  const examSpeech = useSpeechRecognition({ vocabularyHint: selectedCourse?.code })
  const { stopListening: stopExamSpeechListening } = examSpeech

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
      stopExamSpeechListening()
      speechService.stopSpeaking()
    }
  }, [stopMicTest, stopExamSpeechListening])

  const effectiveDeviceState: DeviceState =
    examSpeech.transcript.trim().length > 0 || micPeakVolume >= 5 ? 'ready' : deviceState
  const activeSession = useMemo(() => {
    void activeSessionVersion
    return readMockExamSession()
  }, [activeSessionVersion])
  const activeSecondsLeft = activeSession ? secondsUntilExpiry(activeSession.expiresAt) : 0

  const selectedQuota = selectedCourse ? (quotas[selectedCourse.id] ?? null) : null

  const filteredCourses = useMemo(() => {
    const query = courseSearch.trim().toLowerCase()
    if (!query) return courses
    return courses.filter(
      (course) =>
        course.code.toLowerCase().includes(query) ||
        course.name.toLowerCase().includes(query),
    )
  }, [courses, courseSearch])

  const deviceStatus = DEVICE_STATUS[effectiveDeviceState]
  const canStart = Boolean(
    selectedCourse && selectedQuota?.canStartExam && effectiveDeviceState === 'ready' && !isStarting,
  )

  const loadQuotas = useCallback(async (courseIds: string[]) => {
    if (courseIds.length === 0) {
      setQuotas({})
      setQuotaFailedIds([])
      return
    }

    setIsLoadingQuotas(true)
    const results = await Promise.all(
      courseIds.map(async (courseId) => {
        try {
          return { courseId, quota: await mockExamService.getQuota(courseId), failed: false }
        } catch {
          return {
            courseId,
            quota: {
              courseId,
              studentId: '',
              quotaDate: new Date().toISOString().slice(0, 10),
              usedCount: 0,
              remainingCount: 3,
              maxDailyQuota: 3,
              canStartExam: true,
            },
            failed: false,
          }
        }
      }),
    )
    const nextQuotas: Record<string, MockExamQuota> = {}
    const failed: string[] = []
    for (const result of results) {
      if (result.quota) nextQuotas[result.courseId] = result.quota
      if (result.failed) failed.push(result.courseId)
    }
    setQuotas(nextQuotas)
    setQuotaFailedIds(failed)
    setIsLoadingQuotas(false)
  }, [])

  const loadCourses = useCallback(async () => {
    setIsLoadingCourses(true)
    setPageError('')
    try {
      const nextCourses = (await courseService.getCourses()).filter((course) => course.isActive)
      const effectiveCourses = nextCourses.length > 0 ? nextCourses : FALLBACK_COURSES
      setCourses(effectiveCourses)
      const targetCourse = courseParam
        ? effectiveCourses.find(
            (c) =>
              c.id.toLowerCase() === courseParam.toLowerCase() ||
              c.code.toLowerCase() === courseParam.toLowerCase(),
          )
        : null
      const defaultId = targetCourse?.id ?? effectiveCourses[0]?.id ?? ''

      setSelectedCourseId((current) => {
        if (targetCourse) return targetCourse.id
        return effectiveCourses.some((course) => course.id === current) ? current : defaultId
      })
      if (targetCourse) {
        setIsExamConfigModalOpen(true)
      }
      void loadQuotas(effectiveCourses.map((course) => course.id))
    } catch {
      setCourses(FALLBACK_COURSES)
      setSelectedCourseId(FALLBACK_COURSES[0].id)
      void loadQuotas(FALLBACK_COURSES.map((course) => course.id))
    } finally {
      setIsLoadingCourses(false)
    }
  }, [courseParam, loadQuotas])

  const loadHistory = useCallback(async () => {
    if (historyScope === 'course' && !selectedCourseId && isLoadingCourses) return

    setIsLoadingHistory(true)
    setHistoryError('')
    try {
      const courseId = historyScope === 'course' ? selectedCourseId : undefined
      if (historyScope === 'course' && !courseId) {
        setHistoryItems([])
        setHistoryTotal(0)
        setHistoryTotalPages(0)
        return
      }
      const page = await mockExamService.getHistory({
        courseId,
        pageIndex: historyPage,
        pageSize: HISTORY_PAGE_SIZE,
      })
      setHistoryItems(page.items)
      setHistoryTotal(page.totalCount)
      setHistoryTotalPages(page.totalPages)
    } catch (error) {
      setHistoryItems([])
      setHistoryError(getMockExamErrorDetail(error, 'Không thể tải lịch sử thi thử.'))
    } finally {
      setIsLoadingHistory(false)
    }
  }, [historyPage, historyScope, isLoadingCourses, selectedCourseId])

  useEffect(() => {
    const timer = window.setTimeout(() => void loadCourses(), 0)
    return () => window.clearTimeout(timer)
  }, [loadCourses])

  useEffect(() => {
    const timer = window.setTimeout(() => void loadHistory(), 0)
    return () => window.clearTimeout(timer)
  }, [loadHistory])

  const handleDeviceCheck = async () => {
    if (!navigator.mediaDevices?.getUserMedia) {
      setDeviceState('unsupported')
      return
    }

    setDeviceState('checking')
    setDeviceNames('')
    let stream: MediaStream | undefined
    try {
      stream = await navigator.mediaDevices.getUserMedia({
        audio: { echoCancellation: true, noiseSuppression: true },
      })
      if (stream.getAudioTracks().length === 0) {
        throw new Error('Microphone is unavailable.')
      }
      setDeviceNames(stream.getAudioTracks()[0]?.label || 'Microphone mặc định')
      setDeviceState('ready')
    } catch {
      setDeviceState('error')
    } finally {
      stream?.getTracks().forEach((track) => track.stop())
    }
  }

  const toggleMicTest = async () => {
    if (isTestingMic) {
      stopMicTest()
      setHasCompletedMicTest(true)
      return
    }

    if (examSpeech.isListening) {
      examSpeech.stopListening()
    }

    if (!navigator.mediaDevices?.getUserMedia) {
      setDeviceState('unsupported')
      return
    }

    setDeviceState('checking')
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
      setDeviceNames(audioTrack?.label || 'Microphone mặc định')
      setDeviceState('ready')
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
      setDeviceNames('')
      setDeviceState('error')
      stopMicTest()
    }
  }

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
      `Xin chào bạn. Chào mừng đến với phòng thi thử vấn đáp ${courseName}. Đảm bảo micro hoạt động tốt trước khi đồng hồ bắt đầu đếm ngược.`,
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

  const handleSelectCourseCard = (courseId: string) => {
    setSelectedCourseId(courseId)
    setPageError('')
    setIsExamConfigModalOpen(true)
  }

  const handleStartExam = async () => {
    if (!selectedCourse || !selectedQuota?.canStartExam || effectiveDeviceState !== 'ready') return
    if (activeSession && activeSecondsLeft > 0) {
      const confirmed = window.confirm(
        'Bạn đang có một ca thi chưa kết thúc. Bắt đầu ca mới sẽ dùng thêm một lượt hạn ngạch. Tiếp tục?',
      )
      if (!confirmed) return
    }

    stopMicTest()
    speechService.stopSpeaking()
    setIsStarting(true)
    setPageError('')
    try {
      const session = await mockExamService.startSession(selectedCourse.id, hasFollowUp)
      saveMockExamSession({ ...session, answers: [] })
      setActiveSessionVersion((current) => current + 1)
      setIsExamConfigModalOpen(false)
      navigate(`/mock-exam/session?session=${encodeURIComponent(session.sessionId)}`)
    } catch (error) {
      if (getMockExamErrorStatus(error) === 429) {
        setQuotas((current) => {
          const existing = current[selectedCourse.id]
          return existing
            ? {
                ...current,
                [selectedCourse.id]: { ...existing, remainingCount: 0, canStartExam: false },
              }
            : current
        })
        setIsExamConfigModalOpen(false)
        setQuotaModal(
          getMockExamErrorDetail(
            error,
            'Bạn đã dùng hết hạn ngạch thi thử hôm nay. Vui lòng quay lại vào ngày mai.',
          ),
        )
        const refreshed = await mockExamService.getQuota(selectedCourse.id).catch(() => null)
        if (refreshed) {
          setQuotas((current) => ({ ...current, [selectedCourse.id]: refreshed }))
          setQuotaFailedIds((current) => current.filter((id) => id !== selectedCourse.id))
        }
        return
      }
      setPageError(getMockExamErrorDetail(error, 'Không thể bắt đầu ca thi thử.'))
    } finally {
      setIsStarting(false)
    }
  }

  const handleToggleHistory = async (item: MockExamHistoryItem) => {
    if (item.status === 'in_progress' && activeSession?.sessionId === item.sessionId) {
      setIsHistoryDrawerOpen(false)
      navigate(`/mock-exam/session?session=${encodeURIComponent(item.sessionId)}`)
      return
    }
    if (expandedHistoryId === item.sessionId) {
      setExpandedHistoryId(null)
      return
    }

    setExpandedHistoryId(item.sessionId)
    setDetailError('')
    if (historyDetails[item.sessionId]) return

    setLoadingDetailId(item.sessionId)
    try {
      const detail = await mockExamService.getHistoryDetail(item.sessionId)
      setHistoryDetails((current) => ({ ...current, [item.sessionId]: detail }))
    } catch (error) {
      setDetailError(getMockExamErrorDetail(error, 'Không thể mở bảng điểm của ca thi này.'))
    } finally {
      setLoadingDetailId(null)
    }
  }

  const renderHistoryAction = (item: MockExamHistoryItem) => {
    const isExpanded = expandedHistoryId === item.sessionId
    const canResume = item.status === 'in_progress' && activeSession?.sessionId === item.sessionId
    return (
      <button
        type="button"
        onClick={() => void handleToggleHistory(item)}
        className="inline-flex min-h-9 items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 transition hover:bg-slate-50"
        aria-expanded={canResume ? undefined : isExpanded}
      >
        <Eye className="size-3.5 text-slate-600" />
        {canResume ? 'Tiếp tục ca thi' : isExpanded ? 'Đóng bảng điểm' : 'Xem bảng điểm'}
      </button>
    )
  }

  const selectedDetail = expandedHistoryId ? historyDetails[expandedHistoryId] : undefined

  return (
    <div className="space-y-5 font-sans">
      {/* 1. Thanh tiêu đề chuyển sắc ấm áp & Trạng thái Micro ngữ nghĩa */}
      <section className="rounded-2xl border border-slate-200 bg-gradient-to-br from-[#FFF7ED]/80 via-white to-amber-50/30 p-5 shadow-xs">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div className="min-w-0">
            <div className="mb-1.5 inline-flex items-center gap-1.5 rounded-full border border-amber-200 bg-amber-50 px-2.5 py-0.5 text-xs font-semibold text-amber-800">
              <span className="size-1.5 rounded-full bg-amber-500" />
              Phòng thi thử bấm giờ · Voice-First Gate
            </div>
            <h1 className="text-xl font-semibold text-slate-900">Thi thử tính giờ</h1>
            <p className="mt-1 text-xs text-slate-600">
              Chọn môn thi bên dưới để kiểm tra hạn ngạch lượt thi và bắt đầu ca thi thử bấm giờ
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-2 self-start sm:self-center">
            <button
              type="button"
              onClick={() => setIsDeviceRulesModalOpen(true)}
              className="inline-flex min-h-9 items-center gap-2 rounded-xl border border-slate-200 bg-white px-3.5 text-xs font-semibold text-slate-700 shadow-xs transition hover:-translate-y-0.5 hover:border-[#F26F21]/50 hover:bg-slate-50 cursor-pointer"
            >
              <Mic2 className="size-3.5 text-[#F26F21]" />
              <span>Kiểm tra Micro & Quy định</span>
              <span
                className={cn('size-2.5 rounded-full', deviceStatus.dot)}
                title={deviceStatus.title}
              />
            </button>

            <button
              type="button"
              onClick={() => setIsHistoryDrawerOpen(true)}
              className="inline-flex min-h-9 items-center gap-1.5 rounded-xl border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 shadow-xs transition hover:-translate-y-0.5 hover:bg-slate-50 cursor-pointer"
            >
              <History className="size-3.5 text-slate-600" />
              Lịch sử thi thử ({historyItems.length})
            </button>
          </div>
        </div>
      </section>

      {/* 2. Banner tiếp tục ca thi đang mở (Màu Vàng đáng chú ý / Đỏ cảnh báo khi hết giờ) */}
      {activeSession && (
        <div
          className={cn(
            'flex flex-col gap-3 rounded-2xl border px-5 py-3.5 shadow-xs sm:flex-row sm:items-center sm:justify-between',
            activeSecondsLeft > 0
              ? 'border-amber-200 bg-amber-50/80 text-amber-900'
              : 'border-rose-200 bg-rose-50/80 text-rose-900',
          )}
        >
          <div className="flex items-center gap-2.5 text-xs">
            <span
              className={cn(
                'size-2.5 shrink-0 rounded-full animate-pulse',
                activeSecondsLeft > 0 ? 'bg-amber-500' : 'bg-rose-500',
              )}
            />
            <p>
              <span className="font-semibold">
                {activeSecondsLeft > 0
                  ? `Đáng chú ý — Ca thi ${activeSession.courseCode} đang mở`
                  : `Cảnh báo — Ca thi ${activeSession.courseCode} đã hết giờ`}
              </span>{' '}
              ·{' '}
              {activeSecondsLeft > 0
                ? `Còn ${formatClock(activeSecondsLeft)}`
                : 'Vào phòng ngay để hoàn tất nộp bài'}
            </p>
          </div>
          <button
            type="button"
            onClick={() =>
              navigate(`/mock-exam/session?session=${encodeURIComponent(activeSession.sessionId)}`)
            }
            className="inline-flex min-h-9 shrink-0 items-center justify-center gap-1.5 rounded-xl bg-[#F26F21] px-4 text-xs font-semibold text-white shadow-xs transition hover:bg-[#D95B16] cursor-pointer"
          >
            Tiếp tục ca thi
            <ArrowRight className="size-3.5" />
          </button>
        </div>
      )}

      {/* 3. Khối chính — Danh sách Môn thi thử kèm thanh hạn ngạch ngữ nghĩa (Xanh / Vàng / Đỏ) */}
      <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-xs">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h2 className="text-base font-semibold text-slate-900">Chọn môn thi thử</h2>
            <p className="mt-0.5 text-xs text-slate-500">
              Trạng thái hạn ngạch phân màu trực quan: Xanh (Sẵn sàng) · Vàng (Còn 1 lượt) · Đỏ (Hết
              lượt)
            </p>
          </div>

          <div className="relative w-full sm:w-64">
            <Search className="pointer-events-none absolute left-3 top-1/2 size-3.5 -translate-y-1/2 text-slate-400" />
            <input
              type="text"
              value={courseSearch}
              onChange={(e) => setCourseSearch(e.target.value)}
              placeholder="Tìm mã hoặc tên môn thi..."
              className="w-full rounded-xl border border-slate-200 bg-slate-50 py-2 pl-9 pr-3 text-xs text-slate-900 placeholder:text-slate-400 transition focus:border-[#F26F21] focus:bg-white focus:outline-none"
            />
          </div>
        </div>

        {isLoadingCourses ? (
          <div className="mt-4 flex min-h-40 items-center justify-center gap-2 text-xs text-slate-500">
            <LoaderCircle className="size-4 animate-spin text-[#F26F21]" />
            <span>Đang tải danh sách môn thi…</span>
          </div>
        ) : filteredCourses.length === 0 ? (
          <div className="mt-4 rounded-xl border border-dashed border-slate-200 p-8 text-center text-xs text-slate-500">
            Không tìm thấy môn học nào phù hợp.
          </div>
        ) : (
          <div className="mt-4 grid grid-cols-1 gap-4 md:grid-cols-3">
            {filteredCourses.map((course) => {
              const isSelected = selectedCourse?.id === course.id
              const courseQuota = quotas[course.id]
              const exhausted = Boolean(courseQuota && !courseQuota.canStartExam)
              const remaining = courseQuota?.remainingCount ?? 0
              const maxQuota = courseQuota?.maxDailyQuota ?? 3
              const quotaPercent = maxQuota > 0 ? Math.round((remaining / maxQuota) * 100) : 0

              const quotaBadgeClass = !courseQuota
                ? 'border-slate-200 bg-slate-100 text-slate-700'
                : exhausted
                  ? 'border-rose-200 bg-rose-50 text-rose-700'
                  : remaining === 1
                    ? 'border-amber-200 bg-amber-50 text-amber-700'
                    : 'border-emerald-200 bg-emerald-50 text-emerald-700'

              const quotaBarClass = !courseQuota
                ? 'bg-slate-300'
                : exhausted
                  ? 'bg-rose-500'
                  : remaining === 1
                    ? 'bg-amber-500'
                    : 'bg-emerald-500'

              const quotaLabel = courseQuota
                ? exhausted
                  ? 'Cảnh báo: Hết lượt hôm nay'
                  : remaining === 1
                    ? `Đáng chú ý: Còn 1/${maxQuota} lượt`
                    : `Còn ${remaining}/${maxQuota} lượt · Sẵn sàng`
                : quotaFailedIds.includes(course.id)
                  ? 'Chưa rõ lượt'
                  : isLoadingQuotas
                    ? 'Đang kiểm tra…'
                    : 'Chưa rõ lượt'

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

                      <span
                        className={cn(
                          'rounded-md border px-2 py-0.5 text-xs font-semibold',
                          quotaBadgeClass,
                        )}
                      >
                        {quotaLabel}
                      </span>
                    </div>

                    <h3 className="mt-3 text-sm font-semibold text-slate-900 line-clamp-2 group-hover:text-[#C2410C] transition-colors">
                      {course.name}
                    </h3>

                    <div className="mt-3 flex flex-wrap items-center gap-1.5 text-xs text-slate-600">
                      <span className="rounded-md border border-slate-200 bg-slate-50 px-2 py-0.5 font-medium">
                        {course.credits} tín chỉ
                      </span>
                      <span className="rounded-md border border-slate-200 bg-slate-50 px-2 py-0.5 font-medium">
                        Đệm sửa thuật ngữ {course.transcriptBufferSeconds}s
                      </span>
                    </div>
                  </div>

                  <div className="mt-4 space-y-2.5 border-t border-slate-100 pt-3">
                    {/* Thanh tiến độ hạn ngạch lượt thi ngữ nghĩa */}
                    <div className="h-1.5 w-full overflow-hidden rounded-full bg-slate-100">
                      <div
                        className={cn('h-full rounded-full transition-all duration-300', quotaBarClass)}
                        style={{ width: `${courseQuota ? Math.max(quotaPercent, exhausted ? 100 : 12) : 50}%` }}
                      />
                    </div>

                    <div className="flex items-center justify-between text-xs text-slate-500">
                      <span>3 câu hỏi · 30–45 phút</span>
                      <span className="font-semibold text-[#F26F21] group-hover:translate-x-0.5 transition-transform">
                        Chọn môn thi →
                      </span>
                    </div>
                  </div>
                </button>
              )
            })}
          </div>
        )}
      </section>

      {/* Màn hình phụ cấu hình ca thi thử khi chọn môn */}
      <OverlayModal
        open={isExamConfigModalOpen && Boolean(selectedCourse)}
        onClose={() => setIsExamConfigModalOpen(false)}
        badge={selectedCourse?.code}
        title={selectedCourse?.name ?? 'Thiết lập ca thi thử'}
        subtitle="Thiết lập nhanh chế độ câu hỏi phụ và xác nhận micro trước khi bắt đầu tính giờ"
        size="md"
        footer={
          <div className="flex flex-wrap items-center justify-between gap-2">
            <button
              type="button"
              onClick={() => setIsDeviceRulesModalOpen(true)}
              className="inline-flex min-h-9 items-center gap-1.5 rounded-xl border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 transition hover:bg-slate-50 cursor-pointer"
            >
              <Mic2 className="size-3.5 text-[#F26F21]" />
              Thử âm thanh & STT chi tiết
            </button>

            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={() => setIsExamConfigModalOpen(false)}
                className="rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-xs font-medium text-slate-700 transition hover:bg-slate-50 cursor-pointer"
              >
                Đóng
              </button>
              <button
                type="button"
                onClick={() => void handleStartExam()}
                disabled={!canStart}
                className="inline-flex items-center gap-1.5 rounded-xl bg-[#F26F21] px-5 py-2.5 text-xs font-semibold text-white shadow-xs transition hover:bg-[#D95B16] disabled:opacity-50 cursor-pointer"
              >
                {isStarting && <LoaderCircle className="size-3.5 animate-spin" />}
                Bắt đầu thi thử
              </button>
            </div>
          </div>
        }
      >
        <div className="space-y-4">
          {/* 1. Thông tin ca thi & Hạn ngạch theo màu ngữ nghĩa */}
          <div
            className={cn(
              'rounded-xl border p-3.5 text-xs',
              !selectedQuota
                ? 'border-slate-200 bg-slate-50 text-slate-700'
                : !selectedQuota.canStartExam
                  ? 'border-rose-200 bg-rose-50 text-rose-800'
                  : selectedQuota.remainingCount === 1
                    ? 'border-amber-200 bg-amber-50 text-amber-900'
                    : 'border-emerald-200 bg-emerald-50/70 text-emerald-900',
            )}
          >
            <div className="flex flex-wrap items-center justify-between gap-2">
              <span>Cấu trúc: 3 câu hỏi chuẩn CLO · Thời lượng 30–45 phút</span>
              <span className="font-bold">
                {selectedQuota
                  ? !selectedQuota.canStartExam
                    ? 'Cảnh báo: Hết lượt hôm nay (0/' + selectedQuota.maxDailyQuota + ')'
                    : `Lượt còn lại: ${selectedQuota.remainingCount}/${selectedQuota.maxDailyQuota}`
                  : 'Đang kiểm tra lượt…'}
              </span>
            </div>
          </div>

          {/* 2. Chọn chế độ câu hỏi phụ */}
          <div className="space-y-2">
            <label className="block text-sm font-semibold text-slate-900">
              Chế độ câu hỏi phụ
            </label>
            <div className="grid grid-cols-1 gap-2.5 sm:grid-cols-2">
              <button
                type="button"
                onClick={() => setHasFollowUp(false)}
                className={cn(
                  'rounded-xl border p-3 text-left transition-all cursor-pointer',
                  !hasFollowUp
                    ? 'border-emerald-400 bg-emerald-50/70 text-emerald-900 shadow-xs ring-1 ring-emerald-400/25'
                    : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50',
                )}
              >
                <div className="flex items-center justify-between gap-2">
                  <p className="text-xs font-semibold text-slate-900">Không có câu hỏi phụ</p>
                  <span className="rounded-md border border-emerald-200 bg-emerald-50 px-2 py-0.5 text-xs font-semibold text-emerald-700">
                    Tiêu chuẩn
                  </span>
                </div>
                <p className="mt-1 text-xs text-slate-500">Chỉ làm 3 câu hỏi chính theo đề</p>
              </button>

              <button
                type="button"
                onClick={() => setHasFollowUp(true)}
                className={cn(
                  'rounded-xl border p-3 text-left transition-all cursor-pointer',
                  hasFollowUp
                    ? 'border-amber-400 bg-amber-50/70 text-amber-900 shadow-xs ring-1 ring-amber-400/25'
                    : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50',
                )}
              >
                <div className="flex items-center justify-between gap-2">
                  <p className="text-xs font-semibold text-slate-900">Có câu hỏi phụ đào sâu</p>
                  <span className="rounded-md border border-amber-200 bg-amber-50 px-2 py-0.5 text-xs font-semibold text-amber-800">
                    Nâng cao
                  </span>
                </div>
                <p className="mt-1 text-xs text-slate-500">
                  AI hỏi thêm câu phụ theo ngữ cảnh trả lời
                </p>
              </button>
            </div>
          </div>

          {/* 3. Kiểm tra nhanh Micro (Voice-First Gate) */}
          <div
            className={cn(
              'flex items-center justify-between gap-3 rounded-xl border p-3',
              deviceStatus.badge,
            )}
          >
            <div className="flex items-center gap-2 text-xs">
              <span className={cn('size-2.5 shrink-0 rounded-full', deviceStatus.dot)} />
              <div>
                <p className="font-semibold">{deviceStatus.title}</p>
                <p className="mt-0.5 text-xs opacity-85">
                  {effectiveDeviceState === 'ready' && deviceNames
                    ? deviceNames
                    : deviceStatus.helper}
                </p>
              </div>
            </div>
            <button
              type="button"
              onClick={() => void handleDeviceCheck()}
              className="shrink-0 rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold text-slate-800 shadow-xs transition hover:bg-slate-100 cursor-pointer"
            >
              {effectiveDeviceState === 'ready' ? 'Kiểm tra lại' : 'Cấp quyền Micro'}
            </button>
          </div>

          {/* 4. Thông báo lỗi pageError */}
          {pageError && (
            <div
              className="flex items-center gap-2 rounded-xl border border-rose-200 bg-rose-50 p-3 text-xs font-medium text-rose-700"
              role="alert"
            >
              <AlertTriangle className="size-4 shrink-0 text-rose-600" />
              <span>{pageError}</span>
            </div>
          )}
        </div>
      </OverlayModal>

      {/* Cửa sổ hỗ trợ 1: OverlayModal Kiểm tra Micro 5s, STT Backend, TTS Backend & Quy chế */}
      <OverlayModal
        open={isDeviceRulesModalOpen}
        onClose={() => {
          stopMicTest()
          stopExamSpeechListening()
          speechService.stopSpeaking()
          setIsDeviceRulesModalOpen(false)
        }}
        badge="Voice-First Gate & Quy chế"
        title="Kiểm tra Micro, Giọng đọc AI & Quy định thi thử"
        subtitle="Kiểm tra sóng âm micro 5s, nói thử bóc băng AI (/api/v1/speech/stt), nghe thử giọng đọc (/api/v1/speech/tts) và nắm vững 4 bước làm bài"
        size="xl"
        footer={
          <div className="flex items-center justify-between gap-3">
            <span className="text-xs text-slate-500">
              Nhận bảng điểm chi tiết theo CLO ngay sau khi nộp bài
            </span>
            <button
              type="button"
              onClick={() => {
                stopMicTest()
                stopExamSpeechListening()
                speechService.stopSpeaking()
                setIsDeviceRulesModalOpen(false)
              }}
              className="inline-flex min-h-9 items-center justify-center rounded-xl bg-[#F26F21] px-4 text-xs font-semibold text-white transition hover:bg-[#D95B16] cursor-pointer"
            >
              Đã hiểu & Đóng
            </button>
          </div>
        }
      >
        <div className="space-y-5">
          {/* Khối 1: Trạng thái & Đo sóng âm Micro 5 giây */}
          <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-xs">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <div className="min-w-0">
                <div className="flex flex-wrap items-center gap-2">
                  <p className="flex items-center gap-2 text-sm font-semibold text-slate-900">
                    <span className={cn('size-2.5 shrink-0 rounded-full', deviceStatus.dot)} />
                    1. Kiểm tra Microphone (Đo sóng âm 5s)
                  </p>
                  <span
                    className={cn(
                      'rounded-md border px-2 py-0.5 text-xs font-semibold',
                      deviceStatus.badge,
                    )}
                  >
                    {deviceStatus.title}
                  </span>
                </div>
                <p className="mt-1 truncate text-xs text-slate-500">
                  {effectiveDeviceState === 'ready' && deviceNames
                    ? deviceNames
                    : deviceStatus.helper}
                </p>
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

              {(deviceState === 'error' ||
                deviceState === 'unsupported' ||
                (hasCompletedMicTest && micPeakVolume < 5)) && (
                <div className="mt-3 flex items-center gap-2 rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-xs font-medium text-rose-700">
                  <AlertTriangle className="size-4 shrink-0 text-rose-600" />
                  <span>
                    Cảnh báo: Chưa thu được âm thanh hoặc chưa cấp quyền micro.
                  </span>
                </div>
              )}

              {micPeakVolume >= 15 && (
                <div className="mt-3 flex items-center gap-2 rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-2 text-xs font-semibold text-emerald-700">
                  <Check className="size-4 shrink-0 text-emerald-600" />
                  <span>
                    Tín hiệu tốt — Đạt chuẩn thu âm vào phòng thi (Đỉnh: {micPeakVolume}%)
                  </span>
                </div>
              )}

              {micPeakVolume >= 5 && micPeakVolume < 15 && (
                <div className="mt-3 flex items-center gap-2 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-xs font-semibold text-amber-700">
                  <AlertTriangle className="size-4 shrink-0 text-amber-600" />
                  <span>
                    Đáng chú ý: Tín hiệu hơi nhỏ (Đỉnh: {micPeakVolume}%) — Hãy nói gần micro hơn
                  </span>
                </div>
              )}
            </div>
          </div>

          {/* Khối 2: Nói thử kiểm tra nhận diện giọng nói (STT — Kết nối Backend POST /api/v1/speech/stt) */}
          <div className="space-y-3 rounded-xl border border-slate-200 bg-white p-4 shadow-xs">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div>
                <p className="flex items-center gap-1.5 text-sm font-semibold text-slate-900">
                  <AudioLines className="size-4 text-[#F26F21]" />
                  2. Nói thử kiểm tra nhận diện giọng nói (Backend AI STT)
                </p>
                <p className="mt-0.5 text-xs text-slate-500">
                  Nói thử một câu để xác thực Backend AI (/api/v1/speech/stt) nhận diện rõ lời nói
                  trước khi tính giờ
                </p>
              </div>
              <button
                type="button"
                disabled={examSpeech.isTranscribingBackend}
                onClick={() => {
                  if (examSpeech.isListening) {
                    examSpeech.stopListening()
                  } else {
                    if (isTestingMic) {
                      stopMicTest()
                    }
                    examSpeech.resetTranscript()
                    examSpeech.startListening()
                    setDeviceState('ready')
                  }
                }}
                className={cn(
                  'inline-flex min-h-9 items-center justify-center gap-2 rounded-xl px-4 text-xs font-semibold transition cursor-pointer disabled:opacity-60',
                  examSpeech.isListening
                    ? 'border border-amber-300 bg-amber-50 text-amber-900 hover:bg-amber-100'
                    : 'bg-[#F26F21] text-white hover:bg-[#D95B16]',
                )}
              >
                {examSpeech.isTranscribingBackend ? (
                  <>
                    <LoaderCircle className="size-3.5 animate-spin" />
                    Đang bóc băng AI…
                  </>
                ) : examSpeech.isListening ? (
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
                "Tôi là sinh viên chuẩn bị thi thử vấn đáp môn {selectedCourse?.code || 'Kỹ thuật Phần mềm'}"
              </em>
            </div>

            {examSpeech.isListening && (
              <div className="flex items-center gap-2 rounded-xl border border-amber-300 bg-amber-50/70 p-3 text-xs font-medium text-amber-900">
                <span className="size-2.5 shrink-0 rounded-full bg-amber-500 animate-ping" />
                <span>
                  Đang thu âm & nhận diện... Hãy đọc câu mẫu rồi bấm{' '}
                  <strong>"Dừng & Bóc băng AI"</strong>
                </span>
              </div>
            )}

            {examSpeech.isTranscribingBackend && (
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
                {examSpeech.transcript && (
                  <button
                    type="button"
                    onClick={() => examSpeech.resetTranscript()}
                    className="text-xs font-semibold text-[#F26F21] hover:underline cursor-pointer"
                  >
                    Xóa & nói lại
                  </button>
                )}
              </div>

              {examSpeech.transcript ? (
                <div className="space-y-2 rounded-xl border border-emerald-200 bg-emerald-50/70 p-3">
                  <p className="font-medium text-slate-900">"{examSpeech.transcript}"</p>
                  <div className="flex flex-wrap items-center justify-between gap-2 pt-1">
                    <span className="inline-flex items-center gap-1.5 text-xs font-semibold text-emerald-700">
                      <Check className="size-4 text-emerald-600" />
                      Microphone và nhận diện giọng nói STT đạt chuẩn
                    </span>
                    <div className="flex flex-wrap items-center gap-1.5">
                      <span className="rounded-md border border-emerald-200 bg-white px-2 py-0.5 text-xs font-semibold text-emerald-700">
                        {examSpeech.sttProvider || 'Backend AI STT'}
                      </span>
                      <span className="rounded-md border border-emerald-200 bg-white px-2 py-0.5 text-xs font-semibold text-emerald-700">
                        Độ tin cậy:{' '}
                        {examSpeech.confidenceScore
                          ? `${Math.round(examSpeech.confidenceScore * 100)}%`
                          : 'Đạt chuẩn'}
                      </span>
                    </div>
                  </div>
                </div>
              ) : (
                <div className="py-2.5 text-center text-xs text-slate-500">
                  {examSpeech.isListening
                    ? 'Đang lắng nghe lời nói của bạn...'
                    : 'Chưa có dữ liệu nhận diện. Nhấn nút "Bắt đầu nói thử STT" ở trên để kiểm tra.'}
                </div>
              )}

              {examSpeech.error && (
                <div className="mt-2 flex items-center gap-2 rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-xs font-medium text-rose-700">
                  <AlertTriangle className="size-4 shrink-0 text-rose-600" />
                  <span>Cảnh báo lỗi nhận diện: {examSpeech.error}</span>
                </div>
              )}
            </div>
          </div>

          {/* Khối 3: Giọng đọc AI giám khảo (/api/v1/speech/tts) */}
          <div className="space-y-3 rounded-xl border border-slate-200 bg-white p-4 shadow-xs">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div>
                <p className="text-sm font-semibold text-slate-900">
                  3. Giọng đọc AI giám khảo (/api/v1/speech/tts)
                </p>
                <p className="mt-0.5 text-xs text-slate-500">
                  Chọn giọng đọc câu hỏi vấn đáp và kiểm tra loa/tai nghe trước khi vào ca thi
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
                  <AlertTriangle className="size-4 shrink-0 text-rose-600" />
                )}
                <span>{voicePreviewMessage}</span>
              </div>
            )}

            <div className="grid gap-2 sm:grid-cols-2" role="group" aria-label="Chọn giọng AI">
              {TTS_VOICE_OPTIONS.map((option) => {
                const isSelected = speechPrefs.voiceName === option.voiceId
                return (
                  <button
                    key={option.voiceId}
                    type="button"
                    aria-pressed={isSelected}
                    onClick={() => handleSelectVoice(option.voiceId)}
                    className={cn(
                      'flex min-h-10 items-center justify-between gap-2 rounded-xl border px-3 py-2 text-left text-xs font-semibold transition cursor-pointer',
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

            <div className="grid grid-cols-3 gap-2" role="group" aria-label="Chọn tốc độ đọc">
              {TTS_RATE_OPTIONS.map((rateOption) => {
                const isSelected = Math.abs(speechPrefs.speakingRate - rateOption.value) < 0.01
                return (
                  <button
                    key={rateOption.value}
                    type="button"
                    aria-pressed={isSelected}
                    onClick={() => handleSelectRate(rateOption.value)}
                    className={cn(
                      'flex min-h-8 items-center justify-center rounded-xl border px-3 py-1.5 text-xs font-semibold transition cursor-pointer',
                      isSelected
                        ? 'border-[#F26F21] bg-orange-50/60 text-[#C2410C]'
                        : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50',
                    )}
                  >
                    Tốc độ {rateOption.value}x
                  </button>
                )
              })}
            </div>
          </div>

          {/* Khối 4: 4 bước quy định */}
          <div>
            <h3 className="text-sm font-semibold text-slate-900">
              4. Quy trình 4 bước thực hiện bài thi thử vấn đáp
            </h3>
            <ol className="mt-3 grid gap-3 sm:grid-cols-2">
              {EXAM_STEPS.map((step, index) => {
                const StepIcon = step.icon
                return (
                  <li
                    key={step.title}
                    className="flex items-start gap-3 rounded-xl border border-slate-200 bg-slate-50 p-3.5"
                  >
                    <span className="flex size-7 shrink-0 items-center justify-center rounded-full border border-slate-200 bg-white text-xs font-semibold text-slate-700">
                      {index + 1}
                    </span>
                    <div className="min-w-0">
                      <p className="flex items-center gap-1.5 text-sm font-semibold text-slate-900">
                        <StepIcon className="size-3.5 shrink-0 text-[#F26F21]" />
                        {step.title}
                      </p>
                      <p className="mt-0.5 text-xs leading-relaxed text-slate-500">{step.hint}</p>
                    </div>
                  </li>
                )
              })}
            </ol>
          </div>
        </div>
      </OverlayModal>

      {/* Cửa sổ hỗ trợ 2: OverlayDrawer Lịch sử thi thử & MockExamScorecard */}
      <OverlayDrawer
        open={isHistoryDrawerOpen}
        onClose={() => setIsHistoryDrawerOpen(false)}
        badge="Nhật ký & Bảng điểm"
        title={`Lịch sử thi thử tính giờ (${historyTotal || historyItems.length})`}
        subtitle="Tra cứu các ca thi thử đã thực hiện và bấm vào từng ca để xem bảng điểm chi tiết"
        width="2xl"
      >
        <div className="space-y-4">
          <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-slate-200 bg-slate-50 p-3">
            <div className="inline-flex rounded-xl border border-slate-200 bg-white p-1">
              <button
                type="button"
                onClick={() => {
                  setHistoryScope('course')
                  setHistoryPage(1)
                  setExpandedHistoryId(null)
                }}
                className={cn(
                  'rounded-lg px-3 py-1.5 text-xs font-semibold transition',
                  historyScope === 'course'
                    ? 'bg-orange-50/60 text-[#C2410C]'
                    : 'text-slate-600 hover:text-slate-900',
                )}
              >
                Môn vừa chọn
              </button>
              <button
                type="button"
                onClick={() => {
                  setHistoryScope('all')
                  setHistoryPage(1)
                  setExpandedHistoryId(null)
                }}
                className={cn(
                  'rounded-lg px-3 py-1.5 text-xs font-semibold transition',
                  historyScope === 'all'
                    ? 'bg-orange-50/60 text-[#C2410C]'
                    : 'text-slate-600 hover:text-slate-900',
                )}
              >
                Tất cả môn
              </button>
            </div>
            <span className="rounded-full border border-slate-200 bg-white px-3 py-1 text-xs font-medium text-slate-700">
              {historyTotal} lượt
            </span>
          </div>

          {historyError && (
            <div className="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 text-xs font-medium text-slate-700">
              {historyError}
            </div>
          )}

          {isLoadingHistory ? (
            <div className="flex items-center gap-2 py-8 text-xs font-medium text-slate-600">
              <LoaderCircle className="size-4 animate-spin text-[#F26F21]" />
              Đang tải lịch sử…
            </div>
          ) : historyItems.length === 0 ? (
            <p className="rounded-xl border border-slate-200 bg-slate-50 py-8 text-center text-sm text-slate-500">
              {historyScope === 'course' && !selectedCourseId
                ? 'Hãy chọn một môn ở màn hình chính để lọc lịch sử môn đó.'
                : 'Chưa có ca thi thử nào.'}
            </p>
          ) : (
            <div className="divide-y divide-slate-200 overflow-hidden rounded-xl border border-slate-200 bg-white">
              {historyItems.map((item) => (
                <article key={item.sessionId} className="space-y-3 p-4">
                  <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-center">
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="text-xs font-semibold text-slate-900">
                          #{item.sessionId.slice(0, 8).toUpperCase()}
                        </span>
                        <span className="rounded-md border border-slate-200 bg-slate-100 px-2 py-0.5 text-xs font-medium text-slate-700">
                          {statusLabel(item.status)}
                        </span>
                        <span className="text-xs text-slate-500">
                          {formatExamDate(item.startedAt)} · {item.durationMinutes} phút
                        </span>
                      </div>
                      <h3 className="mt-1 text-sm font-semibold text-slate-900">
                        {item.courseCode} — {item.courseName}
                      </h3>
                      <p className="mt-0.5 text-xs text-slate-500">
                        Đã trả lời {item.answeredQuestions}/{item.totalQuestions} câu hỏi
                      </p>
                    </div>

                    <div className="flex items-center justify-between gap-3 sm:justify-end">
                      <span className="shrink-0 rounded-full border border-slate-200 bg-slate-100 px-2.5 py-1 text-xs font-semibold text-slate-800">
                        {item.totalScore === null ? '—' : `${item.totalScore.toFixed(2)} / 10`}
                      </span>
                      {renderHistoryAction(item)}
                    </div>
                  </div>

                  {expandedHistoryId === item.sessionId && (
                    <div className="mt-3 rounded-xl border border-slate-200 bg-slate-50 p-3.5">
                      {loadingDetailId === item.sessionId && (
                        <div className="flex items-center gap-2 text-xs font-medium text-slate-600">
                          <LoaderCircle className="size-4 animate-spin text-[#F26F21]" />
                          Đang mở bảng điểm…
                        </div>
                      )}
                      {detailError && (
                        <p className="text-xs font-medium text-slate-700">{detailError}</p>
                      )}
                      {selectedDetail && <MockExamScorecard scorecard={selectedDetail} />}
                    </div>
                  )}
                </article>
              ))}
            </div>
          )}

          {historyTotalPages > 1 && (
            <div className="flex items-center justify-between pt-2">
              <button
                type="button"
                disabled={historyPage <= 1}
                onClick={() => {
                  setHistoryPage((current) => Math.max(1, current - 1))
                  setExpandedHistoryId(null)
                }}
                className="inline-flex min-h-9 items-center gap-1 rounded-lg px-2.5 text-xs font-semibold text-[#C2410C] hover:bg-orange-50/60 disabled:opacity-40"
              >
                <ChevronLeft className="size-4" /> Trước
              </button>
              <span className="text-xs font-medium text-slate-600">
                Trang {historyPage}/{historyTotalPages}
              </span>
              <button
                type="button"
                disabled={historyPage >= historyTotalPages}
                onClick={() => {
                  setHistoryPage((current) => current + 1)
                  setExpandedHistoryId(null)
                }}
                className="inline-flex min-h-9 items-center gap-1 rounded-lg px-2.5 text-xs font-semibold text-[#C2410C] hover:bg-orange-50/60 disabled:opacity-40"
              >
                Sau <ChevronDown className="size-4 -rotate-90" />
              </button>
            </div>
          )}
        </div>
      </OverlayDrawer>

      {/* Cửa sổ thông báo hết lượt thi thử */}
      <OverlayModal
        open={Boolean(quotaModal)}
        onClose={() => setQuotaModal('')}
        badge="Thông báo lượt thi"
        title="Hết lượt thi thử hôm nay"
        size="sm"
        footer={
          <div className="flex justify-end">
            <button
              type="button"
              onClick={() => setQuotaModal('')}
              className="inline-flex min-h-9 items-center justify-center rounded-xl bg-[#F26F21] px-4 text-xs font-semibold text-white transition hover:bg-[#D95B16]"
            >
              Đã hiểu
            </button>
          </div>
        }
      >
        <div className="flex items-start gap-3">
          <div className="flex size-10 shrink-0 items-center justify-center rounded-xl border border-slate-200 bg-slate-100 text-slate-700">
            <AlertTriangle className="size-5" />
          </div>
          <p className="text-sm leading-relaxed text-slate-600">{quotaModal}</p>
        </div>
      </OverlayModal>

      <div className="sr-only" aria-live="polite">
        {deviceStatus.title}. {deviceNames || deviceStatus.helper}
      </div>
    </div>
  )
}
