import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
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
  Upload,
  ImageIcon,
  WandSparkles,
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
import { BannerFact, LecturerPageBanner } from '@/design/ui'
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
  distribution?: string
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
  const difficultyTotal = easyCount + mediumCount + hardCount

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
    setSelectedGenerated((current) => {
      const next = new Set(current)
      next.delete(question.tempId)
      return next
    })
    setSavedQuestionId('')
    setManualErrors({})
    setCriterionErrors({})
    setManualNotice('Đã nạp đề xuất AI. Hãy rà soát và chỉnh sửa trước khi lưu bản nháp.')
    setMode('manual')
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
    if (isSaving || savedQuestionId) return
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
    if (!Number.isInteger(numberOfQuestions) || numberOfQuestions < 1 || numberOfQuestions > 10) {
      errors.numberOfQuestions = 'Số câu cần sinh phải từ 1 đến 10.'
    }
    if (difficultyTotal !== numberOfQuestions) {
      errors.distribution = 'Tổng số câu Dễ + Trung bình + Khó phải bằng số câu cần sinh.'
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

  const modeToggle = (
    <div
      className={cn(
        'rounded-2xl border border-[#82aedb]/55 bg-white p-1',
        isDialog ? 'grid w-full grid-cols-2 gap-1' : 'inline-flex',
      )}
      role="tablist"
      aria-label="Chế độ tạo câu hỏi"
    >
      <button
        type="button"
        role="tab"
        aria-selected={mode === 'manual'}
        onClick={() => setMode('manual')}
        className={cn(
          'inline-flex min-h-10 items-center justify-center gap-2 rounded-xl px-3 text-sm font-bold transition',
          mode === 'manual'
            ? 'bg-[#1C4D8D] text-white shadow-[0_8px_16px_rgba(28,77,141,0.22)]'
            : 'text-[#5f7088] hover:bg-[#F4FBFD] hover:text-[#0F2854]',
        )}
      >
        <FilePenLine className="size-3.5 shrink-0" />
        Soạn thủ công
      </button>
      <button
        type="button"
        role="tab"
        aria-selected={mode === 'ai'}
        onClick={() => setMode('ai')}
        className={cn(
          'inline-flex min-h-10 items-center justify-center gap-2 rounded-xl px-3 text-sm font-bold transition',
          mode === 'ai'
            ? 'bg-[#1C4D8D] text-white shadow-[0_8px_16px_rgba(28,77,141,0.22)]'
            : 'text-[#5f7088] hover:bg-[#F4FBFD] hover:text-[#0F2854]',
        )}
      >
        <WandSparkles className="size-3.5 shrink-0" />
        AI từ FLM
      </button>
    </div>
  )

  const dialogBusy = isSaving || isSubmittingReview || isGenerating

  const dialogFooter = (
    <DialogFooter className="bg-white sm:items-center sm:justify-between">
      {mode === 'manual' ? (
        <>
          <p className="hidden text-xs text-[#5b6b84] sm:block">
            Lưu DRAFT → gửi Trưởng BM duyệt → vào kho luyện tập.
          </p>
          <div className="flex w-full flex-col gap-2 sm:ml-auto sm:w-auto sm:flex-row sm:items-center">
            {manualNotice && (
              <p
                className="order-first text-xs font-semibold text-[#176f58] sm:order-none sm:mr-1"
                role="status"
              >
                {manualNotice}
              </p>
            )}
            {savedQuestionId ? (
              <>
                <button type="button" onClick={resetManualDraft} disabled={dialogBusy} className={ds.outlineButton}>
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
                <button type="button" onClick={onClose} disabled={dialogBusy} className={ds.outlineButton}>
                  Đóng
                </button>
              </>
            ) : (
              <>
                <button type="button" onClick={onClose} disabled={dialogBusy} className={ds.outlineButton}>
                  Hủy
                </button>
                <button type="submit" form="create-practice-manual-form" disabled={isSaving} className={ds.primaryButton}>
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
        </>
      ) : (
        <button type="button" onClick={onClose} disabled={dialogBusy} className={ds.outlineButton}>
          Đóng
        </button>
      )}
    </DialogFooter>
  )

  const pageChrome = !isDialog ? (
    <>
      <LecturerPageBanner
        icon={Sparkles}
        eyebrow="Câu hỏi luyện tập"
        title="Tạo câu hỏi luyện tập"
        description="Soạn câu hỏi, lưu bản nháp rồi gửi trưởng bộ môn duyệt trước khi dùng cho luyện tập, thi thử và ca thi trên lớp."
        extras={
          <>
            <BannerFact icon={FilePenLine}>Soạn thủ công</BannerFact>
            <BannerFact icon={WandSparkles} tone="green">
              AI từ FLM
            </BannerFact>
            <BannerFact icon={Send} tone="violet">
              Lưu bản nháp
            </BannerFact>
          </>
        }
      />
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <Link
          to="/question-bank"
          className="inline-flex min-h-10 w-fit items-center gap-1.5 text-sm font-bold text-[#1C4D8D] transition duration-200 hover:-translate-y-0.5 hover:text-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transform-none"
        >
          <ArrowLeft className="size-4" aria-hidden="true" />
          Quay lại ngân hàng đề
        </Link>
        {modeToggle}
      </div>
    </>
  ) : null

  const formFields = (
    <div className="space-y-4">
      <section
        className={cn(
          'rounded-2xl border border-[#82aedb]/55 p-4',
          ds.panelShadow,
          isDialog ? 'bg-white' : 'bg-[linear-gradient(135deg,#BDE8F5,#BDE8F5)]',
        )}
      >
        <div
          className={cn(
            'grid gap-3',
            isDialog ? 'sm:grid-cols-1' : 'max-w-4xl sm:grid-cols-[minmax(0,1fr)_auto] sm:items-end',
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
              <span className="tracking-normal text-[#b03a46] normal-case">Bắt buộc</span>
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
            <section className={cn('rounded-2xl border border-[#82aedb]/55 bg-white p-4 sm:p-5', ds.panelShadow)}>
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
                <span className={fieldLabelClass(Boolean(manualErrors.content))}>
                  Nội dung câu hỏi
                </span>
                <textarea
                  value={content}
                  onChange={(event) => {
                    setContent(event.target.value)
                    clearManualError('content')
                  }}
                  placeholder="Viết đầy đủ yêu cầu mà sinh viên cần trình bày..."
                  aria-invalid={Boolean(manualErrors.content)}
                  className={cn(
                    'mt-1.5 min-h-28 w-full resize-y rounded-xl border border-[#4988C4] bg-white px-3 py-2.5 text-sm leading-relaxed text-[#0F2854] transition outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30',
                    manualErrors.content && fieldInvalidClass,
                  )}
                />
                <FieldError message={manualErrors.content} />
              </label>

              {/* Technical Diagram / Image Upload (MF-03) */}
              <div className="mt-4 rounded-xl border border-dashed border-[#82aedb] bg-[#f8fbfe] p-3.5">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div className="flex items-center gap-2">
                    <ImageIcon className="size-4 text-[#1C4D8D]" />
                    <span className="text-xs font-bold text-[#0F2854] uppercase">
                      Hình ảnh sơ đồ / Kiến trúc kỹ thuật (Tùy chọn)
                    </span>
                  </div>
                  <span className="text-[11px] text-[#63738a]">PNG, JPG, WEBP &le; 5MB</span>
                </div>

                <input
                  type="file"
                  ref={fileInputRef}
                  onChange={handleImageFileChange}
                  accept="image/png,image/jpeg,image/webp"
                  className="hidden"
                />

                {imageUrl ? (
                  <div className="mt-3 space-y-3">
                    <div className="relative inline-block overflow-hidden rounded-xl border border-[#82aedb]/55 bg-white shadow-sm">
                      <img
                        src={imageUrl}
                        alt="Sơ đồ câu hỏi"
                        className="max-h-56 w-auto object-contain"
                      />
                      <button
                        type="button"
                        onClick={handleRemoveImage}
                        className="absolute top-2 right-2 flex size-7 items-center justify-center rounded-lg bg-black/60 text-white transition hover:bg-[#b43d49]"
                        title="Xóa hình ảnh này"
                      >
                        <Trash2 className="size-3.5" />
                      </button>
                    </div>
                    <label className="block">
                      <span className="text-xs font-semibold text-[#5b6b84]">
                        Mô tả hình ảnh (Gemini Multimodal CoT Grading)
                      </span>
                      <input
                        value={imageDescription}
                        onChange={(e) => setImageDescription(e.target.value)}
                        placeholder="Ví dụ: Sơ đồ kiến trúc microservices với message broker Kafka..."
                        className="mt-1 w-full rounded-xl border border-[#4988C4] bg-white px-3 py-2 text-xs text-[#0F2854] outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30"
                      />
                    </label>
                  </div>
                ) : (
                  <div className="mt-2.5 flex items-center gap-3">
                    <button
                      type="button"
                      disabled={isUploadingImage}
                      onClick={() => fileInputRef.current?.click()}
                      className="inline-flex items-center gap-1.5 rounded-xl border border-[#4988C4] bg-white px-3 py-2 text-xs font-bold text-[#1C4D8D] shadow-sm transition hover:bg-[#edf5fc] disabled:cursor-not-allowed disabled:opacity-50"
                    >
                      {isUploadingImage ? (
                        <>
                          <LoaderCircle className="size-3.5 animate-spin" />
                          Đang tải và kiểm tra Magic Bytes...
                        </>
                      ) : (
                        <>
                          <Upload className="size-3.5" />
                          Chọn ảnh sơ đồ đính kèm
                        </>
                      )}
                    </button>
                    <span className="text-xs text-[#63738a]">
                      Hỗ trợ thẩm định và chấm điểm đa phương thức CoT tự động
                    </span>
                  </div>
                )}
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
                  value={sampleAnswer}
                  onChange={(event) => {
                    setSampleAnswer(event.target.value)
                    clearManualError('sampleAnswer')
                  }}
                  placeholder="Câu trả lời chuẩn mực dùng làm mốc chấm..."
                  aria-invalid={Boolean(manualErrors.sampleAnswer)}
                  className={cn(
                    'mt-1.5 min-h-36 w-full resize-y rounded-xl border border-[#4988C4] bg-white px-3 py-2.5 text-sm leading-relaxed text-[#0F2854] transition outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30',
                    manualErrors.sampleAnswer && fieldInvalidClass,
                  )}
                />
                <FieldError message={manualErrors.sampleAnswer} />
              </label>

              <label className="mt-4 block">
                <span className="text-xs font-bold text-[#5b6b84] uppercase">
                  Key points · mỗi dòng một ý
                </span>
                <textarea
                  value={keyPointsInput}
                  onChange={(event) => setKeyPointsInput(event.target.value)}
                  placeholder={'Khái niệm trọng tâm\nLập luận cần có\nVí dụ hoặc tình huống'}
                  className="mt-1.5 min-h-24 w-full resize-y rounded-xl border border-[#4988C4] bg-white px-3 py-2.5 text-sm leading-relaxed text-[#0F2854] transition outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30"
                />
              </label>

              <div className="mt-4 block max-w-xs">
                <span className="text-xs font-bold text-[#5b6b84] uppercase">Độ khó</span>
                <FilterDropMenu
                  label="Chọn độ khó"
                  value={difficulty}
                  onChange={(value) => changeDifficulty(value as QuestionDifficulty)}
                  options={DIFFICULTIES}
                  className="mt-1.5 w-full"
                />
              </div>
            </section>

            <section className={cn('rounded-2xl border border-[#82aedb]/55 bg-white p-4 sm:p-5', ds.panelShadow)}>
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
                  className="inline-flex min-h-9 items-center justify-center gap-1.5 rounded-xl border border-[#82aedb]/55 bg-[#4988C4]/16 px-3 text-xs font-bold text-[#5b6b84] transition duration-200 hover:-translate-y-0.5 hover:bg-[#f0f4fb] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transform-none"
                >
                  <Plus className="size-3.5" />
                  Thêm tiêu chí
                </button>
              </div>

              <div className="mt-5 grid gap-4 sm:grid-cols-2">
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
                  <article
                    key={criterion.localId}
                    className="rounded-2xl border border-[#82aedb]/55 bg-white p-3.5"
                  >
                    <div className="grid gap-3 sm:grid-cols-[minmax(0,1fr)_100px_auto]">
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
                        className="min-h-16 w-full resize-y rounded-lg border border-[#4988C4] bg-white px-3 py-2 text-xs leading-relaxed outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30"
                      />
                    </label>
                  </article>
                ))}
                <FieldError message={manualErrors.criteria} />
                <FieldError message={manualErrors.rubricTotal} />
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
                        className="inline-flex h-10 w-full items-center justify-center gap-2 rounded-xl bg-[#16836a] px-4 text-sm font-bold text-white shadow-[0_8px_18px_rgba(22,131,106,0.22)] transition duration-200 hover:-translate-y-0.5 hover:bg-[#136f5a] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#16836a] disabled:cursor-not-allowed disabled:opacity-60 motion-reduce:transform-none"
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
                        className="inline-flex min-h-10 w-full items-center justify-center gap-2 rounded-xl border border-[#4988C4] bg-white px-4 text-xs font-bold text-[#526780] transition duration-200 hover:-translate-y-0.5 hover:bg-[#4988C4]/18 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transform-none"
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
            <section className={cn('rounded-2xl border border-[#82aedb]/55 bg-white p-4 sm:p-5', ds.panelShadow)}>
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
                  <span className={fieldLabelClass(Boolean(aiErrors.clo))}>
                    Mã CLO · ngăn cách bằng dấu phẩy
                  </span>
                  <textarea
                    value={cloInput}
                    onChange={(event) => {
                      setCloInput(event.target.value)
                      clearAiError('clo')
                    }}
                    placeholder="CLO1, CLO2"
                    aria-invalid={Boolean(aiErrors.clo)}
                    className={cn(
                      'mt-1.5 min-h-20 w-full resize-y rounded-xl border border-[#4988C4] bg-white px-3 py-2.5 text-sm outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30',
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
                    value={topicsInput}
                    onChange={(event) => {
                      setTopicsInput(event.target.value)
                      clearAiError('topics')
                    }}
                    placeholder={'Kiến trúc hệ thống\nThiết kế API'}
                    aria-invalid={Boolean(aiErrors.topics)}
                    className={cn(
                      'mt-1.5 min-h-20 w-full resize-y rounded-xl border border-[#4988C4] bg-white px-3 py-2.5 text-sm outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30',
                      aiErrors.topics && fieldInvalidClass,
                    )}
                  />
                  <FieldError message={aiErrors.topics} />
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
                            ? 'border-[#82aedb]/55 bg-[#4988C4]/16 text-[#1C4D8D]'
                            : 'border-[#82aedb]/55 bg-white text-[#6d7d91] hover:bg-[#f3f6fa]',
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
                  <span className={fieldLabelClass(Boolean(aiErrors.numberOfQuestions))}>
                    Số câu cần sinh
                  </span>
                  <input
                    type="number"
                    min="1"
                    max="10"
                    value={numberOfQuestions}
                    onChange={(event) => {
                      setNumberOfQuestions(Number(event.target.value))
                      clearAiError('numberOfQuestions', 'distribution')
                    }}
                    aria-invalid={Boolean(aiErrors.numberOfQuestions)}
                    className={cn(
                      'mt-1.5 min-h-11 w-full rounded-xl border border-[#4988C4] bg-white px-3 font-mono text-sm font-bold outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30',
                      aiErrors.numberOfQuestions && fieldInvalidClass,
                    )}
                  />
                  <FieldError message={aiErrors.numberOfQuestions} />
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
                        onChange={(event) => {
                          item.setter(Number(event.target.value))
                          clearAiError('distribution')
                        }}
                        aria-invalid={Boolean(aiErrors.distribution)}
                        className={cn(
                          'mt-1 min-h-10 w-full rounded-xl border border-[#4988C4] bg-white px-3 text-center font-mono text-sm font-bold outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30',
                          aiErrors.distribution && fieldInvalidClass,
                        )}
                      />
                    </label>
                  ))}
                </div>
                <FieldError message={aiErrors.distribution} />
              </div>
            </section>

            <aside
              className={cn(
                'space-y-4 self-start',
                isDialog ? 'lg:sticky lg:top-0' : 'xl:sticky xl:top-20',
              )}
            >
              <section className={cn('rounded-2xl border border-[#82aedb]/55 bg-[linear-gradient(145deg,#F4FBFD,#E8F4FA)] p-5', ds.panelShadow)}>
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

              {aiNotice && (
                <div
                  className="rounded-2xl border border-[#bce4d7] bg-[#effaf6] p-4 text-xs leading-relaxed font-semibold text-[#176f58]"
                  role="status"
                >
                  {aiNotice}
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
                <button
                  type="button"
                  onClick={() => void saveGeneratedDrafts()}
                  disabled={selectedGenerated.size === 0 || isSavingGenerated}
                  className="inline-flex min-h-10 items-center justify-center gap-2 rounded-xl bg-[#1C4D8D] px-4 text-xs font-bold text-white shadow-[0_8px_20px_rgba(28,77,141,0.22)] transition duration-200 hover:-translate-y-0.5 hover:bg-[#0F2854] hover:shadow-[0_12px_22px_rgba(15,40,84,0.28)] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-50 motion-reduce:transform-none"
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

                      <p className="mt-3 line-clamp-3 text-xs leading-relaxed text-[#63738a]">
                        {question.content}
                      </p>
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
                            className="inline-flex min-h-8 items-center gap-1.5 rounded-lg border border-[#4988C4] bg-white px-2.5 text-xs font-bold text-[#1C4D8D] transition hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-45"
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
        title="Tạo câu hỏi luyện tập"
        subtitle="Soạn thủ công hoặc nhờ AI · lưu DRAFT · gửi Trưởng BM duyệt"
        icon={Sparkles}
        onClose={() => onClose?.()}
        busy={dialogBusy}
        size="2xl"
        maxHeight="max-h-[min(90dvh,760px)]"
        beforeBody={
          <div className="shrink-0 border-b border-[#82aedb]/55 bg-white px-5 py-3">{modeToggle}</div>
        }
        footer={dialogFooter}
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
