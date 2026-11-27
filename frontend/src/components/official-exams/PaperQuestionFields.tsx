import { QuestionDifficultyBadge } from './QuestionDifficultyBadge'
import { useRef, useState } from 'react'
import {
  ArrowLeft,
  ArrowRight,
  Check,
  ChevronRight,
  FilePenLine,
  ImagePlus,
  Plus,
  Trash2,
} from 'lucide-react'
import { TwoStepProgress } from '@/design/TwoStepProgress'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { FieldError } from '@/components/common/formField'
import { fieldInvalidClass, fieldLabelClass } from '@/components/common/formFieldClasses'
import { TextareaField } from '@/components/common/TextareaField'
import { FilterDropMenu } from '@/components/common/FilterDropMenu'
import { QuestionImage } from '@/components/question/QuestionImage'
import { questionService } from '@/services/question.service'
import { preparationError } from '@/lib/examPreparation'
import {
  questionErrors,
  bloomOptions,
  type PaperQuestionDraft,
  type QuestionSlot,
} from '@/lib/examPaperAuthoring'
import type { CourseClo } from '@/types/courseClo.types'
import type { BloomLevel } from '@/types/rubric.types'

export function PaperQuestionFields({
  slot,
  index,
  clos,
  errors: submittedErrors,
  busy,
  onChange,
  onStep,
  onUploading,
  onClear,
}: {
  slot: QuestionSlot
  index: number
  clos: CourseClo[]
  errors: Record<string, string>
  busy: boolean
  onChange: (question: PaperQuestionDraft) => void
  onStep: (step: 1 | 2) => void
  onClear?: () => void
  onUploading: (uploading: boolean) => void
}) {
  const q = slot.question
  const readOnly = q.origin === 'bank'
  const [touchedFields, setTouchedFields] = useState<Record<string, boolean>>({})
  const validation = questionErrors(q, clos)
  const errors = {
    ...Object.fromEntries(Object.entries(validation).filter(([key]) => touchedFields[key])),
    ...submittedErrors,
  }
  const touch = (key: string) => setTouchedFields((old) => ({ ...old, [key]: true }))

  const fileRef = useRef<HTMLInputElement>(null)
  const [imageError, setImageError] = useState('')
  const update = (patch: Partial<PaperQuestionDraft>) =>
    onChange({ ...q, ...patch, persisted: false })
  const clo = clos.find((c) => c.id === q.cloId)
  async function upload(file?: File) {
    if (!file) return
    if (
      !['image/jpeg', 'image/png', 'image/webp'].includes(file.type) ||
      file.size > 5 * 1024 * 1024
    ) {
      setImageError('Chọn ảnh PNG, JPG hoặc WebP, tối đa 5 MB.')
      return
    }
    onUploading(true)
    setImageError('')
    try {
      const result = await questionService.uploadQuestionImage(file)
      update({ imageUrl: result.imageUrl })
    } catch (failure) {
      setImageError(preparationError(failure))
    } finally {
      onUploading(false)
      if (fileRef.current) fileRef.current.value = ''
    }
  }
  const total = q.rubric.criteria.reduce((sum, c) => sum + c.maxScore, 0)
  return (
    <div className="min-w-0 space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <h3 className="text-lg font-bold text-[#0F2854]">Câu {index + 1}</h3>
        <QuestionDifficultyBadge difficulty={slot.difficulty} />
        <span className={ds.infoChip}>
          {readOnly
            ? 'Từ ngân hàng · Chỉ đọc'
            : q.origin === 'ai'
              ? 'Nguồn AI · Cần rà soát'
              : 'Soạn thủ công'}
        </span>
        {onClear && (
          <button
            type="button"
            onClick={onClear}
            disabled={busy}
            className="ml-auto inline-flex min-h-9 shrink-0 items-center gap-1.5 rounded-xl border border-[#efbdc6] bg-[#fff1f2] px-3 text-xs font-bold text-[#c2415b] transition-colors hover:border-[#c2415b] hover:bg-[#ffe4e8] focus-visible:outline-2 focus-visible:outline-[#c2415b] disabled:opacity-45"
          >
            <Trash2 className="size-3.5" aria-hidden="true" /> Xóa
          </button>
        )}
      </div>
      <TwoStepProgress
        label={`Tiến độ soạn câu ${index + 1}`}
        step={slot.step}
        steps={[
          { id: 1, title: 'Nội dung' },
          { id: 2, title: 'Barem rubric' },
        ]}
        disabled={busy}
        canAdvance
        onStepChange={onStep}
      />
      <fieldset disabled={busy} className="min-w-0 space-y-3 disabled:opacity-70">
        {slot.step === 1 ? (
          <>
            <div
              className={cn(
                'grid min-w-0 gap-3 rounded-2xl border border-[#82aedb]/55 bg-white p-3 sm:grid-cols-[minmax(0,7fr)_minmax(0,3fr)] sm:p-4',
                ds.panelShadow,
              )}
            >
              <div className="min-w-0">
                <p className={fieldLabelClass(Boolean(errors.cloId))}>Chuẩn đầu ra (CLO)</p>
                <FilterDropMenu
                  label="CLO của câu hỏi"
                  value={q.cloId}
                  options={clos.map((c) => ({
                    value: c.id,
                    label: `${c.cloCode} — ${c.description}`,
                  }))}
                  onChange={(value) => update({ cloId: value })}
                  disabled={readOnly || busy}
                  className="mt-1.5 w-full"
                  wrapOptions
                  invalid={Boolean(errors.cloId)}
                  menuMinWidth={0}
                />
                <FieldError message={errors.cloId} />
                {clo && (
                  <p className="mt-1.5 text-xs text-[#5b6b84]">
                    Bloom mục tiêu của CLO:{' '}
                    <span className="font-semibold text-[#1C4D8D]">{clo.bloomLevel}</span> · Thông
                    tin tham khảo
                  </p>
                )}
              </div>
              <div className="min-w-0">
                <p className={fieldLabelClass(Boolean(errors.bloomLevel))}>Bloom của câu hỏi</p>
                <FilterDropMenu
                  invalid={Boolean(errors.bloomLevel)}
                  label="Bloom câu hỏi"
                  value={q.bloomLevel}
                  options={bloomOptions.map((value) => ({ value, label: value }))}
                  onChange={(value) => update({ bloomLevel: value as BloomLevel })}
                  disabled={readOnly || busy}
                  className="mt-1.5 w-full"
                  menuMinWidth={0}
                />
                <FieldError message={errors.bloomLevel} />
              </div>
            </div>
            <section
              className={cn(
                'mb-2! min-w-0 space-y-3 rounded-2xl border border-[#82aedb]/55 bg-white p-3 sm:p-4',
                ds.panelShadow,
              )}
            >
              <label className="block min-w-0">
                <span className={fieldLabelClass(Boolean(errors.title))}>Tiêu đề</span>
                <input
                  className={cn(
                    'mt-1.5 min-h-11 w-full max-w-full min-w-0 rounded-xl border border-[#4988C4] bg-white px-3 text-sm text-[#0F2854] transition outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30',
                    errors.title && fieldInvalidClass,
                  )}
                  aria-invalid={Boolean(errors.title)}
                  onBlur={() => touch('title')}
                  value={q.title}
                  readOnly={readOnly}
                  maxLength={300}
                  placeholder="Tên ngắn gọn giúp nhận diện câu hỏi"
                  onChange={(e) => update({ title: e.target.value })}
                />
                <FieldError message={errors.title} />
              </label>
              <TextareaField
                label="Nội dung câu hỏi"
                className="[field-sizing:content] min-h-[calc(2lh+1.25rem+2px)]"
                onBlur={() => touch('content')}
                value={q.content}
                readOnly={readOnly}
                rows={2}
                placeholder="Viết đầy đủ yêu cầu mà sinh viên cần trình bày..."
                onChange={(e) => update({ content: e.target.value })}
                error={errors.content}
              />
              {q.imageUrl && (
                <QuestionImage
                  imageUrl={q.imageUrl}
                  imageAlt={q.imageDescription || 'Ảnh câu hỏi'}
                />
              )}
              {!readOnly && (
                <div>
                  <input
                    ref={fileRef}
                    type="file"
                    accept="image/png,image/jpeg,image/webp"
                    className="hidden"
                    onChange={(e) => void upload(e.target.files?.[0])}
                  />
                  <div className="flex flex-wrap items-center gap-2">
                    <button
                      type="button"
                      className={ds.outlineButton}
                      onClick={() => fileRef.current?.click()}
                    >
                      <ImagePlus className="size-4" />
                      {q.imageUrl ? 'Đổi ảnh' : 'Thêm ảnh'}
                    </button>
                    {!q.imageUrl && (
                      <span className="text-xs text-slate-600">PNG, JPG, WebP · tối đa 5 MB</span>
                    )}
                    {q.imageUrl && (
                      <button
                        type="button"
                        className={ds.outlineButton}
                        onClick={() => update({ imageUrl: '', imageDescription: '' })}
                      >
                        Bỏ ảnh
                      </button>
                    )}
                  </div>
                  <FieldError message={imageError} />
                </div>
              )}
              {q.imageUrl && (
                <TextareaField
                  label="Mô tả ảnh"
                  value={q.imageDescription}
                  readOnly={readOnly}
                  rows={2}
                  placeholder="Mô tả nội dung trong ảnh"
                  onChange={(e) => update({ imageDescription: e.target.value })}
                />
              )}
              <label className="block min-w-0">
                <span
                  className={cn(
                    'flex items-center justify-between gap-3',
                    fieldLabelClass(Boolean(errors.sampleAnswer)),
                  )}
                >
                  Model Answer
                  <span
                    className={cn(
                      'normal-case',
                      q.sampleAnswer.trim().length >= 50 ? 'text-[#168367]' : 'text-[#b03a46]',
                    )}
                  >
                    {q.sampleAnswer.trim().length}/50 ký tự tối thiểu
                  </span>
                </span>
                <textarea
                  rows={2}
                  value={q.sampleAnswer}
                  readOnly={readOnly}
                  onChange={(e) => update({ sampleAnswer: e.target.value })}
                  placeholder="Câu trả lời chuẩn mực dùng làm mốc chấm..."
                  onBlur={() => touch('sampleAnswer')}
                  aria-label="Model Answer"
                  aria-invalid={Boolean(errors.sampleAnswer)}
                  className={cn(
                    'mt-1.5 [field-sizing:content] min-h-[calc(2lh+1.25rem+2px)] w-full max-w-full min-w-0 resize-y rounded-xl border border-[#4988C4] bg-white px-3 py-2.5 text-sm leading-relaxed text-[#0F2854] transition outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30',
                    errors.sampleAnswer && fieldInvalidClass,
                  )}
                />
                <FieldError message={errors.sampleAnswer} />
              </label>
              <details className="mt-3 min-w-0">
                <summary className="cursor-pointer text-sm font-semibold text-[#0F2854]">
                  Ý chính (tùy chọn)
                </summary>
                <textarea
                  aria-label="Ý chính, mỗi dòng một ý"
                  value={q.keyPoints.join('\n')}
                  readOnly={readOnly}
                  onChange={(e) => update({ keyPoints: e.target.value.split('\n') })}
                  placeholder={'Khái niệm trọng tâm\nLập luận cần có\nVí dụ hoặc tình huống'}
                  className="mt-1.5 min-h-24 w-full max-w-full min-w-0 resize-y rounded-xl border border-[#4988C4] bg-white px-3 py-2.5 text-sm leading-relaxed text-[#0F2854] transition outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30"
                />
              </details>
            </section>
            <div className="mt-0! flex justify-end">
              <button
                type="button"
                onClick={() => onStep(2)}
                className="inline-flex min-h-8 items-center gap-1.5 rounded-lg text-sm font-bold text-[#1C4D8D] underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]"
              >
                Barem <ArrowRight className="size-4" aria-hidden="true" />
              </button>
            </div>
          </>
        ) : (
          <>
            <details className="group min-w-0 overflow-hidden rounded-xl border border-[#c4d8ec] bg-white">
              <summary className="flex cursor-pointer list-none items-center gap-2.5 px-3 py-2 text-sm font-semibold text-[#0F2854] hover:bg-[#f4faff] [&::-webkit-details-marker]:hidden">
                <FilePenLine className="size-4 shrink-0 text-[#1C4D8D]" />
                <span className="flex-1">Xem câu hỏi</span>
                <ChevronRight className="size-4 shrink-0 text-[#1C4D8D] group-open:rotate-90" />
              </summary>
              <div className="space-y-1 border-t border-[#c4d8ec] p-3 [overflow-wrap:anywhere]">
                <h3 className="text-sm font-bold text-[#0F2854]">{q.title}</h3>
                <p className="text-sm whitespace-pre-wrap text-slate-700">{q.content}</p>
                <QuestionImage
                  imageUrl={q.imageUrl}
                  imageAlt={q.imageDescription || 'Ảnh câu hỏi'}
                />
                <div className="space-y-1 rounded-lg bg-[#E7F3FB] p-2.5">
                  <h4 className="text-xs font-bold text-[#0F2854]">Đáp án mẫu</h4>
                  <p className="text-sm whitespace-pre-wrap text-slate-700">{q.sampleAnswer}</p>
                </div>
              </div>
            </details>
            <section
              className={cn(
                'mb-2! min-w-0 space-y-3 rounded-2xl border-2 bg-white p-3 sm:p-4',
                Math.abs(total - 10) < 0.001 ? 'border-[#8bd6bd]' : 'border-[#e46a74]',
                ds.panelShadow,
              )}
            >
              <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                <div>
                  <h4 className="text-sm font-bold text-[#0F2854] sm:text-base">
                    Tiêu chí chấm điểm
                  </h4>
                  <p className="mt-0.5 text-xs text-[#5b6b84]">
                    Tối thiểu hai tiêu chí, tổng điểm bằng chính xác 10.0.
                  </p>
                </div>
                <div className="flex shrink-0 items-center gap-2 whitespace-nowrap">
                  <span
                    className={cn(
                      'inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-xs font-bold tabular-nums',
                      Math.abs(total - 10) < 0.001
                        ? 'bg-[#eaf9f4] text-[#14765c]'
                        : 'bg-[#fff0f1] text-[#ae3945]',
                    )}
                  >
                    {Math.abs(total - 10) < 0.001 && (
                      <Check className="size-3" aria-hidden="true" />
                    )}
                    {total.toFixed(1)} / 10 điểm
                  </span>
                  {!readOnly && (
                    <button
                      type="button"
                      className="inline-flex min-h-9 items-center justify-center gap-1.5 rounded-xl border border-[#82aedb]/55 bg-[#4988C4]/16 px-3 text-xs font-bold text-[#5b6b84] transition duration-200 hover:-translate-y-0.5 hover:bg-[#f0f4fb] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] enabled:hover:border-[#1C4D8D] motion-reduce:transform-none"
                      onClick={() =>
                        update({
                          rubric: {
                            ...q.rubric,
                            criteria: [
                              ...q.rubric.criteria,
                              {
                                criterionName: '',
                                description: '',
                                maxScore: 0,
                                weight: 0,
                                bloomLevel: q.bloomLevel,
                                orderIndex: q.rubric.criteria.length + 1,
                              },
                            ],
                          },
                        })
                      }
                    >
                      <Plus className="size-4" />
                      Thêm tiêu chí
                    </button>
                  )}
                </div>
              </div>
              <div className="grid min-w-0 gap-3 sm:grid-cols-2">
                <label className="block min-w-0">
                  <span className={fieldLabelClass(Boolean(errors.rubricName))}>Tên rubric</span>
                  <input
                    className={cn(ds.field, 'mt-1.5 h-10', errors.rubricName && fieldInvalidClass)}
                    value={q.rubric.name}
                    onBlur={() => touch('rubricName')}
                    aria-invalid={Boolean(errors.rubricName)}
                    readOnly={readOnly}
                    placeholder="Ví dụ: Rubric đánh giá kiến trúc"
                    onChange={(e) => update({ rubric: { ...q.rubric, name: e.target.value } })}
                  />
                  <FieldError message={errors.rubricName} />
                </label>
                <label className="block min-w-0">
                  <span className={fieldLabelClass(false)}>Mô tả</span>
                  <input
                    className={cn(ds.field, 'mt-1.5 h-10')}
                    value={q.rubric.description ?? ''}
                    readOnly={readOnly}
                    placeholder="Mục tiêu đánh giá của rubric"
                    onChange={(e) =>
                      update({ rubric: { ...q.rubric, description: e.target.value } })
                    }
                  />
                </label>
              </div>
              {q.rubric.criteria.map((c, i) => (
                <div key={i} className="relative min-w-0">
                  <details
                    open={
                      errors[`criterion-${i}-name`] || errors[`criterion-${i}-score`]
                        ? true
                        : undefined
                    }
                    className="min-w-0 rounded-2xl border border-[#82aedb]/55 bg-white p-3.5"
                  >
                    <summary className="cursor-pointer rounded-lg py-1 pr-10 text-sm font-semibold wrap-anywhere text-[#0F2854]">
                      {c.criterionName || `Tiêu chí ${i + 1}`} · {c.maxScore} điểm
                    </summary>
                    <div className="grid min-w-0 gap-3 sm:grid-cols-[minmax(0,1fr)_100px]">
                      <label className="block min-w-0">
                        <span className={fieldLabelClass(Boolean(errors[`criterion-${i}-name`]))}>
                          Tiêu chí {i + 1}
                        </span>
                        <input
                          className={cn(
                            'mt-1 min-h-10 w-full max-w-full min-w-0 rounded-xl border border-[#4988C4] bg-white px-3 text-xs outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30',
                            errors[`criterion-${i}-name`] && fieldInvalidClass,
                          )}
                          onBlur={() => touch(`criterion-${i}-name`)}
                          aria-invalid={Boolean(errors[`criterion-${i}-name`])}
                          value={c.criterionName}
                          readOnly={readOnly}
                          placeholder="Tên tiêu chí đánh giá"
                          onChange={(e) =>
                            update({
                              rubric: {
                                ...q.rubric,
                                criteria: q.rubric.criteria.map((x, n) =>
                                  n === i ? { ...x, criterionName: e.target.value } : x,
                                ),
                              },
                            })
                          }
                        />
                        <FieldError message={errors[`criterion-${i}-name`]} />
                      </label>
                      <label className="block min-w-0">
                        <span className={fieldLabelClass(Boolean(errors[`criterion-${i}-score`]))}>
                          Điểm
                        </span>
                        <input
                          type="number"
                          min={0.1}
                          max={10}
                          step={0.1}
                          className={cn(
                            'mt-1 min-h-10 w-full max-w-full min-w-0 rounded-xl border border-[#4988C4] bg-white px-2 text-center font-mono text-sm font-black outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30',
                            errors[`criterion-${i}-score`] && fieldInvalidClass,
                          )}
                          aria-invalid={Boolean(errors[`criterion-${i}-score`])}
                          onBlur={() => {
                            touch(`criterion-${i}-score`)
                            touch('rubricTotal')
                          }}
                          value={c.maxScore}
                          readOnly={readOnly}
                          onChange={(e) =>
                            update({
                              rubric: {
                                ...q.rubric,
                                criteria: q.rubric.criteria.map((x, n) =>
                                  n === i
                                    ? {
                                        ...x,
                                        maxScore: Number(e.target.value),
                                        weight: Number(e.target.value) / 10,
                                      }
                                    : x,
                                ),
                              },
                            })
                          }
                        />
                        <FieldError message={errors[`criterion-${i}-score`]} />
                      </label>
                    </div>
                    <label className="mt-3 block min-w-0">
                      <span className="sr-only">Mô tả tiêu chí {i + 1}</span>
                      <textarea
                        rows={2}
                        value={c.description}
                        readOnly={readOnly}
                        placeholder="Mô tả biểu hiện đạt điểm của tiêu chí..."
                        className="w-full max-w-full min-w-0 resize-y rounded-lg border border-[#4988C4] bg-white px-3 py-2 text-xs leading-relaxed outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30"
                        onChange={(e) =>
                          update({
                            rubric: {
                              ...q.rubric,
                              criteria: q.rubric.criteria.map((x, n) =>
                                n === i ? { ...x, description: e.target.value } : x,
                              ),
                            },
                          })
                        }
                      />
                    </label>
                  </details>
                  <div className="absolute top-3 right-3">
                    {!readOnly && (
                      <button
                        type="button"
                        className="flex size-8 items-center justify-center rounded-lg border border-red-200 bg-red-50 text-red-700 transition-colors focus-visible:outline-2 focus-visible:outline-[#b43d49] enabled:hover:border-red-500 enabled:hover:bg-red-100 disabled:cursor-not-allowed disabled:opacity-35"
                        disabled={busy || q.rubric.criteria.length <= 2}
                        aria-label={`Xóa tiêu chí ${i + 1}`}
                        onClick={() =>
                          update({
                            rubric: {
                              ...q.rubric,
                              criteria: q.rubric.criteria.filter((_, n) => n !== i),
                            },
                          })
                        }
                      >
                        <Trash2 className="size-4" />
                      </button>
                    )}
                  </div>
                </div>
              ))}
              <FieldError message={errors.criteria} />
              <FieldError message={errors.rubricTotal} />
            </section>
            <div className="mt-0! flex justify-start">
              <button
                type="button"
                onClick={() => onStep(1)}
                className="inline-flex min-h-8 items-center gap-1.5 rounded-lg text-sm font-bold text-[#1C4D8D] underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]"
              >
                <ArrowLeft className="size-4" aria-hidden="true" /> Nội dung
              </button>
            </div>
          </>
        )}
      </fieldset>
    </div>
  )
}
