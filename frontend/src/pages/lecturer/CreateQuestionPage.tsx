import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import {
  AlertCircle,
  ArrowLeft,
  Bot,
  Check,
  CheckCircle2,
  ChevronRight,
  CircleHelp,
  FilePenLine,
  LoaderCircle,
  PencilLine,
  Plus,
  RotateCcw,
  Save,
  Send,
  Sparkles,
  Trash2,
  WandSparkles,
  X,
} from 'lucide-react'
import { FilterDropMenu } from '@/components/common/FilterDropMenu'
import { QuestionImage } from '@/components/question/QuestionImage'
import { useAuth } from '@/hooks/useAuth'
import { courseService } from '@/services/course.service'
import { questionService } from '@/services/question.service'
import type {
  BloomLevel,
  GenerateQuestionsFromFlmResponse,
  GeneratedQuestionDraft,
  QuestionDifficulty,
  QuestionSource,
  RubricCriterionDraft,
  UsageScope,
} from '@/types/rubric.types'
import { cn } from '@/lib/utils'
import { ds } from '@/design/system'

export interface CreatePracticeQuestionFormProps {
  presentation?: 'page' | 'dialog'
  onClose?: () => void
  onCreated?: (questionId?: string) => void
}

type StudioMode = 'manual' | 'ai'

interface CriterionForm extends RubricCriterionDraft {
  localId: string
}

const BLOOM_LEVELS: BloomLevel[] = [
  'Remember',
  'Understand',
  'Apply',
  'Analyze',
  'Evaluate',
  'Create',
]

const DIFFICULTIES: Array<{ value: QuestionDifficulty; label: string }> = [
  { value: 'easy', label: 'Dễ' },
  { value: 'medium', label: 'Trung bình' },
  { value: 'hard', label: 'Khó' },
]

function createCriterion(maxScore = 5): CriterionForm {
  return {
    localId: crypto.randomUUID(),
    criterionName: '',
    maxScore,
    weight: maxScore / 10,
    bloomLevel: 'Understand',
    description: '',
  }
}

function getErrorMessage(error: unknown) {
  if (typeof error === 'object' && error !== null && 'detail' in error) {
    const detail = (error as { detail?: unknown }).detail
    if (typeof detail === 'string' && detail.trim()) return detail
  }
  return 'Không thể hoàn tất yêu cầu. Vui lòng kiểm tra dữ liệu và thử lại.'
}

function splitTerms(value: string) {
  return value
    .split(/[\n,;]/)
    .map((item) => item.trim())
    .filter(Boolean)
}

function isUuid(value: string) {
  return /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(value)
}

function getGeneratedQuestionValidity(question: GeneratedQuestionDraft) {
  const total = question.rubric.criteria.reduce((sum, criterion) => sum + criterion.maxScore, 0)
  return Math.abs(total - 10) < 0.001 && question.modelAnswer.trim().length >= 50
}

function getDifficultyLabel(value: QuestionDifficulty) {
  return DIFFICULTIES.find((item) => item.value === value)?.label ?? value
}

export function CreatePracticeQuestionForm({
  presentation = 'page',
  onClose,
  onCreated,
}: CreatePracticeQuestionFormProps) {
  const { user } = useAuth()
  const isDialog = presentation === 'dialog'
  const [mode, setMode] = useState<StudioMode>('manual')
  const [courseId, setCourseId] = useState('')
  /** Luồng này cố định kho luyện tập (practice_questions). */
  const usageScope: UsageScope = 'practice'
  const [availableCourses, setAvailableCourses] = useState<Array<{ id: string; code: string; name: string }>>([])
  const [isSubmittingReview, setIsSubmittingReview] = useState(false)

  useEffect(() => {
    let active = true
    void courseService
      .getLecturerCourses()
      .then((courses) => {
        if (!active) return
        setAvailableCourses(courses)
      })
      .catch(() => {
        // Fallback: vẫn cho nhập UUID nếu API môn phân công lỗi
      })
    return () => {
      active = false
    }
  }, [])

  const courseOptions = useMemo(
    () => [
      { value: '', label: 'Chọn môn học' },
      ...availableCourses.map((course) => ({
        value: course.id,
        label: `${course.code} · ${course.name}`,
      })),
    ],
    [availableCourses],
  )
  const [title, setTitle] = useState('')
  const [content, setContent] = useState('')
  const [sampleAnswer, setSampleAnswer] = useState('')
  const [keyPointsInput, setKeyPointsInput] = useState('')
  const [difficulty, setDifficulty] = useState<QuestionDifficulty>('medium')
  const [bloomLevel, setBloomLevel] = useState<BloomLevel>('Understand')
  const [rubricName, setRubricName] = useState('')
  const [rubricDescription, setRubricDescription] = useState('')
  const [manualSource, setManualSource] = useState<QuestionSource>('manual')
  const [criteria, setCriteria] = useState<CriterionForm[]>([
    createCriterion(5),
    createCriterion(5),
  ])
  const [manualError, setManualError] = useState('')
  const [manualNotice, setManualNotice] = useState('')
  const [isSaving, setIsSaving] = useState(false)
  const [savedQuestionId, setSavedQuestionId] = useState('')

  const [cloInput, setCloInput] = useState('')
  const [topicsInput, setTopicsInput] = useState('')
  const [numberOfQuestions, setNumberOfQuestions] = useState(3)
  const [targetBloomLevels, setTargetBloomLevels] = useState<BloomLevel[]>([
    'Understand',
    'Apply',
    'Analyze',
  ])
  const [easyCount, setEasyCount] = useState(1)
  const [mediumCount, setMediumCount] = useState(1)
  const [hardCount, setHardCount] = useState(1)
  const [generation, setGeneration] = useState<GenerateQuestionsFromFlmResponse | null>(null)
  const [selectedGenerated, setSelectedGenerated] = useState<Set<string>>(new Set())
  const [savedGeneratedIds, setSavedGeneratedIds] = useState<Set<string>>(new Set())
  const [editingGeneratedTempId, setEditingGeneratedTempId] = useState('')
  const [aiError, setAiError] = useState('')
  const [aiNotice, setAiNotice] = useState('')
  const [isGenerating, setIsGenerating] = useState(false)
  const [isSavingGenerated, setIsSavingGenerated] = useState(false)

  const totalRubricScore = useMemo(
    () => criteria.reduce((sum, criterion) => sum + criterion.maxScore, 0),
    [criteria],
  )
  const rubricIsValid = Math.abs(totalRubricScore - 10) < 0.001
  const sampleAnswerIsValid = sampleAnswer.trim().length >= 50
  const difficultyTotal = easyCount + mediumCount + hardCount

  const updateCriterion = <Key extends keyof CriterionForm>(
    localId: string,
    key: Key,
    value: CriterionForm[Key],
  ) => {
    setCriteria((current) =>
      current.map((criterion) => {
        if (criterion.localId !== localId) return criterion
        const next = { ...criterion, [key]: value }
        if (key === 'maxScore') next.weight = Number(value) / 10
        return next
      }),
    )
  }

  const addCriterion = () => setCriteria((current) => [...current, createCriterion(0)])

  const removeCriterion = (localId: string) => {
    setCriteria((current) =>
      current.length <= 2 ? current : current.filter((criterion) => criterion.localId !== localId),
    )
  }

  const resetManualDraft = () => {
    setTitle('')
    setContent('')
    setSampleAnswer('')
    setKeyPointsInput('')
    setDifficulty('medium')
    setBloomLevel('Understand')
    setRubricName('')
    setRubricDescription('')
    setCriteria([createCriterion(5), createCriterion(5)])
    setManualSource('manual')
    setEditingGeneratedTempId('')
    setSavedQuestionId('')
    setManualError('')
    setManualNotice('')
  }

  const editGeneratedDraft = (question: GeneratedQuestionDraft) => {
    setTitle(question.title)
    setContent(question.content)
    setSampleAnswer(question.modelAnswer)
    setKeyPointsInput(question.keyPoints.join('\n'))
    setDifficulty(question.difficulty)
    setBloomLevel(question.bloomLevel)
    setRubricName(question.rubric.name)
    setRubricDescription(question.rubric.description ?? '')
    setCriteria(
      question.rubric.criteria.map((criterion) => ({
        ...criterion,
        localId: crypto.randomUUID(),
        description: criterion.description ?? '',
        weight: criterion.maxScore / 10,
      })),
    )
    setManualSource('flm_api')
    setEditingGeneratedTempId(question.tempId)
    setSelectedGenerated((current) => {
      const next = new Set(current)
      next.delete(question.tempId)
      return next
    })
    setSavedQuestionId('')
    setManualError('')
    setManualNotice('Đã nạp đề xuất AI. Hãy rà soát và chỉnh sửa trước khi lưu bản nháp.')
    setMode('manual')
    window.scrollTo({
      top: 0,
      behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth',
    })
  }

  const validateManualForm = () => {
    if (!isUuid(user?.id ?? '')) {
      return 'Phiên đăng nhập không có mã người dùng hợp lệ. Vui lòng đăng xuất và đăng nhập lại.'
    }
    if (!isUuid(courseId.trim())) return 'Course ID phải là một UUID hợp lệ.'
    if (!title.trim() || !content.trim()) return 'Vui lòng nhập đầy đủ tiêu đề và nội dung câu hỏi.'
    if (!sampleAnswerIsValid) return 'Model Answer bắt buộc có ít nhất 50 ký tự.'
    if (!rubricName.trim()) return 'Vui lòng đặt tên cho Barem Rubric.'
    if (criteria.length < 2) return 'Barem Rubric phải có ít nhất hai tiêu chí.'
    if (criteria.some((criterion) => !criterion.criterionName.trim() || criterion.maxScore <= 0)) {
      return 'Mỗi tiêu chí phải có tên và điểm tối đa lớn hơn 0.'
    }
    if (!rubricIsValid) return 'Tổng điểm các tiêu chí phải bằng chính xác 10.0.'
    return ''
  }

  const saveManualDraft = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (isSaving || savedQuestionId) return
    const validationError = validateManualForm()
    if (validationError) {
      setManualError(validationError)
      return
    }

    setManualError('')
    setManualNotice('')
    setIsSaving(true)
    try {
      const result = await questionService.createPracticeQuestion({
        courseId: courseId.trim(),
        title: title.trim(),
        content: content.trim(),
        sampleAnswer: sampleAnswer.trim(),
        keyPoints: splitTerms(keyPointsInput),
        difficulty,
        bloomLevel,
        source: manualSource,
        usageScope: 'practice',
        hasFollowUp: false,
        followUpPrompt: null,
        lecturerId: isUuid(user?.id ?? '') ? user?.id : undefined,
        rubric: {
          name: rubricName.trim(),
          description: rubricDescription.trim() || null,
          totalMaxScore: 10,
          criteria: criteria.map((criterion, index) => ({
            criterionName: criterion.criterionName.trim(),
            maxScore: criterion.maxScore,
            bloomLevel: criterion.bloomLevel,
            description: criterion.description.trim(),
            weight: criterion.maxScore / 10,
            orderIndex: index + 1,
          })),
        },
      })
      setSavedQuestionId(result.questionId)
      if (editingGeneratedTempId) {
        setSavedGeneratedIds((current) => new Set([...current, editingGeneratedTempId]))
      }
      setManualNotice(
        `Đã lưu bản nháp luyện tập. Mã câu hỏi: ${result.questionId}. Bạn có thể gửi Trưởng Bộ môn duyệt ngay bên dưới.`,
      )
      onCreated?.(result.questionId)
    } catch (saveError) {
      setManualError(getErrorMessage(saveError))
    } finally {
      setIsSaving(false)
    }
  }

  const submitSavedDraftForReview = async () => {
    if (!savedQuestionId || !isUuid(courseId.trim()) || isSubmittingReview) return
    if (!isUuid(user?.id ?? '')) {
      setManualError(
        'Phiên đăng nhập không có mã người dùng hợp lệ. Vui lòng đăng xuất và đăng nhập lại.',
      )
      return
    }

    setIsSubmittingReview(true)
    setManualError('')
    setManualNotice('')
    try {
      const result = await questionService.batchSubmitForReview({
        courseId: courseId.trim(),
        questionIds: [savedQuestionId],
        submissionNotes: 'Giảng viên gửi câu hỏi luyện tập lên Trưởng Bộ môn thẩm định.',
        lecturerId: user?.id,
      })
      setManualNotice(
        result.message ||
          'Đã gửi câu hỏi luyện tập lên Trưởng Bộ môn. Trạng thái: Chờ duyệt (SUBMITTED_FOR_REVIEW).',
      )
    } catch (submitError) {
      setManualError(getErrorMessage(submitError))
    } finally {
      setIsSubmittingReview(false)
    }
  }

  const toggleBloomLevel = (level: BloomLevel) => {
    setTargetBloomLevels((current) =>
      current.includes(level)
        ? current.length === 1
          ? current
          : current.filter((item) => item !== level)
        : [...current, level],
    )
  }

  const generateQuestions = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!isUuid(user?.id ?? '')) {
      setAiError(
        'Phiên đăng nhập không có mã người dùng hợp lệ. Vui lòng đăng xuất và đăng nhập lại.',
      )
      return
    }
    if (!isUuid(courseId.trim())) {
      setAiError('Course ID phải là một UUID hợp lệ.')
      return
    }
    if (splitTerms(cloInput).length === 0 || splitTerms(topicsInput).length === 0) {
      setAiError('Vui lòng cung cấp ít nhất một CLO và một chủ đề.')
      return
    }
    if (difficultyTotal !== numberOfQuestions) {
      setAiError('Tổng số câu Dễ + Trung bình + Khó phải bằng số câu cần sinh.')
      return
    }

    setAiError('')
    setAiNotice('')
    setGeneration(null)
    setSelectedGenerated(new Set())
    setSavedGeneratedIds(new Set())
    setEditingGeneratedTempId('')
    setIsGenerating(true)
    try {
      const result = await questionService.generateFromFlm({
        courseId: courseId.trim(),
        selectedCloCodes: splitTerms(cloInput),
        topics: splitTerms(topicsInput),
        numberOfQuestions,
        usageScope,
        targetBloomLevels,
        difficultyDistribution: {
          easy: easyCount,
          medium: mediumCount,
          hard: hardCount,
        },
        requesterUserId: isUuid(user?.id ?? '') ? user?.id : undefined,
      })
      setGeneration(result)
      setSelectedGenerated(
        new Set(
          result.questions
            .filter((question) => getGeneratedQuestionValidity(question))
            .map((question) => question.tempId),
        ),
      )
    } catch (generationError) {
      setAiError(getErrorMessage(generationError))
    } finally {
      setIsGenerating(false)
    }
  }

  const toggleGeneratedQuestion = (question: GeneratedQuestionDraft) => {
    if (!getGeneratedQuestionValidity(question) || savedGeneratedIds.has(question.tempId)) return
    setSelectedGenerated((current) => {
      const next = new Set(current)
      if (next.has(question.tempId)) next.delete(question.tempId)
      else next.add(question.tempId)
      return next
    })
  }

  const saveGeneratedDrafts = async () => {
    if (!generation || selectedGenerated.size === 0) return
    if (!isUuid(user?.id ?? '')) {
      setAiError(
        'Phiên đăng nhập không có mã người dùng hợp lệ. Vui lòng đăng xuất và đăng nhập lại.',
      )
      return
    }
    setAiError('')
    setAiNotice('')
    setIsSavingGenerated(true)

    const selected = generation.questions.filter(
      (question) =>
        selectedGenerated.has(question.tempId) && !savedGeneratedIds.has(question.tempId),
    )
    if (selected.length === 0) {
      setIsSavingGenerated(false)
      return
    }
    let savedCount = 0
    const failures: string[] = []
    const successfulIds = new Set<string>()

    for (const question of selected) {
      try {
        await questionService.createPracticeQuestion({
          courseId: generation.courseId,
          title: question.title,
          content: question.content,
          sampleAnswer: question.modelAnswer,
          keyPoints: question.keyPoints,
          difficulty: question.difficulty,
          bloomLevel: question.bloomLevel,
          source: 'flm_api',
          usageScope: 'practice',
          hasFollowUp: false,
          followUpPrompt: null,
          lecturerId: isUuid(user?.id ?? '') ? user?.id : undefined,
          rubric: {
            ...question.rubric,
            totalMaxScore: 10,
            criteria: question.rubric.criteria.map((criterion, index) => ({
              ...criterion,
              weight: criterion.maxScore / 10,
              orderIndex: index + 1,
            })),
          },
        })
        savedCount += 1
        successfulIds.add(question.tempId)
      } catch (saveError) {
        failures.push(`${question.title}: ${getErrorMessage(saveError)}`)
      }
    }

    if (savedCount > 0) {
      setAiNotice(
        `Đã lưu ${savedCount}/${selected.length} câu hỏi AI vào kho luyện tập (DRAFT). Tiếp tục gửi duyệt tại Ngân hàng câu hỏi luyện tập.`,
      )
      setSavedGeneratedIds((current) => new Set([...current, ...successfulIds]))
      setSelectedGenerated(
        (current) => new Set([...current].filter((tempId) => !successfulIds.has(tempId))),
      )
      onCreated?.()
    }
    if (failures.length > 0) {
      setAiError(`Không lưu được ${failures.length} câu hỏi. ${failures[0]}`)
    }
    setIsSavingGenerated(false)
  }

  const modeToggle = isDialog ? (
    <div
      className="grid w-full grid-cols-2 gap-1 rounded-2xl border border-[#4988C4]/50 bg-white/80 p-1"
      role="tablist"
      aria-label="Chế độ tạo câu hỏi"
    >
      <button
        type="button"
        role="tab"
        aria-selected={mode === 'manual'}
        onClick={() => setMode('manual')}
        className={cn(
          'inline-flex min-h-11 items-center justify-center gap-2 rounded-[14px] px-3 text-[15px] font-bold transition',
          mode === 'manual'
            ? 'bg-[#1C4D8D] text-white shadow-[0_8px_18px_rgba(28,77,141,0.28)]'
            : 'text-[#5f7088] hover:bg-[#F4FBFD] hover:text-[#0F2854]',
        )}
      >
        <FilePenLine className="size-4 shrink-0" />
        Soạn thủ công
      </button>
      <button
        type="button"
        role="tab"
        aria-selected={mode === 'ai'}
        onClick={() => setMode('ai')}
        className={cn(
          'inline-flex min-h-11 items-center justify-center gap-2 rounded-[14px] px-3 text-[15px] font-bold transition',
          mode === 'ai'
            ? 'bg-[#1C4D8D] text-white shadow-[0_8px_18px_rgba(28,77,141,0.28)]'
            : 'text-[#5f7088] hover:bg-[#F4FBFD] hover:text-[#0F2854]',
        )}
      >
        <WandSparkles className="size-4 shrink-0" />
        AI từ FLM
      </button>
    </div>
  ) : (
    <div className="flex rounded-xl border border-white/30 bg-white/10 p-1">
      <button
        type="button"
        onClick={() => setMode('manual')}
        className={cn(
          'inline-flex min-h-10 items-center gap-2 rounded-lg px-3 text-xs font-bold transition focus-visible:outline-2 focus-visible:outline-white',
          mode === 'manual' ? 'bg-white text-[#1C4D8D] shadow-sm' : 'text-white/80 hover:text-white',
        )}
        aria-pressed={mode === 'manual'}
      >
        <FilePenLine className="size-3.5" />
        Soạn thủ công
      </button>
      <button
        type="button"
        onClick={() => setMode('ai')}
        className={cn(
          'inline-flex min-h-10 items-center gap-2 rounded-lg px-3 text-xs font-bold transition focus-visible:outline-2 focus-visible:outline-white',
          mode === 'ai' ? 'bg-white text-[#1C4D8D] shadow-sm' : 'text-white/80 hover:text-white',
        )}
        aria-pressed={mode === 'ai'}
      >
        <WandSparkles className="size-3.5" />
        AI từ FLM
      </button>
    </div>
  )

  return (
    <div className={cn(!isDialog && ds.page, isDialog && 'flex min-h-0 flex-1 flex-col')}>
      {isDialog ? (
        <header className="shrink-0 space-y-3 border-b border-[#4988C4]/45 bg-[linear-gradient(180deg,#D7F1F8_0%,#F4FBFD_100%)] px-4 py-4 sm:px-5">
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <div className="flex flex-wrap items-center gap-2">
                <p className="text-[11px] font-bold tracking-[0.08em] text-[#1C4D8D] uppercase">
                  MF-03 · Kho luyện tập
                </p>
                <span className="rounded-full border border-[#b7e4cf] bg-[#e8f8f1] px-2.5 py-0.5 text-[11px] font-bold text-[#16836a]">
                  practice_questions
                </span>
              </div>
              <h2 className="mt-1.5 text-xl font-bold tracking-[-0.02em] text-[#0F2854]">
                Tạo câu hỏi luyện tập
              </h2>
              <p className="mt-1 text-sm text-[#5b6b84]">
                Soạn thủ công hoặc nhờ AI đề xuất · lưu DRAFT · gửi Trưởng BM duyệt.
              </p>
            </div>
            <button
              type="button"
              aria-label="Đóng"
              onClick={onClose}
              className="flex size-9 shrink-0 items-center justify-center rounded-xl border border-[#4988C4]/45 bg-white text-[#5b6b84] transition hover:bg-white hover:text-[#0F2854]"
            >
              <X className="size-4" />
            </button>
          </div>
          {modeToggle}
        </header>
      ) : (
        <header className={cn(ds.banner, 'p-5 sm:p-6')}>
          <Link
            to="/question-bank"
            className="inline-flex min-h-9 items-center gap-1.5 rounded-lg text-xs font-bold text-white/80 transition hover:text-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white"
          >
            <ArrowLeft className="size-3.5" aria-hidden="true" />
            Quay lại ngân hàng đề
          </Link>
          <div className="mt-3 flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
            <div>
              <p className={cn(ds.bannerChip, 'w-fit')}>
                <Sparkles className="size-3.5" aria-hidden="true" />
                Kho luyện tập · MF-03 / R8
              </p>
              <h1 className={ds.bannerTitle}>Tạo câu hỏi luyện tập & Rubric 10.0</h1>
              <p className={ds.bannerText}>
                Soạn câu hỏi vào kho <strong className="text-white">practice_questions</strong>, lưu
                DRAFT rồi gửi Trưởng Bộ môn duyệt trước khi dùng cho luyện tập / thi thử / ca thi trên
                lớp.
              </p>
            </div>
            {modeToggle}
          </div>
        </header>
      )}

      <div
        className={cn(
          isDialog && 'min-h-0 flex-1 space-y-4 overflow-y-auto bg-[#F8FBFD] p-4 sm:p-5',
          isDialog && mode === 'manual' && 'pb-28',
        )}
      >
      <section
        className={cn(
          'rounded-2xl border border-[#4988C4]/45 p-4',
          isDialog ? 'bg-white shadow-sm' : 'bg-[linear-gradient(135deg,#BDE8F5,#BDE8F5)]',
        )}
      >
        <div
          className={cn(
            'grid gap-3',
            isDialog ? 'sm:grid-cols-1' : 'max-w-4xl sm:grid-cols-[minmax(0,1fr)_auto] sm:items-end',
          )}
        >
          <div className="block">
            <span className="flex items-center gap-2 text-xs font-bold tracking-[0.07em] text-[#5b6b84] uppercase">
              Môn học
              <span className="tracking-normal text-[#b03a46] normal-case">Bắt buộc</span>
            </span>
            <FilterDropMenu
              label="Chọn môn học"
              value={courseId}
              onChange={setCourseId}
              options={courseOptions}
              className="mt-1.5 w-full"
            />
          </div>
          {!isDialog && (
            <div className="inline-flex min-h-10 items-center rounded-full border border-[#b7e4cf] bg-[#e8f8f1] px-4 text-xs font-bold text-[#16836a]">
              Kho luyện tập (practice)
            </div>
          )}
        </div>
        {!isDialog && (
          <p className="mt-2 flex items-start gap-1.5 text-xs leading-relaxed text-[#5b6b84]">
            <CircleHelp className="mt-0.5 size-3.5 shrink-0 text-[#6485b2]" aria-hidden="true" />
            Chọn môn học được phân công. Câu hỏi sẽ lưu ở trạng thái DRAFT trong kho luyện tập.
          </p>
        )}
      </section>

      {mode === 'manual' ? (
        <form
          onSubmit={saveManualDraft}
          id="create-practice-manual-form"
          className={cn(
            'grid gap-4',
            isDialog ? 'lg:grid-cols-[minmax(0,1fr)_280px]' : 'xl:grid-cols-[minmax(0,1fr)_330px]',
          )}
        >
          <div className="space-y-4">
            <section className="rounded-2xl border border-[#4988C4]/45 bg-white p-4 shadow-[0_8px_28px_rgba(31,61,105,0.045)] sm:p-5">
              <div className="flex items-center gap-3">
                <span className="flex size-8 items-center justify-center rounded-lg bg-[#4988C4]/16 font-mono text-xs font-black text-[#1C4D8D]">
                  01
                </span>
                <div>
                  <h2 className="text-base font-bold text-[#0F2854] sm:text-lg">Nội dung học thuật</h2>
                  <p className="mt-0.5 text-xs text-[#5b6b84]">
                    Câu hỏi, đáp án chuẩn và các ý chính để AI đối chiếu.
                  </p>
                </div>
              </div>

              <div
                className={cn(
                  'mt-5 grid gap-4',
                  isDialog ? 'sm:grid-cols-1' : 'sm:grid-cols-3',
                )}
              >
                <label className={cn('block', !isDialog && 'sm:col-span-2')}>
                  <span className="text-xs font-bold text-[#5b6b84] uppercase">Tiêu đề</span>
                  <input
                    value={title}
                    onChange={(event) => setTitle(event.target.value)}
                    maxLength={300}
                    placeholder="Tên ngắn gọn giúp nhận diện câu hỏi"
                    className="mt-1.5 min-h-11 w-full rounded-xl border border-[#4988C4]/45 bg-white px-3 text-sm text-[#0F2854] transition outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30"
                  />
                </label>
                {!isDialog && (
                  <div className="block">
                    <span className="text-xs font-bold text-[#5b6b84] uppercase">Kho đích</span>
                    <div className="mt-1.5 flex min-h-11 items-center rounded-xl border border-[#b7e4cf] bg-[#e8f8f1] px-3 text-sm font-bold text-[#16836a]">
                      Luyện tập (practice)
                    </div>
                  </div>
                )}
              </div>

              <label className="mt-4 block">
                <span className="text-xs font-bold text-[#5b6b84] uppercase">
                  Nội dung câu hỏi
                </span>
                <textarea
                  value={content}
                  onChange={(event) => setContent(event.target.value)}
                  placeholder="Viết đầy đủ yêu cầu mà sinh viên cần trình bày..."
                  className="mt-1.5 min-h-28 w-full resize-y rounded-xl border border-[#4988C4]/45 bg-white px-3 py-2.5 text-sm leading-relaxed text-[#0F2854] transition outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30"
                />
              </label>

              <label className="mt-4 block">
                <span className="flex items-center justify-between gap-3 text-xs font-bold text-[#5b6b84] uppercase">
                  Model Answer
                  <span
                    className={cn(
                      'normal-case',
                      sampleAnswerIsValid ? 'text-[#168367]' : 'text-[#b03a46]',
                    )}
                  >
                    {sampleAnswer.trim().length}/50 ký tự tối thiểu
                  </span>
                </span>
                <textarea
                  value={sampleAnswer}
                  onChange={(event) => setSampleAnswer(event.target.value)}
                  placeholder="Câu trả lời chuẩn mực dùng làm mốc chấm..."
                  className="mt-1.5 min-h-36 w-full resize-y rounded-xl border border-[#4988C4]/45 bg-white px-3 py-2.5 text-sm leading-relaxed text-[#0F2854] transition outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30"
                />
              </label>

              <label className="mt-4 block">
                <span className="text-xs font-bold text-[#5b6b84] uppercase">
                  Key points · mỗi dòng một ý
                </span>
                <textarea
                  value={keyPointsInput}
                  onChange={(event) => setKeyPointsInput(event.target.value)}
                  placeholder={'Khái niệm trọng tâm\nLập luận cần có\nVí dụ hoặc tình huống'}
                  className="mt-1.5 min-h-24 w-full resize-y rounded-xl border border-[#4988C4]/45 bg-white px-3 py-2.5 text-sm leading-relaxed text-[#0F2854] transition outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30"
                />
              </label>

              <div className="mt-4 grid gap-4 sm:grid-cols-2">
                <div className="block">
                  <span className="text-xs font-bold text-[#5b6b84] uppercase">Độ khó</span>
                  <FilterDropMenu
                    label="Chọn độ khó"
                    value={difficulty}
                    onChange={(value) => setDifficulty(value as QuestionDifficulty)}
                    options={DIFFICULTIES}
                    className="mt-1.5 w-full"
                  />
                </div>
                <div className="block">
                  <span className="text-xs font-bold text-[#5b6b84] uppercase">Bậc Bloom</span>
                  <FilterDropMenu
                    label="Chọn bậc Bloom"
                    value={bloomLevel}
                    onChange={(value) => setBloomLevel(value as BloomLevel)}
                    options={BLOOM_LEVELS.map((level) => ({ value: level, label: level }))}
                    className="mt-1.5 w-full"
                  />
                </div>
              </div>
            </section>

            <section className="rounded-2xl border border-[#4988C4]/45 bg-white p-4 shadow-[0_8px_28px_rgba(31,61,105,0.045)] sm:p-5">
              <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                <div className="flex items-center gap-3">
                  <span className="flex size-8 items-center justify-center rounded-lg bg-[#4988C4]/16 font-mono text-xs font-black text-[#1C4D8D]">
                    02
                  </span>
                  <div>
                    <h2 className="text-base font-bold text-[#0F2854] sm:text-lg">Barem Rubric 10.0</h2>
                    <p className="mt-0.5 text-xs text-[#5b6b84]">
                      Tối thiểu hai tiêu chí, tổng điểm bằng chính xác 10.0.
                    </p>
                  </div>
                </div>
                <button
                  type="button"
                  onClick={addCriterion}
                  className="inline-flex min-h-9 items-center justify-center gap-1.5 rounded-xl border border-[#4988C4]/45 bg-[#4988C4]/16 px-3 text-xs font-bold text-[#5b6b84] transition hover:bg-[#f0f4fb] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]"
                >
                  <Plus className="size-3.5" />
                  Thêm tiêu chí
                </button>
              </div>

              <div className="mt-5 grid gap-4 sm:grid-cols-2">
                <label className="block">
                  <span className="text-xs font-bold text-[#5b6b84] uppercase">Tên rubric</span>
                  <input
                    value={rubricName}
                    onChange={(event) => setRubricName(event.target.value)}
                    placeholder="Ví dụ: Rubric đánh giá kiến trúc"
                    className="mt-1.5 min-h-11 w-full rounded-xl border border-[#4988C4]/45 bg-white px-3 text-sm outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30"
                  />
                </label>
                <label className="block">
                  <span className="text-xs font-bold text-[#5b6b84] uppercase">Mô tả</span>
                  <input
                    value={rubricDescription}
                    onChange={(event) => setRubricDescription(event.target.value)}
                    placeholder="Mục tiêu đánh giá của rubric"
                    className="mt-1.5 min-h-11 w-full rounded-xl border border-[#4988C4]/45 bg-white px-3 text-sm outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30"
                  />
                </label>
              </div>

              <div className="mt-4 space-y-3">
                {criteria.map((criterion, index) => (
                  <article
                    key={criterion.localId}
                    className="rounded-2xl border border-[#4988C4]/45 bg-white p-3.5"
                  >
                    <div className="grid gap-3 sm:grid-cols-[minmax(0,1fr)_100px_150px_auto]">
                      <label className="block">
                        <span className="text-xs font-bold text-[#5b6b84] uppercase">
                          Tiêu chí {index + 1}
                        </span>
                        <input
                          value={criterion.criterionName}
                          onChange={(event) =>
                            updateCriterion(criterion.localId, 'criterionName', event.target.value)
                          }
                          placeholder="Tên tiêu chí đánh giá"
                          className="mt-1 min-h-10 w-full rounded-xl border border-[#4988C4]/45 bg-white px-3 text-xs outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30"
                        />
                      </label>
                      <label className="block">
                        <span className="text-xs font-bold text-[#5b6b84] uppercase">Điểm</span>
                        <input
                          type="number"
                          min="0.1"
                          max="10"
                          step="0.1"
                          value={criterion.maxScore}
                          onChange={(event) =>
                            updateCriterion(
                              criterion.localId,
                              'maxScore',
                              Number(event.target.value),
                            )
                          }
                          className="mt-1 min-h-10 w-full rounded-xl border border-[#4988C4]/45 bg-white px-2 text-center font-mono text-sm font-black outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30"
                        />
                      </label>
                      <label className="block">
                        <span className="text-xs font-bold text-[#5b6b84] uppercase">Bloom</span>
                        <select
                          value={criterion.bloomLevel}
                          onChange={(event) =>
                            updateCriterion(
                              criterion.localId,
                              'bloomLevel',
                              event.target.value as BloomLevel,
                            )
                          }
                          className="mt-1 min-h-10 w-full rounded-xl border border-[#4988C4]/45 bg-white px-2 text-xs font-semibold outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30"
                        >
                          {BLOOM_LEVELS.map((level) => (
                            <option key={level} value={level}>
                              {level}
                            </option>
                          ))}
                        </select>
                      </label>
                      <button
                        type="button"
                        onClick={() => removeCriterion(criterion.localId)}
                        disabled={criteria.length <= 2}
                        className="mt-auto flex size-10 items-center justify-center rounded-lg text-[#8a99ab] transition hover:bg-[#fff0f1] hover:text-[#b43d49] focus-visible:outline-2 focus-visible:outline-[#b43d49] disabled:cursor-not-allowed disabled:opacity-35"
                        aria-label={`Xóa tiêu chí ${index + 1}`}
                      >
                        <Trash2 className="size-4" />
                      </button>
                    </div>
                    <label className="mt-3 block">
                      <span className="sr-only">Mô tả tiêu chí {index + 1}</span>
                      <textarea
                        value={criterion.description}
                        onChange={(event) =>
                          updateCriterion(criterion.localId, 'description', event.target.value)
                        }
                        placeholder="Mô tả biểu hiện đạt điểm của tiêu chí..."
                        className="min-h-16 w-full resize-y rounded-lg border border-[#4988C4]/45 bg-white px-3 py-2 text-xs leading-relaxed outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30"
                      />
                    </label>
                  </article>
                ))}
              </div>
            </section>
          </div>

          <aside
            className={cn(
              'space-y-4 self-start',
              isDialog ? 'lg:sticky lg:top-0' : 'xl:sticky xl:top-20',
            )}
          >
            <section
              className={cn(
                'rounded-2xl border-2 bg-white p-5 shadow-[0_12px_34px_rgba(31,61,105,0.065)]',
                rubricIsValid ? 'border-[#8bd6bd]' : 'border-[#efb9bf]',
              )}
            >
              <p className="text-xs font-bold tracking-[0.08em] text-[#5b6b84] uppercase">
                Cổng kiểm tra
              </p>
              <div className="mt-4 text-center">
                <strong
                  className={cn(
                    'font-mono text-4xl font-black tracking-[-0.05em]',
                    rubricIsValid ? 'text-[#168367]' : 'text-[#b8404b]',
                  )}
                >
                  {totalRubricScore.toFixed(1)}
                </strong>
                <span className="ml-1 text-xs font-bold text-[#8aa0b8]">/ 10.0</span>
              </div>
              <div className="mt-4 h-2 overflow-hidden rounded-full bg-[#4988C4]/16">
                <div
                  className={cn(
                    'h-full rounded-full transition-[width,background-color] duration-300',
                    rubricIsValid ? 'bg-[#2fbd91]' : 'bg-[#e46a74]',
                  )}
                  style={{ width: `${Math.min(Math.max(totalRubricScore * 10, 0), 100)}%` }}
                />
              </div>
              <p
                className={cn(
                  'mt-3 flex items-start gap-2 rounded-xl p-3 text-xs leading-relaxed',
                  rubricIsValid ? 'bg-[#eefaf6] text-[#176f58]' : 'bg-[#fff3f4] text-[#9f3a45]',
                )}
              >
                {rubricIsValid ? (
                  <CheckCircle2 className="mt-0.5 size-3.5 shrink-0" />
                ) : (
                  <AlertCircle className="mt-0.5 size-3.5 shrink-0" />
                )}
                {rubricIsValid
                  ? 'Barem đạt invariant tổng điểm chính xác 10.0.'
                  : 'Điều chỉnh điểm các tiêu chí để tổng bằng 10.0.'}
              </p>
            </section>

            {(manualError || manualNotice) && !isDialog && (
              <div
                className={cn(
                  'rounded-2xl border p-4 text-xs leading-relaxed font-semibold',
                  manualError
                    ? 'border-[#efc8cc] bg-[#fff5f6] text-[#a43c47]'
                    : 'border-[#bce4d7] bg-[#effaf6] text-[#176f58]',
                )}
                role={manualError ? 'alert' : 'status'}
              >
                {manualError || manualNotice}
              </div>
            )}

            {!isDialog && (
              <>
                <div className="space-y-2">
                  <button
                    type="submit"
                    disabled={isSaving || Boolean(savedQuestionId)}
                    className="inline-flex min-h-12 w-full items-center justify-center gap-2 rounded-xl bg-[#1C4D8D] px-4 text-sm font-bold text-white shadow-[0_10px_24px_rgba(36,94,234,0.24)] transition hover:-translate-y-0.5 hover:bg-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-60"
                  >
                    {isSaving ? (
                      <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" />
                    ) : savedQuestionId ? (
                      <CheckCircle2 className="size-4" />
                    ) : (
                      <Save className="size-4" />
                    )}
                    {isSaving
                      ? 'Đang lưu...'
                      : savedQuestionId
                        ? 'Bản nháp đã được lưu'
                        : 'Lưu thành bản nháp'}
                  </button>
                  {savedQuestionId && (
                    <>
                      <button
                        type="button"
                        onClick={() => void submitSavedDraftForReview()}
                        disabled={isSubmittingReview}
                        className="inline-flex min-h-11 w-full items-center justify-center gap-2 rounded-xl bg-[#16836a] px-4 text-sm font-bold text-white shadow-[0_8px_18px_rgba(22,131,106,0.22)] transition hover:bg-[#136f5a] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#16836a] disabled:cursor-not-allowed disabled:opacity-60"
                      >
                        {isSubmittingReview ? (
                          <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" />
                        ) : (
                          <Send className="size-4" />
                        )}
                        {isSubmittingReview ? 'Đang gửi duyệt...' : 'Gửi Trưởng Bộ môn duyệt'}
                      </button>
                      <Link
                        to="/question-bank"
                        className="inline-flex min-h-10 w-full items-center justify-center gap-2 rounded-xl border border-[#4988C4] bg-white px-4 text-xs font-bold text-[#1C4D8D] transition hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]"
                      >
                        Đến ngân hàng luyện tập
                        <ChevronRight className="size-3.5" />
                      </Link>
                      <button
                        type="button"
                        onClick={resetManualDraft}
                        className="inline-flex min-h-10 w-full items-center justify-center gap-2 rounded-xl border border-[#4988C4]/45 bg-white px-4 text-xs font-bold text-[#526780] transition hover:bg-[#4988C4]/18 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]"
                      >
                        <RotateCcw className="size-3.5" />
                        Soạn câu hỏi khác
                      </button>
                    </>
                  )}
                </div>

                <p className="rounded-xl border border-[#4988C4]/45 bg-[#4988C4]/16 p-3 text-xs leading-relaxed text-[#5b6b84]">
                  Luồng này chỉ ghi vào <strong>practice_questions</strong>. Sau khi lưu DRAFT, nhấn
                  “Gửi Trưởng Bộ môn duyệt” hoặc chọn nhiều bản nháp tại Ngân hàng câu hỏi luyện tập.
                </p>
              </>
            )}
          </aside>
        </form>
      ) : (
        <div className="space-y-4">
          <form
            onSubmit={generateQuestions}
            className={cn(
              'grid gap-4',
              isDialog ? 'lg:grid-cols-[minmax(0,1fr)_280px]' : 'xl:grid-cols-[minmax(0,1fr)_330px]',
            )}
          >
            <section className="rounded-2xl border border-[#4988C4]/45 bg-white p-4 shadow-[0_8px_28px_rgba(31,61,105,0.045)] sm:p-5">
              <div className="flex items-center gap-3">
                <span className="flex size-9 items-center justify-center rounded-xl bg-[#4988C4]/16 text-[#1C4D8D]">
                  <Bot className="size-[18px]" />
                </span>
                <div>
                  <h2 className="text-base font-bold text-[#0F2854] sm:text-lg">Sinh câu hỏi từ FLM</h2>
                  <p className="mt-0.5 text-xs text-[#5b6b84]">
                    AI tạo bản đề xuất; giảng viên vẫn phải rà soát và lưu thành DRAFT.
                  </p>
                </div>
              </div>

              <div className="mt-5 grid gap-4 sm:grid-cols-2">
                <label className="block">
                  <span className="text-xs font-bold text-[#5b6b84] uppercase">
                    Mã CLO · ngăn cách bằng dấu phẩy
                  </span>
                  <textarea
                    value={cloInput}
                    onChange={(event) => setCloInput(event.target.value)}
                    placeholder="CLO1, CLO2"
                    className="mt-1.5 min-h-20 w-full resize-y rounded-xl border border-[#4988C4]/45 bg-white px-3 py-2.5 text-sm outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30"
                  />
                </label>
                <label className="block">
                  <span className="text-xs font-bold text-[#5b6b84] uppercase">
                    Chủ đề · mỗi dòng một nội dung
                  </span>
                  <textarea
                    value={topicsInput}
                    onChange={(event) => setTopicsInput(event.target.value)}
                    placeholder={'Kiến trúc hệ thống\nThiết kế API'}
                    className="mt-1.5 min-h-20 w-full resize-y rounded-xl border border-[#4988C4]/45 bg-white px-3 py-2.5 text-sm outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30"
                  />
                </label>
              </div>

              <div className="mt-4">
                <span className="text-xs font-bold text-[#5b6b84] uppercase">
                  Bậc Bloom mục tiêu
                </span>
                <div className="mt-2 flex flex-wrap gap-2">
                  {BLOOM_LEVELS.map((level) => {
                    const selected = targetBloomLevels.includes(level)
                    return (
                      <button
                        key={level}
                        type="button"
                        onClick={() => toggleBloomLevel(level)}
                        className={cn(
                          'inline-flex min-h-9 items-center gap-1.5 rounded-xl border px-3 text-xs font-bold transition focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]',
                          selected
                            ? 'border-[#4988C4] bg-[#4988C4]/16 text-[#1C4D8D]'
                            : 'border-[#d8e0eb] bg-white text-[#6d7d91] hover:bg-[#f3f6fa]',
                        )}
                        aria-pressed={selected}
                      >
                        {selected && <Check className="size-3" />}
                        {level}
                      </button>
                    )
                  })}
                </div>
              </div>

              <div className={cn('mt-4 grid gap-4', isDialog ? 'sm:grid-cols-1' : 'sm:grid-cols-2')}>
                <label className="block">
                  <span className="text-xs font-bold text-[#5b6b84] uppercase">
                    Số câu cần sinh
                  </span>
                  <input
                    type="number"
                    min="1"
                    max="10"
                    value={numberOfQuestions}
                    onChange={(event) => setNumberOfQuestions(Number(event.target.value))}
                    className="mt-1.5 min-h-11 w-full rounded-xl border border-[#4988C4]/45 bg-white px-3 font-mono text-sm font-bold outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30"
                  />
                </label>
                {!isDialog && (
                  <div className="block">
                    <span className="text-xs font-bold text-[#5b6b84] uppercase">Kho đích</span>
                    <div className="mt-1.5 flex min-h-11 items-center rounded-xl border border-[#b7e4cf] bg-[#e8f8f1] px-3 text-sm font-bold text-[#16836a]">
                      Luyện tập (practice)
                    </div>
                  </div>
                )}
              </div>

              <div className="mt-4">
                <div className="flex items-center justify-between gap-2">
                  <span className="text-xs font-bold text-[#5b6b84] uppercase">
                    Phân bổ độ khó
                  </span>
                  <span
                    className={cn(
                      'text-xs font-bold',
                      difficultyTotal === numberOfQuestions ? 'text-[#168367]' : 'text-[#b8404b]',
                    )}
                  >
                    {difficultyTotal}/{numberOfQuestions} câu
                  </span>
                </div>
                <div className="mt-2 grid grid-cols-3 gap-3">
                  {[
                    { label: 'Dễ', value: easyCount, setter: setEasyCount },
                    { label: 'Trung bình', value: mediumCount, setter: setMediumCount },
                    { label: 'Khó', value: hardCount, setter: setHardCount },
                  ].map((item) => (
                    <label key={item.label} className="block">
                      <span className="text-xs font-semibold text-[#7b899c]">{item.label}</span>
                      <input
                        type="number"
                        min="0"
                        max="10"
                        value={item.value}
                        onChange={(event) => item.setter(Number(event.target.value))}
                        className="mt-1 min-h-10 w-full rounded-xl border border-[#4988C4]/45 bg-white px-3 text-center font-mono text-sm font-bold outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30"
                      />
                    </label>
                  ))}
                </div>
              </div>
            </section>

            <aside
              className={cn(
                'space-y-4 self-start',
                isDialog ? 'lg:sticky lg:top-0' : 'xl:sticky xl:top-20',
              )}
            >
              <section className="rounded-2xl border border-[#4988C4]/45 bg-[linear-gradient(145deg,#F4FBFD,#E8F4FA)] p-5">
                <div className="flex items-center gap-2 text-xs font-bold text-[#1C4D8D]">
                  <Sparkles className="size-4" />
                  Cổng kiểm tra nội dung AI
                </div>
                <ol className="mt-4 space-y-3">
                  {[
                    'AI đọc phạm vi FLM và CLO đã chọn.',
                    'Mỗi đề xuất phải có Model Answer ≥ 50 ký tự.',
                    'Mỗi Rubric phải đạt tổng chính xác 10.0.',
                  ].map((item, index) => (
                    <li
                      key={item}
                      className="flex gap-2.5 text-xs leading-relaxed text-[#5b6b84]"
                    >
                      <span className="flex size-5 shrink-0 items-center justify-center rounded-full bg-white font-mono text-xs font-bold text-[#1C4D8D] shadow-sm">
                        {index + 1}
                      </span>
                      {item}
                    </li>
                  ))}
                </ol>
              </section>

              {(aiError || aiNotice) && (
                <div
                  className={cn(
                    'rounded-2xl border p-4 text-xs leading-relaxed font-semibold',
                    aiError
                      ? 'border-[#efc8cc] bg-[#fff5f6] text-[#a43c47]'
                      : 'border-[#bce4d7] bg-[#effaf6] text-[#176f58]',
                  )}
                  role={aiError ? 'alert' : 'status'}
                >
                  {aiError || aiNotice}
                </div>
              )}

              <button
                type="submit"
                disabled={isGenerating}
                className="inline-flex min-h-12 w-full items-center justify-center gap-2 rounded-xl bg-[#1C4D8D] px-4 text-sm font-bold text-white shadow-[0_10px_24px_rgba(28,77,141,0.28)] transition hover:-translate-y-0.5 hover:bg-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-60"
              >
                {isGenerating ? (
                  <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" />
                ) : (
                  <WandSparkles className="size-4" />
                )}
                {isGenerating ? 'AI đang phân tích...' : 'Sinh câu hỏi từ FLM'}
              </button>
            </aside>
          </form>

          {generation && (
            <section className="overflow-hidden rounded-2xl border border-[#4988C4]/45 bg-white shadow-[0_10px_34px_rgba(31,61,105,0.055)]">
              <div className="flex flex-col gap-3 border-b border-[#4988C4]/45 bg-[#F4FBFD]/80 px-4 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-5">
                <div>
                  <p className="text-xs font-bold tracking-[0.08em] text-[#1C4D8D] uppercase">
                    Xem trước bản nháp AI · {generation.courseCode}
                  </p>
                  <h2 className="mt-1 text-base font-bold text-[#0F2854] sm:text-lg">
                    {generation.generatedCount} đề xuất cần giảng viên rà soát
                  </h2>
                </div>
                <button
                  type="button"
                  onClick={() => void saveGeneratedDrafts()}
                  disabled={selectedGenerated.size === 0 || isSavingGenerated}
                  className="inline-flex min-h-10 items-center justify-center gap-2 rounded-xl bg-[#1C4D8D] px-4 text-xs font-bold text-white shadow-[0_8px_20px_rgba(28,77,141,0.22)] transition hover:bg-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-50"
                >
                  {isSavingGenerated ? (
                    <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" />
                  ) : (
                    <Save className="size-3.5" />
                  )}
                  Lưu {selectedGenerated.size} bản nháp đã chọn
                </button>
              </div>

              <div className="grid gap-3 p-4 sm:p-5 lg:grid-cols-2">
                {generation.questions.map((question) => {
                  const valid = getGeneratedQuestionValidity(question)
                  const selected = selectedGenerated.has(question.tempId)
                  const alreadySaved = savedGeneratedIds.has(question.tempId)
                  const rubricTotal = question.rubric.criteria.reduce(
                    (sum, criterion) => sum + criterion.maxScore,
                    0,
                  )

                  return (
                    <article
                      key={question.tempId}
                      className={cn(
                        'relative rounded-2xl border p-4 transition',
                        selected
                          ? 'border-[#9fb6ec] bg-[#4988C4]/16 shadow-[0_8px_22px_rgba(36,94,234,0.08)]'
                          : 'border-[#4988C4]/45 bg-white',
                        !valid && 'border-[#efc4c9] bg-[#fff7f7]',
                        alreadySaved && 'border-[#bce4d7] bg-[#f4fbf8]',
                      )}
                    >
                      <div className="flex items-start justify-between gap-3">
                        <label
                          className={cn(
                            'flex min-w-0 flex-1 items-start gap-3',
                            alreadySaved ? 'cursor-not-allowed' : 'cursor-pointer',
                          )}
                        >
                          <input
                            type="checkbox"
                            checked={selected}
                            disabled={!valid || alreadySaved}
                            onChange={() => toggleGeneratedQuestion(question)}
                            className="mt-0.5 size-4 accent-[#4988C4] disabled:cursor-not-allowed disabled:opacity-40"
                          />
                          <div className="min-w-0">
                            <div className="flex flex-wrap items-center gap-2">
                              <span className="rounded-full bg-[#4988C4]/16 px-2 py-1 text-xs font-bold text-[#1C4D8D]">
                                {question.cloCode}
                              </span>
                              <span className="rounded-full bg-[#4988C4]/16 px-2 py-1 text-xs font-bold text-[#3d62ad]">
                                {question.bloomLevel}
                              </span>
                              <span className="rounded-full bg-white px-2 py-1 text-xs font-bold text-[#66788f] shadow-sm">
                                {getDifficultyLabel(question.difficulty)}
                              </span>
                              {alreadySaved && (
                                <span className="rounded-full bg-[#eaf9f4] px-2 py-1 text-xs font-bold text-[#17745b]">
                                  Đã lưu
                                </span>
                              )}
                            </div>
                            <h3 className="mt-2 line-clamp-2 text-sm leading-snug font-bold text-[#253850]">
                              {question.title}
                            </h3>
                          </div>
                        </label>
                        <span
                          className={cn(
                            'shrink-0 rounded-xl px-2.5 py-1.5 font-mono text-xs font-black',
                            Math.abs(rubricTotal - 10) < 0.001
                              ? 'bg-[#eaf9f4] text-[#14765c]'
                              : 'bg-[#fff0f1] text-[#ae3945]',
                          )}
                        >
                          {rubricTotal.toFixed(1)}/10
                        </span>
                      </div>

                      <p className="mt-3 line-clamp-3 text-xs leading-relaxed text-[#63738a]">
                        {question.content}
                      </p>
                      <QuestionImage
                        imageUrl={question.imageUrl}
                        imageAlt={question.imageAlt}
                        className="mt-3"
                      />
                      <div className="mt-3 flex flex-wrap items-center justify-between gap-2 border-t border-[#4988C4]/45 pt-3 text-xs font-semibold text-[#7a899c]">
                        <span>{question.rubric.criteria.length} tiêu chí rubric</span>
                        <div className="flex flex-wrap items-center justify-end gap-2">
                          <span
                            className={
                              valid ? 'text-[#168367]' : 'flex items-center gap-1 text-[#b8404b]'
                            }
                          >
                            {valid ? (
                              'Đạt validation gate'
                            ) : (
                              <>
                                <AlertCircle className="size-3" />
                                Cần chỉnh trước khi lưu
                              </>
                            )}
                          </span>
                          <button
                            type="button"
                            onClick={() => editGeneratedDraft(question)}
                            disabled={alreadySaved}
                            className="inline-flex min-h-8 items-center gap-1.5 rounded-lg border border-[#4988C4]/45 bg-white px-2.5 text-xs font-bold text-[#1C4D8D] transition hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-45"
                          >
                            <PencilLine className="size-3" />
                            Chỉnh sửa trước khi lưu
                          </button>
                        </div>
                      </div>
                    </article>
                  )
                })}
              </div>

              <div className="flex items-start gap-2 border-t border-[#4988C4]/45 bg-[#fafbfe] px-4 py-3 text-xs leading-relaxed text-[#5b6b84] sm:px-5">
                <Send className="mt-0.5 size-3.5 shrink-0 text-[#1C4D8D]" />
                Lưu ở đây chỉ tạo DRAFT. Sau khi tự rà soát, giảng viên chọn các bản nháp trong Ngân
                hàng đề để gửi Trưởng Bộ môn thẩm định.
                {isDialog ? (
                  <button
                    type="button"
                    onClick={onClose}
                    className="ml-auto inline-flex shrink-0 items-center gap-1 font-bold text-[#1C4D8D] hover:underline"
                  >
                    Đóng popup
                    <ChevronRight className="size-3" />
                  </button>
                ) : (
                  <Link
                    to="/question-bank"
                    className="ml-auto inline-flex shrink-0 items-center gap-1 font-bold text-[#1C4D8D] hover:underline"
                  >
                    Đến ngân hàng đề
                    <ChevronRight className="size-3" />
                  </Link>
                )}
              </div>
            </section>
          )}
        </div>
      )}
      </div>

      {isDialog && mode === 'manual' && (
        <footer className="shrink-0 border-t border-[#4988C4]/45 bg-white/95 px-4 py-3 backdrop-blur-sm sm:px-5">
          {(manualError || manualNotice) && (
            <p
              className={cn(
                'mb-2 text-xs font-semibold',
                manualError ? 'text-[#a43c47]' : 'text-[#176f58]',
              )}
              role={manualError ? 'alert' : 'status'}
            >
              {manualError || manualNotice}
            </p>
          )}
          <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
            <p className="hidden text-xs text-[#5b6b84] sm:block">
              Lưu DRAFT → gửi Trưởng BM duyệt → vào kho luyện tập.
            </p>
            <div className="flex flex-col gap-2 sm:ml-auto sm:flex-row sm:items-center">
              {savedQuestionId ? (
                <>
                  <button
                    type="button"
                    onClick={resetManualDraft}
                    className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl border border-[#4988C4]/45 bg-white px-4 text-sm font-bold text-[#526780] transition hover:bg-[#F4FBFD]"
                  >
                    <RotateCcw className="size-3.5" />
                    Soạn câu khác
                  </button>
                  <button
                    type="button"
                    onClick={() => void submitSavedDraftForReview()}
                    disabled={isSubmittingReview}
                    className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[#16836a] px-4 text-sm font-bold text-white shadow-[0_8px_18px_rgba(22,131,106,0.22)] transition hover:bg-[#136f5a] disabled:cursor-not-allowed disabled:opacity-60"
                  >
                    {isSubmittingReview ? (
                      <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" />
                    ) : (
                      <Send className="size-4" />
                    )}
                    {isSubmittingReview ? 'Đang gửi...' : 'Gửi duyệt'}
                  </button>
                  <button
                    type="button"
                    onClick={onClose}
                    className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl border border-[#4988C4] bg-white px-4 text-sm font-bold text-[#1C4D8D] transition hover:bg-[#F4FBFD]"
                  >
                    Đóng
                  </button>
                </>
              ) : (
                <>
                  <button
                    type="button"
                    onClick={onClose}
                    className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl border border-[#4988C4]/45 bg-white px-4 text-sm font-bold text-[#526780] transition hover:bg-[#F4FBFD]"
                  >
                    Hủy
                  </button>
                  <button
                    type="submit"
                    form="create-practice-manual-form"
                    disabled={isSaving}
                    className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[#1C4D8D] px-5 text-sm font-bold text-white shadow-[0_10px_24px_rgba(28,77,141,0.28)] transition hover:bg-[#0F2854] disabled:cursor-not-allowed disabled:opacity-60"
                  >
                    {isSaving ? (
                      <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" />
                    ) : (
                      <Save className="size-4" />
                    )}
                    {isSaving ? 'Đang lưu...' : 'Lưu bản nháp'}
                  </button>
                </>
              )}
            </div>
          </div>
        </footer>
      )}
    </div>
  )
}

export function CreatePracticeQuestionDialog({
  open,
  onClose,
  onCreated,
}: {
  open: boolean
  onClose: () => void
  onCreated?: (questionId?: string) => void
}) {
  useEffect(() => {
    if (!open) return
    const previousOverflow = document.body.style.overflow
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose()
    }
    document.body.style.overflow = 'hidden'
    window.addEventListener('keydown', onKeyDown)
    return () => {
      document.body.style.overflow = previousOverflow
      window.removeEventListener('keydown', onKeyDown)
    }
  }, [open, onClose])

  if (!open) return null

  return (
    <div
      className="fixed inset-0 z-50 flex items-end justify-center bg-[#0F2854]/50 p-0 backdrop-blur-[3px] sm:items-center sm:p-5"
      role="presentation"
      onClick={(event) => {
        if (event.target === event.currentTarget) onClose()
      }}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-label="Tạo câu hỏi luyện tập"
        className="flex max-h-[min(94dvh,920px)] w-full max-w-6xl flex-col overflow-hidden rounded-t-3xl border border-[#4988C4]/45 bg-white shadow-[0_28px_80px_rgba(15,40,84,0.28)] sm:rounded-3xl"
      >
        <CreatePracticeQuestionForm
          presentation="dialog"
          onClose={onClose}
          onCreated={onCreated}
        />
      </div>
    </div>
  )
}

/** Route `/question-bank/create` chuyển về ngân hàng và mở popup tạo câu hỏi. */
export function CreateQuestionPage() {
  const navigate = useNavigate()
  useEffect(() => {
    navigate('/question-bank', { replace: true, state: { openCreate: true } })
  }, [navigate])
  return null
}
