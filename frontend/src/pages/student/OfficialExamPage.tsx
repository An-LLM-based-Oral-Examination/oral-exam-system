import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import {
  AlertTriangle,
  CheckCircle2,
  Clock3,
  LoaderCircle,
  LockKeyhole,
  Mic2,
  ShieldAlert,
  UploadCloud,
} from 'lucide-react'
import { useAuth } from '@/hooks/useAuth'
import { useKioskLockdown } from '@/hooks/useKioskLockdown'
import { useAudioRecorder } from '@/hooks/useAudioRecorder'
import { cn } from '@/lib/utils'
import { ds } from '@/design/system'
import { officialExamService } from '@/services/officialExam.service'
import { proctorService } from '@/services/proctor.service'
import { transcribeSpeech } from '@/services/speech.service'
import type {
  AudioUploadAuthorization,
  OfficialExamAnswerSubmissionRequest,
  OfficialExamSubmissionStatusResponse,
  StudentWorkstationTicket,
  SubmitOfficialExamResponse,
} from '@/types/officialExam.types'

type MF04StudentService = {
  getCurrentTicket: () => Promise<StudentWorkstationTicket>
  checkIn: (ticketId: string) => Promise<unknown>
  createAudioUploadAuthorization: (ticketId: string) => Promise<AudioUploadAuthorization>
  sealAudio: (ticketId: string, payload: { objectKey: string; sha256: string }) => Promise<unknown>
  uploadAudio: (authorization: AudioUploadAuthorization, audio: Blob) => Promise<void>
  submitExam: (ticketId: string, answers: OfficialExamAnswerSubmissionRequest[]) => Promise<SubmitOfficialExamResponse>
  getSubmissionStatus: (ticketId: string) => Promise<OfficialExamSubmissionStatusResponse>
  suspendTicket: (ticketId: string, payload: { reason: string; blurViolationCount?: number }) => Promise<unknown>
}

const mf04StudentService = officialExamService as unknown as MF04StudentService

function mf04ErrorMessage(error: unknown) {
  if (typeof error === 'object' && error !== null && 'detail' in error) {
    const detail = (error as { detail?: unknown }).detail
    if (typeof detail === 'string' && detail.trim()) return detail
  }
  return error instanceof Error ? error.message : 'Không thể hoàn tất yêu cầu phòng thi.'
}

async function calculateSha256(blob: Blob) {
  const digest = await crypto.subtle.digest('SHA-256', await blob.arrayBuffer())
  return Array.from(new Uint8Array(digest))
    .map((byte) => byte.toString(16).padStart(2, '0'))
    .join('')
}

function formatMf04Date(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return new Intl.DateTimeFormat('vi-VN', { dateStyle: 'medium', timeStyle: 'short' }).format(date)
}

function formatRecordingDuration(seconds: number) {
  const safeSeconds = Math.max(0, Math.floor(seconds))
  return `${Math.floor(safeSeconds / 60).toString().padStart(2, '0')}:${(safeSeconds % 60).toString().padStart(2, '0')}`
}

function getExamQuestionImageUrl(url: string | null | undefined) {
  if (!url) return null
  if (/^https?:\/\//i.test(url)) return url
  return url.startsWith('/') ? url : `/${url}`
}

function OfficialExamLivePage() {
  const { user } = useAuth()
  const [ticket, setTicket] = useState<StudentWorkstationTicket | null>(null)
  const [answers, setAnswers] = useState<Record<string, string>>({})
  const [timestampsByQuestion, setTimestampsByQuestion] = useState<Record<string, string | null>>({})
  const [transcribingQuestionId, setTranscribingQuestionId] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [recordingSeconds, setRecordingSeconds] = useState(0)
  const [sealed, setSealed] = useState(false)
  const [submission, setSubmission] = useState<OfficialExamSubmissionStatusResponse | null>(null)
  const [isSuspended, setIsSuspended] = useState(false)
  const recordingStartedAtRef = useRef<number | null>(null)
  const recordedExamAudioRef = useRef<Blob | null>(null)
  const [hasRecordedAudioBlob, setHasRecordedAudioBlob] = useState(false)
  const mountedRef = useRef(true)
  const suspendedRef = useRef(false)
  const questionSegmentPromisesRef = useRef<Array<Promise<string>>>([])
  const handleQuestionSegment = useCallback((segmentBlob: Blob) => {
    const segmentPromise = transcribeSpeech(segmentBlob, {
      fileName: `question-segment-${Date.now()}.webm`,
    })
      .then((res) => res.transcript.trim())
      .catch(() => '')
    questionSegmentPromisesRef.current.push(segmentPromise)
  }, [])
  const audioRecorder = useAudioRecorder()
  const questionRecorder = useAudioRecorder({
    segmentDurationMs: 20_000,
    onSegment: handleQuestionSegment,
  })
  const [recordingQuestionId, setRecordingQuestionId] = useState<string | null>(null)

  const transcribeQuestionAudio = useCallback(async (questionAudio: Blob, fileName: string) => {
    await new Promise<void>((resolve) => window.setTimeout(resolve, 50))
    const preTranscribedPromises = [...questionSegmentPromisesRef.current]
    questionSegmentPromisesRef.current = []

    let text = ''
    let timestampsJson: string | null = null
    if (preTranscribedPromises.length > 0) {
      const tailPromise =
        questionAudio.size >= 2_048
          ? transcribeSpeech(questionAudio, { fileName })
              .then((res) => {
                timestampsJson = res.timestampsJson ?? null
                return res.transcript.trim()
              })
              .catch(() => '')
          : Promise.resolve('')
      const allParts = await Promise.all([...preTranscribedPromises, tailPromise])
      text = allParts.filter((part) => part.length > 0).join(' ').trim()
    }

    if (!text && questionAudio.size > 0) {
      const sttResponse = await transcribeSpeech(questionAudio, { fileName })
      text = sttResponse.transcript.trim()
      timestampsJson = sttResponse.timestampsJson ?? null
    }

    return { text, timestampsJson }
  }, [])

  const isInProgress = ticket?.status === 'IN_PROGRESS'
  const isSubmitted = Boolean(ticket && ['SUBMITTED', 'AI_GRADED', 'AUDITED', 'PUBLISHED', 'LOCKED'].includes(ticket.status))
  const isVoiceOnly = ticket?.examInputMode === 'VoiceOnly'
  const questions = useMemo(() => ticket?.questions ?? [], [ticket?.questions])

  useEffect(() => {
    mountedRef.current = true
    void (async () => {
      setLoading(true)
      setError(null)
      try {
        const currentTicket = await mf04StudentService.getCurrentTicket()
        if (mountedRef.current) {
          setTicket(currentTicket)
          if (currentTicket.status === 'IN_PROGRESS') {
            try {
              const rawDraft = localStorage.getItem(`mf04_kiosk_draft_${currentTicket.id}`)
              if (rawDraft) {
                const parsedDraft = JSON.parse(rawDraft) as {
                  answers?: Record<string, string>
                  timestamps?: Record<string, string | null>
                }
                if (parsedDraft.answers && typeof parsedDraft.answers === 'object') {
                  setAnswers(parsedDraft.answers)
                }
                if (parsedDraft.timestamps && typeof parsedDraft.timestamps === 'object') {
                  setTimestampsByQuestion(parsedDraft.timestamps)
                }
              }
            } catch {
              // Ignore localStorage parse errors
            }
          }
        }
      } catch (requestError) {
        if (mountedRef.current) setError(mf04ErrorMessage(requestError))
      } finally {
        if (mountedRef.current) setLoading(false)
      }
    })()
    return () => {
      mountedRef.current = false
    }
  }, [])

  useEffect(() => {
    if (!ticket || ticket.status !== 'IN_PROGRESS') return
    try {
      localStorage.setItem(
        `mf04_kiosk_draft_${ticket.id}`,
        JSON.stringify({ answers, timestamps: timestampsByQuestion }),
      )
    } catch {
      // Ignore quota errors
    }
  }, [answers, timestampsByQuestion, ticket])

  useEffect(() => {
    if (!audioRecorder.isRecording) return
    recordingStartedAtRef.current ??= Date.now()
    const timer = window.setInterval(() => {
      if (recordingStartedAtRef.current) {
        setRecordingSeconds(Math.floor((Date.now() - recordingStartedAtRef.current) / 1000))
      }
    }, 1000)
    return () => window.clearInterval(timer)
  }, [audioRecorder.isRecording])

  const suspendForLockdown = useCallback(async () => {
    if (!ticket || suspendedRef.current) return
    suspendedRef.current = true
    audioRecorder.cancelRecording()
    try {
      await proctorService.suspendTicket(ticket.id, {
        reason: 'Kiosk Lockdown: vượt quá số lần vi phạm cửa sổ/toàn màn hình.',
        blurViolationCount: 3,
      })
      setIsSuspended(true)
      setTicket((current) => current ? { ...current, status: 'LOCKED' } : current)
      setError('Vé thi đã bị đình chỉ do vi phạm quy chế Kiosk. Vui lòng liên hệ giám thị.')
    } catch (requestError) {
      setError(`Không thể ghi nhận đình chỉ: ${mf04ErrorMessage(requestError)}`)
    }
  }, [audioRecorder, ticket])

  const kiosk = useKioskLockdown(
    3,
    () => { void suspendForLockdown() },
    Boolean(isInProgress && audioRecorder.isRecording && !isSuspended),
  )

  const refreshTicket = async () => {
    setBusy(true)
    setError(null)
    try {
      const currentTicket = await mf04StudentService.getCurrentTicket()
      setTicket(currentTicket)
      if (currentTicket.status !== 'IN_PROGRESS') setRecordingSeconds(0)
    } catch (requestError) {
      setError(mf04ErrorMessage(requestError))
    } finally {
      setBusy(false)
    }
  }

  const handleCheckIn = async () => {
    if (!ticket) return
    setBusy(true)
    setError(null)
    try {
      await mf04StudentService.checkIn(ticket.id)
      await refreshTicket()
      setNotice('Điểm danh nhận máy thành công. Hãy kiểm tra microphone và bắt đầu ghi âm bài thi.')
    } catch (requestError) {
      setError(mf04ErrorMessage(requestError))
    } finally {
      setBusy(false)
    }
  }

  const handleStartRecording = async () => {
    if (!ticket || !isInProgress || isSubmitted || busy) return
    setError(null)
    try {
      await audioRecorder.startRecording()
      recordingStartedAtRef.current = Date.now()
      setRecordingSeconds(0)
      setNotice('Đang ghi âm toàn bộ bài thi. Không rời khỏi màn hình phòng thi.')
    } catch (recordingError) {
      setError(mf04ErrorMessage(recordingError))
    }
  }

  const handleToggleQuestionVoice = async (questionId: string, index: number) => {
    if (!ticket || !isInProgress || !audioRecorder.isRecording || isSubmitted || busy) return
    setError(null)
    if (recordingQuestionId === questionId && questionRecorder.isRecording) {
      setTranscribingQuestionId(questionId)
      try {
        const questionAudio = await questionRecorder.stopRecording()
        setRecordingQuestionId(null)
        setNotice(`Đang bóc băng giọng nói Câu ${index + 1}...`)
        const { text, timestampsJson } = await transcribeQuestionAudio(
          questionAudio,
          `question-${index + 1}-${questionId}.webm`,
        )
        setAnswers((current) => ({ ...current, [questionId]: text }))
        setTimestampsByQuestion((current) => ({
          ...current,
          [questionId]: timestampsJson,
        }))
        setNotice(`Đã ghi nhận bản bóc băng giọng nói cho Câu ${index + 1}.`)
      } catch (sttError) {
        setRecordingQuestionId(null)
        setError(mf04ErrorMessage(sttError))
      } finally {
        setTranscribingQuestionId(null)
      }
      return
    }

    if (questionRecorder.isRecording) {
      questionRecorder.cancelRecording()
    }
    questionSegmentPromisesRef.current = []
    try {
      await questionRecorder.startRecording()
      setRecordingQuestionId(questionId)
      setNotice(`Đang thu âm trả lời riêng cho Câu ${index + 1}...`)
    } catch (recError) {
      setError(mf04ErrorMessage(recError))
    }
  }

  const handleSubmit = useCallback(async () => {
    if (!ticket || !isInProgress || (!audioRecorder.isRecording && !recordedExamAudioRef.current) || busy) return
    setBusy(true)
    setError(null)
    try {
      const effectiveAnswers = { ...answers }
      const effectiveTimestamps = { ...timestampsByQuestion }
      if (recordingQuestionId && questionRecorder.isRecording) {
        const activeQId = recordingQuestionId
        const questionAudio = await questionRecorder.stopRecording()
        setRecordingQuestionId(null)
        const { text, timestampsJson } = await transcribeQuestionAudio(
          questionAudio,
          `question-${activeQId}.webm`,
        )
        effectiveAnswers[activeQId] = text
        effectiveTimestamps[activeQId] = timestampsJson
        setAnswers((current) => ({ ...current, [activeQId]: text }))
        setTimestampsByQuestion((current) => ({
          ...current,
          [activeQId]: timestampsJson,
        }))
      }

      const audio = audioRecorder.isRecording
        ? await audioRecorder.stopRecording()
        : recordedExamAudioRef.current!
      recordedExamAudioRef.current = audio
      setHasRecordedAudioBlob(true)

      const authorization = await mf04StudentService.createAudioUploadAuthorization(ticket.id)
      await mf04StudentService.uploadAudio(authorization, audio)
      const audioHash = await calculateSha256(audio)
      await mf04StudentService.sealAudio(ticket.id, { objectKey: authorization.objectKey, sha256: audioHash })
      setSealed(true)

      const hasAnyQuestionTranscript = questions.some((question) => Boolean(effectiveAnswers[question.questionId]?.trim()))
      let sttTranscript: string | null = null
      let sttTimestamps: string | null = null
      if (!hasAnyQuestionTranscript && questions.length > 0) {
        setNotice('Đang bóc băng giọng nói trước khi niêm phong nộp bài...')
        const fileName = authorization.objectKey.split('/').pop() || 'exam-audio.webm'
        const sttResponse = await transcribeSpeech(audio, { fileName })
        sttTranscript = sttResponse.transcript.trim()
        sttTimestamps = sttResponse.timestampsJson ?? null
        const firstQuestionId = questions[0]?.questionId
        if (firstQuestionId) {
          setAnswers((current) => ({
            ...current,
            [firstQuestionId]: sttTranscript ?? '',
          }))
        }
      }

      const payload = questions.map((question, index) => ({
        examQuestionId: question.questionId,
        transcriptWhisper:
          effectiveAnswers[question.questionId]?.trim() ||
          (!hasAnyQuestionTranscript && index === 0 ? sttTranscript : null) ||
          '',
        timestampsWhisper:
          effectiveTimestamps[question.questionId] ??
          (!hasAnyQuestionTranscript && index === 0 ? sttTimestamps : null),
        timeSpentSeconds: recordingSeconds,
      }))
      const submitted = await mf04StudentService.submitExam(ticket.id, payload)
      recordedExamAudioRef.current = null
      setHasRecordedAudioBlob(false)
      try {
        localStorage.removeItem(`mf04_kiosk_draft_${ticket.id}`)
      } catch {
        // Ignore
      }
      setSubmission({
        ticketId: submitted.ticketId,
        status: submitted.status,
        submittedAt: submitted.submittedAt,
        totalSubmissions: submitted.submissionCount,
        pendingSubmissions: submitted.submissionCount,
        gradedSubmissions: 0,
        failedSubmissions: 0,
      })
      setTicket((current) => current ? { ...current, status: submitted.status } : current)
      setNotice('Bài thi đã được lưu an toàn và chuyển vào hệ thống chấm điểm.')
    } catch (requestError) {
      setError(`${mf04ErrorMessage(requestError)} (Bản ghi âm và bài làm đã được giữ an toàn trên máy trạm; hệ thống sẽ tự động nộp lại ngay khi có mạng hoặc khi bấm Nộp bài).`)
    } finally {
      setBusy(false)
    }
  }, [answers, audioRecorder, busy, isInProgress, questionRecorder, questions, recordingQuestionId, recordingSeconds, ticket, timestampsByQuestion, transcribeQuestionAudio])

  useEffect(() => {
    const onOnline = () => {
      if (recordedExamAudioRef.current && ticket?.status === 'IN_PROGRESS' && !busy) {
        setNotice('Đã kết nối mạng trở lại. Đang tự động nộp bản ghi âm đã lưu...')
        void handleSubmit()
      }
    }
    window.addEventListener('online', onOnline)
    return () => window.removeEventListener('online', onOnline)
  }, [busy, handleSubmit, ticket?.status])

  const refreshSubmission = async () => {
    if (!ticket) return
    setBusy(true)
    try {
      setSubmission(await mf04StudentService.getSubmissionStatus(ticket.id))
    } catch (requestError) {
      setError(mf04ErrorMessage(requestError))
    } finally {
      setBusy(false)
    }
  }

  if (loading) return <div className={cn(ds.page, 'flex min-h-[420px] items-center justify-center')}><LoaderCircle className="size-7 animate-spin text-[#1C4D8D]" /></div>

  if (!ticket) return <div className={cn(ds.page, 'pb-8')}><section className={cn(ds.section, 'p-8 text-center')}><ShieldAlert className="mx-auto size-10 text-[#1C4D8D]" /><h1 className="mt-3 text-xl font-bold text-[#0F2854]">Chưa có vé thi tại máy trạm này</h1><p className="mx-auto mt-2 max-w-xl text-sm text-[#596b84]">Hãy kiểm tra đúng máy phòng Lab được phân công và thời gian ca thi, sau đó thử tải lại vé thi.</p>{error && <p className="mt-4 rounded-xl bg-[#fff4f5] px-4 py-3 text-sm text-[#b33d52]">{error}</p>}<button type="button" onClick={() => void refreshTicket()} className="mt-5 rounded-xl bg-[#1C4D8D] px-5 py-2.5 text-sm font-bold text-white transition hover:bg-[#0F2854]">Tải lại vé thi</button></section></div>

  const statusLabel = isSubmitted ? 'Đã nộp bài' : isInProgress ? 'Ca thi đang diễn ra' : 'Chờ nhận máy'
  const studentName = user?.fullName ?? 'Sinh viên'
  const inputModeLabel = isVoiceOnly ? 'Chỉ thu âm giọng nói' : 'Thu âm kèm hiệu đính thuật ngữ'

  return <div className={cn(ds.page, 'pb-8')}>
    <section className={cn(ds.banner, 'p-6 text-white sm:p-8')}><div className="relative z-10 flex flex-wrap items-start justify-between gap-4"><div><p className="text-xs font-bold tracking-[0.12em] text-[#BDE8F5] uppercase">Kỳ thi vấn đáp chính thức · Phòng Lab</p><h1 className="mt-2 text-2xl font-bold sm:text-3xl">{ticket.sessionTitle || ticket.shiftName}</h1><p className="mt-2 text-sm text-white/80">Phòng {ticket.roomLab} · Máy số {ticket.seatNumber} · {studentName}</p></div><span className="rounded-full bg-white/15 px-3.5 py-1.5 text-xs font-bold">{statusLabel}</span></div><div className="relative z-10 mt-6 grid gap-3 text-sm sm:grid-cols-3"><div className="rounded-2xl bg-white/12 p-3.5"><Clock3 className="size-4 text-[#BDE8F5]" /><p className="mt-2 text-xs text-white/70">Thời gian ca thi</p><p className="mt-1 font-bold">{formatMf04Date(ticket.startTime)} – {formatMf04Date(ticket.endTime)}</p></div><div className="rounded-2xl bg-white/12 p-3.5"><Mic2 className="size-4 text-[#BDE8F5]" /><p className="mt-2 text-xs text-white/70">Thời lượng ghi âm</p><p className="mt-1 font-bold">{formatRecordingDuration(recordingSeconds)}</p></div><div className="rounded-2xl bg-white/12 p-3.5"><LockKeyhole className="size-4 text-[#BDE8F5]" /><p className="mt-2 text-xs text-white/70">Trạng thái vé thi</p><p className="mt-1 font-bold">{ticket.status}</p></div></div></section>
    {(error || notice) && <div className={cn('rounded-2xl border px-4 py-3 text-sm', error ? 'border-[#f3c8cf] bg-[#fff5f6] text-[#b33d52]' : 'border-[#bcebdc] bg-[#effbf6] text-[#16785f]')}>{error ?? notice}</div>}
    {(kiosk.isLocked || audioRecorder.error) && <div className="flex items-start gap-3 rounded-2xl border border-[#f3c8cf] bg-[#fff5f6] p-4 text-sm text-[#b33d52]"><AlertTriangle className="mt-0.5 size-5 shrink-0" /><div><strong>{kiosk.isLocked ? 'Phòng thi đã đình chỉ vé thi.' : 'Lỗi microphone.'}</strong><p className="mt-1">{kiosk.isLocked ? `Số lần rời màn hình vượt quy định (${kiosk.violations} lần). Vui lòng liên hệ giám thị.` : audioRecorder.error}</p></div></div>}
    <section className={cn(ds.section, 'overflow-hidden')}><div className={cn(ds.sectionHeader, 'flex flex-wrap items-center justify-between gap-3')}><div><div className={ds.sectionTitleRow}><span className={ds.sectionIconWrap}><Mic2 className="size-4 text-[#1C4D8D]" /></span><h2 className={ds.sectionTitle}>Bài thi vấn đáp chính thức</h2></div><p className={ds.sectionHint}>Chế độ trả lời: {inputModeLabel} · Thời gian đệm hiệu đính: {ticket.transcriptBufferSeconds} giây</p></div>{!isSubmitted && <div className="flex flex-wrap gap-2">{!isInProgress ? <button type="button" disabled={busy} onClick={() => void handleCheckIn()} className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[#1C4D8D] px-4 text-sm font-bold text-white transition hover:bg-[#0F2854] disabled:opacity-50"><CheckCircle2 className="size-4" />Xác nhận nhận máy (Check-in)</button> : (!audioRecorder.isRecording && !hasRecordedAudioBlob) ? <button type="button" disabled={busy || kiosk.isLocked} onClick={() => void handleStartRecording()} className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[#1C4D8D] px-4 text-sm font-bold text-white transition hover:bg-[#0F2854] disabled:opacity-50"><Mic2 className="size-4" />Bắt đầu ghi âm</button> : <button type="button" disabled={busy || kiosk.isLocked} onClick={() => void handleSubmit()} className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[#b33d52] px-4 text-sm font-bold text-white transition hover:bg-[#932c3f] disabled:opacity-50"><UploadCloud className="size-4" />{busy ? 'Đang lưu...' : 'Nộp bài và niêm phong'}</button>}</div>}</div><div className="p-5 sm:p-6">{isSubmitted && <div className="mb-4 rounded-2xl border border-[#bcebdc] bg-[#effbf6] p-4 text-sm text-[#16785f]"><strong>Bài thi đã được tiếp nhận và niêm phong an toàn.</strong> Bạn có thể ký biên bản phòng thi và theo dõi kết quả sau khi giảng viên công bố điểm. {submission && `Trạng thái: ${submission.status}.`}</div>}{audioRecorder.isRecording && <div className="mb-4 flex items-center gap-2 rounded-xl bg-[#fff4f5] px-3.5 py-2.5 text-sm font-semibold text-[#b33d52]"><span className="size-2 animate-pulse rounded-full bg-[#d9455d]" />Đang ghi âm · {formatRecordingDuration(recordingSeconds)} · Còn {kiosk.remainingStrikes} lần nhắc nhở toàn màn hình</div>}<div className="space-y-4">{questions.length === 0 ? <div className="rounded-2xl border border-dashed border-[#cdd8e6] p-6 text-center text-sm text-[#60718a]">Ca thi chưa được gán bộ câu hỏi. Hãy liên hệ giám thị phòng thi.</div> : questions.map((question, index) => <article key={question.questionId} className="rounded-2xl border border-[#dce4ee] bg-[#F4FBFD] p-4"><div className="flex items-start gap-3"><span className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-[#1C4D8D] text-sm font-bold text-white">{index + 1}</span><div className="min-w-0 flex-1"><div className="flex flex-wrap items-center justify-between gap-2"><p className="text-xs font-bold text-[#1C4D8D]">{question.title || `Câu hỏi ${index + 1}`} · {question.difficulty}</p>{audioRecorder.isRecording && !isSubmitted && <button type="button" disabled={busy || transcribingQuestionId === question.questionId} onClick={() => void handleToggleQuestionVoice(question.questionId, index)} className={cn('inline-flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-xs font-bold transition disabled:opacity-50', recordingQuestionId === question.questionId ? 'bg-[#b33d52] text-white' : 'bg-[#1C4D8D] text-white')}><Mic2 className="size-3.5" />{transcribingQuestionId === question.questionId ? 'Đang bóc băng...' : recordingQuestionId === question.questionId ? 'Dừng & Bóc băng câu này' : 'Thu âm trả lời câu này'}</button>}</div><h3 className="mt-1 text-sm font-bold leading-relaxed text-[#0F2854]">{question.content}</h3>{getExamQuestionImageUrl(question.imageUrl) && <img src={getExamQuestionImageUrl(question.imageUrl) ?? undefined} alt={question.imageDescription || question.title || 'Hình ảnh câu hỏi'} className="mt-3 max-h-72 w-full rounded-xl border border-[#dce4ee] bg-white object-contain" />}{question.imageDescription && <p className="mt-2 text-xs italic text-[#60718a]">{question.imageDescription}</p>}<textarea value={answers[question.questionId] ?? ''} readOnly={isVoiceOnly} disabled={!audioRecorder.isRecording || isSubmitted || isVoiceOnly} onChange={(event) => { if (!isVoiceOnly) setAnswers((current) => ({ ...current, [question.questionId]: event.target.value })) }} placeholder={isVoiceOnly ? 'Chế độ Chỉ Giọng Nói: Hệ thống tự động bóc băng âm thanh cho từng câu hoặc khi nộp bài, không cho phép gõ phím.' : 'Kiểm tra và hiệu đính các thuật ngữ chuyên ngành sau khi thu âm...'} className="mt-4 min-h-24 w-full resize-y rounded-xl border border-[#dce4ee] bg-white p-3 text-sm text-[#0F2854] outline-none focus:border-[#1C4D8D] disabled:bg-[#f0f3f7]" /></div></div></article>)}</div></div></section>
    {sealed && <p className="flex items-center gap-2 text-xs font-semibold text-[#16836a]"><CheckCircle2 className="size-4" />Bản ghi âm đã được tải lên và niêm phong mã băm SHA-256 an toàn.</p>}
    {isSubmitted && <div className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-[#4988C4] bg-white p-4"><p className="text-xs font-medium text-[#0F2854]">Theo dõi trạng thái xử lý bài thi sau khi nộp.</p><button type="button" disabled={busy} onClick={() => void refreshSubmission()} className="rounded-xl border border-[#4988C4] bg-[#F4FBFD] px-3.5 py-2 text-xs font-bold text-[#1C4D8D] transition hover:bg-[#4988C4]/16">Cập nhật trạng thái</button></div>}
    <p className="flex items-start gap-2 text-xs leading-relaxed text-[#5b6b84]"><ShieldAlert className="mt-0.5 size-4 shrink-0 text-[#1C4D8D]" />Bản thu âm ca thi chính thức được niêm phong bảo mật. Kết quả điểm thi sẽ hiển thị tại mục Kết quả sau khi giảng viên hoàn tất hậu kiểm và công bố.</p>
  </div>
}

export function OfficialExamPage() {
  return <OfficialExamLivePage />
}
