import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import {
  AlertCircle,
  ArrowLeft,
  Bot,
  Check,
  CheckCircle2,
  CircleHelp,
  LoaderCircle,
  PencilLine,
  Plus,
  RotateCcw,
  Save,
  Sparkles,
  Trash2,
  WandSparkles,
} from 'lucide-react'
import { OverlayDrawer } from '@/components/ui/OverlayDrawer'
import { courseService } from '@/services/course.service'
import { questionService } from '@/services/question.service'
import { useAuth } from '@/hooks/useAuth'
import type { CourseDto } from '@/types/practice.types'
import type {
  BloomLevel,
  GenerateQuestionsFromFlmResponse,
  GeneratedQuestionDraft,
  QuestionDifficulty,
  QuestionSource,
  RubricCriterionDraft,
} from '@/types/rubric.types'
import { cn } from '@/lib/utils'

interface CriterionForm extends RubricCriterionDraft {
  localId: string
}

const FALLBACK_COURSES: CourseDto[] = [
  {
    id: '00000000-0000-0000-0000-000000000008',
    code: 'PRN231',
    name: 'Building Cross-Platform Back-End Applications with .NET',
    credits: 3,
    transcriptBufferSeconds: 60,
    maxFollowUpQuestions: 2,
    hasFollowUp: true,
    examInputMode: 'VoiceWithTranscriptEdit',
    isActive: true,
  },
  {
    id: '00000000-0000-0000-0000-000000000009',
    code: 'SWD392',
    name: 'Software Architecture and Design',
    credits: 3,
    transcriptBufferSeconds: 60,
    maxFollowUpQuestions: 2,
    hasFollowUp: true,
    examInputMode: 'VoiceWithTranscriptEdit',
    isActive: true,
  },
  {
    id: '00000000-0000-0000-0000-00000000000a',
    code: 'SWR302',
    name: 'Software Requirements',
    credits: 3,
    transcriptBufferSeconds: 60,
    maxFollowUpQuestions: 2,
    hasFollowUp: true,
    examInputMode: 'VoiceWithTranscriptEdit',
    isActive: true,
  },
]

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

export function CreateQuestionPage() {
  const { user } = useAuth()
  const [isAiDrawerOpen, setIsAiDrawerOpen] = useState(false)
  const [courses, setCourses] = useState<CourseDto[]>(FALLBACK_COURSES)
  const [isLoadingCourses, setIsLoadingCourses] = useState(true)
  const [courseId, setCourseId] = useState(FALLBACK_COURSES[0].id)
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

  const [cloInput, setCloInput] = useState('CLO1, CLO2')
  const [topicsInput, setTopicsInput] = useState(
    'Clean Architecture & Dependency Inversion\nCQRS MediatR & FluentValidation',
  )
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

  useEffect(() => {
    let isActive = true
    void courseService
      .getCourses()
      .then((loadedCourses) => {
        if (!isActive || loadedCourses.length === 0) return
        setCourses(loadedCourses)
        setCourseId((prev) =>
          loadedCourses.some((item) => item.id === prev) ? prev : loadedCourses[0].id,
        )
      })
      .catch(() => {
        // Giữ danh sách môn học dự phòng nếu kết nối tạm gián đoạn
      })
      .finally(() => {
        if (isActive) setIsLoadingCourses(false)
      })
    return () => {
      isActive = false
    }
  }, [])

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

  const applyGeneratedDraftToForm = (question: GeneratedQuestionDraft) => {
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
    setManualNotice('Đã áp dụng câu hỏi AI vào biểu mẫu chính. Hãy rà soát và lưu bản nháp.')
    setIsAiDrawerOpen(false)
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
      const result = await questionService.createDraft({
        courseId: courseId.trim(),
        title: title.trim(),
        content: content.trim(),
        sampleAnswer: sampleAnswer.trim(),
        keyPoints: splitTerms(keyPointsInput),
        difficulty,
        bloomLevel,
        source: manualSource,
        usageScope: 'exam',
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
      setManualNotice(`Đã lưu bản nháp thành công. Mã câu hỏi: ${result.questionId}`)
    } catch (saveError) {
      setManualError(getErrorMessage(saveError))
    } finally {
      setIsSaving(false)
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
        usageScope: 'exam',
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
        await questionService.createDraft({
          courseId: generation.courseId,
          title: question.title,
          content: question.content,
          sampleAnswer: question.modelAnswer,
          keyPoints: question.keyPoints,
          difficulty: question.difficulty,
          bloomLevel: question.bloomLevel,
          source: 'flm_api',
          usageScope: 'exam',
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
        `Đã lưu ${savedCount}/${selected.length} câu hỏi AI thành bản nháp để giảng viên tiếp tục rà soát.`,
      )
      setSavedGeneratedIds((current) => new Set([...current, ...successfulIds]))
      setSelectedGenerated(
        (current) => new Set([...current].filter((tempId) => !successfulIds.has(tempId))),
      )
    }
    if (failures.length > 0) {
      setAiError(`Không lưu được ${failures.length} câu hỏi. ${failures[0]}`)
    }
    setIsSavingGenerated(false)
  }

  return (
    <div className="space-y-4 sm:space-y-5">
      <header className="overflow-hidden rounded-2xl border border-[#E2E8F0] border-t-4 border-t-[#F26F21] bg-white p-5 shadow-sm sm:p-6">
        <Link
          to="/question-bank"
          className="inline-flex min-h-9 items-center gap-1.5 rounded-lg text-xs font-bold text-[#475569] transition hover:text-[#F26F21] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#F26F21]"
        >
          <ArrowLeft className="size-4" aria-hidden="true" />
          Quay lại ngân hàng đề
        </Link>
        <div className="mt-3 flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <div className="inline-flex items-center gap-2 rounded-full border border-[#FED7AA] bg-[#FFF7ED] px-3 py-1 text-xs font-bold tracking-wide text-[#C2410C] uppercase">
              <Sparkles className="size-3.5" aria-hidden="true" />
              Biên soạn Câu hỏi & Barem Đánh giá
            </div>
            <h1 className="mt-2.5 text-xl font-bold tracking-tight text-[#1E293B] sm:text-2xl">
              Tạo câu hỏi & Barem Rubric 10.0
            </h1>
            <p className="mt-2 max-w-3xl text-xs leading-relaxed text-[#475569] sm:text-sm">
              Mọi câu hỏi được lưu ở trạng thái DRAFT. Giảng viên rà soát trên biểu mẫu chính hoặc
              dùng AI sinh nhanh từ FLM Syllabus theo CLO.
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-2.5">
            <button
              type="button"
              onClick={() => setIsAiDrawerOpen(true)}
              className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl border border-[#FED7AA] bg-[#FFF7ED] px-4 text-xs font-bold text-[#C2410C] shadow-2xs transition hover:bg-[#FFEDD5] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#F26F21] sm:text-sm"
            >
              <WandSparkles className="size-4 text-[#F26F21]" aria-hidden="true" />
              ✨ Dùng AI sinh câu hỏi từ FLM Syllabus
            </button>
          </div>
        </div>
      </header>

      <section className="rounded-2xl border border-[#E2E8F0] bg-white p-4 shadow-sm sm:p-5">
        <label className="block max-w-2xl">
          <span className="flex items-center gap-2 text-xs font-bold tracking-wide text-[#334155] uppercase">
            Học phần phân công
            <span className="tracking-normal text-[#BE123C] normal-case">Bắt buộc</span>
            {isLoadingCourses && (
              <span className="tracking-normal text-[#64748B] normal-case">
                (Đang đồng bộ danh sách môn...)
              </span>
            )}
          </span>
          <select
            value={courseId}
            onChange={(event) => setCourseId(event.target.value)}
            className="mt-1.5 min-h-11 w-full rounded-xl border border-[#CBD5E1] bg-[#F8FAFC] px-3.5 text-xs font-semibold text-[#1E293B] transition outline-none focus:border-[#F26F21] focus:bg-white focus:ring-2 focus:ring-[#FED7AA] sm:text-sm"
          >
            {courses.map((course) => (
              <option key={course.id} value={course.id}>
                {course.code} — {course.name} ({course.credits} tín chỉ)
              </option>
            ))}
          </select>
        </label>
        <p className="mt-2 flex items-start gap-2 text-xs leading-relaxed text-[#64748B]">
          <CircleHelp className="mt-0.5 size-4 shrink-0 text-[#F26F21]" aria-hidden="true" />
          Chọn học phần để biên soạn câu hỏi và tiêu chí đánh giá.
        </p>
      </section>

      <form onSubmit={saveManualDraft} className="grid gap-4 xl:grid-cols-12">
        <div className="space-y-4 xl:col-span-8">
          <section className="rounded-2xl border border-[#E2E8F0] bg-white p-4 shadow-sm sm:p-5">
            <div className="flex items-center gap-3">
              <span className="flex size-9 items-center justify-center rounded-xl border border-[#FED7AA] bg-[#FFF7ED] font-mono text-xs font-black text-[#C2410C]">
                01
              </span>
              <div>
                <h2 className="text-base font-bold text-[#1E293B]">Nội dung học thuật</h2>
                <p className="mt-0.5 text-xs text-[#64748B]">
                  Câu hỏi, đáp án chuẩn và các ý chính để AI đối chiếu.
                </p>
              </div>
            </div>

            <div className="mt-5 grid gap-4 sm:grid-cols-3">
              <label className="block sm:col-span-2">
                <span className="text-xs font-bold text-[#334155] uppercase">Tiêu đề</span>
                <input
                  value={title}
                  onChange={(event) => setTitle(event.target.value)}
                  maxLength={300}
                  placeholder="Tên ngắn gọn giúp nhận diện câu hỏi"
                  className="mt-1.5 min-h-11 w-full rounded-xl border border-[#CBD5E1] bg-[#F8FAFC] px-3.5 text-xs text-[#1E293B] transition outline-none focus:border-[#F26F21] focus:bg-white focus:ring-2 focus:ring-[#FED7AA] sm:text-sm"
                />
              </label>
              <div className="block">
                <span className="text-xs font-bold text-[#334155] uppercase">Phạm vi</span>
                <div className="mt-1.5 flex min-h-11 items-center rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] px-3 text-xs font-semibold text-[#475569]">
                  Kho thi chính thức · phải qua phê duyệt
                </div>
              </div>
            </div>

            <label className="mt-4 block">
              <span className="text-xs font-bold text-[#334155] uppercase">Nội dung câu hỏi</span>
              <textarea
                value={content}
                onChange={(event) => setContent(event.target.value)}
                placeholder="Viết đầy đủ yêu cầu mà sinh viên cần trình bày..."
                className="mt-1.5 min-h-28 w-full resize-y rounded-xl border border-[#CBD5E1] bg-[#F8FAFC] px-3.5 py-2.5 text-xs leading-relaxed text-[#1E293B] transition outline-none focus:border-[#F26F21] focus:bg-white focus:ring-2 focus:ring-[#FED7AA] sm:text-sm"
              />
            </label>

            <label className="mt-4 block">
              <span className="flex items-center justify-between gap-3 text-xs font-bold text-[#334155] uppercase">
                Model Answer
                <span
                  className={cn(
                    'normal-case',
                    sampleAnswerIsValid ? 'text-[#047857]' : 'text-[#BE123C]',
                  )}
                >
                  {sampleAnswer.trim().length}/50 ký tự tối thiểu
                </span>
              </span>
              <textarea
                value={sampleAnswer}
                onChange={(event) => setSampleAnswer(event.target.value)}
                placeholder="Câu trả lời chuẩn mực dùng làm mốc chấm..."
                className="mt-1.5 min-h-36 w-full resize-y rounded-xl border border-[#CBD5E1] bg-[#F8FAFC] px-3.5 py-2.5 text-xs leading-relaxed text-[#1E293B] transition outline-none focus:border-[#F26F21] focus:bg-white focus:ring-2 focus:ring-[#FED7AA] sm:text-sm"
              />
            </label>

            <label className="mt-4 block">
              <span className="text-xs font-bold text-[#334155] uppercase">
                Key points · mỗi dòng một ý
              </span>
              <textarea
                value={keyPointsInput}
                onChange={(event) => setKeyPointsInput(event.target.value)}
                placeholder={'Khái niệm trọng tâm\nLập luận cần có\nVí dụ hoặc tình huống'}
                className="mt-1.5 min-h-24 w-full resize-y rounded-xl border border-[#CBD5E1] bg-[#F8FAFC] px-3.5 py-2.5 text-xs leading-relaxed text-[#1E293B] transition outline-none focus:border-[#F26F21] focus:bg-white focus:ring-2 focus:ring-[#FED7AA] sm:text-sm"
              />
            </label>

            <div className="mt-4 grid gap-4 sm:grid-cols-2">
              <label className="block">
                <span className="text-xs font-bold text-[#334155] uppercase">Độ khó</span>
                <select
                  value={difficulty}
                  onChange={(event) => setDifficulty(event.target.value as QuestionDifficulty)}
                  className="mt-1.5 min-h-11 w-full rounded-xl border border-[#CBD5E1] bg-[#F8FAFC] px-3 text-xs font-semibold text-[#334155] outline-none focus:border-[#F26F21] focus:bg-white focus:ring-2 focus:ring-[#FED7AA] sm:text-sm"
                >
                  {DIFFICULTIES.map((item) => (
                    <option key={item.value} value={item.value}>
                      {item.label}
                    </option>
                  ))}
                </select>
              </label>
              <label className="block">
                <span className="text-xs font-bold text-[#334155] uppercase">Bậc Bloom</span>
                <select
                  value={bloomLevel}
                  onChange={(event) => setBloomLevel(event.target.value as BloomLevel)}
                  className="mt-1.5 min-h-11 w-full rounded-xl border border-[#CBD5E1] bg-[#F8FAFC] px-3 text-xs font-semibold text-[#334155] outline-none focus:border-[#F26F21] focus:bg-white focus:ring-2 focus:ring-[#FED7AA] sm:text-sm"
                >
                  {BLOOM_LEVELS.map((level) => (
                    <option key={level} value={level}>
                      {level}
                    </option>
                  ))}
                </select>
              </label>
            </div>
          </section>

          <section className="rounded-2xl border border-[#E2E8F0] bg-white p-4 shadow-sm sm:p-5">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <div className="flex items-center gap-3">
                <span className="flex size-9 items-center justify-center rounded-xl bg-[#F26F21] font-mono text-xs font-black text-white">
                  02
                </span>
                <div>
                  <h2 className="text-base font-bold text-[#1E293B]">Barem Rubric 10.0</h2>
                  <p className="mt-0.5 text-xs text-[#64748B]">
                    Tối thiểu hai tiêu chí, tổng điểm bằng chính xác 10.0.
                  </p>
                </div>
              </div>
              <button
                type="button"
                onClick={addCriterion}
                className="inline-flex min-h-10 items-center justify-center gap-1.5 rounded-xl border border-[#FED7AA] bg-[#FFF7ED] px-3.5 text-xs font-bold text-[#C2410C] transition hover:bg-[#FFEDD5] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#F26F21]"
              >
                <Plus className="size-4" />
                Thêm tiêu chí
              </button>
            </div>

            <div className="mt-5 grid gap-4 sm:grid-cols-2">
              <label className="block">
                <span className="text-xs font-bold text-[#334155] uppercase">Tên rubric</span>
                <input
                  value={rubricName}
                  onChange={(event) => setRubricName(event.target.value)}
                  placeholder="Ví dụ: Rubric đánh giá kiến trúc"
                  className="mt-1.5 min-h-11 w-full rounded-xl border border-[#CBD5E1] bg-[#F8FAFC] px-3.5 text-xs text-[#1E293B] outline-none focus:border-[#F26F21] focus:bg-white focus:ring-2 focus:ring-[#FED7AA] sm:text-sm"
                />
              </label>
              <label className="block">
                <span className="text-xs font-bold text-[#334155] uppercase">Mô tả</span>
                <input
                  value={rubricDescription}
                  onChange={(event) => setRubricDescription(event.target.value)}
                  placeholder="Mục tiêu đánh giá của rubric"
                  className="mt-1.5 min-h-11 w-full rounded-xl border border-[#CBD5E1] bg-[#F8FAFC] px-3.5 text-xs text-[#1E293B] outline-none focus:border-[#F26F21] focus:bg-white focus:ring-2 focus:ring-[#FED7AA] sm:text-sm"
                />
              </label>
            </div>

            <div className="mt-4 space-y-3.5">
              {criteria.map((criterion, index) => (
                <article
                  key={criterion.localId}
                  className="rounded-2xl border border-[#E2E8F0] bg-[#F8FAFC] p-4"
                >
                  <div className="grid gap-3 sm:grid-cols-12">
                    <label className="block sm:col-span-6">
                      <span className="text-xs font-bold text-[#475569] uppercase">
                        Tiêu chí {index + 1}
                      </span>
                      <input
                        value={criterion.criterionName}
                        onChange={(event) =>
                          updateCriterion(criterion.localId, 'criterionName', event.target.value)
                        }
                        placeholder="Tên tiêu chí đánh giá"
                        className="mt-1.5 min-h-10 w-full rounded-lg border border-[#CBD5E1] bg-white px-3 text-xs text-[#1E293B] outline-none focus:border-[#F26F21] focus:ring-2 focus:ring-[#FED7AA] sm:text-sm"
                      />
                    </label>
                    <label className="block sm:col-span-2">
                      <span className="text-xs font-bold text-[#475569] uppercase">Điểm</span>
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
                        className="mt-1.5 min-h-10 w-full rounded-lg border border-[#CBD5E1] bg-white px-2 text-center font-mono text-sm font-black text-[#1E293B] outline-none focus:border-[#F26F21] focus:ring-2 focus:ring-[#FED7AA]"
                      />
                    </label>
                    <label className="block sm:col-span-3">
                      <span className="text-xs font-bold text-[#475569] uppercase">Bloom</span>
                      <select
                        value={criterion.bloomLevel}
                        onChange={(event) =>
                          updateCriterion(
                            criterion.localId,
                            'bloomLevel',
                            event.target.value as BloomLevel,
                          )
                        }
                        className="mt-1.5 min-h-10 w-full rounded-lg border border-[#CBD5E1] bg-white px-2.5 text-xs font-semibold text-[#334155] outline-none focus:border-[#F26F21] focus:ring-2 focus:ring-[#FED7AA]"
                      >
                        {BLOOM_LEVELS.map((level) => (
                          <option key={level} value={level}>
                            {level}
                          </option>
                        ))}
                      </select>
                    </label>
                    <div className="flex items-end justify-end sm:col-span-1">
                      <button
                        type="button"
                        onClick={() => removeCriterion(criterion.localId)}
                        disabled={criteria.length <= 2}
                        className="flex size-10 items-center justify-center rounded-lg text-[#64748B] transition hover:bg-[#FFF1F2] hover:text-[#BE123C] focus-visible:outline-2 focus-visible:outline-[#BE123C] disabled:cursor-not-allowed disabled:opacity-35"
                        aria-label={`Xóa tiêu chí ${index + 1}`}
                      >
                        <Trash2 className="size-4" />
                      </button>
                    </div>
                  </div>
                  <label className="mt-3 block">
                    <span className="sr-only">Mô tả tiêu chí {index + 1}</span>
                    <textarea
                      value={criterion.description}
                      onChange={(event) =>
                        updateCriterion(criterion.localId, 'description', event.target.value)
                      }
                      placeholder="Mô tả biểu hiện đạt điểm của tiêu chí..."
                      className="min-h-16 w-full resize-y rounded-lg border border-[#CBD5E1] bg-white px-3 py-2 text-xs leading-relaxed text-[#1E293B] outline-none focus:border-[#F26F21] focus:ring-2 focus:ring-[#FED7AA] sm:text-sm"
                    />
                  </label>
                </article>
              ))}
            </div>
          </section>
        </div>

        <aside className="space-y-4 xl:sticky xl:top-20 xl:col-span-4 xl:self-start">
          <section
            className={cn(
              'rounded-2xl border-2 bg-white p-5 shadow-sm',
              rubricIsValid ? 'border-[#6EE7B7]' : 'border-[#FDA4AF]',
            )}
          >
            <p className="text-xs font-bold tracking-wider text-[#64748B] uppercase">
              Cổng kiểm tra Rubric
            </p>
            <div className="mt-4 text-center">
              <strong
                className={cn(
                  'font-mono text-4xl font-black tracking-tight',
                  rubricIsValid ? 'text-[#047857]' : 'text-[#BE123C]',
                )}
              >
                {totalRubricScore.toFixed(1)}
              </strong>
              <span className="ml-1 text-sm font-bold text-[#64748B]">/ 10.0</span>
            </div>
            <div className="mt-4 h-2.5 overflow-hidden rounded-full bg-[#E2E8F0]">
              <div
                className={cn(
                  'h-full rounded-full transition-[width,background-color] duration-300',
                  rubricIsValid ? 'bg-[#10B981]' : 'bg-[#F43F5E]',
                )}
                style={{ width: `${Math.min(Math.max(totalRubricScore * 10, 0), 100)}%` }}
              />
            </div>
            <p
              className={cn(
                'mt-3 flex items-start gap-2 rounded-xl p-3 text-xs leading-relaxed',
                rubricIsValid ? 'bg-[#ECFDF5] text-[#047857]' : 'bg-[#FFF1F2] text-[#9F1239]',
              )}
            >
              {rubricIsValid ? (
                <CheckCircle2 className="mt-0.5 size-4 shrink-0" />
              ) : (
                <AlertCircle className="mt-0.5 size-4 shrink-0" />
              )}
              {rubricIsValid
                ? 'Barem đạt invariant tổng điểm chính xác 10.0.'
                : 'Điều chỉnh điểm các tiêu chí để tổng bằng 10.0.'}
            </p>
          </section>

          {(manualError || manualNotice) && (
            <div
              className={cn(
                'rounded-2xl border p-4 text-xs leading-relaxed font-semibold sm:text-sm',
                manualError
                  ? 'border-[#efc8cc] bg-[#fff5f6] text-[#a43c47]'
                  : 'border-[#bce4d7] bg-[#effaf6] text-[#176f58]',
              )}
              role={manualError ? 'alert' : 'status'}
            >
              {manualError || manualNotice}
            </div>
          )}

          <div className="space-y-2.5">
            <button
              type="submit"
              disabled={isSaving || Boolean(savedQuestionId)}
              className="inline-flex min-h-12 w-full items-center justify-center gap-2 rounded-xl bg-[#F26F21] px-4 text-xs font-bold text-white shadow-sm transition hover:bg-[#D95B16] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#F26F21] disabled:cursor-not-allowed disabled:opacity-60 sm:text-sm"
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
              <button
                type="button"
                onClick={resetManualDraft}
                className="inline-flex min-h-10 w-full items-center justify-center gap-2 rounded-xl border border-[#CBD5E1] bg-white px-4 text-xs font-bold text-[#334155] transition hover:bg-[#F8FAFC] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#F26F21] sm:text-sm"
              >
                <RotateCcw className="size-4" />
                Soạn câu hỏi khác
              </button>
            )}
          </div>

          <p className="rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] p-3.5 text-xs leading-relaxed text-[#475569]">
            Số lượng câu hỏi phụ được cấu hình tự động theo quy định của từng học phần và kỳ thi.
          </p>
        </aside>
      </form>

      <OverlayDrawer
        open={isAiDrawerOpen}
        onClose={() => setIsAiDrawerOpen(false)}
        title="AI Sinh Câu Hỏi Tự Động Theo CLO (FLM Syllabus)"
        subtitle="AI đọc chuẩn đầu ra CLO và chủ đề học phần để tạo đề xuất câu hỏi kèm Barem Rubric 10.0 điểm."
        badge="Trợ lý Biên soạn theo Đề cương"
        width="xl"
      >
        <div className="space-y-5">
          <form onSubmit={generateQuestions} className="space-y-4">
            <div className="rounded-2xl border border-[#FED7AA] bg-[#FFF7ED]/60 p-4">
              <div className="flex items-center gap-2.5">
                <span className="flex size-9 items-center justify-center rounded-xl border border-[#FED7AA] bg-white text-[#C2410C]">
                  <Bot className="size-4" />
                </span>
                <div>
                  <h3 className="text-sm font-bold text-[#1E293B]">
                    Thông số sinh câu hỏi từ FLM Syllabus
                  </h3>
                  <p className="text-xs text-[#64748B]">
                    Mỗi đề xuất tự động kèm Model Answer ≥ 50 ký tự và Rubric tổng 10.0 điểm.
                  </p>
                </div>
              </div>

              <div className="mt-4 grid gap-3.5 sm:grid-cols-2">
                <label className="block">
                  <span className="text-xs font-bold text-[#334155] uppercase">
                    Mã CLO · ngăn cách bằng dấu phẩy
                  </span>
                  <textarea
                    value={cloInput}
                    onChange={(event) => setCloInput(event.target.value)}
                    placeholder="CLO1, CLO2"
                    className="mt-1.5 min-h-20 w-full resize-y rounded-xl border border-[#CBD5E1] bg-white px-3 py-2 text-xs text-[#1E293B] outline-none focus:border-[#F26F21] focus:ring-2 focus:ring-[#FED7AA] sm:text-sm"
                  />
                </label>
                <label className="block">
                  <span className="text-xs font-bold text-[#334155] uppercase">
                    Chủ đề / Mục tiêu môn học
                  </span>
                  <textarea
                    value={topicsInput}
                    onChange={(event) => setTopicsInput(event.target.value)}
                    placeholder={'Kiến trúc hệ thống\nThiết kế API'}
                    className="mt-1.5 min-h-20 w-full resize-y rounded-xl border border-[#CBD5E1] bg-white px-3 py-2 text-xs text-[#1E293B] outline-none focus:border-[#F26F21] focus:ring-2 focus:ring-[#FED7AA] sm:text-sm"
                  />
                </label>
              </div>

              <div className="mt-4">
                <span className="text-xs font-bold text-[#334155] uppercase">
                  Bậc Bloom mục tiêu
                </span>
                <div className="mt-2 flex flex-wrap gap-1.5">
                  {BLOOM_LEVELS.map((level) => {
                    const selected = targetBloomLevels.includes(level)
                    return (
                      <button
                        key={level}
                        type="button"
                        onClick={() => toggleBloomLevel(level)}
                        className={cn(
                          'inline-flex min-h-8 items-center gap-1 rounded-xl border px-3 text-xs font-bold transition focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#F26F21]',
                          selected
                            ? 'border-[#F26F21] bg-[#F26F21] text-white'
                            : 'border-[#CBD5E1] bg-white text-[#475569] hover:border-[#FED7AA]',
                        )}
                        aria-pressed={selected}
                      >
                        {selected && <Check className="size-3.5" />}
                        {level}
                      </button>
                    )
                  })}
                </div>
              </div>

              <div className="mt-4 grid gap-3.5 sm:grid-cols-4">
                <label className="block">
                  <span className="text-xs font-bold text-[#334155] uppercase">Tổng số câu</span>
                  <input
                    type="number"
                    min="1"
                    max="10"
                    value={numberOfQuestions}
                    onChange={(event) => setNumberOfQuestions(Number(event.target.value))}
                    className="mt-1.5 min-h-10 w-full rounded-xl border border-[#CBD5E1] bg-white px-3 text-center font-mono text-sm font-bold text-[#1E293B] outline-none focus:border-[#F26F21]"
                  />
                </label>
                {[
                  { label: 'Dễ', value: easyCount, setter: setEasyCount },
                  { label: 'Trung bình', value: mediumCount, setter: setMediumCount },
                  { label: 'Khó', value: hardCount, setter: setHardCount },
                ].map((item) => (
                  <label key={item.label} className="block">
                    <span className="text-xs font-semibold text-[#475569]">{item.label}</span>
                    <input
                      type="number"
                      min="0"
                      max="10"
                      value={item.value}
                      onChange={(event) => item.setter(Number(event.target.value))}
                      className="mt-1.5 min-h-10 w-full rounded-xl border border-[#CBD5E1] bg-white px-3 text-center font-mono text-sm font-bold text-[#1E293B] outline-none focus:border-[#F26F21]"
                    />
                  </label>
                ))}
              </div>
              <p
                className={cn(
                  'mt-2 text-xs font-bold',
                  difficultyTotal === numberOfQuestions ? 'text-[#047857]' : 'text-[#BE123C]',
                )}
              >
                Phân bổ độ khó: {difficultyTotal}/{numberOfQuestions} câu
              </p>
            </div>

            {(aiError || aiNotice) && (
              <div
                className={cn(
                  'rounded-2xl border p-3.5 text-xs leading-relaxed font-semibold sm:text-sm',
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
              className="inline-flex min-h-11 w-full items-center justify-center gap-2 rounded-xl bg-[#F26F21] px-4 text-xs font-bold text-white shadow-sm transition hover:bg-[#D95B16] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#F26F21] disabled:cursor-not-allowed disabled:opacity-60 sm:text-sm"
            >
              {isGenerating ? (
                <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" />
              ) : (
                <WandSparkles className="size-4" />
              )}
              {isGenerating ? 'AI đang phân tích FLM Syllabus...' : 'Sinh câu hỏi từ FLM'}
            </button>
          </form>

          {generation && (
            <section className="overflow-hidden rounded-2xl border border-[#E2E8F0] bg-white shadow-2xs">
              <div className="flex flex-col gap-3 border-b border-[#E2E8F0] bg-[#F8FAFC] px-4 py-3.5 sm:flex-row sm:items-center sm:justify-between">
                <div>
                  <p className="text-xs font-bold tracking-wider text-[#C2410C] uppercase">
                    Kết quả sinh AI · {generation.courseCode}
                  </p>
                  <h4 className="mt-0.5 text-sm font-bold text-[#1E293B]">
                    {generation.generatedCount} đề xuất sẵn sàng áp dụng hoặc lưu nháp
                  </h4>
                </div>
                <button
                  type="button"
                  onClick={() => void saveGeneratedDrafts()}
                  disabled={selectedGenerated.size === 0 || isSavingGenerated}
                  className="inline-flex min-h-9 items-center justify-center gap-1.5 rounded-xl bg-[#F26F21] px-3.5 text-xs font-bold text-white shadow-2xs transition hover:bg-[#D95B16] disabled:cursor-not-allowed disabled:opacity-50"
                >
                  {isSavingGenerated ? (
                    <LoaderCircle className="size-3.5 animate-spin motion-reduce:animate-none" />
                  ) : (
                    <Save className="size-3.5" />
                  )}
                  Lưu {selectedGenerated.size} bản nháp đã chọn
                </button>
              </div>

              <div className="space-y-3 p-4">
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
                        'rounded-2xl border p-4 transition',
                        selected
                          ? 'border-[#F26F21] bg-[#FFF7ED]/60'
                          : 'border-[#E2E8F0] bg-[#F8FAFC]',
                        !valid && 'border-[#FDA4AF] bg-[#FFF1F2]',
                        alreadySaved && 'border-[#6EE7B7] bg-[#ECFDF5]',
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
                            className="mt-1 size-4 accent-[#F26F21] disabled:cursor-not-allowed disabled:opacity-40"
                          />
                          <div className="min-w-0">
                            <div className="flex flex-wrap items-center gap-1.5">
                              <span className="rounded-full border border-[#FED7AA] bg-[#FFF7ED] px-2.5 py-0.5 text-xs font-bold text-[#C2410C]">
                                {question.cloCode}
                              </span>
                              <span className="rounded-full bg-white px-2.5 py-0.5 text-xs font-bold text-[#334155]">
                                {question.bloomLevel}
                              </span>
                              <span className="rounded-full border border-[#E2E8F0] bg-white px-2.5 py-0.5 text-xs font-bold text-[#475569]">
                                {getDifficultyLabel(question.difficulty)}
                              </span>
                              {alreadySaved && (
                                <span className="rounded-full bg-[#ECFDF5] px-2.5 py-0.5 text-xs font-bold text-[#047857]">
                                  Đã lưu
                                </span>
                              )}
                            </div>
                            <h5 className="mt-2 text-sm leading-snug font-bold text-[#1E293B]">
                              {question.title}
                            </h5>
                          </div>
                        </label>
                        <span
                          className={cn(
                            'shrink-0 rounded-xl px-2.5 py-1 font-mono text-xs font-black',
                            Math.abs(rubricTotal - 10) < 0.001
                              ? 'bg-[#ECFDF5] text-[#047857]'
                              : 'bg-[#FFF1F2] text-[#BE123C]',
                          )}
                        >
                          {rubricTotal.toFixed(1)}/10
                        </span>
                      </div>

                      <p className="mt-2.5 text-xs leading-relaxed text-[#475569] sm:text-sm">
                        {question.content}
                      </p>
                      <div className="mt-3 flex flex-wrap items-center justify-between gap-2 border-t border-[#E2E8F0] pt-3 text-xs font-semibold text-[#64748B]">
                        <span>{question.rubric.criteria.length} tiêu chí rubric</span>
                        <button
                          type="button"
                          onClick={() => applyGeneratedDraftToForm(question)}
                          disabled={alreadySaved}
                          className="inline-flex min-h-9 items-center gap-1.5 rounded-xl border border-[#FED7AA] bg-[#FFF7ED] px-3 text-xs font-bold text-[#C2410C] transition hover:bg-[#F26F21] hover:text-white disabled:cursor-not-allowed disabled:opacity-45"
                        >
                          <PencilLine className="size-3.5" />
                          Áp dụng vào biểu mẫu chính
                        </button>
                      </div>
                    </article>
                  )
                })}
              </div>
            </section>
          )}
        </div>
      </OverlayDrawer>
    </div>
  )
}
