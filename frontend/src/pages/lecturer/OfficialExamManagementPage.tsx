import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react'
import {
  CalendarPlus,
  ClipboardList,
  LoaderCircle,
  MessageSquareMore,
  Plus,
  RefreshCw,
  Save,
} from 'lucide-react'
import { useAuth } from '@/hooks/useAuth'
import { courseService } from '@/services/course.service'
import { officialExamService } from '@/services/officialExam.service'
import { CLO_BLOOM_LEVELS, type CloBloomLevel } from '@/types/courseClo.types'
import type { ExamStructure, OfficialExamSession } from '@/types/officialExam.types'
import { cn } from '@/lib/utils'
import { EmptyState } from '@/components/common/EmptyState'
import { toast } from '@/components/common/toastStore'
import { BannerFact, LecturerPageBanner, SectionHeader } from '@/design/ui'
import { ds } from '@/design/system'
import { userMessage } from '@/lib/userMessage'

const LOCKED_FOLLOW_UP_SESSION_STATUSES = new Set([
  'in_progress',
  'grading',
  'auditing',
  'concluded',
])

const emptyStructure = {
  courseId: '',
  name: '',
  totalQuestions: 5,
  durationMinutes: 30,
  isActive: true,
}

const BLOOM_LABELS: Record<CloBloomLevel, string> = {
  Remember: 'Remember (Nhớ)',
  Understand: 'Understand (Hiểu)',
  Apply: 'Apply (Áp dụng)',
  Analyze: 'Analyze (Phân tích)',
  Evaluate: 'Evaluate (Đánh giá)',
  Create: 'Create (Sáng tạo)',
}

interface FollowUpDraft {
  hasFollowUp: boolean
  maxFollowUpQuestions: number
}

function createEmptyBloomCounts(): Record<CloBloomLevel, number> {
  return {
    Remember: 0,
    Understand: 0,
    Apply: 0,
    Analyze: 0,
    Evaluate: 0,
    Create: 0,
  }
}

function formatOfficialExamError(error: unknown, fallback: string) {
  const message = userMessage(error, fallback)
  return message.replace(/^VALIDATION\s*[:\-–—]\s*/i, '').trim() || fallback
}

function parseBloomDistribution(raw?: string | null): Array<[string, number]> {
  if (!raw || typeof raw !== 'string' || !raw.trim()) return []
  try {
    const parsed: unknown = JSON.parse(raw)
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) return []
    return Object.entries(parsed as Record<string, unknown>)
      .filter(([, value]) => typeof value === 'number' && Number.isFinite(value) && value > 0)
      .map(([key, value]) => [key, value as number])
  } catch {
    return []
  }
}

export function OfficialExamManagementPage({ embedded = false }: { embedded?: boolean } = {}) {
  const { user } = useAuth()
  const canManageAcademic = user?.role === 'DepartmentHead'
  const [structures, setStructures] = useState<ExamStructure[]>([])
  const [sessions, setSessions] = useState<OfficialExamSession[]>([])
  const [followUpDrafts, setFollowUpDrafts] = useState<Record<string, FollowUpDraft>>({})
  const [savingSessionId, setSavingSessionId] = useState<string | null>(null)
  const [assignedCourses, setAssignedCourses] = useState<
    Array<{ id: string; code: string; name: string }>
  >([])
  const [structureForm, setStructureForm] = useState(emptyStructure)
  const [bloomCounts, setBloomCounts] = useState<Record<CloBloomLevel, number>>(
    createEmptyBloomCounts,
  )
  const [structureError, setStructureError] = useState('')
  const [followUpError, setFollowUpError] = useState('')
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)

  const bloomSum = useMemo(
    () => CLO_BLOOM_LEVELS.reduce((sum, level) => sum + (bloomCounts[level] || 0), 0),
    [bloomCounts],
  )

  const loadData = useCallback(async () => {
    if (!canManageAcademic) return
    setLoading(true)
    setFollowUpError('')
    try {
      const structuresPromise = Promise.all([
        officialExamService.getExamStructures({ isActive: true }),
        officialExamService.getExamStructures({ isActive: false }),
      ]).then((results) => [...new Map(results.flat().map((item) => [item.id, item])).values()])

      const coursesPromise = courseService.getLecturerCourses()
      const sessionsPromise =
        typeof officialExamService.getSessions === 'function'
          ? officialExamService.getSessions().catch(() => [] as OfficialExamSession[])
          : Promise.resolve([] as OfficialExamSession[])

      const [structureResult, courseResult, sessionResult] = await Promise.all([
        structuresPromise,
        coursesPromise,
        sessionsPromise,
      ])

      const normalizedCourses = Array.isArray(courseResult) ? courseResult : []
      const ownedCourseIds = new Set(normalizedCourses.map((item) => item.id))
      const ownedSessions = (Array.isArray(sessionResult) ? sessionResult : []).filter((item) =>
        ownedCourseIds.has(item.courseId),
      )

      setStructures(Array.isArray(structureResult) ? structureResult : [])
      setAssignedCourses(normalizedCourses)
      setSessions(ownedSessions)
      setFollowUpDrafts(() => {
        const next: Record<string, FollowUpDraft> = {}
        for (const session of ownedSessions) {
          next[session.id] = {
            hasFollowUp: Boolean(session.hasFollowUp),
            maxFollowUpQuestions: Math.min(5, Math.max(1, session.maxFollowUpQuestions || 2)),
          }
        }
        return next
      })

      if (normalizedCourses.length > 0) {
        setStructureForm((current) =>
          current.courseId && normalizedCourses.some((item) => item.id === current.courseId)
            ? current
            : { ...current, courseId: normalizedCourses[0].id },
        )
      }
    } catch (loadError) {
      toast.error(
        formatOfficialExamError(loadError, 'Không tải được dữ liệu học thuật kỳ thi chính thức.'),
      )
    } finally {
      setLoading(false)
    }
  }, [canManageAcademic])

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadData()
    }, 0)
    return () => window.clearTimeout(timer)
  }, [loadData])

  const submitStructure = async (event: FormEvent) => {
    event.preventDefault()
    if (!canManageAcademic) return

    const totalQuestions = Number(structureForm.totalQuestions)
    const durationMinutes = Number(structureForm.durationMinutes)
    if (!structureForm.courseId) {
      setStructureError('Vui lòng chọn môn học phụ trách.')
      return
    }
    if (!structureForm.name.trim()) {
      setStructureError('Vui lòng nhập tên cấu trúc đề thi.')
      return
    }
    if (!Number.isInteger(totalQuestions) || totalQuestions < 1 || totalQuestions > 20) {
      setStructureError('Tổng số câu hỏi phải là số nguyên từ 1 đến 20.')
      return
    }
    if (!Number.isInteger(durationMinutes) || durationMinutes < 5 || durationMinutes > 180) {
      setStructureError('Thời lượng thi phải từ 5 đến 180 phút.')
      return
    }
    const invalidBloom = CLO_BLOOM_LEVELS.some((level) => {
      const count = bloomCounts[level]
      return !Number.isInteger(count) || count < 0
    })
    if (invalidBloom) {
      setStructureError('Số câu ở mỗi bậc Bloom phải là số nguyên không âm.')
      return
    }
    if (bloomSum > totalQuestions) {
      setStructureError('Tổng số câu phân bổ theo bậc Bloom không được vượt quá tổng số câu hỏi.')
      return
    }

    const bloomMap: Record<string, number> = {}
    for (const level of CLO_BLOOM_LEVELS) {
      const count = bloomCounts[level]
      if (count > 0) bloomMap[level] = count
    }

    setStructureError('')
    setBusy(true)
    try {
      await officialExamService.createExamStructure({
        courseId: structureForm.courseId,
        name: structureForm.name.trim(),
        totalQuestions,
        durationMinutes,
        bloomDistribution: JSON.stringify(bloomMap),
        isActive: true,
      })
      setStructureForm((current) => ({
        ...emptyStructure,
        courseId: current.courseId || assignedCourses[0]?.id || '',
      }))
      setBloomCounts(createEmptyBloomCounts())
      toast.success('Đã tạo cấu trúc đề thi.')
      await loadData()
    } catch (submitError) {
      const msg = formatOfficialExamError(
        submitError,
        'Không tạo được cấu trúc đề thi. Vui lòng thử lại.',
      )
      setStructureError(msg)
      toast.error(msg)
    } finally {
      setBusy(false)
    }
  }

  const toggleStructure = async (structure: ExamStructure) => {
    if (!canManageAcademic) return
    setBusy(true)
    try {
      await officialExamService.updateExamStructure(structure.id, {
        name: structure.name,
        totalQuestions: structure.totalQuestions,
        durationMinutes: structure.durationMinutes,
        bloomDistribution: structure.bloomDistribution,
        isActive: !structure.isActive,
        ...(typeof structure.requiredSetCount === 'number'
          ? { requiredSetCount: structure.requiredSetCount }
          : {}),
        ...(structure.slotDefinitions !== undefined
          ? { slotDefinitions: structure.slotDefinitions }
          : {}),
      })
      toast.success(`Đã ${structure.isActive ? 'tắt' : 'bật'} cấu trúc đề thi ${structure.name}.`)
      await loadData()
    } catch (updateError) {
      toast.error(
        formatOfficialExamError(
          updateError,
          'Không cập nhật được cấu trúc đề thi. Vui lòng thử lại.',
        ),
      )
    } finally {
      setBusy(false)
    }
  }

  const saveFollowUpConfig = async (session: OfficialExamSession) => {
    if (
      !canManageAcademic ||
      savingSessionId ||
      typeof officialExamService.configureFollowUp !== 'function'
    ) {
      return
    }
    const draft = followUpDrafts[session.id] ?? {
      hasFollowUp: session.hasFollowUp,
      maxFollowUpQuestions: session.maxFollowUpQuestions || 2,
    }
    const parsedMax = Number(draft.maxFollowUpQuestions)
    if (draft.hasFollowUp && (!Number.isInteger(parsedMax) || parsedMax < 1 || parsedMax > 5)) {
      setFollowUpError('Số câu hỏi phụ tối đa phải là số nguyên từ 1 đến 5.')
      return
    }

    setFollowUpError('')
    setSavingSessionId(session.id)
    try {
      const updated = await officialExamService.configureFollowUp(session.id, {
        hasFollowUp: draft.hasFollowUp,
        maxFollowUpQuestions: draft.hasFollowUp ? parsedMax : 2,
      })
      setSessions((current) =>
        current.map((item) => (item.id === updated.id ? { ...item, ...updated } : item)),
      )
      toast.success(`Đã lưu cấu hình Follow-up cho "${session.title}".`)
    } catch (saveError) {
      const message = formatOfficialExamError(
        saveError,
        'Không lưu được cấu hình Follow-up cho kỳ thi.',
      )
      setFollowUpError(message)
      toast.error(message)
    } finally {
      setSavingSessionId(null)
    }
  }

  if (!canManageAcademic) {
    return (
      <div role="alert" className="rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-4 py-3 text-sm font-semibold text-[#a43d4b]">
        Chỉ Trưởng bộ môn được truy cập cấu trúc đề thi và cấu hình Follow-up học thuật MF-04.
      </div>
    )
  }

  return (
    <div className={embedded ? 'space-y-4' : ds.page}>
      {!embedded && (
        <LecturerPageBanner
          icon={ClipboardList}
          eyebrow="Trưởng bộ môn · MF-03 / MF-04"
          title="Cấu trúc đề thi & Follow-up kỳ thi chính thức"
          description="Quản lý cấu trúc đề theo bậc Bloom cho các môn phụ trách và cấu hình độc quyền câu hỏi phụ (Follow-up 1–5 câu) cho kỳ thi chính thức."
          extras={
            <>
              <BannerFact icon={ClipboardList}>Ma trận Bloom 3NF</BannerFact>
              <BannerFact icon={MessageSquareMore} tone="green">
                Độc quyền Follow-up MF-04
              </BannerFact>
              <BannerFact icon={CalendarPlus} tone="violet">
                Tách biệt Khảo thí
              </BannerFact>
            </>
          }
        />
      )}

      <section className="grid gap-4 lg:grid-cols-[minmax(0,360px)_minmax(0,1fr)]">
        <form onSubmit={submitStructure} className={ds.section}>
          <SectionHeader
            icon={ClipboardList}
            title="Cấu trúc đề thi"
            hint="Cấu hình số câu và bậc Bloom"
          />
          <div className="space-y-3 p-4">
            {structureError ? (
              <div
                role="alert"
                className="rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-3 py-2.5 text-xs font-semibold text-[#c2415b]"
              >
                {structureError}
              </div>
            ) : null}
            <label className="block">
              <span className="mb-1 block text-xs font-bold text-[#0F2854]">Môn học phụ trách</span>
              <select
                required
                aria-label="Môn học"
                className={ds.field}
                value={structureForm.courseId}
                onChange={(e) => {
                  setStructureError('')
                  setStructureForm({ ...structureForm, courseId: e.target.value })
                }}
              >
                <option value="">
                  {assignedCourses.length === 0
                    ? 'Chưa có môn được phân công'
                    : 'Chọn môn học phụ trách'}
                </option>
                {assignedCourses.map((course) => (
                  <option key={course.id} value={course.id}>
                    {course.code} · {course.name}
                  </option>
                ))}
              </select>
            </label>
            <label className="block">
              <span className="mb-1 block text-xs font-bold text-[#0F2854]">Tên cấu trúc</span>
              <input
                required
                className={ds.field}
                placeholder="Tên cấu trúc"
                value={structureForm.name}
                onChange={(e) => {
                  setStructureError('')
                  setStructureForm({ ...structureForm, name: e.target.value })
                }}
              />
            </label>
            <div className="grid grid-cols-2 gap-2">
              <label className="block">
                <span className="mb-1 block text-xs font-bold text-[#0F2854]">Số câu (1–20)</span>
                <input
                  required
                  type="number"
                  min={1}
                  max={20}
                  step={1}
                  className={ds.field}
                  placeholder="Số câu"
                  value={structureForm.totalQuestions}
                  onChange={(e) => {
                    setStructureError('')
                    setStructureForm({ ...structureForm, totalQuestions: Number(e.target.value) })
                  }}
                />
              </label>
              <label className="block">
                <span className="mb-1 block text-xs font-bold text-[#0F2854]">Phút (5–180)</span>
                <input
                  required
                  type="number"
                  min={5}
                  max={180}
                  step={1}
                  className={ds.field}
                  placeholder="Phút"
                  value={structureForm.durationMinutes}
                  onChange={(e) => {
                    setStructureError('')
                    setStructureForm({ ...structureForm, durationMinutes: Number(e.target.value) })
                  }}
                />
              </label>
            </div>
            <div className="space-y-2 rounded-xl border border-[#82aedb]/45 bg-[#F8FBFD] p-3">
              <div className="flex items-center justify-between gap-2">
                <span className="text-xs font-bold text-[#0F2854]">Phân bổ theo bậc Bloom</span>
                <span
                  className={cn(
                    'text-xs font-bold tabular-nums',
                    bloomSum > structureForm.totalQuestions ? 'text-[#c2415b]' : 'text-[#1C4D8D]',
                  )}
                >
                  Phân bổ Bloom: {bloomSum} / {structureForm.totalQuestions} câu
                </span>
              </div>
              <div className="grid grid-cols-2 gap-2">
                {CLO_BLOOM_LEVELS.map((level) => (
                  <label key={level} className="block">
                    <span className="mb-1 block text-[11px] font-semibold text-[#5b6b84]">
                      {BLOOM_LABELS[level]}
                    </span>
                    <input
                      type="number"
                      min={0}
                      step={1}
                      aria-label={`Bloom ${level}`}
                      className={cn(ds.field, 'min-h-9 py-1.5 text-xs')}
                      value={bloomCounts[level]}
                      onChange={(e) => {
                        setStructureError('')
                        const parsed = Math.max(0, Math.floor(Number(e.target.value) || 0))
                        setBloomCounts((current) => ({ ...current, [level]: parsed }))
                      }}
                    />
                  </label>
                ))}
              </div>
            </div>
            <button disabled={busy} className={ds.primaryButton}>
              <Plus className="size-4" /> Tạo cấu trúc
            </button>
          </div>
        </form>

        <section className={ds.section}>
          <SectionHeader
            icon={ClipboardList}
            title="Danh sách cấu trúc"
            hint="Bật/tắt cấu trúc đề đang có"
            action={
              <button
                type="button"
                className={ds.outlineButton}
                onClick={() => void loadData()}
                disabled={loading}
              >
                <RefreshCw className={cn('size-4', loading && 'animate-spin')} /> Làm mới
              </button>
            }
          />
          <div className="grid gap-3 p-4 md:grid-cols-2">
            {structures.map((structure) => {
              const bloomEntries = parseBloomDistribution(structure.bloomDistribution)
              return (
                <article
                  key={structure.id}
                  className="rounded-xl border border-[#82aedb]/55 p-4"
                >
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <h3 className="font-bold text-[#0F2854]">{structure.name}</h3>
                      <p className="mt-1 text-xs text-[#687a91]">
                        {structure.courseCode} · {structure.totalQuestions} câu ·{' '}
                        {structure.durationMinutes} phút
                      </p>
                    </div>
                    <span className={ds.infoChip}>{structure.isActive ? 'ACTIVE' : 'INACTIVE'}</span>
                  </div>
                  <div className="mt-2.5 flex flex-wrap gap-1.5">
                    {bloomEntries.length > 0 ? (
                      bloomEntries.map(([level, count]) => (
                        <span
                          key={level}
                          className="rounded-full border border-[#82aedb]/45 bg-[#F4FBFD] px-2 py-0.5 text-[11px] font-semibold text-[#1C4D8D]"
                        >
                          {level}: {count}
                        </span>
                      ))
                    ) : (
                      <span className="text-xs text-[#687a91]">Phân bổ linh hoạt</span>
                    )}
                  </div>
                  <button
                    type="button"
                    disabled={busy}
                    onClick={() => void toggleStructure(structure)}
                    className="mt-3 rounded-lg border border-[#82aedb]/55 px-3 py-2 text-xs font-bold text-[#1C4D8D]"
                  >
                    {structure.isActive ? 'Tắt cấu trúc' : 'Bật cấu trúc'}
                  </button>
                </article>
              )
            })}
            {structures.length === 0 ? (
              <EmptyState
                compact
                icon={ClipboardList}
                title="Chưa có cấu trúc đề thi"
                description="Tạo cấu trúc đề thi mới ở cột bên trái để làm chuẩn cho thi thử và thi chính thức."
              />
            ) : null}
          </div>
        </section>
      </section>

      <section className={ds.section}>
        <SectionHeader
          icon={MessageSquareMore}
          title="Cấu hình Follow-up Kỳ thi chính thức (MF-04)"
          badge={sessions.length}
          hint="Chỉ Trưởng bộ môn được bật/tắt câu hỏi phụ (1–5 câu, mặc định 2) trước khi kỳ thi bắt đầu."
          action={
            <button
              type="button"
              className={ds.outlineButton}
              onClick={() => void loadData()}
              disabled={loading}
            >
              <RefreshCw className={cn('size-4', loading && 'animate-spin')} /> Làm mới
            </button>
          }
        />
        <div className="space-y-3 p-4">
          {followUpError ? (
            <div
              role="alert"
              className="rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-3 py-2.5 text-xs font-semibold text-[#c2415b]"
            >
              {followUpError}
            </div>
          ) : null}

          {loading ? (
            <div className="flex items-center justify-center p-10">
              <LoaderCircle className="size-7 animate-spin text-[#1C4D8D]" />
            </div>
          ) : sessions.length === 0 ? (
            <EmptyState
              compact
              icon={MessageSquareMore}
              title="Chưa có kỳ thi chính thức thuộc môn phụ trách"
              description="Khi Khảo thí khởi tạo kỳ thi FE/RE cho các môn bạn phụ trách, cấu hình Follow-up sẽ hiển thị tại đây."
            />
          ) : (
            <div className="grid gap-3 md:grid-cols-2">
              {sessions.map((session) => {
                const draft = followUpDrafts[session.id] ?? {
                  hasFollowUp: session.hasFollowUp,
                  maxFollowUpQuestions: session.maxFollowUpQuestions || 2,
                }
                const isLockedStatus = LOCKED_FOLLOW_UP_SESSION_STATUSES.has(
                  session.status.toLowerCase(),
                )
                const isSaving = savingSessionId === session.id
                const attemptType = session.examAttemptType === 'RE' ? 'RE' : 'FE'

                return (
                  <article
                    key={session.id}
                    className="rounded-xl border border-[#82aedb]/55 bg-[#F8FBFD] p-4"
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div>
                        <div className="flex items-center gap-2">
                          <span className={ds.infoChip}>{attemptType}</span>
                          <h3 className="font-bold text-[#0F2854]">
                            {session.title || 'Kỳ thi chính thức'}
                          </h3>
                        </div>
                        <p className="mt-1 text-xs text-[#687a91]">
                          Ngày thi: {session.examDate} · Trạng thái: {session.status}
                        </p>
                      </div>
                      <span className={draft.hasFollowUp ? ds.successChip : ds.infoChip}>
                        {draft.hasFollowUp
                          ? `Follow-up: ${draft.maxFollowUpQuestions} câu`
                          : 'Tắt Follow-up'}
                      </span>
                    </div>

                    <div className="mt-3 flex flex-wrap items-end gap-3 border-t border-[#d9e8f2] pt-3">
                      <label className="inline-flex min-h-10 items-center gap-2 text-xs font-bold text-[#0F2854]">
                        <input
                          type="checkbox"
                          aria-label={`Bật Follow-up cho ${session.title}`}
                          checked={draft.hasFollowUp}
                          disabled={isLockedStatus || isSaving}
                          onChange={(event) => {
                            const checked = event.target.checked
                            setFollowUpError('')
                            setFollowUpDrafts((current) => ({
                              ...current,
                              [session.id]: {
                                hasFollowUp: checked,
                                maxFollowUpQuestions: current[session.id]?.maxFollowUpQuestions ?? 2,
                              },
                            }))
                          }}
                        />
                        Bật câu hỏi phụ (Follow-up)
                      </label>

                      <label className="grid gap-1 text-xs font-bold text-[#52657d]">
                        Số câu phụ (1–5)
                        <input
                          type="number"
                          min={1}
                          max={5}
                          step={1}
                          aria-label={`Số câu Follow-up cho ${session.title}`}
                          className={cn(ds.field, 'min-h-9 w-28 py-1.5 text-xs')}
                          value={draft.maxFollowUpQuestions}
                          disabled={!draft.hasFollowUp || isLockedStatus || isSaving}
                          onChange={(event) => {
                            setFollowUpError('')
                            const parsed = Number(event.target.value)
                            setFollowUpDrafts((current) => ({
                              ...current,
                              [session.id]: {
                                hasFollowUp: current[session.id]?.hasFollowUp ?? draft.hasFollowUp,
                                maxFollowUpQuestions: Number.isFinite(parsed) ? parsed : 2,
                              },
                            }))
                          }}
                        />
                      </label>

                      <button
                        type="button"
                        disabled={isLockedStatus || isSaving}
                        onClick={() => void saveFollowUpConfig(session)}
                        className={ds.primaryButton}
                      >
                        {isSaving ? (
                          <LoaderCircle className="size-4 animate-spin" />
                        ) : (
                          <Save className="size-4" />
                        )}
                        Lưu Follow-up
                      </button>
                    </div>

                    {isLockedStatus ? (
                      <p className="mt-2 text-[11px] font-medium text-[#7c8ca1]">
                        Kỳ thi đã chuyển sang trạng thái {session.status} — khóa thay đổi Follow-up.
                      </p>
                    ) : null}
                  </article>
                )
              })}
            </div>
          )}
        </div>
      </section>
    </div>
  )
}
