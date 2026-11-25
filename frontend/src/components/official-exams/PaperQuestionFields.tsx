import { useRef, useState } from 'react'
import { ImagePlus, Plus, Trash2 } from 'lucide-react'
import { TwoStepProgress } from '@/design/TwoStepProgress'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { FieldError } from '@/components/common/formField'
import { fieldLabelClass } from '@/components/common/formFieldClasses'
import { TextareaField } from '@/components/common/TextareaField'
import { FilterDropMenu } from '@/components/common/FilterDropMenu'
import { QuestionImage } from '@/components/question/QuestionImage'
import { questionService } from '@/services/question.service'
import { preparationError, DIFFICULTY_LABELS } from '@/lib/examPreparation'
import { bloomOptions, type PaperQuestionDraft, type QuestionSlot } from '@/lib/examPaperAuthoring'
import type { CourseClo } from '@/types/courseClo.types'
import type { BloomLevel } from '@/types/rubric.types'

export function PaperQuestionFields({
  slot,
  index,
  clos,
  errors,
  busy,
  onChange,
  onStep,
  onUploading,
}: {
  slot: QuestionSlot
  index: number
  clos: CourseClo[]
  errors: Record<string, string>
  busy: boolean
  onChange: (question: PaperQuestionDraft) => void
  onStep: (step: 1 | 2) => void
  onUploading: (uploading: boolean) => void
}) {
  const q = slot.question
  const readOnly = q.origin === 'bank'
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
    <div className="min-w-0 space-y-4">
      <div className="flex flex-wrap items-center gap-2">
        <h3 className="text-lg font-bold text-[#0F2854]">Câu {index + 1}</h3>
        <span className={ds.infoChip}>{DIFFICULTY_LABELS[slot.difficulty]}</span>
        <span className={ds.infoChip}>
          {readOnly
            ? 'Từ ngân hàng · Chỉ đọc'
            : q.origin === 'ai'
              ? 'Nguồn AI · Cần rà soát'
              : 'Soạn thủ công'}
        </span>
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
      <fieldset disabled={busy} className="min-w-0 space-y-4 disabled:opacity-70">
        {slot.step === 1 ? (
          <>
            <div className="grid min-w-0 gap-3 sm:grid-cols-2">
              <div className="min-w-0">
                <p className={fieldLabelClass(Boolean(errors.cloId))}>CLO của câu hỏi</p>
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
                  menuMinWidth={0}
                />
                <FieldError message={errors.cloId} />
                {clo && (
                  <p className="mt-1 text-xs text-[#5b6b84]">Bloom mục tiêu: {clo.bloomLevel}</p>
                )}
              </div>
              <div className="min-w-0">
                <p className={fieldLabelClass(false)}>Bloom câu hỏi</p>
                <FilterDropMenu
                  label="Bloom câu hỏi"
                  value={q.bloomLevel}
                  options={bloomOptions.map((value) => ({ value, label: value }))}
                  onChange={(value) => update({ bloomLevel: value as BloomLevel })}
                  disabled={readOnly || busy}
                  className="mt-1.5 w-full"
                  menuMinWidth={0}
                />
              </div>
            </div>
            <label className="block min-w-0">
              <span className={fieldLabelClass(Boolean(errors.title))}>Tiêu đề</span>
              <input
                className={cn(ds.field, 'mt-1.5 h-10')}
                value={q.title}
                readOnly={readOnly}
                maxLength={300}
                placeholder="Nhập tiêu đề câu hỏi"
                onChange={(e) => update({ title: e.target.value })}
              />
              <FieldError message={errors.title} />
            </label>
            <TextareaField
              label="Nội dung câu hỏi"
              value={q.content}
              readOnly={readOnly}
              rows={4}
              placeholder="Nhập yêu cầu và nội dung câu hỏi"
              onChange={(e) => update({ content: e.target.value })}
              error={errors.content}
            />
            {q.imageUrl && (
              <QuestionImage imageUrl={q.imageUrl} imageAlt={q.imageDescription || 'Ảnh câu hỏi'} />
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
                <div className="flex flex-wrap gap-2">
                  <button
                    type="button"
                    className={ds.outlineButton}
                    onClick={() => fileRef.current?.click()}
                  >
                    <ImagePlus className="size-4" />
                    {q.imageUrl ? 'Đổi ảnh' : 'Thêm ảnh'}
                  </button>
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
            <TextareaField
              label="Đáp án mẫu"
              value={q.sampleAnswer}
              readOnly={readOnly}
              rows={5}
              placeholder="Nhập đáp án mẫu, ít nhất 50 ký tự"
              onChange={(e) => update({ sampleAnswer: e.target.value })}
              error={errors.sampleAnswer}
            />
            <div className="flex justify-end">
              <button type="button" className={ds.primaryButton} onClick={() => onStep(2)}>
                Tiếp tục
              </button>
            </div>
          </>
        ) : (
          <>
            <div className="flex flex-wrap items-center justify-between gap-2">
              <h4 className="font-bold text-[#0F2854]">Barem rubric</h4>
              <div className="flex items-center gap-2">
                <span className={Math.abs(total - 10) < 0.001 ? ds.successChip : ds.dangerChip}>
                  {total.toFixed(1)} / 10 điểm
                </span>
                {!readOnly && (
                  <button
                    type="button"
                    className={ds.outlineButton}
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
            <label className="block min-w-0">
              <span className={fieldLabelClass(Boolean(errors.rubricName))}>Tên rubric</span>
              <input
                className={cn(ds.field, 'mt-1.5 h-10')}
                value={q.rubric.name}
                readOnly={readOnly}
                placeholder="Nhập tên barem đánh giá"
                onChange={(e) => update({ rubric: { ...q.rubric, name: e.target.value } })}
              />
              <FieldError message={errors.rubricName} />
            </label>
            <TextareaField
              label="Mô tả rubric"
              value={q.rubric.description ?? ''}
              readOnly={readOnly}
              rows={2}
              placeholder="Mục tiêu đánh giá của rubric"
              onChange={(e) => update({ rubric: { ...q.rubric, description: e.target.value } })}
            />
            {q.rubric.criteria.map((c, i) => (
              <section
                key={i}
                className={cn(
                  'min-w-0 rounded-2xl border border-[#82aedb]/55 bg-white p-4',
                  ds.panelShadow,
                )}
              >
                <div className="mb-3 flex items-center justify-between gap-2">
                  <h5 className="text-sm font-bold text-[#0F2854]">Tiêu chí {i + 1}</h5>
                  {!readOnly && (
                    <button
                      type="button"
                      className="rounded-lg border border-red-200 p-2 text-red-700 disabled:opacity-35"
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
                <div className="grid min-w-0 gap-3 sm:grid-cols-[minmax(0,1fr)_90px]">
                  <label className="block min-w-0">
                    <span className={fieldLabelClass(false)}>Tên tiêu chí {i + 1}</span>
                    <input
                      className={cn(ds.field, 'mt-1.5 h-10')}
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
                  </label>
                  <label className="block min-w-0">
                    <span className={fieldLabelClass(false)}>Điểm {i + 1}</span>
                    <input
                      type="number"
                      min={0.1}
                      max={10}
                      step={0.1}
                      className={cn(ds.field, 'mt-1.5 h-10 text-center')}
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
                  </label>
                </div>
                <TextareaField
                  wrapperClassName="mt-3"
                  label={`Mô tả tiêu chí ${i + 1}`}
                  value={c.description}
                  readOnly={readOnly}
                  rows={2}
                  placeholder="Mô tả biểu hiện đạt điểm"
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
              </section>
            ))}
            <FieldError message={errors.criteria} />
            <button type="button" className={ds.outlineButton} onClick={() => onStep(1)}>
              Quay lại nội dung
            </button>
          </>
        )}
      </fieldset>
    </div>
  )
}
