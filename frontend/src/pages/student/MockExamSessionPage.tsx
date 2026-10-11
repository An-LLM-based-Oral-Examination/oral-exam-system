import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import {
  ArrowLeft,
  ArrowRight,
  Camera,
  CheckCircle2,
  Clock3,
  LoaderCircle,
  LockKeyhole,
  Mic2,
  MicOff,
  Volume2,
} from 'lucide-react'
import { cn } from '@/lib/utils'

type AnswerStage = 'preparing' | 'recording' | 'review'
type MediaStatus = 'connecting' | 'ready' | 'error'

interface StoredMockExamConfig {
  sessionId: string
  subjectId: string
  durationMinutes: number
  questionCount: number
  deviceReady: boolean
  startedAt: string
}

interface MockAnswer {
  questionIndex: number
  question: string
  transcript: string
  durationSeconds: number
  hasLocalRecording: boolean
}

interface SessionSubject {
  name: string
  track: string
  questions: string[]
}

const SESSION_SUBJECTS: Record<string, SessionSubject> = {
  AI3001: {
    name: 'Trí tuệ Nhân tạo & Học máy',
    track: 'AI3001 · Học máy nâng cao & Mạng nơ-ron',
    questions: [
      'Hãy phân biệt supervised learning và unsupervised learning, đồng thời nêu một tình huống phù hợp cho mỗi phương pháp.',
      'Overfitting là gì? Hãy trình bày ít nhất ba kỹ thuật giúp cải thiện khả năng khái quát hóa của mô hình.',
      'Giải thích cơ chế backpropagation trong mạng nơ-ron và vai trò của learning rate.',
      'Bạn sẽ chọn những chỉ số nào để đánh giá một mô hình phân loại mất cân bằng dữ liệu? Vì sao?',
    ],
  },
  SWE302: {
    name: 'Kỹ nghệ Phần mềm',
    track: 'SWE302 · Kiến trúc Microservices & CI/CD',
    questions: [
      'Hãy so sánh kiến trúc monolith và microservices trong bối cảnh một sản phẩm tăng trưởng nhanh.',
      'Trình bày cách Circuit Breaker giúp hạn chế cascading failure giữa các dịch vụ.',
      'Một pipeline CI/CD an toàn nên có những cổng kiểm soát chất lượng nào?',
      'Hãy giải thích khi nào nên dùng giao tiếp bất đồng bộ thay vì REST đồng bộ.',
    ],
  },
  DBI202: {
    name: 'Cơ sở dữ liệu',
    track: 'DBI202 · Tối ưu truy vấn & Transaction',
    questions: [
      'Hãy giải thích cách chỉ mục B-Tree cải thiện truy vấn và trường hợp chỉ mục có thể gây bất lợi.',
      'Phân biệt bốn isolation level và hiện tượng đọc bất thường mà mỗi mức ngăn chặn.',
      'Bạn sẽ phân tích và tối ưu một truy vấn SQL chậm theo quy trình nào?',
    ],
  },
}

function formatTime(totalSeconds: number) {
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`
}

function parseBoundedNumber(
  value: string | null,
  fallback: number,
  minimum: number,
  maximum: number,
) {
  if (value === null || value.trim() === '') return fallback
  const parsed = Number(value)
  if (!Number.isFinite(parsed)) return fallback
  return Math.min(maximum, Math.max(minimum, parsed))
}

function readStoredConfig(): StoredMockExamConfig | null {
  try {
    const value = window.sessionStorage.getItem('mock_exam_session_config')
    if (!value) return null
    const parsed = JSON.parse(value) as Partial<StoredMockExamConfig>
    if (
      typeof parsed.sessionId !== 'string' ||
      typeof parsed.subjectId !== 'string' ||
      typeof parsed.durationMinutes !== 'number' ||
      typeof parsed.questionCount !== 'number' ||
      parsed.deviceReady !== true ||
      typeof parsed.startedAt !== 'string'
    ) {
      return null
    }
    return parsed as StoredMockExamConfig
  } catch {
    return null
  }
}

export function MockExamSessionPage() {
  const [searchParams] = useSearchParams()
  return <MockExamSession key={searchParams.toString()} searchParams={searchParams} />
}

function MockExamSession({ searchParams }: { searchParams: URLSearchParams }) {
  const navigate = useNavigate()
  const subjectId = searchParams.get('subject') ?? 'AI3001'
  const durationMinutes = parseBoundedNumber(searchParams.get('duration'), 20, 5, 60)
  const configuredQuestionCount = Math.floor(
    parseBoundedNumber(searchParams.get('questions'), 4, 1, 10),
  )
  const subject = SESSION_SUBJECTS[subjectId] ?? SESSION_SUBJECTS.AI3001
  const questionCount = Math.min(configuredQuestionCount, subject.questions.length)
  const [storedConfig] = useState(readStoredConfig)
  const isValidSession =
    storedConfig?.deviceReady === true &&
    storedConfig.subjectId === subjectId &&
    storedConfig.durationMinutes === durationMinutes &&
    storedConfig.questionCount === configuredQuestionCount
  const [deadline, setDeadline] = useState<number | null>(null)
  const [secondsLeft, setSecondsLeft] = useState(durationMinutes * 60)
  const [questionIndex, setQuestionIndex] = useState(0)
  const [stage, setStage] = useState<AnswerStage>('preparing')
  const [preparationSeconds, setPreparationSeconds] = useState(45)
  const [recordingSeconds, setRecordingSeconds] = useState(0)
  const [transcript, setTranscript] = useState('')
  const [answers, setAnswers] = useState<MockAnswer[]>([])
  const [completed, setCompleted] = useState(false)
  const [mediaStatus, setMediaStatus] = useState<MediaStatus>('connecting')
  const [recordingUrl, setRecordingUrl] = useState<string | null>(null)
  const [violations, setViolations] = useState(0)
  const hasExpiredRef = useRef(false)
  const streamRef = useRef<MediaStream | null>(null)
  const mediaRecorderRef = useRef<MediaRecorder | null>(null)
  const recordingChunksRef = useRef<Blob[]>([])
  const videoRef = useRef<HTMLVideoElement>(null)
  const preparationDeadlineRef = useRef(0)
  const recordingStartedAtRef = useRef(0)

  const currentQuestion = subject.questions[questionIndex]
  const progress = ((questionIndex + (stage === 'review' ? 1 : 0)) / questionCount) * 100
  const isUrgent = secondsLeft <= 60

  useEffect(() => {
    if (!isValidSession) navigate('/mock-exam', { replace: true })
  }, [isValidSession, navigate])

  useEffect(() => {
    if (!isValidSession) return
    let cancelled = false
    let videoElement: HTMLVideoElement | null = null

    const connectDevices = async () => {
      if (!navigator.mediaDevices?.getUserMedia || !('MediaRecorder' in window)) {
        setMediaStatus('error')
        return
      }

      try {
        const stream = await navigator.mediaDevices.getUserMedia({
          audio: { echoCancellation: true, noiseSuppression: true },
          video: { facingMode: 'user', width: { ideal: 640 }, height: { ideal: 360 } },
        })
        if (stream.getAudioTracks().length === 0 || stream.getVideoTracks().length === 0) {
          stream.getTracks().forEach((track) => track.stop())
          throw new Error('Required media tracks are unavailable.')
        }
        if (cancelled) {
          stream.getTracks().forEach((track) => track.stop())
          return
        }
        streamRef.current = stream
        videoElement = videoRef.current
        if (videoElement) videoElement.srcObject = stream
        preparationDeadlineRef.current = Date.now() + 45_000
        setPreparationSeconds(45)
        setMediaStatus('ready')
        setDeadline((current) => current ?? Date.now() + durationMinutes * 60_000)
      } catch {
        setMediaStatus('error')
      }
    }

    void connectDevices()
    return () => {
      cancelled = true
      if (mediaRecorderRef.current?.state === 'recording') {
        mediaRecorderRef.current.stop()
      }
      streamRef.current?.getTracks().forEach((track) => track.stop())
      streamRef.current = null
      if (videoElement) videoElement.srcObject = null
    }
  }, [durationMinutes, isValidSession])

  useEffect(() => {
    const handleVisibilityChange = () => {
      if (document.visibilityState === 'hidden' && !completed) {
        setViolations((current) => current + 1)
      }
    }
    document.addEventListener('visibilitychange', handleVisibilityChange)
    return () => document.removeEventListener('visibilitychange', handleVisibilityChange)
  }, [completed])

  useEffect(() => {
    return () => {
      if (recordingUrl) URL.revokeObjectURL(recordingUrl)
    }
  }, [recordingUrl])

  useEffect(() => {
    if (completed || !isValidSession || deadline === null) return

    const syncTimer = () => {
      const nextSeconds = Math.max(0, Math.ceil((deadline - Date.now()) / 1000))
      setSecondsLeft(nextSeconds)
      if (nextSeconds === 0 && !hasExpiredRef.current) {
        hasExpiredRef.current = true
        setCompleted(true)
      }
    }

    syncTimer()
    const interval = window.setInterval(syncTimer, 1_000)
    return () => window.clearInterval(interval)
  }, [completed, deadline, isValidSession])

  useEffect(() => {
    if (stage !== 'preparing' || completed || mediaStatus !== 'ready') return

    const syncPreparation = () => {
      const nextSeconds = Math.max(
        0,
        Math.ceil((preparationDeadlineRef.current - Date.now()) / 1000),
      )
      setPreparationSeconds(nextSeconds)
      if (nextSeconds === 0 && mediaStatus === 'ready') setStage('recording')
    }

    syncPreparation()
    const interval = window.setInterval(syncPreparation, 250)
    return () => window.clearInterval(interval)
  }, [completed, mediaStatus, stage])

  useEffect(() => {
    if (stage !== 'recording' || completed) return

    recordingStartedAtRef.current = Date.now()
    const syncRecording = () => {
      const elapsed = Math.min(
        180,
        Math.max(0, Math.floor((Date.now() - recordingStartedAtRef.current) / 1000)),
      )
      setRecordingSeconds(elapsed)
      if (elapsed >= 180) setStage('review')
    }

    syncRecording()
    const interval = window.setInterval(syncRecording, 500)
    return () => window.clearInterval(interval)
  }, [completed, stage])

  useEffect(() => {
    if (stage !== 'recording' || mediaStatus !== 'ready') return
    const stream = streamRef.current
    const audioTracks = stream?.getAudioTracks() ?? []
    if (audioTracks.length === 0) {
      setMediaStatus('error')
      setStage('review')
      return
    }

    const audioStream = new MediaStream(audioTracks)
    const recorder = new MediaRecorder(audioStream)
    recordingChunksRef.current = []
    mediaRecorderRef.current = recorder
    recorder.ondataavailable = (event) => {
      if (event.data.size > 0) recordingChunksRef.current.push(event.data)
    }
    recorder.onstop = () => {
      if (recordingChunksRef.current.length === 0) return
      const blob = new Blob(recordingChunksRef.current, {
        type: recorder.mimeType || 'audio/webm',
      })
      setRecordingUrl(URL.createObjectURL(blob))
    }
    recorder.start(500)

    return () => {
      if (recorder.state !== 'inactive') recorder.stop()
      if (mediaRecorderRef.current === recorder) mediaRecorderRef.current = null
    }
  }, [mediaStatus, stage])

  useEffect(() => {
    return () => {
      if ('speechSynthesis' in window) window.speechSynthesis.cancel()
    }
  }, [])

  const stageLabel = useMemo(() => {
    if (stage === 'preparing') return `Chuẩn bị · ${preparationSeconds}s`
    if (stage === 'recording') return `Đang trả lời · ${formatTime(recordingSeconds)}`
    return 'Rà soát câu trả lời'
  }, [preparationSeconds, recordingSeconds, stage])

  const handleReadQuestion = () => {
    if (!('speechSynthesis' in window)) return
    const utterance = new SpeechSynthesisUtterance(currentQuestion)
    utterance.lang = 'vi-VN'
    utterance.rate = 0.94
    window.speechSynthesis.cancel()
    window.speechSynthesis.speak(utterance)
  }

  const handleNextQuestion = () => {
    const answer: MockAnswer = {
      questionIndex,
      question: currentQuestion,
      transcript: transcript.trim(),
      durationSeconds: recordingSeconds,
      hasLocalRecording: Boolean(recordingUrl) || recordingChunksRef.current.length > 0,
    }
    const nextAnswers = [...answers, answer]
    setAnswers(nextAnswers)

    try {
      window.sessionStorage.setItem(
        'mock_exam_attempt_draft',
        JSON.stringify({
          sessionId: storedConfig?.sessionId,
          subjectId,
          updatedAt: new Date().toISOString(),
          answers: nextAnswers,
        }),
      )
    } catch {
      // In-memory state still preserves the answers for the current page lifecycle.
    }

    if (questionIndex + 1 >= questionCount) {
      setCompleted(true)
      return
    }
    setQuestionIndex((current) => current + 1)
    setStage('preparing')
    setPreparationSeconds(45)
    preparationDeadlineRef.current = Date.now() + 45_000
    setRecordingSeconds(0)
    setTranscript('')
    setRecordingUrl(null)
    recordingChunksRef.current = []
  }

  if (!isValidSession) {
    return (
      <div className="flex min-h-[320px] items-center justify-center rounded-[22px] border border-[#dce5f1] bg-white p-6 text-center shadow-[0_14px_40px_rgba(31,61,105,0.07)]">
        <div>
          <LoaderCircle className="mx-auto size-7 animate-spin text-[#5865d8]" />
          <p className="mt-4 text-sm font-bold text-[#26364d]">
            Đang quay lại bước kiểm tra thiết bị…
          </p>
        </div>
      </div>
    )
  }

  if (mediaStatus === 'error') {
    return (
      <section className="mx-auto max-w-xl rounded-[22px] border border-[#efcbd1] bg-white p-6 text-center shadow-[0_14px_40px_rgba(31,61,105,0.07)]">
        <div className="mx-auto flex size-14 items-center justify-center rounded-2xl bg-[#fff0f2] text-[#d04b61]">
          <Camera className="size-7" />
        </div>
        <h1 className="mt-4 text-lg font-bold text-[#26364d]">Không thể mở micro hoặc camera</h1>
        <p className="mx-auto mt-2 max-w-md text-[11px] leading-relaxed text-[#6d7b8f]">
          Phiên chưa bắt đầu và đồng hồ chưa chạy. Hãy cấp lại quyền thiết bị rồi thực hiện kiểm tra
          preflight.
        </p>
        <button
          type="button"
          onClick={() => navigate('/mock-exam', { replace: true })}
          className="mt-5 inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[#245eea] px-5 text-[10px] font-bold text-white transition hover:bg-[#194fce] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#245eea]"
        >
          <ArrowLeft className="size-4" /> Quay lại kiểm tra thiết bị
        </button>
      </section>
    )
  }

  if (completed) {
    return (
      <section className="mx-auto max-w-2xl overflow-hidden rounded-[22px] border border-[#dce5f1] bg-white text-center shadow-[0_18px_48px_rgba(31,61,105,0.1)]">
        <div className="bg-[linear-gradient(135deg,#eef2ff,#f4fbff)] px-6 py-10">
          <div className="mx-auto flex size-16 items-center justify-center rounded-2xl bg-white text-[#245eea] shadow-[0_12px_28px_rgba(36,94,234,0.14)]">
            <CheckCircle2 className="size-8" />
          </div>
          <p className="mt-5 text-[9px] font-bold tracking-[0.1em] text-[#5b68d8] uppercase">
            Phiên thi thử đã kết thúc
          </p>
          <h1 className="mt-2 text-2xl font-bold tracking-[-0.04em] text-[#14233d]">
            Bài làm đã được lưu an toàn
          </h1>
          <p className="mx-auto mt-2 max-w-lg text-[11px] leading-relaxed text-[#66768c]">
            {answers.length} câu trả lời đã được lưu tạm trên trình duyệt. Hệ thống sẵn sàng gửi dữ
            liệu sang dịch vụ chấm AI khi API được kết nối.
          </p>
        </div>
        <div className="flex flex-col justify-center gap-3 p-5 sm:flex-row">
          <button
            type="button"
            onClick={() => navigate('/mock-exam')}
            className="inline-flex min-h-11 items-center justify-center rounded-xl border border-[#dce4ef] px-5 text-[10px] font-bold text-[#52627a] transition hover:bg-[#f4f7fb] focus-visible:outline-2 focus-visible:outline-[#245eea]"
          >
            Về trang thi thử
          </button>
          <button
            type="button"
            onClick={() => navigate('/results?source=mock-exam')}
            className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[#245eea] px-5 text-[10px] font-bold text-white shadow-[0_10px_24px_rgba(36,94,234,0.22)] transition hover:bg-[#194fce] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#245eea]"
          >
            Xem trang kết quả <ArrowRight className="size-4" />
          </button>
        </div>
      </section>
    )
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <Link
          to="/mock-exam"
          className="inline-flex min-h-9 w-fit items-center gap-1.5 rounded-lg px-2 text-[10px] font-semibold text-[#425fc3] transition hover:bg-[#eaf0ff] focus-visible:outline-2 focus-visible:outline-[#5865d8]"
        >
          <ArrowLeft className="size-4" /> Quay lại phòng chờ
        </Link>
        <div
          role="timer"
          aria-live="off"
          aria-label={`Thời gian còn lại ${formatTime(secondsLeft)}`}
          className={cn(
            'inline-flex min-h-9 items-center gap-2 rounded-xl border px-3 font-mono text-[12px] font-bold',
            isUrgent
              ? 'animate-pulse border-[#f0bec7] bg-[#fff2f4] text-[#c64359] motion-reduce:animate-none'
              : 'border-[#dce4ef] bg-white text-[#263851]',
          )}
        >
          <Clock3 className="size-4" />
          <span className="font-sans text-[9px] font-medium text-[#77869a]">Còn lại</span>
          {formatTime(secondsLeft)}
        </div>
      </div>

      <section className="overflow-hidden rounded-[22px] border border-[#dfe6f0] bg-white shadow-[0_12px_38px_rgba(31,61,105,0.065)]">
        <div className="border-b border-[#e9eef5] bg-[linear-gradient(135deg,#0a1d3c,#17386d)] px-5 py-5 text-white sm:px-6">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
            <div>
              <p className="text-[9px] font-bold tracking-[0.09em] text-[#83e8f2] uppercase">
                Phiên thi thử đang diễn ra · Voice-First
              </p>
              <h1 className="mt-2 text-lg font-bold tracking-[-0.025em] sm:text-xl">
                {subject.name}
              </h1>
              <p className="mt-1 text-[9px] text-[#b8cae2]">{subject.track}</p>
            </div>
            <div className="flex items-center gap-2.5 rounded-xl border border-white/10 bg-white/8 p-2 pr-3 text-[#d9e8fa] backdrop-blur-sm">
              <div className="relative h-9 w-12 overflow-hidden rounded-lg border border-white/12 bg-[#08162f]">
                <video
                  ref={videoRef}
                  autoPlay
                  muted
                  playsInline
                  className="size-full object-cover opacity-80"
                  aria-label="Xem trước camera của bạn"
                />
                <span className="absolute top-1 right-1 size-1.5 animate-pulse rounded-full bg-[#37d29c] ring-2 ring-[#0a2248] motion-reduce:animate-none" />
              </div>
              <div>
                <p className="text-[9px] font-semibold">Camera preview đang hoạt động</p>
                <p className="mt-0.5 text-[8px] text-[#9fb8d7]">
                  Proctor mô phỏng · Rời tab {violations}/3 lần
                </p>
              </div>
            </div>
          </div>
          <div className="mt-5 h-1.5 overflow-hidden rounded-full bg-white/10">
            <div
              className="h-full rounded-full bg-[linear-gradient(90deg,#4fcddd,#6e80ff)] transition-[width] duration-500"
              style={{ width: `${progress}%` }}
            />
          </div>
        </div>

        <div className="space-y-5 p-5 sm:p-6">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <span className="rounded-full bg-[#eef2ff] px-3 py-1 text-[9px] font-bold text-[#5361ca]">
              Câu hỏi {questionIndex + 1} / {questionCount}
            </span>
            <span
              className={cn(
                'inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-[9px] font-bold',
                stage === 'recording'
                  ? 'bg-[#fff0f2] text-[#c64a5f]'
                  : stage === 'review'
                    ? 'bg-[#edfaf6] text-[#16836a]'
                    : 'bg-[#fff8e9] text-[#9a6a17]',
              )}
            >
              <span
                className={cn(
                  'size-1.5 rounded-full',
                  stage === 'recording' && 'animate-pulse bg-[#e85c70] motion-reduce:animate-none',
                  stage === 'review' && 'bg-[#2fc092]',
                  stage === 'preparing' && 'bg-[#dfa33c]',
                )}
              />
              {stageLabel}
            </span>
          </div>

          <div className="rounded-2xl border border-[#e1e7f0] bg-[#f8faff] p-5 sm:p-6">
            <p className="text-[9px] font-bold tracking-[0.08em] text-[#738299] uppercase">
              Câu hỏi của giám khảo AI
            </p>
            <h2 className="mt-3 text-[16px] leading-relaxed font-bold text-[#1c2c44] sm:text-lg">
              {currentQuestion}
            </h2>
            <button
              type="button"
              onClick={handleReadQuestion}
              className="mt-4 inline-flex min-h-9 items-center gap-2 rounded-lg border border-[#dce4ef] bg-white px-3 text-[9px] font-semibold text-[#52637a] transition hover:border-[#bdcce0] hover:text-[#405fc6] focus-visible:outline-2 focus-visible:outline-[#5865d8]"
            >
              <Volume2 className="size-4 text-[#5365d5]" /> Nghe AI đọc câu hỏi
            </button>
          </div>

          <div className="rounded-2xl border border-[#e1e7f0] bg-white p-4 sm:p-5">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <div className="flex items-center gap-3">
                <div
                  className={cn(
                    'flex size-10 items-center justify-center rounded-xl',
                    stage === 'recording'
                      ? 'bg-[#fff0f2] text-[#d34b62]'
                      : 'bg-[#eef2ff] text-[#5362d1]',
                  )}
                >
                  {stage === 'recording' ? (
                    <Mic2 className="size-5 animate-pulse motion-reduce:animate-none" />
                  ) : (
                    <MicOff className="size-5" />
                  )}
                </div>
                <div>
                  <p className="text-[10px] font-bold text-[#2b3b52]">
                    {stage === 'preparing'
                      ? 'Micro đang chờ'
                      : stage === 'recording'
                        ? 'Đang ghi nhận phần trình bày'
                        : 'Đã hoàn thành phần nói'}
                  </p>
                  <p className="mt-0.5 text-[9px] text-[#7c899b]">
                    Camera preview và bộ đếm rời tab đang hoạt động trong toàn bộ phiên.
                  </p>
                </div>
              </div>

              {stage === 'preparing' && (
                <button
                  type="button"
                  onClick={() => setStage('recording')}
                  disabled={mediaStatus !== 'ready'}
                  className="inline-flex min-h-10 items-center justify-center gap-2 rounded-xl bg-[#245eea] px-4 text-[10px] font-bold text-white shadow-[0_9px_22px_rgba(36,94,234,0.2)] transition hover:bg-[#194fce] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#245eea] disabled:pointer-events-none disabled:opacity-50"
                >
                  <Mic2 className="size-4" /> Bắt đầu trả lời
                </button>
              )}
              {stage === 'recording' && (
                <button
                  type="button"
                  onClick={() => setStage('review')}
                  className="inline-flex min-h-10 items-center justify-center gap-2 rounded-xl bg-[#d94d63] px-4 text-[10px] font-bold text-white shadow-[0_9px_22px_rgba(217,77,99,0.18)] transition hover:bg-[#c13e54] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#d94d63]"
                >
                  <MicOff className="size-4" /> Kết thúc phần nói
                </button>
              )}
            </div>

            <div className="relative mt-4">
              <label htmlFor="mock-transcript" className="sr-only">
                Transcript câu trả lời
              </label>
              <textarea
                id="mock-transcript"
                value={transcript}
                onChange={(event) => setTranscript(event.target.value)}
                disabled={stage !== 'review'}
                placeholder="Sau khi hoàn tất phần nói, hãy nhập hoặc chỉnh lại transcript tại đây…"
                className="min-h-36 w-full resize-y rounded-xl border border-[#dfe6ef] bg-[#f9fbfd] p-3 text-[11px] leading-relaxed text-[#34445b] transition focus:border-[#6070dc] focus:ring-3 focus:ring-[#6070dc]/10 focus:outline-none disabled:cursor-not-allowed disabled:text-[#8a97a9]"
              />
              {stage !== 'review' && (
                <div className="pointer-events-none absolute inset-0 flex items-center justify-center rounded-xl bg-white/38 backdrop-blur-[1px]">
                  <span className="inline-flex items-center gap-2 rounded-full border border-[#dfe6ef] bg-white px-3 py-2 text-[9px] font-semibold text-[#66768c] shadow-sm">
                    <LockKeyhole className="size-3.5 text-[#5b68d8]" />
                    Voice-First: hoàn tất phần nói để mở khóa rà soát
                  </span>
                </div>
              )}
            </div>

            {stage === 'review' && recordingUrl && (
              <div className="mt-3 flex flex-col gap-2 rounded-xl border border-[#dfe7ef] bg-[#f7fbfc] p-3 sm:flex-row sm:items-center sm:justify-between">
                <span className="text-[9px] font-semibold text-[#51667e]">
                  Bản ghi âm cục bộ · {formatTime(recordingSeconds)}
                </span>
                <audio controls src={recordingUrl} className="h-8 w-full sm:w-[280px]">
                  Trình duyệt không hỗ trợ phát bản ghi âm.
                </audio>
              </div>
            )}
          </div>

          <div className="flex flex-col-reverse gap-3 border-t border-[#edf1f6] pt-4 sm:flex-row sm:items-center sm:justify-between">
            <p className="flex items-center gap-1.5 text-[9px] text-[#718095]">
              <Camera className="size-3.5 text-[#5365d5]" /> Không chuyển tab trong khi thi thử
            </p>
            <button
              type="button"
              onClick={handleNextQuestion}
              disabled={stage !== 'review' || !recordingUrl}
              className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[#245eea] px-5 text-[10px] font-bold text-white shadow-[0_10px_24px_rgba(36,94,234,0.22)] transition hover:bg-[#194fce] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#245eea] disabled:pointer-events-none disabled:opacity-40"
            >
              {questionIndex + 1 >= questionCount ? 'Nộp bài thi thử' : 'Lưu câu & tiếp tục'}
              <ArrowRight className="size-4" />
            </button>
          </div>
        </div>
      </section>

      <div className="sr-only" aria-live="polite">
        {stage === 'preparing'
          ? 'Đang trong thời gian chuẩn bị.'
          : stage === 'recording'
            ? 'Đã bắt đầu ghi âm câu trả lời.'
            : 'Đã mở khóa bước rà soát câu trả lời.'}
      </div>
    </div>
  )
}
