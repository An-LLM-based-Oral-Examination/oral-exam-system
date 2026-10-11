import { lazy, Suspense, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  AlertTriangle,
  ArrowRight,
  Bot,
  Camera,
  ChevronDown,
  Clock3,
  Eye,
  FileCheck2,
  History,
  LoaderCircle,
  Mic2,
  Play,
  RefreshCw,
  ShieldCheck,
  Sparkles,
  TimerReset,
  Trophy,
} from 'lucide-react'
import { cn } from '@/lib/utils'

const MockExamHeroThreeScene = lazy(() =>
  import('./MockExamHeroThreeScene').then((module) => ({
    default: module.MockExamHeroThreeScene,
  })),
)

type DeviceState = 'idle' | 'checking' | 'ready' | 'error' | 'unsupported'

interface MockExamSubject {
  id: string
  code: string
  name: string
  track: string
  durationMinutes: number
  questionCount: number
  quotaRemaining: number
  quotaLimit: number
}

interface MockHistoryItem {
  id: string
  examCode: string
  subject: string
  track: string
  date: string
  duration: string
  score: number
  summary: string
  strengths: string
}

const MOCK_EXAM_SUBJECTS: MockExamSubject[] = [
  {
    id: 'AI3001',
    code: 'AI3001',
    name: 'Trí tuệ Nhân tạo & Học máy',
    track: 'Học máy nâng cao & Mạng nơ-ron',
    durationMinutes: 20,
    questionCount: 4,
    quotaRemaining: 2,
    quotaLimit: 3,
  },
  {
    id: 'SWE302',
    code: 'SWE302',
    name: 'Kỹ nghệ Phần mềm',
    track: 'Kiến trúc Microservices & CI/CD',
    durationMinutes: 18,
    questionCount: 4,
    quotaRemaining: 1,
    quotaLimit: 3,
  },
  {
    id: 'DBI202',
    code: 'DBI202',
    name: 'Cơ sở dữ liệu',
    track: 'Tối ưu truy vấn & Transaction',
    durationMinutes: 15,
    questionCount: 3,
    quotaRemaining: 3,
    quotaLimit: 3,
  },
]

const MOCK_HISTORY: MockHistoryItem[] = [
  {
    id: 'mock-ai-01',
    examCode: 'MOCK-AI-01',
    subject: 'Trí tuệ Nhân tạo',
    track: 'Học máy nâng cao & Mạng nơ-ron',
    date: '19/05/2026',
    duration: '18 phút',
    score: 8.4,
    summary: 'Hoàn thành 4/4 câu. AI ghi nhận lập luận tốt ở phần supervised learning.',
    strengths: 'Phân biệt rõ overfitting, regularization và quy trình đánh giá mô hình.',
  },
  {
    id: 'mock-se-03',
    examCode: 'MOCK-SE-03',
    subject: 'Kỹ nghệ Phần mềm',
    track: 'Kiến trúc Microservices & CI/CD',
    date: '12/05/2026',
    duration: '19 phút',
    score: 7.9,
    summary: 'Hoàn thành 4/4 câu. Phần xử lý failure scenario còn thiếu ví dụ cụ thể.',
    strengths: 'Trình bày mạch lạc luồng CI/CD và nguyên tắc tách service.',
  },
  {
    id: 'mock-db-02',
    examCode: 'MOCK-DB-02',
    subject: 'Cơ sở dữ liệu',
    track: 'Tối ưu truy vấn & Transaction',
    date: '05/05/2026',
    duration: '14 phút',
    score: 8.7,
    summary: 'Hoàn thành 3/3 câu. Kiểm soát thời gian tốt và kết luận ngắn gọn.',
    strengths: 'Giải thích chính xác isolation level và chỉ mục tổng hợp.',
  },
]

const DEVICE_STATUS: Record<
  DeviceState,
  { title: string; helper: string; color: string; dot: string }
> = {
  idle: {
    title: 'Chưa kiểm tra micro & camera',
    helper: 'Kiểm tra trước để tránh gián đoạn khi đồng hồ bắt đầu.',
    color: 'text-[#5d6d83]',
    dot: 'bg-[#9aa9bc]',
  },
  checking: {
    title: 'AI Proctor đang kiểm tra thiết bị…',
    helper: 'Vui lòng cho phép trình duyệt sử dụng micro và camera.',
    color: 'text-[#385ed0]',
    dot: 'bg-[#5c73e7] animate-pulse motion-reduce:animate-none',
  },
  ready: {
    title: 'Micro & camera đã sẵn sàng',
    helper: 'Thiết bị đạt yêu cầu cho phiên thi thử.',
    color: 'text-[#16836a]',
    dot: 'bg-[#33c391] shadow-[0_0_8px_rgba(51,195,145,0.65)]',
  },
  error: {
    title: 'Không thể truy cập thiết bị',
    helper: 'Hãy cấp quyền micro và camera rồi kiểm tra lại.',
    color: 'text-[#cf4b61]',
    dot: 'bg-[#ed6075]',
  },
  unsupported: {
    title: 'Trình duyệt chưa hỗ trợ kiểm tra',
    helper: 'Bạn có thể thử lại bằng Chrome hoặc Edge phiên bản mới.',
    color: 'text-[#a26812]',
    dot: 'bg-[#e1a13f]',
  },
}

export const MockExamPage = () => {
  const navigate = useNavigate()
  const [selectedSubjectId, setSelectedSubjectId] = useState(MOCK_EXAM_SUBJECTS[0].id)
  const [deviceState, setDeviceState] = useState<DeviceState>('idle')
  const [deviceNames, setDeviceNames] = useState('')
  const [rulesExpanded, setRulesExpanded] = useState(true)
  const [expandedHistoryId, setExpandedHistoryId] = useState<string | null>(null)

  const selectedSubject = useMemo(
    () =>
      MOCK_EXAM_SUBJECTS.find((subject) => subject.id === selectedSubjectId) ??
      MOCK_EXAM_SUBJECTS[0],
    [selectedSubjectId],
  )
  const deviceStatus = DEVICE_STATUS[deviceState]
  const selectedHistory = MOCK_HISTORY.find((item) => item.id === expandedHistoryId)

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
        video: { facingMode: 'user', width: { ideal: 640 }, height: { ideal: 360 } },
      })
      if (stream.getAudioTracks().length === 0 || stream.getVideoTracks().length === 0) {
        throw new Error('Required media tracks are unavailable.')
      }
      const microphone = stream.getAudioTracks()[0]?.label || 'Microphone mặc định'
      const camera = stream.getVideoTracks()[0]?.label || 'Camera mặc định'
      setDeviceNames(`${microphone} · ${camera}`)
      setDeviceState('ready')
    } catch {
      setDeviceState('error')
    } finally {
      stream?.getTracks().forEach((track) => track.stop())
    }
  }

  const handleStartExam = () => {
    if (deviceState !== 'ready') return

    const config = {
      sessionId: `mock-${Date.now()}`,
      subjectId: selectedSubject.id,
      subjectCode: selectedSubject.code,
      subjectName: selectedSubject.name,
      track: selectedSubject.track,
      durationMinutes: selectedSubject.durationMinutes,
      questionCount: selectedSubject.questionCount,
      attemptNumber: selectedSubject.quotaLimit - selectedSubject.quotaRemaining + 1,
      deviceReady: true,
      startedAt: new Date().toISOString(),
    }

    try {
      window.sessionStorage.setItem('mock_exam_session_config', JSON.stringify(config))
    } catch {
      // Query parameters below preserve the essential configuration.
    }

    const search = new URLSearchParams({
      subject: selectedSubject.id,
      duration: String(selectedSubject.durationMinutes),
      questions: String(selectedSubject.questionCount),
    })
    navigate(`/mock-exam/session?${search.toString()}`)
  }

  const renderHistoryToggle = (item: MockHistoryItem) => {
    const isExpanded = item.id === expandedHistoryId
    return (
      <button
        type="button"
        onClick={() => setExpandedHistoryId(isExpanded ? null : item.id)}
        className="inline-flex min-h-8 items-center gap-1.5 rounded-lg border border-[#e1e7ef] bg-white px-2.5 text-[9px] font-semibold text-[#43546d] transition hover:border-[#b8c9df] hover:bg-[#f5f8fd] hover:text-[#245bd2] focus-visible:outline-2 focus-visible:outline-[#245eea]"
        aria-expanded={isExpanded}
      >
        <Eye className="size-3" />
        {isExpanded ? 'Đóng bảng điểm' : 'Xem bảng điểm'}
      </button>
    )
  }

  return (
    <div className="space-y-4 sm:space-y-5">
      <section className="relative isolate min-h-[218px] overflow-hidden rounded-[22px] border border-[#d9e3f2] bg-[linear-gradient(118deg,#ffffff_0%,#f5f8ff_48%,#e9efff_100%)] shadow-[0_14px_38px_rgba(31,61,105,0.08)] sm:min-h-[205px]">
        <div className="pointer-events-none absolute inset-0 [background-image:radial-gradient(#a9bbe0_0.8px,transparent_0.8px)] [background-size:22px_22px] opacity-55" />
        <div className="pointer-events-none absolute inset-y-0 right-0 w-[75%] bg-[radial-gradient(circle_at_72%_48%,rgba(88,111,230,0.15),transparent_46%)] sm:w-[60%]" />

        <div className="relative z-10 flex min-h-[218px] max-w-[760px] flex-col items-start justify-center px-5 py-6 sm:min-h-[205px] sm:px-7 lg:max-w-[64%] lg:px-8">
          <div className="inline-flex items-center gap-2 rounded-full border border-[#cbd8f3] bg-white/75 px-3 py-1 text-[9px] font-bold tracking-[0.08em] text-[#4b5fc6] uppercase shadow-sm backdrop-blur-sm">
            <ShieldCheck className="size-3" />
            Phòng khảo thí mô phỏng · AI Proctor
          </div>
          <h1 className="mt-3 text-[clamp(1.6rem,3vw,2.35rem)] leading-[1.08] font-bold tracking-[-0.045em] text-[#101d38]">
            Thi thử Vấn đáp Giả lập
          </h1>
          <p className="mt-2 max-w-[620px] text-[11px] leading-relaxed text-[#5f6f87] sm:text-[12px]">
            Mô phỏng áp lực phòng thi chính thức với giám thị mô phỏng, đồng hồ nghiêm ngặt và chấm
            điểm theo rubric chuẩn đại học.
          </p>
          <div className="mt-4 flex flex-wrap items-center gap-2">
            <span className="inline-flex items-center gap-1.5 rounded-full border border-[#dde4f3] bg-white/75 px-3 py-1.5 text-[9px] font-semibold text-[#576980] backdrop-blur-sm">
              <TimerReset className="size-3 text-[#5b63db]" /> Đồng hồ đồng bộ thời gian thực
            </span>
            <span className="inline-flex items-center gap-1.5 rounded-full border border-[#d6ece7] bg-[#f5fffc]/80 px-3 py-1.5 text-[9px] font-semibold text-[#26816c] backdrop-blur-sm">
              <span className="size-1.5 animate-pulse rounded-full bg-[#31c494] motion-reduce:animate-none" />
              Hệ thống mô phỏng sẵn sàng
            </span>
          </div>
        </div>

        <div className="pointer-events-none absolute inset-y-0 right-[-30%] z-0 w-[72%] opacity-35 sm:right-[-7%] sm:w-[62%] sm:opacity-85 lg:right-0 lg:w-[46%] lg:opacity-100">
          <Suspense
            fallback={
              <div className="flex size-full items-center justify-center" aria-hidden="true">
                <div className="size-20 animate-pulse rounded-full border border-[#8194ea]/30 bg-[#6573e6]/10 motion-reduce:animate-none" />
              </div>
            }
          >
            <MockExamHeroThreeScene
              scanning={deviceState === 'checking'}
              ready={deviceState === 'ready'}
            />
          </Suspense>
        </div>

        <div className="pointer-events-none absolute top-5 right-5 z-10 hidden items-center gap-2 rounded-xl border border-[#d8e1f2] bg-white/72 px-3 py-2 text-[9px] font-semibold text-[#58677c] shadow-sm backdrop-blur-md md:flex">
          <Bot className="size-4 text-[#5765dc]" />
          <span>
            AI Proctor v4.2
            <span className="mt-0.5 block text-[8px] font-medium text-[#8793a6]">
              Camera check & theo dõi rời tab
            </span>
          </span>
        </div>
      </section>

      <section className="overflow-hidden rounded-2xl border border-[#e0e7f0] bg-white shadow-[0_10px_34px_rgba(31,61,105,0.055)]">
        <div className="flex flex-col gap-4 border-b border-[#edf1f6] px-5 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-6">
          <div>
            <div className="flex items-center gap-2 text-[9px] font-bold tracking-[0.08em] text-[#6072c7] uppercase">
              <Sparkles className="size-3" />
              Kỳ thi mô phỏng học kỳ 2026 · Bắt buộc micro & camera
            </div>
            <h2 className="mt-2 text-[17px] font-bold tracking-[-0.025em] text-[#1b2a42] sm:text-xl">
              Thi thử Kết thúc học phần
            </h2>
          </div>

          <div className="relative min-w-0 sm:w-[360px]">
            <label htmlFor="mock-subject" className="sr-only">
              Chọn học phần thi thử
            </label>
            <select
              id="mock-subject"
              value={selectedSubjectId}
              onChange={(event) => setSelectedSubjectId(event.target.value)}
              className="min-h-11 w-full appearance-none rounded-xl border border-[#dfe6f0] bg-[#f8faff] py-2 pr-9 pl-3 text-[10px] font-semibold text-[#26364d] transition hover:border-[#bdcce0] focus:border-[#5b68db] focus:ring-3 focus:ring-[#5b68db]/10 focus:outline-none sm:text-[11px]"
            >
              {MOCK_EXAM_SUBJECTS.map((subject) => (
                <option key={subject.id} value={subject.id}>
                  {subject.name} ({subject.code})
                </option>
              ))}
            </select>
            <ChevronDown className="pointer-events-none absolute top-1/2 right-3 size-4 -translate-y-1/2 text-[#74839a]" />
          </div>
        </div>

        <div className="space-y-4 p-5 sm:p-6">
          <div>
            <p className="text-[15px] font-bold text-[#1e2f47] sm:text-lg">
              {selectedSubject.name} ({selectedSubject.code})
            </p>
            <p className="mt-1 text-[10px] text-[#748297]">{selectedSubject.track}</p>
          </div>

          <div className="grid gap-2 sm:grid-cols-3">
            <div className="flex min-h-11 items-center gap-2 rounded-xl border border-[#e3e9f1] bg-[#f8faff] px-3 text-[10px] font-semibold text-[#4e6078]">
              <Clock3 className="size-4 text-[#4f63d3]" />
              {selectedSubject.durationMinutes} phút
            </div>
            <div className="flex min-h-11 items-center gap-2 rounded-xl border border-[#e3e9f1] bg-[#f8faff] px-3 text-[10px] font-semibold text-[#4e6078]">
              <FileCheck2 className="size-4 text-[#4f63d3]" />
              {selectedSubject.questionCount} câu vấn đáp chuyên sâu
            </div>
            <div
              className={cn(
                'flex min-h-11 items-center gap-2 rounded-xl border px-3 text-[10px] font-semibold',
                selectedSubject.quotaRemaining > 0
                  ? 'border-[#f2dfad] bg-[#fffaf0] text-[#8b641c]'
                  : 'border-[#f2cbd1] bg-[#fff5f6] text-[#ba4356]',
              )}
            >
              <RefreshCw className="size-4" />
              Lượt còn lại: {selectedSubject.quotaRemaining}/{selectedSubject.quotaLimit}
            </div>
          </div>

          <div className="overflow-hidden rounded-xl border border-[#e3e8f1] bg-[#f7f9fc]">
            <button
              type="button"
              onClick={() => setRulesExpanded((current) => !current)}
              className="focus-visible:outline-inset flex min-h-11 w-full items-center justify-between gap-3 px-4 text-left transition hover:bg-[#f0f4fa] focus-visible:outline-2 focus-visible:outline-[#5b68db]"
              aria-expanded={rulesExpanded}
            >
              <span className="flex items-center gap-2 text-[10px] font-bold text-[#34455e]">
                <ShieldCheck className="size-4 text-[#5865d8]" />
                Quy chế & hướng dẫn phòng thi thử
              </span>
              <ChevronDown
                className={cn(
                  'size-4 text-[#75849a] transition-transform',
                  rulesExpanded && 'rotate-180',
                )}
              />
            </button>

            {rulesExpanded && (
              <div className="grid gap-x-6 gap-y-3 border-t border-[#e5eaf2] px-4 py-4 sm:grid-cols-2">
                {[
                  'Thí sinh trả lời trực tiếp qua micro; camera và bộ đếm rời tab hoạt động liên tục.',
                  'Mỗi câu có tối đa 45 giây chuẩn bị và khoảng 3 phút trả lời.',
                  'AI có thể sinh một câu hỏi phụ nếu câu trả lời chưa đủ chiều sâu.',
                  'Điểm thi thử không tính vào điểm học phần chính thức.',
                ].map((rule, index) => (
                  <div
                    key={rule}
                    className="flex items-start gap-2.5 text-[9px] leading-relaxed text-[#64748a] sm:text-[10px]"
                  >
                    <span className="flex size-5 shrink-0 items-center justify-center rounded-full bg-white text-[8px] font-bold text-[#5968d8] shadow-sm">
                      {index + 1}
                    </span>
                    {rule}
                  </div>
                ))}
              </div>
            )}
          </div>

          <div className="flex flex-col gap-3 rounded-xl border border-[#e1e8f1] bg-white p-3.5 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex min-w-0 items-center gap-3">
              <div className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-[#eef1ff] text-[#5865d8]">
                <span className="relative">
                  <Camera className="size-4" />
                  <Mic2 className="absolute -right-1.5 -bottom-1.5 size-3 rounded-full bg-[#eef1ff]" />
                </span>
              </div>
              <div className="min-w-0">
                <p
                  className={cn(
                    'flex items-center gap-1.5 text-[10px] font-bold',
                    deviceStatus.color,
                  )}
                >
                  <span className={cn('size-1.5 shrink-0 rounded-full', deviceStatus.dot)} />
                  {deviceStatus.title}
                </p>
                <p className="mt-0.5 truncate text-[9px] text-[#7d8a9c]">
                  {deviceNames || deviceStatus.helper}
                </p>
              </div>
            </div>
            <button
              type="button"
              onClick={handleDeviceCheck}
              disabled={deviceState === 'checking'}
              className="inline-flex min-h-9 shrink-0 items-center justify-center gap-1.5 rounded-lg px-3 text-[9px] font-bold text-[#5261d2] transition hover:bg-[#eef1ff] focus-visible:outline-2 focus-visible:outline-[#5b68db] disabled:cursor-wait disabled:opacity-60"
            >
              {deviceState === 'checking' ? (
                <LoaderCircle className="size-3.5 animate-spin" />
              ) : (
                <RefreshCw className="size-3.5" />
              )}
              {deviceState === 'ready' ? 'Kiểm tra lại' : 'Kiểm tra thiết bị'}
            </button>
          </div>

          {selectedSubject.quotaRemaining === 0 && (
            <div className="flex items-start gap-2 rounded-xl border border-[#f1c7ce] bg-[#fff5f6] p-3 text-[9px] leading-relaxed text-[#ae3d50]">
              <AlertTriangle className="mt-0.5 size-4 shrink-0" />
              Bạn đã sử dụng hết lượt thi thử hôm nay cho học phần này. Hãy quay lại vào ngày mai.
            </div>
          )}

          <div className="flex flex-col gap-3 border-t border-[#edf1f6] pt-4 sm:flex-row sm:items-center">
            <button
              type="button"
              onClick={handleStartExam}
              disabled={selectedSubject.quotaRemaining === 0 || deviceState !== 'ready'}
              className="group inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[#245eea] px-6 text-[10px] font-bold text-white shadow-[0_11px_26px_rgba(36,94,234,0.24)] transition duration-200 hover:-translate-y-0.5 hover:bg-[#194fce] hover:shadow-[0_15px_32px_rgba(36,94,234,0.3)] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#245eea] disabled:pointer-events-none disabled:opacity-45 motion-reduce:transform-none"
            >
              <Play className="size-4 fill-current" />
              {deviceState === 'ready' ? 'Bắt đầu thi thử' : 'Kiểm tra thiết bị để tiếp tục'}
              <ArrowRight className="size-4 transition-transform group-hover:translate-x-0.5 motion-reduce:transform-none" />
            </button>
            <p className="flex items-center gap-1.5 text-[9px] text-[#718095]">
              <ShieldCheck className="size-3.5 text-[#24a77f]" />
              Hệ thống giám sát mô phỏng; dữ liệu không ảnh hưởng điểm học phần.
            </p>
          </div>
        </div>
      </section>

      <section className="overflow-hidden rounded-2xl border border-[#e0e7f0] bg-white shadow-[0_9px_30px_rgba(31,61,105,0.05)]">
        <div className="flex items-start justify-between gap-4 border-b border-[#edf1f6] px-5 py-4 sm:items-center sm:px-6">
          <div>
            <div className="flex items-center gap-2">
              <History className="size-4 text-[#5865d8]" />
              <h2 className="text-[13px] font-bold text-[#213149] sm:text-[15px]">
                Lịch sử Thi thử Vấn đáp
              </h2>
            </div>
            <p className="mt-1 text-[9px] text-[#7e8b9e]">
              Chỉ hiển thị các phiên thi thử đã bấm ghi chuẩn quy chế
            </p>
          </div>
          <span className="rounded-full bg-[#f1f4f9] px-2.5 py-1 text-[8px] font-semibold text-[#68778c]">
            Tổng số: {MOCK_HISTORY.length} lượt đã nộp
          </span>
        </div>

        <div className="hidden md:block">
          <table className="w-full table-fixed border-collapse text-left">
            <thead className="bg-[#f7f9fc] text-[8px] font-bold tracking-[0.06em] text-[#718095] uppercase">
              <tr>
                <th className="w-[14%] px-5 py-3">Mã đề giả lập</th>
                <th className="w-[30%] px-3 py-3">Môn thi</th>
                <th className="w-[15%] px-3 py-3">Ngày thực hiện</th>
                <th className="w-[12%] px-3 py-3">Thời gian</th>
                <th className="w-[12%] px-3 py-3">Điểm mô phỏng</th>
                <th className="w-[17%] px-3 py-3 text-right">Thao tác</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#edf1f6]">
              {MOCK_HISTORY.map((item) => (
                <tr
                  key={item.id}
                  className="text-[9px] text-[#66758a] transition hover:bg-[#fafcff]"
                >
                  <td className="px-5 py-3 font-mono font-bold text-[#3f5fc7]">{item.examCode}</td>
                  <td className="px-3 py-3">
                    <p className="font-bold text-[#26364d]">{item.subject}</p>
                    <p className="mt-0.5 truncate text-[8px] text-[#8794a6]">{item.track}</p>
                  </td>
                  <td className="px-3 py-3">{item.date}</td>
                  <td className="px-3 py-3">{item.duration}</td>
                  <td className="px-3 py-3">
                    <span className="inline-flex items-center gap-1 rounded-full bg-[#edfbf6] px-2 py-1 font-bold text-[#16866c]">
                      <span className="size-1.5 rounded-full bg-[#36c798]" />
                      {item.score.toFixed(1)} / 10
                    </span>
                  </td>
                  <td className="px-3 py-3 text-right">{renderHistoryToggle(item)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="divide-y divide-[#edf1f6] md:hidden">
          {MOCK_HISTORY.map((item) => (
            <article key={item.id} className="space-y-3 p-4">
              <div className="flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <p className="font-mono text-[9px] font-bold text-[#4961c8]">{item.examCode}</p>
                  <h3 className="mt-1 text-[11px] font-bold text-[#26364d]">{item.subject}</h3>
                  <p className="mt-0.5 truncate text-[8px] text-[#8794a6]">{item.track}</p>
                </div>
                <span className="shrink-0 rounded-full bg-[#edfbf6] px-2 py-1 text-[9px] font-bold text-[#16866c]">
                  {item.score.toFixed(1)}/10
                </span>
              </div>
              <div className="flex items-center justify-between gap-3 text-[9px] text-[#758398]">
                <span>
                  {item.date} · {item.duration}
                </span>
                {renderHistoryToggle(item)}
              </div>
            </article>
          ))}
        </div>

        {selectedHistory && (
          <div className="grid gap-3 border-t border-[#dfe6f1] bg-[#f7f9fd] px-5 py-4 sm:grid-cols-2 sm:px-6">
            <div className="rounded-xl border border-white bg-white/75 p-3">
              <p className="flex items-center gap-1.5 text-[9px] font-bold text-[#4c5fc5] uppercase">
                <FileCheck2 className="size-3.5" /> Tổng kết phiên
              </p>
              <p className="mt-2 text-[9px] leading-relaxed text-[#64748a]">
                {selectedHistory.summary}
              </p>
            </div>
            <div className="rounded-xl border border-white bg-white/75 p-3">
              <p className="flex items-center gap-1.5 text-[9px] font-bold text-[#16866c] uppercase">
                <Trophy className="size-3.5" /> Điểm mạnh
              </p>
              <p className="mt-2 text-[9px] leading-relaxed text-[#64748a]">
                {selectedHistory.strengths}
              </p>
            </div>
          </div>
        )}
      </section>

      <div className="sr-only" aria-live="polite">
        {deviceStatus.title}. {deviceNames || deviceStatus.helper}
      </div>
    </div>
  )
}
