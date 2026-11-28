import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { Check, ImagePlus, LoaderCircle, PencilLine, Plus, Save, Trash2, X } from 'lucide-react'
import { FilterDropMenu } from '@/components/common/FilterDropMenu'
import { FieldError } from '@/components/common/formField'
import { fieldInvalidClass, fieldLabelClass } from '@/components/common/formFieldClasses'
import { toast } from '@/components/common/toastStore'
import { QuestionImage } from '@/components/question/QuestionImage'
import { DialogFooter, FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { userMessage } from '@/lib/userMessage'
import {
  hasValidScorePrecision,
  isSafeQuestionImageUrl,
  MAX_IMAGE_DESCRIPTION_LENGTH,
  MAX_IMAGE_URL_LENGTH,
  MAX_QUESTION_TITLE_LENGTH,
  MAX_RUBRIC_NAME_LENGTH,
  roundScore,
  roundWeight,
} from '@/lib/questionValidation'
import { courseCloService } from '@/services/courseClo.service'
import { questionService } from '@/services/question.service'
import type { CourseClo } from '@/types/courseClo.types'
import type {
  BloomLevel,
  QuestionDetail,
  QuestionDifficulty,
  RubricCriterionDraft,
} from '@/types/rubric.types'

export interface EditQuestionDraftDialogProps {
  open: boolean
  question: QuestionDetail | null
  onClose: () => void
  onSaved: (questionId: string) => void
}

interface CriterionEditItem extends RubricCriterionDraft {
  localId: string
}

interface FormFieldErrors {
  title?: string
  cloId?: string
  content?: string
  imageUrl?: string
  imageDescription?: string
  sampleAnswer?: string
  rubricName?: string
  criteria?: string
  rubricTotal?: string
}

type CriterionFieldErrors = Record<string, { name?: string; score?: string }>

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

function isUuid(value: string) {
  return /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(value)
}

function splitKeyPoints(value: string): string[] {
  return value
    .split(/\r?\n/)
    .map((item) => item.trim())
    .filter(Boolean)
}

function createEmptyCriterion(maxScore = 0, bloomLevel: BloomLevel = 'Apply'): CriterionEditItem {
  return {
    localId: crypto.randomUUID(),
    criterionName: '',
    maxScore,
    weight: maxScore / 10,
    bloomLevel,
    description: '',
  }
}

function mapInitialCriteria(question: QuestionDetail): CriterionEditItem[] {
  const mapped = question.rubric.criteria.map((criterion) => ({
    localId: criterion.id || crypto.randomUUID(),
    criterionName: criterion.criterionName,
    maxScore: criterion.maxScore,
    weight: criterion.maxScore / 10,
    bloomLevel: criterion.bloomLevel ?? question.bloomLevel,
    description: criterion.description ?? '',
  }))
  if (mapped.length >= 2) return mapped
  if (mapped.length === 1) {
    const remaining = Math.max(0, Math.round((10 - mapped[0].maxScore) * 100) / 100)
    return [...mapped, createEmptyCriterion(remaining, question.bloomLevel)]
  }
  return [
    createEmptyCriterion(5, question.bloomLevel),
    createEmptyCriterion(5, question.bloomLevel),
  ]
}

export function EditQuestionDraftDialog({
  open,
  question,
  onClose,
  onSaved,
}: EditQuestionDraftDialogProps) {
  if (!open || !question) return null

  return (
    <EditQuestionDraftForm
      key={question.id}
      question={question}
      onClose={onClose}
      onSaved={onSaved}
    />
  )
}

function EditQuestionDraftForm({
  question,
  onClose,
  onSaved,
}: {
  question: QuestionDetail
  onClose: () => void
  onSaved: (questionId: string) => void
}) {
  const [title, setTitle] = useState(question.title)
  const [selectedCloId, setSelectedCloId] = useState(question.cloId ?? '')
  const [difficulty, setDifficulty] = useState<QuestionDifficulty>(question.difficulty)
  const [bloomLevel, setBloomLevel] = useState<BloomLevel>(question.bloomLevel)
  const [content, setContent] = useState(question.content)
  const [sampleAnswer, setSampleAnswer] = useState(question.sampleAnswer ?? '')
  const [keyPointsInput, setKeyPointsInput] = useState(question.keyPoints.join('\n'))
  const [rubricName, setRubricName] = useState(question.rubric.name)
  const [rubricDescription, setRubricDescription] = useState(question.rubric.description ?? '')
  const [criteria, setCriteria] = useState<CriterionEditItem[]>(() => mapInitialCriteria(question))
  const [imageUrl, setImageUrl] = useState<string | null>(question.imageUrl ?? null)
  const [imageDescription, setImageDescription] = useState(
    question.imageDescription ?? question.imageAlt ?? '',
  )
  const [clearImage, setClearImage] = useState(false)
  const [isUploadingImage, setIsUploadingImage] = useState(false)
  const [imageError, setImageError] = useState('')
  const fileInputRef = useRef<HTMLInputElement>(null)
  const isManualSource = (question.source || 'manual').trim().toLowerCase() === 'manual'

  const [availableClos, setAvailableClos] = useState<CourseClo[]>([])
  const [isLoadingClos, setIsLoadingClos] = useState(() => isUuid(question.courseId))
  const [cloLoadError, setCloLoadError] = useState('')
  const [errors, setErrors] = useState<FormFieldErrors>({})
  const [criterionErrors, setCriterionErrors] = useState<CriterionFieldErrors>({})
  const [isSaving, setIsSaving] = useState(false)

  useEffect(() => {
    if (!isUuid(question.courseId)) return
    let active = true
    void courseCloService
      .list(question.courseId, true)
      .then((clos) => {
        if (!active) return
        const activeClos = clos.filter((clo) => clo.isActive)
        setAvailableClos(activeClos)
        setCloLoadError('')
        setSelectedCloId((current) => {
          if (current && activeClos.some((clo) => clo.id === current)) return current
          if (question.cloCode) {
            const matched = activeClos.find(
              (clo) => clo.cloCode.trim().toLowerCase() === question.cloCode?.trim().toLowerCase(),
            )
            if (matched) return matched.id
          }
          return current
        })
      })
      .catch((err: unknown) => {
        if (!active) return
        setAvailableClos([])
        setCloLoadError(userMessage(err, 'Không tải được danh sách CLO của môn học.'))
      })
      .finally(() => {
        if (active) setIsLoadingClos(false)
      })

    return () => {
      active = false
    }
  }, [question.cloCode, question.courseId])

  const cloOptions = useMemo(
    () =>
      availableClos.map((clo) => ({
        value: clo.id,
        label: `${clo.cloCode} · ${clo.description}`,
      })),
    [availableClos],
  )

  const totalRubricScore = useMemo(
    () => criteria.reduce((sum, item) => sum + item.maxScore, 0),
    [criteria],
  )
  const rubricIsValid = Math.abs(totalRubricScore - 10) < 0.001
  const sampleAnswerIsValid = sampleAnswer.trim().length >= 50

  const clearFieldError = (...keys: Array<keyof FormFieldErrors>) => {
    setErrors((current) => {
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

  const updateCriterion = <Key extends keyof CriterionEditItem>(
    localId: string,
    key: Key,
    value: CriterionEditItem[Key],
  ) => {
    if (key === 'criterionName') clearCriterionError(localId, 'name')
    if (key === 'maxScore') {
      clearCriterionError(localId, 'score')
      clearFieldError('rubricTotal')
    }
    setCriteria((current) =>
      current.map((item) => {
        if (item.localId !== localId) return item
        const next = { ...item, [key]: value }
        if (key === 'maxScore') next.weight = Number(value) / 10
        return next
      }),
    )
  }

  const addCriterion = () => {
    clearFieldError('criteria', 'rubricTotal')
    setCriteria((current) => [...current, createEmptyCriterion(0, bloomLevel)])
  }

  const removeCriterion = (localId: string) => {
    clearFieldError('rubricTotal')
    setCriteria((current) =>
      current.length <= 2 ? current : current.filter((item) => item.localId !== localId),
    )
  }

  const handleImageFileChange = async (event: React.ChangeEvent<HTMLInputElement>) => {
    if (!isManualSource || isSaving || isUploadingImage) return
    const file = event.target.files?.[0]
    if (!file) return

    if (file.size > 5 * 1024 * 1024) {
      setImageError('Kích thước tệp vượt quá 5 MB. Vui lòng chọn ảnh nhỏ hơn.')
      if (fileInputRef.current) fileInputRef.current.value = ''
      return
    }

    const validTypes = ['image/png', 'image/jpeg', 'image/jpg', 'image/webp']
    if (!validTypes.includes(file.type)) {
      setImageError('Định dạng tệp không được hỗ trợ. Chỉ chấp nhận PNG, JPG hoặc WebP.')
      if (fileInputRef.current) fileInputRef.current.value = ''
      return
    }

    setIsUploadingImage(true)
    setImageError('')
    try {
      const uploaded = await questionService.uploadQuestionImage(file, imageDescription)
      setImageUrl(uploaded.imageUrl)
      setClearImage(false)
      clearFieldError('imageUrl')
      toast.success('Đã tải ảnh câu hỏi lên.')
    } catch (uploadError) {
      setImageError(userMessage(uploadError, 'Không thể tải hình ảnh lên.'))
    } finally {
      setIsUploadingImage(false)
      if (fileInputRef.current) fileInputRef.current.value = ''
    }
  }

  const handleRemoveImage = () => {
    setImageUrl(null)
    setImageDescription('')
    setClearImage(true)
    setImageError('')
    clearFieldError('imageUrl', 'imageDescription')
    if (fileInputRef.current) fileInputRef.current.value = ''
  }

  const validateForm = () => {
    const nextErrors: FormFieldErrors = {}
    const nextCriterionErrors: CriterionFieldErrors = {}

    const trimmedTitle = title.trim()
    if (!trimmedTitle) {
      nextErrors.title = 'Vui lòng nhập tiêu đề câu hỏi.'
    } else if (trimmedTitle.length > MAX_QUESTION_TITLE_LENGTH) {
      nextErrors.title = 'Tiêu đề không được vượt quá 300 ký tự.'
    }

    if (availableClos.length > 0 && !availableClos.some((clo) => clo.id === selectedCloId)) {
      nextErrors.cloId = 'Vui lòng chọn chuẩn đầu ra (CLO) đang hoạt động của môn học.'
    }

    if (!content.trim()) {
      nextErrors.content = 'Vui lòng nhập nội dung câu hỏi.'
    }

    if (isManualSource) {
      const trimmedImageUrl = imageUrl?.trim() ?? ''
      if (
        !clearImage &&
        trimmedImageUrl &&
        (trimmedImageUrl.length > MAX_IMAGE_URL_LENGTH || !isSafeQuestionImageUrl(trimmedImageUrl))
      ) {
        nextErrors.imageUrl =
          'Đường dẫn hình ảnh không hợp lệ (tối đa 500 ký tự, chỉ chấp nhận /uploads/questions/... hoặc HTTPS công khai an toàn).'
      }
      if (!clearImage && imageDescription.trim().length > MAX_IMAGE_DESCRIPTION_LENGTH) {
        nextErrors.imageDescription = 'Mô tả ảnh không được vượt quá 2000 ký tự.'
      }
    } else if (!clearImage && (imageUrl?.trim() || imageDescription.trim())) {
      nextErrors.imageUrl =
        'Chỉ câu hỏi tạo thủ công mới hỗ trợ đính kèm hình ảnh. Vui lòng bấm "Bỏ ảnh" trước khi lưu.'
    }

    if (!sampleAnswerIsValid) {
      nextErrors.sampleAnswer = 'Câu trả lời mẫu bắt buộc có ít nhất 50 ký tự.'
    }

    const trimmedRubricName = rubricName.trim()
    if (!trimmedRubricName) {
      nextErrors.rubricName = 'Vui lòng nhập tên Barem Rubric.'
    } else if (trimmedRubricName.length > MAX_RUBRIC_NAME_LENGTH) {
      nextErrors.rubricName = 'Tên Barem Rubric không được vượt quá 200 ký tự.'
    }

    if (criteria.length < 2) {
      nextErrors.criteria = 'Barem Rubric phải có ít nhất 2 tiêu chí.'
    }

    for (const criterion of criteria) {
      const entry: { name?: string; score?: string } = {}
      const trimmedCriterionName = criterion.criterionName.trim()
      if (!trimmedCriterionName) {
        entry.name = 'Nhập tên tiêu chí.'
      } else if (trimmedCriterionName.length > MAX_RUBRIC_NAME_LENGTH) {
        entry.name = 'Tên tiêu chí không được vượt quá 200 ký tự.'
      }
      if (!Number.isFinite(criterion.maxScore) || criterion.maxScore < 0.01 || criterion.maxScore > 10) {
        entry.score = 'Điểm tiêu chí phải từ 0.01 đến 10.'
      } else if (!hasValidScorePrecision(criterion.maxScore)) {
        entry.score = 'Điểm tiêu chí chỉ hỗ trợ tối đa 2 chữ số thập phân.'
      }
      if (entry.name || entry.score) nextCriterionErrors[criterion.localId] = entry
    }

    if (!rubricIsValid) {
      nextErrors.rubricTotal = 'Tổng điểm các tiêu chí phải bằng chính xác 10.00.'
    }

    return { nextErrors, nextCriterionErrors }
  }

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (isSaving || isUploadingImage) return

    const { nextErrors, nextCriterionErrors } = validateForm()
    setErrors(nextErrors)
    setCriterionErrors(nextCriterionErrors)
    if (Object.keys(nextErrors).length > 0 || Object.keys(nextCriterionErrors).length > 0) {
      return
    }

    setIsSaving(true)
    try {
      const trimmedImageUrl = imageUrl?.trim() || null
      const imagePayload = isManualSource
        ? clearImage
          ? { imageUrl: null, imageDescription: null, clearImage: true }
          : trimmedImageUrl
            ? {
                imageUrl: trimmedImageUrl,
                imageDescription: imageDescription.trim(),
                clearImage: false,
              }
            : { imageUrl: null, imageDescription: null, clearImage: false }
        : clearImage
          ? { imageUrl: null, imageDescription: null, clearImage: true }
          : {}

      const response = await questionService.updateDraft(question.id, {
        title: title.trim(),
        content: content.trim(),
        sampleAnswer: sampleAnswer.trim(),
        keyPoints: splitKeyPoints(keyPointsInput),
        difficulty,
        bloomLevel,
        cloId: selectedCloId.trim() ? selectedCloId.trim() : null,
        ...imagePayload,
        rubric: {
          name: rubricName.trim(),
          description: rubricDescription.trim() || null,
          totalMaxScore: 10,
          criteria: criteria.map((criterion, index) => ({
            criterionName: criterion.criterionName.trim(),
            maxScore: roundScore(criterion.maxScore),
            weight: roundWeight(criterion.maxScore),
            bloomLevel: criterion.bloomLevel || bloomLevel,
            description: criterion.description.trim(),
            orderIndex: index + 1,
          })),
        },
      })
      toast.success('Đã lưu câu hỏi và Barem Rubric vào bản nháp.')
      onSaved(response.questionId || question.id)
    } catch (saveError) {
      toast.error(userMessage(saveError, 'Không cập nhật được câu hỏi. Vui lòng thử lại.'))
    } finally {
      setIsSaving(false)
    }
  }

  return (
    <FormDialog
      titleId="edit-question-draft-title"
      eyebrow={`Chỉnh sửa câu hỏi · ${question.courseCode}`}
      title="Chỉnh sửa nội dung câu hỏi & Barem Rubric"
      icon={PencilLine}
      size="2xl"
      busy={isSaving || isUploadingImage}
      onClose={onClose}
      maxHeight="max-h-[min(92dvh,840px)]"
      footer={
        <DialogFooter className="bg-white">
          <button
            type="button"
            onClick={onClose}
            disabled={isSaving || isUploadingImage}
            className={ds.outlineButton}
          >
            Hủy
          </button>
          <button
            type="submit"
            form="edit-question-draft-form"
            disabled={isSaving || isLoadingClos || isUploadingImage}
            className={ds.primaryButton}
          >
            {isSaving ? (
              <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" />
            ) : (
              <Save className="size-4" />
            )}
            {isSaving ? 'Đang lưu...' : 'Lưu bản nháp'}
          </button>
        </DialogFooter>
      }
    >
      <form id="edit-question-draft-form" noValidate onSubmit={handleSubmit} className="space-y-3">
        <section className="rounded-2xl border border-[#82aedb]/55 bg-[#F4FBFD] p-4">
          <div className="grid gap-3 sm:grid-cols-2">
            <div>
              <span className={fieldLabelClass(false)}>Độ khó</span>
              <FilterDropMenu
                label="Chọn độ khó"
                value={difficulty}
                onChange={(value) => setDifficulty(value as QuestionDifficulty)}
                options={DIFFICULTIES}
                disabled={isSaving}
                className="mt-1.5 w-full"
                menuMinWidth={0}
              />
            </div>
            <div>
              <span className={fieldLabelClass(false)}>Bậc Bloom</span>
              <FilterDropMenu
                label="Chọn bậc Bloom"
                value={bloomLevel}
                onChange={(value) => {
                  const nextBloom = value as BloomLevel
                  setBloomLevel(nextBloom)
                  setCriteria((current) =>
                    current.map((item) => ({ ...item, bloomLevel: nextBloom })),
                  )
                }}
                options={BLOOM_LEVELS.map((level) => ({ value: level, label: level }))}
                disabled={isSaving}
                className="mt-1.5 w-full"
                menuMinWidth={0}
              />
            </div>
            <div className="sm:col-span-2">
              <span className={fieldLabelClass(Boolean(errors.cloId))}>
                Chuẩn đầu ra (CLO){availableClos.length > 0 ? ' *' : ''}
              </span>
              <FilterDropMenu
                label="Chọn chuẩn đầu ra CLO"
                value={selectedCloId}
                onChange={(value) => {
                  if (availableClos.length > 0 && !value.trim()) return
                  setSelectedCloId(value)
                  clearFieldError('cloId')
                }}
                options={availableClos.length > 0 ? cloOptions : []}
                disabled={isSaving || isLoadingClos}
                invalid={Boolean(errors.cloId)}
                emptyText={
                  isLoadingClos
                    ? 'Đang tải danh sách CLO...'
                    : cloLoadError || 'Môn học chưa có CLO đang hoạt động.'
                }
                wrapOptions
                menuMinWidth={0}
                className="mt-1.5 w-full"
              />
              <FieldError message={errors.cloId || cloLoadError} />
            </div>
          </div>
        </section>

        <section className="space-y-3 rounded-2xl border border-[#82aedb]/55 bg-white p-4">
          <label className="block">
            <span className={cn('flex items-center justify-between gap-2', fieldLabelClass(Boolean(errors.title)))}>
              <span>Tiêu đề câu hỏi</span>
              <span className="text-[11px] font-medium normal-case text-[#6f8198]">
                {title.trim().length}/300
              </span>
            </span>
            <input
              value={title}
              onChange={(event) => {
                setTitle(event.target.value)
                clearFieldError('title')
              }}
              maxLength={300}
              disabled={isSaving}
              aria-invalid={Boolean(errors.title)}
              placeholder="Tên ngắn gọn nhận diện câu hỏi..."
              className={cn(
                'mt-1.5 min-h-10 w-full rounded-xl border border-[#4988C4] bg-white px-3 text-sm text-[#0F2854] outline-none focus:ring-2 focus:ring-[#4988C4]/30',
                errors.title && fieldInvalidClass,
              )}
            />
            <FieldError message={errors.title} />
          </label>

          <label className="block">
            <span className={fieldLabelClass(Boolean(errors.content))}>Nội dung câu hỏi</span>
            <textarea
              rows={3}
              value={content}
              onChange={(event) => {
                setContent(event.target.value)
                clearFieldError('content')
              }}
              disabled={isSaving}
              aria-invalid={Boolean(errors.content)}
              placeholder="Nội dung câu hỏi vấn đáp..."
              className={cn(
                'mt-1.5 w-full resize-y rounded-xl border border-[#4988C4] bg-white px-3 py-2.5 text-sm leading-relaxed text-[#0F2854] outline-none focus:ring-2 focus:ring-[#4988C4]/30',
                errors.content && fieldInvalidClass,
              )}
            />
            <FieldError message={errors.content} />
          </label>

          <div className="space-y-2 rounded-xl border border-[#82aedb]/45 bg-[#F8FBFE] p-3">
            <span className={fieldLabelClass(Boolean(errors.imageUrl || errors.imageDescription))}>
              Hình ảnh minh họa
            </span>
            {imageUrl && (
              <div className="relative">
                <QuestionImage
                  imageUrl={imageUrl}
                  imageAlt={imageDescription || 'Ảnh câu hỏi'}
                />
              </div>
            )}
            {isManualSource ? (
              <>
                <input
                  ref={fileInputRef}
                  type="file"
                  accept="image/png,image/jpeg,image/webp"
                  className="hidden"
                  onChange={(event) => void handleImageFileChange(event)}
                />
                <div className="flex flex-wrap items-center gap-2">
                  <button
                    type="button"
                    disabled={isSaving || isUploadingImage}
                    onClick={() => fileInputRef.current?.click()}
                    className={ds.outlineButton}
                  >
                    {isUploadingImage ? (
                      <LoaderCircle className="size-4 animate-spin" />
                    ) : (
                      <ImagePlus className="size-4" />
                    )}
                    {isUploadingImage ? 'Đang tải…' : imageUrl ? 'Thay ảnh' : 'Thêm ảnh'}
                  </button>
                  {imageUrl ? (
                    <button
                      type="button"
                      disabled={isSaving || isUploadingImage}
                      onClick={handleRemoveImage}
                      className={cn(
                        ds.outlineButton,
                        'border-red-200 text-red-700 hover:border-red-400 hover:bg-red-50',
                      )}
                    >
                      <X className="size-4" />
                      Xóa ảnh
                    </button>
                  ) : (
                    <span className="text-xs text-slate-600">PNG, JPG, WebP · tối đa 5 MB</span>
                  )}
                </div>
                {imageUrl && (
                  <label className="block">
                    <span className={fieldLabelClass(Boolean(errors.imageDescription))}>
                      Mô tả ảnh
                    </span>
                    <input
                      value={imageDescription}
                      maxLength={2000}
                      disabled={isSaving || isUploadingImage}
                      onChange={(event) => {
                        setImageDescription(event.target.value)
                        clearFieldError('imageDescription')
                      }}
                      aria-invalid={Boolean(errors.imageDescription)}
                      placeholder="Mô tả nội dung trong ảnh (không bắt buộc)..."
                      className={cn(
                        'mt-1.5 min-h-10 w-full rounded-xl border border-[#4988C4] bg-white px-3 text-sm text-[#0F2854] outline-none focus:ring-2 focus:ring-[#4988C4]/30',
                        errors.imageDescription && fieldInvalidClass,
                      )}
                    />
                    <FieldError message={errors.imageDescription} />
                  </label>
                )}
                <FieldError message={imageError || errors.imageUrl} />
              </>
            ) : (
              <div className="space-y-2">
                <p className="text-xs text-[#5b6b84]">
                  Chỉ câu hỏi tạo thủ công mới hỗ trợ đính kèm hình ảnh.
                </p>
                {(imageUrl || imageDescription.trim()) && (
                  <button
                    type="button"
                    disabled={isSaving}
                    onClick={handleRemoveImage}
                    className={ds.outlineButton}
                  >
                    <X className="size-4" />
                    Bỏ ảnh
                  </button>
                )}
                <FieldError message={errors.imageUrl} />
              </div>
            )}
          </div>

          <label className="block">
            <span
              className={cn(
                'flex items-center justify-between gap-2',
                fieldLabelClass(Boolean(errors.sampleAnswer)),
              )}
            >
              <span>Câu trả lời mẫu</span>
              <span
                className={cn(
                  'text-[11px] font-semibold normal-case',
                  sampleAnswerIsValid ? 'text-[#16836a]' : 'text-[#b03a46]',
                )}
              >
                {sampleAnswer.trim().length}/50 ký tự tối thiểu
              </span>
            </span>
            <textarea
              rows={3}
              value={sampleAnswer}
              onChange={(event) => {
                setSampleAnswer(event.target.value)
                clearFieldError('sampleAnswer')
              }}
              disabled={isSaving}
              aria-invalid={Boolean(errors.sampleAnswer)}
              placeholder="Đáp án mẫu chuẩn mực dùng làm cơ sở chấm điểm (tối thiểu 50 ký tự)..."
              className={cn(
                'mt-1.5 w-full resize-y rounded-xl border border-[#4988C4] bg-white px-3 py-2.5 text-sm leading-relaxed text-[#0F2854] outline-none focus:ring-2 focus:ring-[#4988C4]/30',
                errors.sampleAnswer && fieldInvalidClass,
              )}
            />
            <FieldError message={errors.sampleAnswer} />
          </label>

          <label className="block">
            <span className={fieldLabelClass(false)}>Ý chính (mỗi dòng một ý)</span>
            <textarea
              rows={2}
              value={keyPointsInput}
              onChange={(event) => setKeyPointsInput(event.target.value)}
              disabled={isSaving}
              placeholder={'Khái niệm trọng tâm\nLập luận cốt lõi'}
              className="mt-1.5 w-full resize-y rounded-xl border border-[#4988C4] bg-white px-3 py-2 text-sm leading-relaxed text-[#0F2854] outline-none focus:ring-2 focus:ring-[#4988C4]/30"
            />
          </label>
        </section>

        <section
          className={cn(
            'rounded-2xl border-2 bg-white p-4',
            rubricIsValid ? 'border-[#8bd6bd]' : 'border-[#e46a74]',
          )}
        >
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div>
              <h3 className="text-sm font-bold text-[#0F2854]">Barem Rubric chấm điểm</h3>
              <p className="text-xs text-[#5b6b84]">
                Tối thiểu 2 tiêu chí dương, tổng điểm chính xác 10.00.
              </p>
            </div>
            <div className="flex items-center gap-2">
              <span
                className={cn(
                  'inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-xs font-bold tabular-nums',
                  rubricIsValid ? 'bg-[#eaf9f4] text-[#14765c]' : 'bg-[#fff0f1] text-[#ae3945]',
                )}
              >
                {rubricIsValid && <Check className="size-3" aria-hidden="true" />}
                {totalRubricScore.toFixed(2)} / 10.00 điểm
              </span>
              <button
                type="button"
                onClick={addCriterion}
                disabled={isSaving}
                className="inline-flex min-h-9 items-center gap-1.5 rounded-xl border border-[#4988C4] bg-[#F4FBFD] px-3 text-xs font-bold text-[#1C4D8D] transition hover:bg-[#E7F3FB]"
              >
                <Plus className="size-3.5" />
                Thêm tiêu chí
              </button>
            </div>
          </div>

          <div className="mt-3 grid gap-3 sm:grid-cols-2">
            <label className="block">
              <span className={fieldLabelClass(Boolean(errors.rubricName))}>Tên Rubric</span>
              <input
                value={rubricName}
                onChange={(event) => {
                  setRubricName(event.target.value)
                  clearFieldError('rubricName')
                }}
                maxLength={200}
                disabled={isSaving}
                aria-invalid={Boolean(errors.rubricName)}
                placeholder="Tên Barem Rubric..."
                className={cn(
                  'mt-1.5 min-h-10 w-full rounded-xl border border-[#4988C4] bg-white px-3 text-sm text-[#0F2854] outline-none focus:ring-2 focus:ring-[#4988C4]/30',
                  errors.rubricName && fieldInvalidClass,
                )}
              />
              <FieldError message={errors.rubricName} />
            </label>
            <label className="block">
              <span className={fieldLabelClass(false)}>Mô tả Rubric</span>
              <input
                value={rubricDescription}
                onChange={(event) => setRubricDescription(event.target.value)}
                disabled={isSaving}
                placeholder="Mô tả ngắn mục tiêu chấm (không bắt buộc)..."
                className="mt-1.5 min-h-10 w-full rounded-xl border border-[#4988C4] bg-white px-3 text-sm text-[#0F2854] outline-none focus:ring-2 focus:ring-[#4988C4]/30"
              />
            </label>
          </div>

          <div className="mt-3 space-y-3">
            {criteria.map((criterion, index) => (
              <div
                key={criterion.localId}
                className="rounded-2xl border border-[#82aedb]/55 bg-[#F4FBFD]/50 p-3.5"
              >
                <div className="grid gap-3 sm:grid-cols-[minmax(0,1fr)_110px_auto] sm:items-start">
                  <label className="block min-w-0">
                    <span className={fieldLabelClass(Boolean(criterionErrors[criterion.localId]?.name))}>
                      Tiêu chí {index + 1}
                    </span>
                    <input
                      value={criterion.criterionName}
                      onChange={(event) =>
                        updateCriterion(criterion.localId, 'criterionName', event.target.value)
                      }
                      maxLength={200}
                      disabled={isSaving}
                      aria-invalid={Boolean(criterionErrors[criterion.localId]?.name)}
                      placeholder="Tên tiêu chí đánh giá..."
                      className={cn(
                        'mt-1 min-h-10 w-full rounded-xl border border-[#4988C4] bg-white px-3 text-sm text-[#0F2854] outline-none focus:ring-2 focus:ring-[#4988C4]/30',
                        criterionErrors[criterion.localId]?.name && fieldInvalidClass,
                      )}
                    />
                    <FieldError message={criterionErrors[criterion.localId]?.name} />
                  </label>

                  <label className="block">
                    <span className={fieldLabelClass(Boolean(criterionErrors[criterion.localId]?.score))}>
                      Điểm tối đa
                    </span>
                    <input
                      type="number"
                      min="0.01"
                      max="10"
                      step="0.01"
                      value={criterion.maxScore}
                      onChange={(event) =>
                        updateCriterion(
                          criterion.localId,
                          'maxScore',
                          Number(event.target.value),
                        )
                      }
                      disabled={isSaving}
                      aria-invalid={Boolean(criterionErrors[criterion.localId]?.score)}
                      className={cn(
                        'mt-1 min-h-10 w-full rounded-xl border border-[#4988C4] bg-white px-2 text-center font-mono text-sm font-black text-[#0F2854] outline-none focus:ring-2 focus:ring-[#4988C4]/30',
                        criterionErrors[criterion.localId]?.score && fieldInvalidClass,
                      )}
                    />
                    <FieldError message={criterionErrors[criterion.localId]?.score} />
                  </label>

                  <div className="sm:pt-5">
                    <button
                      type="button"
                      onClick={() => removeCriterion(criterion.localId)}
                      disabled={isSaving || criteria.length <= 2}
                      aria-label={`Xóa tiêu chí ${index + 1}`}
                      className="flex size-10 items-center justify-center rounded-xl border border-red-200 bg-red-50 text-red-700 transition enabled:hover:border-red-500 enabled:hover:bg-red-100 disabled:cursor-not-allowed disabled:opacity-35"
                    >
                      <Trash2 className="size-4" />
                    </button>
                  </div>
                </div>

                <label className="mt-2.5 block">
                  <span className="sr-only">Mô tả tiêu chí {index + 1}</span>
                  <textarea
                    rows={2}
                    value={criterion.description}
                    onChange={(event) =>
                      updateCriterion(criterion.localId, 'description', event.target.value)
                    }
                    disabled={isSaving}
                    placeholder="Mô tả biểu hiện đạt điểm của tiêu chí..."
                    className="w-full resize-y rounded-xl border border-[#4988C4] bg-white px-3 py-2 text-xs leading-relaxed text-[#0F2854] outline-none focus:ring-2 focus:ring-[#4988C4]/30"
                  />
                </label>
              </div>
            ))}
            <FieldError message={errors.criteria} />
            <FieldError message={errors.rubricTotal} />
          </div>
        </section>
      </form>
    </FormDialog>
  )
}
