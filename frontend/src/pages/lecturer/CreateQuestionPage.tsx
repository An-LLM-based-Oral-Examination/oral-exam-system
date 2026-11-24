import { TwoStepProgress } from '@/design/TwoStepProgress'
import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import {
  AlertCircle,
  ArrowLeft,
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
  ImageIcon,
  WandSparkles,
  X,
} from 'lucide-react'
import { FilterDropMenu } from '@/components/common/FilterDropMenu'
import { FieldError } from '@/components/common/formField'
import { fieldInvalidClass, fieldLabelClass } from '@/components/common/formFieldClasses'
import { toast } from '@/components/common/toastStore'
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
import { userMessage } from '@/lib/userMessage'
import { DialogFooter, FormDialog } from '@/design/overlays'
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

const SESSION_INVALID_MESSAGE =
  'Phiên đăng nhập không có mã người dùng hợp lệ. Vui lòng đăng xuất và đăng nhập lại.'

type ManualFieldErrors = {
  courseId?: string
  title?: string
  content?: string
  sampleAnswer?: string
  rubricName?: string
  criteria?: string
  rubricTotal?: string
}

type CriterionFieldErrors = Record<string, { name?: string; score?: string }>

type AiFieldErrors = {
  courseId?: string
  clo?: string
  topics?: string
  numberOfQuestions?: string
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

/** Bloom gắn theo độ khó: Dễ → Remember, Trung bình → Apply, Khó → Analyze. */
function bloomFromDifficulty(difficulty: QuestionDifficulty): BloomLevel {
  if (difficulty === 'easy') return 'Remember'
  if (difficulty === 'hard') return 'Analyze'
  return 'Apply'
}

function createCriterion(maxScore = 5, bloomLevel: BloomLevel = 'Apply'): CriterionForm {
  return {
    localId: crypto.randomUUID(),
    criterionName: '',
    maxScore,
    weight: maxScore / 10,
    bloomLevel,
    description: '',
  }
}

function getErrorMessage(error: unknown) {
  return userMessage(error, 'Không lưu được câu hỏi. Vui lòng kiểm tra dữ liệu và thử lại.')
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
  const [mode, setMode] = useState<StudioMode | null>(null)
  const [step, setStep] = useState<1 | 2>(1)
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
        setCourseId((current) => current || courses[0]?.id || '')
      })
      .catch(() => {
        // Fallback: vẫn cho nhập UUID nếu API môn phân công lỗi
      })
    return () => {
      active = false
    }
  }, [])

  const courseOptions = useMemo(
    () =>
      availableCourses.map((course) => ({
        value: course.id,
        label: `${course.code} · ${course.name}`,
      })),
    [availableCourses],
  )
  const [title, setTitle] = useState('')
  const [content, setContent] = useState('')
  const [sampleAnswer, setSampleAnswer] = useState('')
  const [keyPointsInput, setKeyPointsInput] = useState('')
  const [difficulty, setDifficulty] = useState<QuestionDifficulty>('medium')
  const bloomLevel = bloomFromDifficulty(difficulty)
  const [rubricName, setRubricName] = useState('')
  const [rubricDescription, setRubricDescription] = useState('')
  const [manualSource, setManualSource] = useState<QuestionSource>('manual')
  const [criteria, setCriteria] = useState<CriterionForm[]>(() => [
    createCriterion(5, bloomFromDifficulty('medium')),
    createCriterion(5, bloomFromDifficulty('medium')),
  ])
  const [manualErrors, setManualErrors] = useState<ManualFieldErrors>({})
  const [criterionErrors, setCriterionErrors] = useState<CriterionFieldErrors>({})
  const [manualNotice, setManualNotice] = useState('')
  const [isSaving, setIsSaving] = useState(false)
  const [savedQuestionId, setSavedQuestionId] = useState('')
  const [imageUrl, setImageUrl] = useState<string | null>(null)
  const [imageDescription, setImageDescription] = useState('')
  const [isUploadingImage, setIsUploadingImage] = useState(false)
  const fileInputRef = useRef<HTMLInputElement>(null)

  const handleImageFileChange = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]
    if (!file) return

    if (file.size > 5 * 1024 * 1024) {
      toast.error('Kích thước tệp vượt quá 5MB. Vui lòng chọn ảnh nhỏ hơn.')
      return
    }

    const validTypes = ['image/png', 'image/jpeg', 'image/jpg', 'image/webp']
    if (!validTypes.includes(file.type)) {
      toast.error('Định dạng tệp không được hỗ trợ. Chỉ chấp nhận PNG, JPG, JPEG hoặc WEBP.')
      return
    }

    setIsUploadingImage(true)
    try {
      const uploadRes = await questionService.uploadQuestionImage(file, imageDescription)
      setImageUrl(uploadRes.imageUrl)
      toast.success('Đã tải hình ảnh sơ đồ thành công!')
    } catch (uploadErr) {
      toast.error(getErrorMessage(uploadErr) || 'Không thể tải hình ảnh lên.')
    } finally {
      setIsUploadingImage(false)
      if (fileInputRef.current) fileInputRef.current.value = ''
    }
  }

  const handleRemoveImage = () => {
    setImageUrl(null)
    setImageDescription('')
  }

  const [cloInput, setCloInput] = useState('')
  const [topicsInput, setTopicsInput] = useState('')
  const [easyCount, setEasyCount] = useState(1)
  const [mediumCount, setMediumCount] = useState(1)
  const [hardCount, setHardCount] = useState(1)
  const [targetBloomLevels, setTargetBloomLevels] = useState<BloomLevel[]>([
    'Understand',
    'Apply',
    'Analyze',
  ])
  const [generation, setGeneration] = useState<GenerateQuestionsFromFlmResponse | null>(null)
  const [selectedGenerated, setSelectedGenerated] = useState<Set<string>>(new Set())
  const [savedGeneratedIds, setSavedGeneratedIds] = useState<Set<string>>(new Set())
  const [editingGeneratedTempId, setEditingGeneratedTempId] = useState('')
  const [aiErrors, setAiErrors] = useState<AiFieldErrors>({})
  const [aiNotice, setAiNotice] = useState('')
  const [isGenerating, setIsGenerating] = useState(false)
  const [isSavingGenerated, setIsSavingGenerated] = useState(false)

  const totalRubricScore = useMemo(
    () => criteria.reduce((sum, criterion) => sum + criterion.maxScore, 0),
    [criteria],
  )
  const rubricIsValid = Math.abs(totalRubricScore - 10) < 0.001
  const sampleAnswerIsValid = sampleAnswer.trim().length >= 50
  const numberOfQuestions = easyCount + mediumCount + hardCount

  const clearManualError = (...keys: Array<keyof ManualFieldErrors>) => {
    setManualErrors((current) => {
      if (!keys.some((key) => current[key])) return current
      const next = { ...current }
      for (const key of keys) delete next[key]
      return next
    })
  }

  const clearAiError = (...keys: Array<keyof AiFieldErrors>) => {
    setAiErrors((current) => {
      if (!keys.some((key) => current[key])) return current
      const next = { ...current }
      for (const key of keys) delete next[key]
      return next
    })
  }

  const clearCriterionError = (localId: string, key: 'name' | 'score') => {
    setCriterionErrors((current) => {
      if (!current[localId]?.[key]) return current
      const next = { ...current, [localId]: { ...current[localId] } }
      delete next[localId][key]
      return next
    })
  }

  const changeCourse = (value: string) => {
    setCourseId(value)
    clearManualError('courseId')
    clearAiError('courseId')
  }

  const updateCriterion = <Key extends keyof CriterionForm>(
    localId: string,
    key: Key,
    value: CriterionForm[Key],
  ) => {
    if (key === 'criterionName') clearCriterionError(localId, 'name')
    if (key === 'maxScore') {
      clearCriterionError(localId, 'score')
      clearManualError('rubricTotal')
    }
    setCriteria((current) =>
      current.map((criterion) => {
        if (criterion.localId !== localId) return criterion
        const next = { ...criterion, [key]: value }
        if (key === 'maxScore') next.weight = Number(value) / 10
        return next
      }),
    )
  }

  const changeDifficulty = (value: QuestionDifficulty) => {
    const nextBloom = bloomFromDifficulty(value)
    setDifficulty(value)
    setCriteria((current) => current.map((criterion) => ({ ...criterion, bloomLevel: nextBloom })))
  }

  const addCriterion = () => {
    clearManualError('criteria')
    setCriteria((current) => [...current, createCriterion(0, bloomFromDifficulty(difficulty))])
  }

  const removeCriterion = (localId: string) => {
    clearManualError('rubricTotal')
    setCriteria((current) =>
      current.length <= 2 ? current : current.filter((criterion) => criterion.localId !== localId),
    )
  }

  const resetManualDraft = () => {
    setStep(1)
    setTitle('')
    setContent('')
    setSampleAnswer('')
    setKeyPointsInput('')
    setDifficulty('medium')
    setRubricName('')
    setRubricDescription('')
    setCriteria([
      createCriterion(5, bloomFromDifficulty('medium')),
      createCriterion(5, bloomFromDifficulty('medium')),
    ])
    setManualSource('manual')
    setEditingGeneratedTempId('')
    setSavedQuestionId('')
    setManualErrors({})
    setCriterionErrors({})
    setManualNotice('')
  }

  const editGeneratedDraft = (question: GeneratedQuestionDraft) => {
    setTitle(question.title)
    setContent(question.content)
    setSampleAnswer(question.modelAnswer)
    setKeyPointsInput(question.keyPoints.join('\n'))
    const nextDifficulty = question.difficulty
    const nextBloom = bloomFromDifficulty(nextDifficulty)
    setDifficulty(nextDifficulty)
    setRubricName(question.rubric.name)
    setRubricDescription(question.rubric.description ?? '')
    setCriteria(
      question.rubric.criteria.map((criterion) => ({
        ...criterion,
        localId: crypto.randomUUID(),
        description: criterion.description ?? '',
        weight: criterion.maxScore / 10,
        bloomLevel: nextBloom,
      })),
    )
    setManualSource('flm_api')
    setEditingGeneratedTempId(question.tempId)
    setSavedQuestionId('')
    setManualErrors({})
    setCriterionErrors({})
    setManualNotice('Đã nạp đề xuất AI. Hãy rà soát và chỉnh sửa trước khi lưu bản nháp.')
    setMode('manual')
    setStep(1)
    window.scrollTo({
      top: 0,
      behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth',
    })
  }

  const validateManualForm = () => {
    const errors: ManualFieldErrors = {}
    const nextCriterionErrors: CriterionFieldErrors = {}

    if (!isUuid(courseId.trim())) errors.courseId = 'Vui lòng chọn môn học hợp lệ.'
    if (!title.trim()) errors.title = 'Vui lòng nhập tiêu đề câu hỏi.'
    if (!content.trim()) errors.content = 'Vui lòng nhập nội dung câu hỏi.'
    if (!sampleAnswerIsValid) errors.sampleAnswer = 'Model Answer bắt buộc có ít nhất 50 ký tự.'
    if (!rubricName.trim()) errors.rubricName = 'Vui lòng đặt tên cho Barem Rubric.'
    if (criteria.length < 2) errors.criteria = 'Barem Rubric phải có ít nhất hai tiêu chí.'
    for (const criterion of criteria) {
      const entry: { name?: string; score?: string } = {}
      if (!criterion.criterionName.trim()) entry.name = 'Nhập tên tiêu chí.'
      if (!(criterion.maxScore > 0)) entry.score = 'Điểm phải lớn hơn 0.'
      if (entry.name || entry.score) nextCriterionErrors[criterion.localId] = entry
    }
    if (!rubricIsValid) errors.rubricTotal = 'Tổng điểm các tiêu chí phải bằng chính xác 10.0.'

    return { errors, criterionErrors: nextCriterionErrors }
  }

  const saveManualDraft = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (isSaving || isUploadingImage || savedQuestionId) return
    if (step === 1) {
      const { courseId, title, content, sampleAnswer } = validateManualForm().errors
      setManualErrors({ courseId, title, content, sampleAnswer })
      if (!courseId && !title && !content && !sampleAnswer) setStep(2)
      return
    }
    const validation = validateManualForm()
    setManualErrors(validation.errors)
    setCriterionErrors(validation.criterionErrors)
    if (Object.keys(validation.errors).length > 0 || Object.keys(validation.criterionErrors).length > 0) {
      return
    }
    if (!isUuid(user?.id ?? '')) {
      toast.error(SESSION_INVALID_MESSAGE)
      return
    }

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
        imageUrl: imageUrl || null,
        imageDescription: imageDescription.trim() || null,
        rubric: {
          name: rubricName.trim(),
          description: rubricDescription.trim() || null,
          totalMaxScore: 10,
          criteria: criteria.map((criterion, index) => ({
            criterionName: criterion.criterionName.trim(),
            maxScore: criterion.maxScore,
            bloomLevel,
            description: criterion.description.trim(),
            weight: criterion.maxScore / 10,
            orderIndex: index + 1,
          })),
        },
      })
      setSavedQuestionId(result.questionId)
      if (editingGeneratedTempId) {
        setSavedGeneratedIds((current) => new Set([...current, editingGeneratedTempId]))
        setSelectedGenerated((current) => {
          const next = new Set(current)
          next.delete(editingGeneratedTempId)
          return next
        })
        setAiNotice('Đã lưu câu hỏi đã chỉnh sửa thành bản nháp.')
        resetManualDraft()
        setMode('ai')
        setStep(2)
        onCreated?.(result.questionId)
        return
      }
      setManualNotice(
        `Đã lưu bản nháp luyện tập. Mã câu hỏi: ${result.questionId}. Bạn có thể gửi Trưởng Bộ môn duyệt ngay bên dưới.`,
      )
      onCreated?.(result.questionId)
    } catch (saveError) {
      toast.error(getErrorMessage(saveError))
    } finally {
      setIsSaving(false)
    }
  }

  const submitSavedDraftForReview = async () => {
    if (!savedQuestionId || !isUuid(courseId.trim()) || isSubmittingReview) return
    if (!isUuid(user?.id ?? '')) {
      toast.error(SESSION_INVALID_MESSAGE)
      return
    }

    setIsSubmittingReview(true)
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
      toast.error(getErrorMessage(submitError))
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
    const errors: AiFieldErrors = {}
    if (!isUuid(courseId.trim())) errors.courseId = 'Vui lòng chọn môn học hợp lệ.'
    if (splitTerms(cloInput).length === 0) errors.clo = 'Nhập ít nhất một mã CLO.'
    if (splitTerms(topicsInput).length === 0) errors.topics = 'Nhập ít nhất một chủ đề.'
    if ([easyCount, mediumCount, hardCount].some((count) => !Number.isInteger(count) || count < 0) || numberOfQuestions < 1 || numberOfQuestions > 10) {
      errors.numberOfQuestions = 'Số câu cần sinh phải từ 1 đến 10.'
    }
    setAiErrors(errors)
    if (Object.keys(errors).length > 0) return
    if (!isUuid(user?.id ?? '')) {
      toast.error(SESSION_INVALID_MESSAGE)
      return
    }

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
      setStep(2)
      setSelectedGenerated(
        new Set(
          result.questions
            .filter((question) => getGeneratedQuestionValidity(question))
            .map((question) => question.tempId),
        ),
      )
    } catch (generationError) {
      toast.error(getErrorMessage(generationError))
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
      toast.error(SESSION_INVALID_MESSAGE)
      return
    }
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
      toast.error(`Không lưu được ${failures.length} câu hỏi. ${failures[0]}`)
    }
    setIsSavingGenerated(false)
  }

  const dialogBusy = isSaving || isSubmittingReview || isGenerating || isSavingGenerated || isUploadingImage
  const flowName = mode === 'manual' ? 'Soạn câu hỏi thủ công' : 'Tạo bộ câu hỏi bằng AI'
  const stepNames = mode === 'manual' ? ['Nội dung', 'Barem và gửi duyệt'] : ['Thiết lập', 'Rà soát']

  const chooseFlow = (flow: StudioMode) => {
    setMode(flow)
    setStep(1)
  }

  const flowPicker = (
    <section className="mx-auto w-full max-w-3xl" aria-label="Chọn cách tạo câu hỏi">
      <div className="grid grid-cols-2 gap-2 sm:gap-3">
        {([
          { id: 'manual', label: 'Soạn câu hỏi thủ công', hint: 'Tạo một câu, có thể đính kèm ảnh.', icon: FilePenLine },
          { id: 'ai', label: 'Tạo bộ câu hỏi bằng AI', hint: 'Tạo nhiều câu, rà soát trước khi lưu.', icon: WandSparkles },
        ] as const).map(({ id, label, hint, icon: Icon }) => (
          <button
            key={id}
            type="button"
            onClick={() => chooseFlow(id)}
            className="group flex flex-col items-start gap-2 rounded-xl border border-[#c4d8ec] bg-white p-2.5 sm:flex-row sm:gap-3 sm:p-4 text-left shadow-sm transition hover:border-[#4988C4] hover:bg-[#f4faff] hover:shadow-md focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]"
          >
            <span className={cn(
              'grid size-8 shrink-0 place-items-center rounded-xl',
              id === 'manual' ? 'bg-[#e5f1fc] text-[#1C4D8D]' : 'bg-violet-100 text-violet-700',
            )}>
              <Icon className="size-4" aria-hidden="true" />
            </span>
            <span className="flex w-full items-start gap-3">
              <span className="flex-1">
                <span className="block text-sm font-bold text-[#0F2854]">{label}</span>
                <span className="mt-1 block text-xs leading-snug text-slate-600">{hint}</span>
              </span>
              <ChevronRight className="mt-0.5 size-4 shrink-0 text-[#1C4D8D] motion-safe:transition-transform motion-safe:group-hover:translate-x-0.5" aria-hidden="true" />
            </span>
          </button>
        ))}
      </div>
    </section>
  )

  const stepNavigation = (
    <div className="rounded-xl border border-[#c4d8ec] bg-white px-3 py-2">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-lg font-bold tracking-tight text-[#0F2854]">{flowName}</h2>
        {!isDialog && (
        <button
          type="button"
          className={cn(ds.outlineButton, 'enabled:hover:border-[#1C4D8D] enabled:hover:bg-[#E7F3FB]')}
          disabled={dialogBusy}
          onClick={() => {
            if (step === 2 && !savedQuestionId) {
              setStep(1)
            } else if (editingGeneratedTempId) {
              resetManualDraft()
              setMode('ai')
              setStep(2)
            } else {
              if (savedQuestionId) resetManualDraft()
              setMode(null)
            }
          }}
        >
          {step === 2 && !savedQuestionId ? 'Quay lại' : editingGeneratedTempId ? 'Về danh sách AI' : 'Đổi cách tạo'}
        </button>
        )}
      </div>
    </div>
  )

  const progress = mode && (
    <TwoStepProgress
      label="Tiến độ tạo câu hỏi"
      step={step}
      steps={[{ id: 1, title: stepNames[0] }, { id: 2, title: stepNames[1] }]}
      disabled={dialogBusy || Boolean(savedQuestionId)}
      canAdvance={mode === 'manual' || step === 2}
      onStepChange={(target) => {
        if (target === 1) setStep(1)
        else if (step === 1) document.querySelector<HTMLFormElement>('#create-practice-manual-form')?.requestSubmit()
      }}
    />
  )

  const dialogFooter = (
    <DialogFooter className="bg-white sm:items-center sm:justify-between">
      {mode && (
        <button
          type="button"
          className={cn(ds.outlineButton, 'enabled:hover:border-[#1C4D8D] enabled:hover:bg-[#E7F3FB]')}
          disabled={dialogBusy}
          onClick={() => {
            if (step === 2 && !savedQuestionId) {
              setStep(1)
            } else if (editingGeneratedTempId) {
              resetManualDraft()
              setMode('ai')
              setStep(2)
            } else {
              if (savedQuestionId) resetManualDraft()
              setMode(null)
            }
          }}
        >
          {step === 2 && !savedQuestionId ? 'Quay lại' : editingGeneratedTempId ? 'Về danh sách AI' : 'Đổi cách tạo'}
        </button>
      )}
      {mode === 'manual' ? (
        <>
          <div className="flex w-full flex-col gap-2 sm:ml-auto sm:w-auto sm:flex-row sm:items-center">
            {manualNotice && (
              <p
                className="order-first text-xs font-semibold text-[#176f58] sm:order-none sm:mr-1"
                role="status"
              >
                {manualNotice}
              </p>
            )}
            {step === 2 && savedQuestionId ? (
              <>
                <button type="button" onClick={resetManualDraft} disabled={dialogBusy} className={cn(ds.outlineButton, 'enabled:hover:border-[#1C4D8D] enabled:hover:bg-[#E7F3FB]')}>
                  <RotateCcw className="size-3.5" />
                  Soạn câu khác
                </button>
                <button
                  type="button"
                  onClick={() => void submitSavedDraftForReview()}
                  disabled={isSubmittingReview}
                  className="inline-flex h-10 items-center justify-center gap-2 rounded-xl bg-[#16836a] px-4 text-sm font-bold text-white shadow-[0_8px_16px_rgba(22,131,106,0.22)] transition duration-200 hover:-translate-y-0.5 hover:bg-[#136f5a] disabled:pointer-events-none disabled:opacity-45 motion-reduce:transform-none"
                >
                  {isSubmittingReview ? (
                    <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" />
                  ) : (
                    <Send className="size-4" />
                  )}
                  {isSubmittingReview ? 'Đang gửi...' : 'Gửi duyệt'}
                </button>
                <button type="button" onClick={onClose} disabled={dialogBusy} className={cn(ds.outlineButton, 'enabled:hover:border-[#1C4D8D] enabled:hover:bg-[#E7F3FB]')}>
                  Đóng
                </button>
              </>
            ) : (
              <>
                <button type="button" onClick={onClose} disabled={dialogBusy} className={cn(ds.outlineButton, 'enabled:hover:border-[#1C4D8D] enabled:hover:bg-[#E7F3FB]')}>
                  Hủy
                </button>
                <button type="submit" form="create-practice-manual-form" disabled={dialogBusy} className={cn(ds.primaryButton, 'border border-transparent enabled:hover:border-[#0F2854] enabled:hover:bg-[#0F2854]')}>
                  {isSaving ? (
                    <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" />
                  ) : (
                    <Save className="size-4" />
                  )}
                  {step === 1 ? 'Tiếp tục' : isSaving ? 'Đang lưu...' : 'Lưu bản nháp'}
                </button>
              </>
            )}
          </div>
        </>
      ) : mode === 'ai' ? (
        step === 1 ? (
          <button type="submit" form="create-practice-ai-form" disabled={dialogBusy} className={cn(ds.primaryButton, 'border border-transparent enabled:hover:border-[#0F2854] enabled:hover:bg-[#0F2854]')}>
            {isGenerating ? 'Đang tạo…' : 'Tạo bộ câu hỏi'}
          </button>
        ) : (
          <button type="button" onClick={() => void saveGeneratedDrafts()}
            disabled={dialogBusy || selectedGenerated.size === 0} className={cn(ds.primaryButton, 'border border-transparent enabled:hover:border-[#0F2854] enabled:hover:bg-[#0F2854]')}>
            {isSavingGenerated ? 'Đang lưu…' : `Lưu ${selectedGenerated.size} bản nháp`}
          </button>
        )
      ) : null}
    </DialogFooter>
  )

  const pageChrome = !isDialog ? (
    <>
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <Link
          to="/question-bank"
          className="inline-flex min-h-10 w-fit items-center gap-1.5 text-sm font-bold text-[#1C4D8D] transition duration-200 hover:-translate-y-0.5 hover:text-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transform-none"
        >
          <ArrowLeft className="size-4" aria-hidden="true" />
          Quay lại ngân hàng đề
        </Link>
      </div>
    </>
  ) : null

  const formFields = mode === null ? flowPicker : (
    <div className="mx-auto w-full max-w-6xl space-y-3">
      {!isDialog && progress}
      {!isDialog && stepNavigation}
      {step === 1 && <section
        className={cn(
          'rounded-2xl border border-[#82aedb]/55 p-4',
          ds.panelShadow,
          isDialog ? 'bg-white' : 'bg-[linear-gradient(135deg,#BDE8F5,#BDE8F5)]',
        )}
      >
        <div
          className={cn(
            'grid gap-3',
            mode === 'manual' && 'sm:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]',
          )}
        >
          <div className="block">
            <span
              className={cn(
                'flex items-center gap-2 text-xs font-bold tracking-[0.07em] uppercase',
                (mode === 'manual' ? manualErrors.courseId : aiErrors.courseId)
                  ? 'text-[#c2415b]'
                  : 'text-[#5b6b84]',
              )}
            >
              Môn học
            </span>
            <FilterDropMenu
              label="Chọn môn học"
              value={courseId}
              onChange={changeCourse}
              options={courseOptions}
              emptyText="Chưa có môn được phân công"
              className={cn(
                'mt-1.5 w-full',
                (mode === 'manual' ? manualErrors.courseId : aiErrors.courseId) &&
                  'rounded-xl ring-2 ring-[#c2415b]/35',
              )}
            />
            <FieldError message={mode === 'manual' ? manualErrors.courseId : aiErrors.courseId} />
          </div>
          {mode === 'manual' && (
              <div className="block">
                <span className="flex items-center text-xs font-bold tracking-[0.07em] text-[#5b6b84] uppercase">Độ khó</span>
                <FilterDropMenu
                  label="Chọn độ khó"
                  value={difficulty}
                  onChange={(value) => changeDifficulty(value as QuestionDifficulty)}
                  options={DIFFICULTIES}
                  className="mt-1.5 w-full"
                />
              </div>
          )}
        </div>
        {!isDialog && (
          <p className="mt-2 flex items-start gap-1.5 text-xs leading-relaxed text-[#5b6b84]">
            <CircleHelp className="mt-0.5 size-3.5 shrink-0 text-[#6485b2]" aria-hidden="true" />
            Chọn môn học được phân công. Câu hỏi sẽ lưu ở trạng thái DRAFT trong kho luyện tập.
          </p>
        )}
      </section>}

      {mode === 'manual' ? (
        <form
          onSubmit={saveManualDraft}
          id="create-practice-manual-form"
          className={cn(
            'grid gap-4',
            step === 2 && !isDialog && 'lg:grid-cols-[minmax(0,1fr)_270px]',
          )}
        >
          <div className="space-y-4">
            {step === 2 && (
              <details className="group overflow-hidden rounded-xl border border-[#c4d8ec] bg-white">
                <summary className="flex cursor-pointer list-none items-center gap-2.5 px-3 py-2 text-sm font-semibold text-[#0F2854] hover:bg-[#f4faff] focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-[#1C4D8D] [&::-webkit-details-marker]:hidden">
                  <FilePenLine className="size-4 shrink-0 text-[#1C4D8D]" aria-hidden="true" />
                  <span className="flex-1">Xem câu hỏi</span>
                  <ChevronRight className="size-4 shrink-0 text-[#1C4D8D] group-open:rotate-90" aria-hidden="true" />
                </summary>
                <div className="space-y-3 border-t border-[#c4d8ec] p-3 [overflow-wrap:anywhere]">
                  <div className="space-y-1">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <h3 className="text-sm font-bold text-[#0F2854]">{title}</h3>
                      <QuestionImage imageUrl={imageUrl} imageAlt={imageDescription} triggerLabel="Xem ảnh" />
                    </div>
                    <p className="whitespace-pre-wrap text-sm text-slate-700">{content}</p>
                  </div>
                  <div className="space-y-1 rounded-lg bg-slate-50 p-2.5">
                    <h4 className="text-xs font-bold text-[#0F2854]">Đáp án mẫu</h4>
                    <p className="whitespace-pre-wrap text-sm text-slate-700">{sampleAnswer}</p>
                  </div>
                </div>
              </details>
            )}
            {step === 1 && <section className={cn('rounded-2xl border border-[#82aedb]/55 bg-white p-3 sm:p-4', ds.panelShadow)}>
              <div
                className={cn(
                  'grid gap-3',
                )}
              >
                <label className="block">
                  <span className={fieldLabelClass(Boolean(manualErrors.title))}>Tiêu đề</span>
                  <input
                    value={title}
                    onChange={(event) => {
                      setTitle(event.target.value)
                      clearManualError('title')
                    }}
                    maxLength={300}
                    placeholder="Tên ngắn gọn giúp nhận diện câu hỏi"
                    aria-invalid={Boolean(manualErrors.title)}
                    className={cn(
                      'mt-1.5 min-h-11 w-full rounded-xl border border-[#4988C4] bg-white px-3 text-sm text-[#0F2854] transition outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30',
                      manualErrors.title && fieldInvalidClass,
                    )}
                  />
                  <FieldError message={manualErrors.title} />
                </label>
              </div>

              <label className="mt-4 block">
                <span className={fieldLabelClass(Boolean(manualErrors.content))}>
                  Nội dung câu hỏi
                </span>
                <textarea
                  rows={2}
                  value={content}
                  onChange={(event) => {
                    setContent(event.target.value)
                    clearManualError('content')
                  }}
                  placeholder="Viết đầy đủ yêu cầu mà sinh viên cần trình bày..."
                  aria-invalid={Boolean(manualErrors.content)}
                  className={cn(
                    'mt-1.5 min-h-[calc(2lh+1.25rem+2px)] w-full resize-y [field-sizing:content] rounded-xl border border-[#4988C4] bg-white px-3 py-2.5 text-sm leading-relaxed text-[#0F2854] transition outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30',
                    manualErrors.content && fieldInvalidClass,
                  )}
                />
                <FieldError message={manualErrors.content} />
              </label>

              <div className="mt-3 flex flex-wrap items-center gap-2">
                <input type="file" ref={fileInputRef} onChange={handleImageFileChange}
                  accept="image/png,image/jpeg,image/webp" className="hidden" />
                {imageUrl && (
                  <div className="relative w-full">
                    <img
                      src={imageUrl}
                      alt={imageDescription || 'Ảnh đính kèm'}
                      className="h-auto w-full rounded-lg border border-slate-200 object-contain"
                    />
                    <button
                      type="button"
                      aria-label="Xóa ảnh đính kèm"
                      title="Xóa ảnh đính kèm"
                      onClick={handleRemoveImage}
                      disabled={isUploadingImage}
                      className="absolute top-2 right-2 grid size-7 place-items-center rounded-full border border-red-200 bg-red-50 text-red-700 enabled:hover:border-red-500 enabled:hover:bg-red-100 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:opacity-50"
                    >
                      <X className="size-3.5" aria-hidden="true" />
                    </button>
                  </div>
                )}
                <button type="button" disabled={isUploadingImage} onClick={() => fileInputRef.current?.click()} className={cn(ds.outlineButton, 'enabled:hover:border-[#1C4D8D] enabled:hover:bg-[#E7F3FB]')}>
                  {isUploadingImage ? <LoaderCircle className="size-4 animate-spin" /> : <ImageIcon className="size-4" />}
                  {isUploadingImage ? 'Đang tải…' : imageUrl ? 'Thay ảnh' : 'Thêm ảnh'}
                </button>
                {imageUrl ? (
                  <>
                    <input aria-label="Mô tả ảnh" value={imageDescription} onChange={(event) => setImageDescription(event.target.value)}
                      placeholder="Mô tả ảnh" className={cn(ds.field, 'min-w-40 flex-1')} />
                  </>
                ) : <span className="text-xs text-slate-600">PNG, JPG, WebP · tối đa 5 MB</span>}
              </div>

              <label className="mt-4 block">
                <span
                  className={cn(
                    'flex items-center justify-between gap-3',
                    fieldLabelClass(Boolean(manualErrors.sampleAnswer)),
                  )}
                >
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
                  rows={2}
                  value={sampleAnswer}
                  onChange={(event) => {
                    setSampleAnswer(event.target.value)
                    clearManualError('sampleAnswer')
                  }}
                  placeholder="Câu trả lời chuẩn mực dùng làm mốc chấm..."
                  aria-invalid={Boolean(manualErrors.sampleAnswer)}
                  className={cn(
                    'mt-1.5 min-h-[calc(2lh+1.25rem+2px)] w-full resize-y [field-sizing:content] rounded-xl border border-[#4988C4] bg-white px-3 py-2.5 text-sm leading-relaxed text-[#0F2854] transition outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30',
                    manualErrors.sampleAnswer && fieldInvalidClass,
                  )}
                />
                <FieldError message={manualErrors.sampleAnswer} />
              </label>

              <details className="mt-3">
                <summary className="cursor-pointer text-sm font-semibold text-[#0F2854]">
                  Ý chính (tùy chọn)
                </summary>
                <textarea
                  aria-label="Ý chính, mỗi dòng một ý"
                  value={keyPointsInput}
                  onChange={(event) => setKeyPointsInput(event.target.value)}
                  placeholder={'Khái niệm trọng tâm\nLập luận cần có\nVí dụ hoặc tình huống'}
                  className="mt-1.5 min-h-24 w-full resize-y rounded-xl border border-[#4988C4] bg-white px-3 py-2.5 text-sm leading-relaxed text-[#0F2854] transition outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30"
                />
              </details>

            </section>}

            {step === 2 && <section className={cn('rounded-2xl border-2 bg-white p-3 sm:p-4', rubricIsValid ? 'border-[#8bd6bd]' : 'border-[#e46a74]', ds.panelShadow)}>
              <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                <div className="flex items-center gap-3">
                  <div>
                    <h2 className="text-base font-bold text-[#0F2854] sm:text-lg">Tiêu chí chấm điểm</h2>
                    <p className="mt-0.5 text-xs text-[#5b6b84]">
                      Tối thiểu hai tiêu chí, tổng điểm bằng chính xác 10.0.
                    </p>
                  </div>
                </div>
                <div className="flex flex-wrap items-center gap-2">
                  <span
                    className={cn(
                      'inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-xs font-bold tabular-nums',
                      rubricIsValid ? 'bg-[#eaf9f4] text-[#14765c]' : 'bg-[#fff0f1] text-[#ae3945]',
                    )}
                  >
                    {rubricIsValid && <Check className="size-3" aria-hidden="true" />}
                    {totalRubricScore.toFixed(1)} / 10 điểm
                  </span>
                  <button
                    type="button"
                    onClick={addCriterion}
                    className="inline-flex min-h-9 items-center justify-center gap-1.5 rounded-xl border border-[#82aedb]/55 bg-[#4988C4]/16 px-3 text-xs font-bold text-[#5b6b84] transition duration-200 hover:-translate-y-0.5 hover:bg-[#f0f4fb] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transform-none enabled:hover:border-[#1C4D8D]"
                  >
                    <Plus className="size-3.5" />
                    Thêm tiêu chí
                  </button>
                </div>
              </div>

              <div className="mt-3 grid gap-3 sm:grid-cols-2">
                <label className="block">
                  <span className={fieldLabelClass(Boolean(manualErrors.rubricName))}>Tên rubric</span>
                  <input
                    value={rubricName}
                    onChange={(event) => {
                      setRubricName(event.target.value)
                      clearManualError('rubricName')
                    }}
                    placeholder="Ví dụ: Rubric đánh giá kiến trúc"
                    aria-invalid={Boolean(manualErrors.rubricName)}
                    className={cn(
                      'mt-1.5 min-h-11 w-full rounded-xl border border-[#4988C4] bg-white px-3 text-sm outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30',
                      manualErrors.rubricName && fieldInvalidClass,
                    )}
                  />
                  <FieldError message={manualErrors.rubricName} />
                </label>
                <label className="block">
                  <span className="text-xs font-bold text-[#5b6b84] uppercase">Mô tả</span>
                  <input
                    value={rubricDescription}
                    onChange={(event) => setRubricDescription(event.target.value)}
                    placeholder="Mục tiêu đánh giá của rubric"
                    className="mt-1.5 min-h-11 w-full rounded-xl border border-[#4988C4] bg-white px-3 text-sm outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30"
                  />
                </label>
              </div>

              <div className="mt-4 space-y-3">
                {criteria.map((criterion, index) => (
                  <div key={criterion.localId} className="relative">
                    <details
                      open={criterionErrors[criterion.localId] ? true : undefined}
                      className="rounded-2xl border border-[#82aedb]/55 bg-white p-3.5"
                    >
                      <summary className="cursor-pointer rounded-lg py-1 pr-10 text-sm font-semibold text-[#0F2854]">
                        {criterion.criterionName || `Tiêu chí ${index + 1}`} · {criterion.maxScore} điểm
                      </summary>
                      <div className="grid gap-3 sm:grid-cols-[minmax(0,1fr)_100px]">
                        <label className="block">
                          <span className={fieldLabelClass(Boolean(criterionErrors[criterion.localId]?.name))}>
                            Tiêu chí {index + 1}
                          </span>
                          <input
                            value={criterion.criterionName}
                            onChange={(event) =>
                              updateCriterion(criterion.localId, 'criterionName', event.target.value)
                            }
                            placeholder="Tên tiêu chí đánh giá"
                            aria-invalid={Boolean(criterionErrors[criterion.localId]?.name)}
                            className={cn(
                              'mt-1 min-h-10 w-full rounded-xl border border-[#4988C4] bg-white px-3 text-xs outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30',
                              criterionErrors[criterion.localId]?.name && fieldInvalidClass,
                            )}
                          />
                          <FieldError message={criterionErrors[criterion.localId]?.name} />
                        </label>
                        <label className="block">
                          <span className={fieldLabelClass(Boolean(criterionErrors[criterion.localId]?.score))}>
                            Điểm
                          </span>
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
                            aria-invalid={Boolean(criterionErrors[criterion.localId]?.score)}
                            className={cn(
                              'mt-1 min-h-10 w-full rounded-xl border border-[#4988C4] bg-white px-2 text-center font-mono text-sm font-black outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30',
                              criterionErrors[criterion.localId]?.score && fieldInvalidClass,
                            )}
                          />
                          <FieldError message={criterionErrors[criterion.localId]?.score} />
                        </label>
                      </div>
                      <label className="mt-3 block">
                        <span className="sr-only">Mô tả tiêu chí {index + 1}</span>
                        <textarea
                          rows={2}
                          value={criterion.description}
                          onChange={(event) =>
                            updateCriterion(criterion.localId, 'description', event.target.value)
                          }
                          placeholder="Mô tả biểu hiện đạt điểm của tiêu chí..."
                          className="w-full resize-y rounded-lg border border-[#4988C4] bg-white px-3 py-2 text-xs leading-relaxed outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30"
                        />
                      </label>
                    </details>
                    <button
                      type="button"
                      onClick={() => removeCriterion(criterion.localId)}
                      disabled={criteria.length <= 2}
                      className="absolute top-3 right-3 flex size-8 items-center justify-center rounded-lg border border-red-200 bg-red-50 text-red-700 transition-colors enabled:hover:border-red-500 enabled:hover:bg-red-100 focus-visible:outline-2 focus-visible:outline-[#b43d49] disabled:cursor-not-allowed disabled:opacity-35"
                      aria-label={`Xóa tiêu chí ${index + 1}`}
                    >
                      <Trash2 className="size-4" />
                    </button>
                  </div>
                ))}
                <FieldError message={manualErrors.criteria} />
                <FieldError message={manualErrors.rubricTotal} />
              </div>
            </section>}
          </div>

          {step === 2 && !isDialog && <aside
            className={cn(
              'space-y-4 self-start',
              isDialog ? 'lg:sticky lg:top-0' : 'xl:sticky xl:top-20',
            )}
          >

            {manualNotice && !isDialog && (
              <div
                className="rounded-2xl border border-[#bce4d7] bg-[#effaf6] p-4 text-xs leading-relaxed font-semibold text-[#176f58]"
                role="status"
              >
                {manualNotice}
              </div>
            )}

            {!isDialog && (
              <>
                <div className="space-y-2">
                  <button
                    type="submit"
                    disabled={isSaving || Boolean(savedQuestionId)}
                    className="inline-flex min-h-12 w-full items-center justify-center gap-2 rounded-xl bg-[#1C4D8D] px-4 text-sm font-bold text-white shadow-[0_10px_24px_rgba(36,94,234,0.24)] transition hover:-translate-y-0.5 hover:bg-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-60 border border-transparent enabled:hover:border-[#1C4D8D]"
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
                        className="inline-flex h-10 w-full items-center justify-center gap-2 rounded-xl bg-[#16836a] px-4 text-sm font-bold text-white shadow-[0_8px_18px_rgba(22,131,106,0.22)] transition duration-200 hover:-translate-y-0.5 hover:bg-[#136f5a] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#16836a] disabled:cursor-not-allowed disabled:opacity-60 motion-reduce:transform-none border border-transparent enabled:hover:border-[#1C4D8D]"
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
                        className="inline-flex min-h-10 w-full items-center justify-center gap-2 rounded-xl border border-[#4988C4] bg-white px-4 text-xs font-bold text-[#1C4D8D] transition duration-200 hover:-translate-y-0.5 hover:border-[#1C4D8D] hover:bg-[#E7F3FB] hover:shadow-[0_8px_16px_rgba(15,40,84,0.14)] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transform-none"
                      >
                        Đến ngân hàng luyện tập
                        <ChevronRight className="size-3.5" />
                      </Link>
                      <button
                        type="button"
                        onClick={resetManualDraft}
                        className="inline-flex min-h-10 w-full items-center justify-center gap-2 rounded-xl border border-[#4988C4] bg-white px-4 text-xs font-bold text-[#526780] transition duration-200 hover:-translate-y-0.5 hover:bg-[#4988C4]/18 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transform-none enabled:hover:border-[#1C4D8D]"
                      >
                        <RotateCcw className="size-3.5" />
                        Soạn câu hỏi khác
                      </button>
                    </>
                  )}
                </div>

                <p className="rounded-xl border border-[#82aedb]/55 bg-[#4988C4]/16 p-3 text-xs leading-relaxed text-[#5b6b84]">
                  Luồng này chỉ ghi vào <strong>practice_questions</strong>. Sau khi lưu DRAFT, nhấn
                  “Gửi Trưởng Bộ môn duyệt” hoặc chọn nhiều bản nháp tại Ngân hàng câu hỏi luyện tập.
                </p>
              </>
            )}
          </aside>}
          {step === 1 && !isDialog && (
            <button type="submit" disabled={dialogBusy} className={cn(ds.primaryButton, 'justify-self-start border border-transparent enabled:hover:border-[#0F2854] enabled:hover:bg-[#0F2854]')}>
              Tiếp tục
              <ChevronRight className="size-4" />
            </button>
          )}
        </form>
      ) : (
        <div className="space-y-4">
          {step === 1 && <form
            id="create-practice-ai-form"
            onSubmit={generateQuestions}
            className={cn(
              'grid gap-4',
              !isDialog && 'xl:grid-cols-[minmax(0,1fr)_330px]',
            )}
          >
            <section className={cn('rounded-2xl border border-[#82aedb]/55 bg-white p-3 sm:p-4', ds.panelShadow)}>
              <div className="grid gap-3 sm:grid-cols-2">
                <label className="block">
                  <span className={fieldLabelClass(Boolean(aiErrors.clo))}>
                    Mã CLO · ngăn cách bằng dấu phẩy
                  </span>
                  <textarea
                    rows={2}
                    value={cloInput}
                    onChange={(event) => {
                      setCloInput(event.target.value)
                      clearAiError('clo')
                    }}
                    placeholder="CLO1, CLO2"
                    aria-invalid={Boolean(aiErrors.clo)}
                    className={cn(
                      'mt-1.5 w-full resize-none overflow-y-auto rounded-xl border border-[#4988C4] bg-white px-3 py-2.5 text-sm leading-relaxed text-[#0F2854] transition outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30',
                      aiErrors.clo && fieldInvalidClass,
                    )}
                  />
                  <FieldError message={aiErrors.clo} />
                </label>
                <label className="block">
                  <span className={fieldLabelClass(Boolean(aiErrors.topics))}>
                    Chủ đề · mỗi dòng một nội dung
                  </span>
                  <textarea
                    rows={2}
                    value={topicsInput}
                    onChange={(event) => {
                      setTopicsInput(event.target.value)
                      clearAiError('topics')
                    }}
                    placeholder={'Kiến trúc hệ thống\nThiết kế API'}
                    aria-invalid={Boolean(aiErrors.topics)}
                    className={cn(
                      'mt-1.5 w-full resize-none overflow-y-auto rounded-xl border border-[#4988C4] bg-white px-3 py-2.5 text-sm leading-relaxed text-[#0F2854] transition outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30',
                      aiErrors.topics && fieldInvalidClass,
                    )}
                  />
                  <FieldError message={aiErrors.topics} />
                </label>
              </div>

              <details open={aiErrors.numberOfQuestions ? true : undefined} className="mt-3 rounded-xl border border-slate-200 px-3 py-2">
                <summary className="cursor-pointer text-sm font-semibold text-[#0F2854]">Tùy chỉnh độ khó và Bloom</summary>
                <div className="mt-3">
                  <span className="text-xs font-bold text-[#5b6b84] uppercase">
                    Bậc Bloom mục tiêu
                  </span>
                  <div className="mt-2 grid grid-cols-2 gap-1.5 min-[420px]:grid-cols-3">
                    {BLOOM_LEVELS.map((level) => {
                      const selected = targetBloomLevels.includes(level)
                      return (
                        <button
                          key={level}
                          type="button"
                          onClick={() => toggleBloomLevel(level)}
                          className={cn(
                            'inline-flex min-h-8 items-center justify-center gap-1 rounded-lg border px-1 text-xs font-bold transition focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]',
                            selected
                              ? 'border-emerald-500 bg-emerald-100 text-emerald-900 hover:border-emerald-600 hover:bg-emerald-200'
                              : 'border-[#82aedb]/55 bg-white text-[#6d7d91] hover:border-[#4988C4] hover:bg-[#f3f6fa]',
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

                <div className="mt-3 border-t border-slate-200 pt-3">
                  <div className="flex items-center justify-between gap-2">
                    <span className="text-xs font-bold text-[#5b6b84] uppercase">
                      Phân bổ độ khó
                    </span>
                    <span className="text-xs font-semibold tabular-nums text-slate-600">{numberOfQuestions} câu</span>
                  </div>
                  <div className="mt-2 grid w-full grid-cols-3 gap-x-3 sm:gap-x-8">
                    {[
                      { label: 'Dễ', value: easyCount, setValue: setEasyCount },
                      { label: 'Trung bình', value: mediumCount, setValue: setMediumCount },
                      { label: 'Khó', value: hardCount, setValue: setHardCount },
                    ].map(({ label, value, setValue }) => (
                      <label key={label} className="grid min-w-0 grid-cols-[minmax(0,max-content)_auto] items-center gap-1.5 first:justify-self-start last:justify-self-end nth-2:justify-self-center">
                        <span aria-hidden="true" className="invisible col-start-1 row-start-1 text-sm font-bold">Trung bình:</span>
                        <span className="col-start-1 row-start-1 text-sm font-bold text-[#0F2854]">{label}:</span>
                        <input
                          type="number"
                          min={0}
                          max={10}
                          value={value}
                          onChange={(event) => {
                            setValue(Number(event.target.value))
                            clearAiError('numberOfQuestions')
                          }}
                          aria-invalid={Boolean(aiErrors.numberOfQuestions)}
                          className={cn(ds.field, 'min-h-9 min-w-0 w-10 shrink px-1 text-center tabular-nums sm:w-16', aiErrors.numberOfQuestions && fieldInvalidClass)}
                        />
                      </label>
                    ))}
                    <FieldError message={aiErrors.numberOfQuestions} />
                  </div>
                </div>
              </details>
            </section>

            {!isDialog && (
            <aside
              className={cn(
                'space-y-4 self-start',
                isDialog ? 'lg:sticky lg:top-0' : 'xl:sticky xl:top-20',
              )}
            >
              <button
                type="submit"
                disabled={isGenerating}
                className="inline-flex min-h-12 w-full items-center justify-center gap-2 rounded-xl bg-[#1C4D8D] px-4 text-sm font-bold text-white shadow-[0_10px_24px_rgba(28,77,141,0.28)] transition hover:-translate-y-0.5 hover:bg-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-60 border border-transparent enabled:hover:border-[#1C4D8D]"
              >
                {isGenerating ? (
                  <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" />
                ) : (
                  <WandSparkles className="size-4" />
                )}
                {isGenerating ? 'AI đang phân tích...' : 'Tạo bộ câu hỏi bằng AI'}
              </button>
            </aside>
            )}
          </form>}

          {step === 2 && generation && (
            <section className={cn('overflow-hidden rounded-2xl border border-[#82aedb]/55 bg-white', ds.panelShadow)}>
              <div className="flex flex-col gap-3 border-b border-[#82aedb]/55 bg-[#F4FBFD]/80 px-4 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-5">
                <div>
                  <p className="text-xs font-bold tracking-[0.08em] text-[#1C4D8D] uppercase">
                    Xem trước bản nháp AI · {generation.courseCode}
                  </p>
                  <h2 className="mt-1 text-base font-bold text-[#0F2854] sm:text-lg">
                    {generation.generatedCount} đề xuất cần giảng viên rà soát
                  </h2>
                </div>
                {!isDialog && (
                <button
                  type="button"
                  onClick={() => void saveGeneratedDrafts()}
                  disabled={selectedGenerated.size === 0 || isSavingGenerated}
                  className="inline-flex min-h-10 items-center justify-center gap-2 rounded-xl bg-[#1C4D8D] px-4 text-xs font-bold text-white shadow-[0_8px_20px_rgba(28,77,141,0.22)] transition duration-200 hover:-translate-y-0.5 hover:bg-[#0F2854] hover:shadow-[0_12px_22px_rgba(15,40,84,0.28)] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-50 motion-reduce:transform-none border border-transparent enabled:hover:border-[#1C4D8D]"
                >
                  {isSavingGenerated ? (
                    <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" />
                  ) : (
                    <Save className="size-3.5" />
                  )}
                  Lưu {selectedGenerated.size} bản nháp đã chọn
                </button>
                )}
              </div>

              {aiNotice && (
                <div
                  className="mx-4 mt-4 rounded-2xl border border-[#bce4d7] bg-[#effaf6] p-4 text-xs leading-relaxed font-semibold text-[#176f58]"
                  role="status"
                >
                  {aiNotice}
                </div>
              )}

              <div className="grid gap-2 p-3 sm:p-4">
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
                          ? 'border-[#82aedb]/55 bg-[#4988C4]/16 shadow-[0_8px_22px_rgba(36,94,234,0.08)]'
                          : 'border-[#82aedb]/55 bg-white',
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

                      <details className="mt-2">
                        <summary className="cursor-pointer text-xs font-semibold text-[#1C4D8D]">Xem đáp án và barem</summary>
                      <p className="mt-2 whitespace-pre-wrap text-xs leading-relaxed text-[#63738a]">
                        {question.content}
                      </p>
                      <p className="mt-2 whitespace-pre-wrap text-sm text-slate-700">{question.modelAnswer}</p>
                      <ul className="mt-2 space-y-1 text-xs text-slate-700">
                        {question.rubric.criteria.map((criterion, index) => (
                          <li key={index}>{criterion.criterionName} · {criterion.maxScore} điểm</li>
                        ))}
                      </ul>
                      <QuestionImage
                        imageUrl={question.imageUrl}
                        imageAlt={question.imageAlt}
                        className="mt-3"
                      />
                      <div className="mt-3 flex flex-wrap items-center justify-between gap-2 border-t border-[#82aedb]/55 pt-3 text-xs font-semibold text-[#7a899c]">
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
                            className="inline-flex min-h-8 items-center gap-1.5 rounded-lg border border-[#4988C4] bg-white px-2.5 text-xs font-bold text-[#1C4D8D] transition hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-45 enabled:hover:border-[#1C4D8D]"
                          >
                            <PencilLine className="size-3" />
                            Chỉnh sửa trước khi lưu
                          </button>
                        </div>
                      </div>
                      </details>
                    </article>
                  )
                })}
              </div>

              <div className="flex items-start gap-2 border-t border-[#82aedb]/55 bg-[#fafbfe] px-4 py-3 text-xs leading-relaxed text-[#5b6b84] sm:px-5">
                <Send className="mt-0.5 size-3.5 shrink-0 text-[#1C4D8D]" />
                Lưu ở đây chỉ tạo DRAFT. Sau khi tự rà soát, giảng viên chọn các bản nháp trong Ngân
                hàng đề để gửi Trưởng Bộ môn thẩm định.
                {!isDialog && (
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
  )

  if (isDialog) {
    return (
      <FormDialog
        titleId="create-practice-question-title"
        eyebrow="Kho luyện tập"
        title={mode ? flowName : 'Chọn cách tạo câu hỏi'}
        icon={Sparkles}
        onClose={() => onClose?.()}
        busy={dialogBusy}
        size="lg"
        maxHeight="max-h-[min(90dvh,640px)]"
        beforeBody={progress}
        footer={mode ? dialogFooter : undefined}
      >
        {formFields}
      </FormDialog>
    )
  }

  return (
    <div className={ds.page}>
      {pageChrome}
      {formFields}
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
  if (!open) return null
  return (
    <CreatePracticeQuestionForm
      presentation="dialog"
      onClose={onClose}
      onCreated={onCreated}
    />
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
