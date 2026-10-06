import { lazy, Suspense, useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  ArrowRight,
  AudioLines,
  BookOpenText,
  Check,
  ChevronDown,
  ChevronRight,
  Clock3,
  Headphones,
  History,
  Lightbulb,
  LoaderCircle,
  Mic2,
  Radio,
  RefreshCw,
  Settings2,
  ShieldCheck,
  Sparkles,
  Volume2,
} from 'lucide-react'
import { cn } from '@/lib/utils'

const PracticeHeroThreeScene = lazy(() =>
  import('./PracticeHeroThreeScene').then((module) => ({
    default: module.PracticeHeroThreeScene,
  })),
)

type FeedbackMode = 'PER_QUESTION' | 'FULL_SESSION'
type MicrophoneState = 'idle' | 'checking' | 'ready' | 'error' | 'unsupported'

interface CourseOption {
  id: string
  code: string
  name: string
  module: string
}

interface PracticeHistoryItem {
  id: string
  course: string
  topic: string
  date: string
  mode: string
  score: number
  feedback: string
}

const COURSE_OPTIONS: CourseOption[] = [
  {
    id: 'INT3204',
    code: 'INT3204',
    name: 'Mạng máy tính',
    module: 'Module 3: Routing Protocols (OSPF, BGP)',
  },
  {
    id: 'DSA201',
    code: 'DSA201',
    name: 'Cấu trúc dữ liệu và giải thuật',
    module: 'Module 5: Cây AVL và B-Tree',
  },
  {
    id: 'OS202',
    code: 'OS202',
    name: 'Hệ điều hành',
    module: 'Module 4: Đồng bộ tiến trình',
  },
  {
    id: 'SWE301',
    code: 'SWE301',
    name: 'Thiết kế phần mềm',
    module: 'Module 2: Design Patterns',
  },
]

const PRACTICE_HISTORY: PracticeHistoryItem[] = [
  {
    id: 'history-network-01',
    course: 'Mạng máy tính',
    topic: 'Định tuyến OSPF và BGP',
    date: '20/05/2026',
    mode: 'Chấm từng câu',
    score: 8.8,
    feedback: 'Lập luận rõ. Cần phân biệt kỹ hơn vai trò của iBGP và eBGP.',
  },
  {
    id: 'history-dsa-01',
    course: 'Cấu trúc dữ liệu',
    topic: 'Cây AVL và phép quay cân bằng',
    date: '18/05/2026',
    mode: 'Cả bộ đề',
    score: 8,
    feedback: 'Nắm đúng nguyên lý cân bằng, ví dụ minh họa nên ngắn gọn hơn.',
  },
  {
    id: 'history-os-01',
    course: 'Hệ điều hành',
    topic: 'Đồng bộ tiến trình',
    date: '15/05/2026',
    mode: 'Chấm từng câu',
    score: 9.2,
    feedback: 'Giải thích tốt race condition, mutex và semaphore bằng tình huống thực tế.',
  },
]

const PRACTICE_TIPS = [
  'Hãy giữ tốc độ nói ổn định từ 110–130 từ/phút và trả lời theo cấu trúc: định nghĩa – cơ chế – ví dụ.',
  'Dành 5 giây đầu để sắp xếp ý, sau đó nói câu kết luận trước khi đi vào phần giải thích.',
  'Khi gặp thuật ngữ tiếng Anh, hãy phát âm rõ và nói thêm một câu giải thích ngắn bằng tiếng Việt.',
]

function getMicrophoneStatus(state: MicrophoneState) {
  switch (state) {
    case 'checking':
      return {
        label: 'Đang kiểm tra microphone…',
        helper: 'Trình duyệt có thể yêu cầu quyền sử dụng micro.',
        className: 'text-[#2563eb]',
        dotClassName: 'bg-[#3b82f6] animate-pulse motion-reduce:animate-none',
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

export function PracticePage() {
  const navigate = useNavigate()
  const [selectedCourseId, setSelectedCourseId] = useState(COURSE_OPTIONS[0].id)
  const [feedbackMode, setFeedbackMode] = useState<FeedbackMode>('PER_QUESTION')
  const [questionCount, setQuestionCount] = useState(5)
  const [microphoneState, setMicrophoneState] = useState<MicrophoneState>('idle')
  const [microphoneName, setMicrophoneName] = useState('')
  const [isVoicePreviewPlaying, setIsVoicePreviewPlaying] = useState(false)
  const [voicePreviewMessage, setVoicePreviewMessage] = useState('')
  const [expandedHistoryId, setExpandedHistoryId] = useState<string | null>(null)
  const [tipIndex, setTipIndex] = useState(0)

  const selectedCourse = useMemo(
    () => COURSE_OPTIONS.find((course) => course.id === selectedCourseId) ?? COURSE_OPTIONS[0],
    [selectedCourseId],
  )
  const estimatedMinutes = questionCount * 3
  const microphoneStatus = getMicrophoneStatus(microphoneState)

  useEffect(() => {
    return () => {
      if ('speechSynthesis' in window && isVoicePreviewPlaying) {
        window.speechSynthesis.cancel()
      }
    }
  }, [isVoicePreviewPlaying])

  const handleVoicePreview = () => {
    if (!('speechSynthesis' in window) || !('SpeechSynthesisUtterance' in window)) {
      setVoicePreviewMessage('Trình duyệt chưa hỗ trợ phát giọng nói.')
      return
    }

    if (isVoicePreviewPlaying) {
      window.speechSynthesis.cancel()
      setIsVoicePreviewPlaying(false)
      setVoicePreviewMessage('Đã dừng nghe thử giọng AI.')
      return
    }

    const utterance = new SpeechSynthesisUtterance(
      `Xin chào. Phiên luyện tập ${selectedCourse.name} đã sẵn sàng. Hãy trả lời rõ ràng và tự tin.`,
    )
    utterance.lang = 'vi-VN'
    utterance.rate = 0.94
    utterance.pitch = 1.02
    const vietnameseVoice = window.speechSynthesis
      .getVoices()
      .find((voice) => voice.lang.toLocaleLowerCase().startsWith('vi'))
    if (vietnameseVoice) utterance.voice = vietnameseVoice

    utterance.onstart = () => {
      setIsVoicePreviewPlaying(true)
      setVoicePreviewMessage('AI đang phát câu chào mẫu.')
    }
    utterance.onend = () => {
      setIsVoicePreviewPlaying(false)
      setVoicePreviewMessage('Đã phát xong câu chào mẫu.')
    }
    utterance.onerror = () => {
      setIsVoicePreviewPlaying(false)
      setVoicePreviewMessage('Không thể phát giọng mẫu trên thiết bị này.')
    }

    window.speechSynthesis.cancel()
    window.speechSynthesis.speak(utterance)
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

  const handleStartPractice = () => {
    const config = {
      courseId: selectedCourse.id,
      courseName: selectedCourse.name,
      courseCode: selectedCourse.code,
      module: selectedCourse.module,
      mode: feedbackMode,
      questionCount,
      microphoneName,
    }

    try {
      window.sessionStorage.setItem('practice_session_config', JSON.stringify(config))
    } catch {
      // Query parameters below still preserve the important session configuration.
    }

    const search = new URLSearchParams({
      course: selectedCourse.id,
      mode: feedbackMode,
      questions: String(questionCount),
    })
    navigate(`/practice/session?${search.toString()}`)
  }

  return (
    <div className="space-y-4 sm:space-y-5">
      <section className="relative isolate min-h-[224px] overflow-hidden rounded-[22px] border border-[#264b82] bg-[linear-gradient(120deg,#071a35_0%,#102c58_55%,#244785_100%)] shadow-[0_16px_42px_rgba(16,44,88,0.18)] sm:min-h-[210px]">
        <div className="pointer-events-none absolute inset-0 [background-image:linear-gradient(rgba(129,220,255,0.22)_1px,transparent_1px),linear-gradient(90deg,rgba(129,220,255,0.22)_1px,transparent_1px)] [background-size:30px_30px] opacity-[0.16]" />
        <div className="pointer-events-none absolute inset-y-0 right-0 w-[78%] bg-[radial-gradient(circle_at_72%_50%,rgba(74,218,246,0.2),transparent_42%)] sm:w-[62%]" />

        <div className="relative z-10 flex min-h-[224px] max-w-[720px] flex-col items-start justify-center px-5 py-6 sm:min-h-[210px] sm:px-7 lg:max-w-[62%] lg:px-8">
          <div className="inline-flex items-center gap-2 rounded-full border border-[#5be1f0]/20 bg-[#0f4580]/72 px-3 py-1 text-[9px] font-bold tracking-[0.08em] text-[#b9f7ff] uppercase backdrop-blur-sm">
            <Sparkles className="size-3" />
            Phòng luyện tập tương tác 1-on-1
          </div>
          <h1 className="mt-3 max-w-[620px] text-[clamp(1.55rem,3vw,2.2rem)] leading-[1.08] font-bold tracking-[-0.045em] text-white">
            Phòng Luyện tập Vấn đáp AI
          </h1>
          <p className="mt-2 max-w-[590px] text-[11px] leading-relaxed text-[#d2e5fa] sm:text-[12px]">
            Tự rèn luyện kỹ năng trả lời phỏng vấn chuyên môn với trợ lý giọng nói AI theo thời gian
            thực.
          </p>

          <button
            type="button"
            onClick={handleVoicePreview}
            className="mt-4 inline-flex min-h-9 items-center gap-2 rounded-xl border border-white/14 bg-white/9 px-3 text-[10px] font-semibold text-white backdrop-blur-md transition hover:-translate-y-0.5 hover:bg-white/15 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#68e4f4] motion-reduce:transform-none"
          >
            {isVoicePreviewPlaying ? (
              <AudioLines className="size-4 animate-pulse text-[#6ceaf5] motion-reduce:animate-none" />
            ) : (
              <Volume2 className="size-4 text-[#6ceaf5]" />
            )}
            {isVoicePreviewPlaying ? 'Dừng nghe thử' : 'Thử giọng AI'}
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

        <div className="pointer-events-none absolute top-5 right-5 z-10 hidden items-center gap-2 rounded-full border border-[#8be7f3]/15 bg-[#09254a]/58 px-3 py-1.5 text-[9px] font-semibold text-[#bceff7] shadow-lg backdrop-blur-md md:flex">
          <AudioLines className="size-3.5 text-[#55ddeb]" />
          Độ trễ phản hồi &lt; 320ms · Neural TTS
        </div>
      </section>

      <div className="grid items-start gap-4 lg:grid-cols-[minmax(0,1.7fr)_minmax(290px,0.78fr)]">
        <section className="overflow-hidden rounded-2xl border border-[#e0e7f0] bg-white shadow-[0_10px_32px_rgba(31,61,105,0.055)]">
          <div className="flex items-center justify-between gap-4 border-b border-[#edf1f6] px-5 py-4 sm:px-6">
            <div className="flex items-center gap-2.5">
              <div className="flex size-9 items-center justify-center rounded-xl bg-[#edf3ff] text-[#245eea]">
                <Settings2 className="size-4" />
              </div>
              <div>
                <h2 className="text-sm font-bold text-[#17243a] sm:text-[15px]">
                  Cấu hình phiên luyện tập
                </h2>
                <p className="mt-0.5 text-[9px] text-[#8491a4]">Thiết lập trước khi bắt đầu</p>
              </div>
            </div>
            <span className="rounded-full bg-[#f2f5fa] px-2.5 py-1 text-[9px] font-semibold text-[#63738a]">
              Bước 1 / 1
            </span>
          </div>

          <div className="space-y-5 p-5 sm:p-6">
            <div>
              <label
                htmlFor="practice-course"
                className="text-[10px] font-bold tracking-[0.02em] text-[#34445b]"
              >
                Học phần & chuyên đề vấn đáp
              </label>
              <div className="relative mt-2">
                <BookOpenText className="pointer-events-none absolute top-1/2 left-3.5 size-4 -translate-y-1/2 text-[#5271a2]" />
                <select
                  id="practice-course"
                  value={selectedCourseId}
                  onChange={(event) => setSelectedCourseId(event.target.value)}
                  className="min-h-12 w-full appearance-none rounded-xl border border-[#dfe6f0] bg-[#f8faff] py-2 pr-10 pl-10 text-[11px] font-semibold text-[#26364d] transition hover:border-[#bdcce0] focus:border-[#4c78e8] focus:ring-3 focus:ring-[#245eea]/10 focus:outline-none sm:text-[12px]"
                >
                  {COURSE_OPTIONS.map((course) => (
                    <option key={course.id} value={course.id}>
                      {course.name} ({course.code}) — {course.module}
                    </option>
                  ))}
                </select>
                <ChevronDown className="pointer-events-none absolute top-1/2 right-3.5 size-4 -translate-y-1/2 text-[#78879a]" />
              </div>
            </div>

            <fieldset>
              <legend className="text-[10px] font-bold tracking-[0.02em] text-[#34445b]">
                Chế độ phản hồi của AI
              </legend>
              <div className="mt-2 grid gap-3 sm:grid-cols-2">
                <label
                  className={cn(
                    'group relative cursor-pointer rounded-xl border p-4 transition duration-200 hover:-translate-y-0.5 hover:shadow-[0_8px_22px_rgba(31,61,105,0.07)] motion-reduce:transform-none',
                    feedbackMode === 'PER_QUESTION'
                      ? 'border-[#8db0ff] bg-[#f5f8ff] shadow-[0_0_0_2px_rgba(36,94,234,0.08)]'
                      : 'border-[#e2e8f1] bg-[#fafbfd]',
                  )}
                >
                  <input
                    type="radio"
                    name="feedback-mode"
                    value="PER_QUESTION"
                    checked={feedbackMode === 'PER_QUESTION'}
                    onChange={() => setFeedbackMode('PER_QUESTION')}
                    className="sr-only"
                  />
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex size-8 items-center justify-center rounded-lg bg-[#e9f0ff] text-[#245eea]">
                      <Radio className="size-4" />
                    </div>
                    <span
                      className={cn(
                        'flex size-5 items-center justify-center rounded-full border transition',
                        feedbackMode === 'PER_QUESTION'
                          ? 'border-[#245eea] bg-[#245eea] text-white'
                          : 'border-[#cad4e2] bg-white text-transparent',
                      )}
                    >
                      <Check className="size-3" />
                    </span>
                  </div>
                  <h3 className="mt-3 text-[12px] font-bold text-[#20314a]">Chấm từng câu</h3>
                  <p className="mt-1 text-[9px] leading-relaxed text-[#6f7e92] sm:text-[10px]">
                    Nhận điểm và phản hồi ngay sau mỗi câu để điều chỉnh cách trả lời.
                  </p>
                  <span className="mt-3 inline-flex items-center gap-1 text-[9px] font-semibold text-[#245eea]">
                    <Sparkles className="size-3" /> Khuyên dùng khi tự luyện
                  </span>
                </label>

                <label
                  className={cn(
                    'group relative cursor-pointer rounded-xl border p-4 transition duration-200 hover:-translate-y-0.5 hover:shadow-[0_8px_22px_rgba(31,61,105,0.07)] motion-reduce:transform-none',
                    feedbackMode === 'FULL_SESSION'
                      ? 'border-[#8db0ff] bg-[#f5f8ff] shadow-[0_0_0_2px_rgba(36,94,234,0.08)]'
                      : 'border-[#e2e8f1] bg-[#fafbfd]',
                  )}
                >
                  <input
                    type="radio"
                    name="feedback-mode"
                    value="FULL_SESSION"
                    checked={feedbackMode === 'FULL_SESSION'}
                    onChange={() => setFeedbackMode('FULL_SESSION')}
                    className="sr-only"
                  />
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex size-8 items-center justify-center rounded-lg bg-[#edf0f7] text-[#536780]">
                      <ShieldCheck className="size-4" />
                    </div>
                    <span
                      className={cn(
                        'flex size-5 items-center justify-center rounded-full border transition',
                        feedbackMode === 'FULL_SESSION'
                          ? 'border-[#245eea] bg-[#245eea] text-white'
                          : 'border-[#cad4e2] bg-white text-transparent',
                      )}
                    >
                      <Check className="size-3" />
                    </span>
                  </div>
                  <h3 className="mt-3 text-[12px] font-bold text-[#20314a]">Làm bộ câu hỏi</h3>
                  <p className="mt-1 text-[9px] leading-relaxed text-[#6f7e92] sm:text-[10px]">
                    Hoàn thành toàn bộ câu hỏi trước khi nhận điểm, phù hợp mô phỏng thi thật.
                  </p>
                  <span className="mt-3 inline-flex items-center gap-1 text-[9px] font-semibold text-[#68788e]">
                    <Clock3 className="size-3" /> Rèn luyện áp lực phòng thi
                  </span>
                </label>
              </div>
            </fieldset>

            <div className="rounded-xl border border-[#e1e8f2] bg-[#f8faff] p-4">
              <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                <div className="flex items-center gap-3">
                  <div className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-white text-[#3568dc] shadow-sm">
                    <Clock3 className="size-4" />
                  </div>
                  <div>
                    <p className="text-[9px] font-bold tracking-[0.06em] text-[#7a879a] uppercase">
                      Quy cách phiên thi
                    </p>
                    <p className="mt-0.5 text-[11px] font-bold text-[#24344c]">
                      Khoảng {estimatedMinutes} phút · {questionCount} câu hỏi ngẫu nhiên
                    </p>
                  </div>
                </div>

                <div
                  className="flex items-center gap-1 rounded-lg border border-[#e0e6ef] bg-white p-1"
                  role="radiogroup"
                  aria-label="Số câu hỏi"
                >
                  {[3, 5, 10].map((count) => (
                    <button
                      key={count}
                      type="button"
                      role="radio"
                      aria-checked={questionCount === count}
                      onClick={() => setQuestionCount(count)}
                      className={cn(
                        'min-h-7 rounded-md px-2.5 text-[9px] font-bold transition focus-visible:outline-2 focus-visible:outline-[#245eea]',
                        questionCount === count
                          ? 'bg-[#245eea] text-white shadow-sm'
                          : 'text-[#68778b] hover:bg-[#f1f5fb]',
                      )}
                    >
                      {count} câu
                    </button>
                  ))}
                </div>
              </div>
            </div>

            <div className="flex flex-col gap-3 rounded-xl border border-[#e1e8f2] bg-white p-3.5 sm:flex-row sm:items-center sm:justify-between">
              <div className="flex min-w-0 items-center gap-3">
                <div className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-[#ebf7f4] text-[#19866d]">
                  <Mic2 className="size-4" />
                </div>
                <div className="min-w-0">
                  <p
                    className={cn(
                      'flex items-center gap-1.5 text-[10px] font-bold',
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
                  <p className="mt-0.5 truncate text-[9px] text-[#7b899b]">
                    {microphoneName || microphoneStatus.helper}
                  </p>
                </div>
              </div>
              <button
                type="button"
                onClick={handleMicrophoneCheck}
                disabled={microphoneState === 'checking'}
                className="inline-flex min-h-9 shrink-0 items-center justify-center gap-1.5 rounded-lg px-3 text-[9px] font-bold text-[#245bd2] transition hover:bg-[#eef4ff] focus-visible:outline-2 focus-visible:outline-[#245eea] disabled:cursor-wait disabled:opacity-60"
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
              onClick={handleStartPractice}
              className="group inline-flex min-h-12 w-full items-center justify-center gap-2 rounded-xl bg-[#245eea] px-5 text-[11px] font-bold text-white shadow-[0_12px_28px_rgba(36,94,234,0.24)] transition duration-200 hover:-translate-y-0.5 hover:bg-[#194fce] hover:shadow-[0_16px_34px_rgba(36,94,234,0.3)] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#245eea] motion-reduce:transform-none"
            >
              <Mic2 className="size-4" />
              Bắt đầu luyện tập
              <ArrowRight className="size-4 transition-transform group-hover:translate-x-0.5 motion-reduce:transform-none" />
            </button>
          </div>
        </section>

        <aside className="space-y-4">
          <section className="overflow-hidden rounded-2xl border border-[#e0e7f0] bg-white shadow-[0_9px_28px_rgba(31,61,105,0.05)]">
            <div className="flex items-center justify-between border-b border-[#edf1f6] px-4 py-3.5">
              <div className="flex items-center gap-2">
                <History className="size-4 text-[#245eea]" />
                <h2 className="text-[12px] font-bold text-[#23334a]">Lịch sử luyện tập</h2>
              </div>
              <span className="text-[8px] font-semibold text-[#8995a6]">3 phiên gần nhất</span>
            </div>

            <div className="space-y-2.5 p-3">
              {PRACTICE_HISTORY.map((item) => {
                const isExpanded = expandedHistoryId === item.id

                return (
                  <article
                    key={item.id}
                    className={cn(
                      'rounded-xl border bg-[#f8faff] transition',
                      isExpanded ? 'border-[#b9cdf6] shadow-sm' : 'border-[#e8edf4]',
                    )}
                  >
                    <button
                      type="button"
                      onClick={() => setExpandedHistoryId(isExpanded ? null : item.id)}
                      className="w-full p-3 text-left focus-visible:rounded-xl focus-visible:outline-2 focus-visible:outline-[#245eea]"
                      aria-expanded={isExpanded}
                    >
                      <div className="flex items-start justify-between gap-3">
                        <div className="min-w-0">
                          <p className="truncate text-[10px] font-bold text-[#28384e]">
                            {item.course} — {item.topic}
                          </p>
                          <div className="mt-1.5 flex flex-wrap items-center gap-1.5 text-[8px] text-[#8190a3]">
                            <span className="rounded bg-white px-1.5 py-0.5 font-semibold text-[#60718a]">
                              {item.mode}
                            </span>
                            <span>{item.date}</span>
                          </div>
                        </div>
                        <strong className="shrink-0 text-[11px] text-[#159378]">
                          {item.score.toFixed(1)}/10
                        </strong>
                      </div>
                      <div className="mt-2 flex items-center justify-between text-[8px] font-semibold text-[#3162cc]">
                        <span>{isExpanded ? 'Thu gọn nhận xét' : 'Xem lại câu trả lời'}</span>
                        <ChevronRight
                          className={cn('size-3 transition-transform', isExpanded && 'rotate-90')}
                        />
                      </div>
                    </button>
                    {isExpanded && (
                      <div className="border-t border-[#e2e9f3] px-3 py-2.5 text-[9px] leading-relaxed text-[#64748a]">
                        {item.feedback}
                      </div>
                    )}
                  </article>
                )
              })}
            </div>
          </section>

          <section className="rounded-2xl border border-[#e3e8f0] bg-[linear-gradient(145deg,#ffffff_0%,#f4f7fb_100%)] p-4 shadow-[0_8px_26px_rgba(31,61,105,0.045)]">
            <div className="flex items-center gap-2 text-[#245eea]">
              <Lightbulb className="size-4" />
              <h2 className="text-[12px] font-bold text-[#26364c]">Mẹo đạt điểm cao</h2>
            </div>
            <p className="mt-3 min-h-14 text-[9px] leading-relaxed text-[#65758b] sm:text-[10px]">
              {PRACTICE_TIPS[tipIndex]}
            </p>
            <button
              type="button"
              onClick={() => setTipIndex((current) => (current + 1) % PRACTICE_TIPS.length)}
              className="mt-3 inline-flex min-h-8 items-center gap-1.5 rounded-lg px-2 text-[9px] font-bold text-[#245bd2] transition hover:bg-[#eaf1ff] focus-visible:outline-2 focus-visible:outline-[#245eea]"
            >
              Mẹo tiếp theo
              <ChevronRight className="size-3" />
            </button>
          </section>

          <div className="flex items-center gap-3 rounded-xl border border-[#dce8f2] bg-[#f5fbfc] p-3 text-[9px] leading-relaxed text-[#5f7187]">
            <Headphones className="size-4 shrink-0 text-[#2095a5]" />
            Nên sử dụng tai nghe và luyện tập ở nơi yên tĩnh để AI nhận diện giọng nói tốt hơn.
          </div>
        </aside>
      </div>

      <div className="sr-only" aria-live="polite">
        {voicePreviewMessage}
        {microphoneState === 'ready' && ` Microphone ${microphoneName} đã sẵn sàng.`}
      </div>
    </div>
  )
}

export { PracticePage as PracticeSetupPage }
