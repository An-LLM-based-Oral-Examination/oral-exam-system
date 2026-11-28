import { SubmitDialogFooter } from '@/components/common/SubmitDialogFooter'
import { useEffect, useState, type FormEvent } from 'react'
import { Save, Settings2 } from 'lucide-react'

import { fieldLabelClass } from '@/components/common/formFieldClasses'
import { toast } from '@/components/common/toastStore'
import { FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { userMessage } from '@/lib/userMessage'
import { courseService } from '@/services/course.service'
import type { UpdateCourseConfigurationRequest } from '@/types/practice.types'

const DIFFICULTIES = [
  { value: 'easy', label: 'Dễ' },
  { value: 'medium', label: 'Trung bình' },
  { value: 'hard', label: 'Khó' },
] as const

const EXAM_INPUT_MODES: Array<{
  value: UpdateCourseConfigurationRequest['examInputMode']
  label: string
}> = [
  { value: 'VoiceWithTranscriptEdit', label: 'Giọng nói + cho phép sửa transcript' },
  { value: 'VoiceOnly', label: 'Chỉ giọng nói (Voice-Only)' },
]

export function CourseConfigurationDialog({
  courseId,
  onClose,
}: {
  courseId: string
  onClose: () => void
}) {
  const [form, setForm] = useState<UpdateCourseConfigurationRequest>()
  const [difficulty, setDifficulty] = useState<Record<string, number>>({})
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    let active = true
    void courseService
      .getConfiguration(courseId)
      .then((config) => {
        if (!active) return
        const distribution: unknown = JSON.parse(
          config.mockExamDifficultyDistribution || '{"easy":0,"medium":0,"hard":0}',
        )
        if (
          !distribution ||
          typeof distribution !== 'object' ||
          Array.isArray(distribution) ||
          Object.entries(distribution).some(
            ([key, value]) =>
              !DIFFICULTIES.some((level) => level.value === key) ||
              typeof value !== 'number' ||
              !Number.isInteger(value) ||
              value < 0,
          )
        ) {
          throw new Error('Phân bổ độ khó hiện tại không hợp lệ.')
        }
        const parsed = distribution as Record<string, number>
        setDifficulty({
          easy: 0,
          medium: 0,
          hard: 0,
          ...parsed,
        })
        setForm({
          transcriptBufferSeconds: config.transcriptBufferSeconds,
          maxFollowUpQuestions: config.maxFollowUpQuestions,
          hasFollowUp: config.hasFollowUp,
          examInputMode:
            config.examInputMode === 'VoiceOnly' ? 'VoiceOnly' : 'VoiceWithTranscriptEdit',
          mockExamDurationMinutes: config.mockExamDurationMinutes,
          mockExamTotalQuestions: config.mockExamTotalQuestions,
          mockExamDifficultyDistribution: config.mockExamDifficultyDistribution,
        })
      })
      .catch((failure) => {
        if (active) setError(userMessage(failure, 'Không tải được cấu hình môn học.'))
      })
    return () => {
      active = false
    }
  }, [courseId])

  const save = async (event: FormEvent) => {
    event.preventDefault()
    if (!form || saving) return
    setError('')

    if (
      !Number.isInteger(form.mockExamDurationMinutes) ||
      (form.mockExamDurationMinutes ?? 0) < 1
    ) {
      setError('Thời gian thi thử phải là số nguyên lớn hơn hoặc bằng 1 phút.')
      return
    }
    if (
      !Number.isInteger(form.transcriptBufferSeconds) ||
      form.transcriptBufferSeconds < 10 ||
      form.transcriptBufferSeconds > 300
    ) {
      setError('Thời gian kiểm tra transcript phải là số nguyên từ 10 đến 300 giây.')
      return
    }
    if (
      !Number.isInteger(form.maxFollowUpQuestions) ||
      form.maxFollowUpQuestions < 1 ||
      form.maxFollowUpQuestions > 5
    ) {
      setError('Số câu hỏi phụ tối đa phải là số nguyên từ 1 đến 5.')
      return
    }
    if (
      Object.values(difficulty).some(
        (count) => !Number.isInteger(count) || count < 0 || count > 100,
      )
    ) {
      setError('Số câu ở mỗi độ khó phải là số nguyên từ 0 đến 100.')
      return
    }
    const totalQuestions = Object.values(difficulty).reduce((sum, count) => sum + count, 0)
    if (totalQuestions < 1 || totalQuestions > 100) {
      setError('Tổng số câu phải từ 1 đến 100.')
      return
    }

    setSaving(true)
    try {
      await courseService.updateConfiguration(courseId, {
        ...form,
        mockExamDifficultyDistribution: JSON.stringify(difficulty),
        mockExamTotalQuestions: totalQuestions,
      })
      toast.success('Đã lưu cấu hình môn học.')
      onClose()
    } catch (failure) {
      setError(userMessage(failure, 'Không lưu được cấu hình môn học.'))
    } finally {
      setSaving(false)
    }
  }

  return (
    <FormDialog
      title="Cấu hình môn học"
      eyebrow="Chi tiết môn học"
      icon={Settings2}
      size="md"
      busy={saving}
      onClose={onClose}
      footer={
        <SubmitDialogFooter
          form="course-configuration"
          label="Lưu cấu hình"
          busyLabel="Đang lưu…"
          busy={saving}
          disabled={!form}
          icon={Save}
          onCancel={onClose}
        />
      }
    >
      <form id="course-configuration" noValidate onSubmit={save} className="space-y-3.5">
        {error && (
          <p role="alert" className="rounded-xl bg-[#fff0f1] p-3 text-sm text-[#c2415b]">
            {error}
          </p>
        )}
        {!form && !error && (
          <p role="status" className="text-sm text-[#5b6b84]">
            Đang tải cấu hình…
          </p>
        )}
        {form && (
          <>
            <div className="grid gap-3 sm:grid-cols-2">
              <label className="block" htmlFor="course-mock-duration">
                <span className={fieldLabelClass(false)}>Thời gian thi thử (phút)</span>
                <input
                  id="course-mock-duration"
                  type="number"
                  min={1}
                  step={1}
                  required
                  disabled={saving}
                  className={cn(ds.field, 'mt-1.5')}
                  value={form.mockExamDurationMinutes ?? ''}
                  onChange={(event) =>
                    setForm({ ...form, mockExamDurationMinutes: Number(event.target.value) })
                  }
                />
              </label>

              <label className="block" htmlFor="course-transcript-buffer">
                <span className={fieldLabelClass(false)}>Thời gian sửa transcript (giây)</span>
                <input
                  id="course-transcript-buffer"
                  type="number"
                  min={10}
                  max={300}
                  step={1}
                  required
                  disabled={saving}
                  className={cn(ds.field, 'mt-1.5')}
                  value={form.transcriptBufferSeconds}
                  onChange={(event) =>
                    setForm({ ...form, transcriptBufferSeconds: Number(event.target.value) })
                  }
                />
                <span className="mt-1 block text-[11px] text-[#5b6b84]">
                  Hợp lệ từ 10 đến 300 giây (mặc định 60 giây).
                </span>
              </label>
            </div>

            <label className="block" htmlFor="course-exam-input-mode">
              <span className={fieldLabelClass(false)}>Chế độ nhập câu trả lời</span>
              <select
                id="course-exam-input-mode"
                disabled={saving}
                className={cn(ds.field, 'mt-1.5')}
                value={form.examInputMode}
                onChange={(event) =>
                  setForm({
                    ...form,
                    examInputMode:
                      event.target.value === 'VoiceOnly'
                        ? 'VoiceOnly'
                        : 'VoiceWithTranscriptEdit',
                  })
                }
              >
                {EXAM_INPUT_MODES.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </label>

            <div className="grid items-end gap-3 sm:grid-cols-2">
              <label className="block" htmlFor="course-max-follow-up">
                <span className={fieldLabelClass(false)}>Câu hỏi phụ tối đa</span>
                <input
                  id="course-max-follow-up"
                  type="number"
                  min={1}
                  max={5}
                  step={1}
                  required
                  disabled={saving}
                  className={cn(ds.field, 'mt-1.5')}
                  value={form.maxFollowUpQuestions}
                  onChange={(event) =>
                    setForm({ ...form, maxFollowUpQuestions: Number(event.target.value) })
                  }
                />
              </label>
              <button
                type="button"
                role="switch"
                aria-checked={form.hasFollowUp}
                disabled={saving}
                onClick={() => setForm({ ...form, hasFollowUp: !form.hasFollowUp })}
                className="flex h-10 items-center justify-between gap-2 rounded-xl border border-[#4988C4] bg-white px-3 text-sm font-bold text-[#0F2854]"
              >
                Câu hỏi phụ
                <span
                  aria-hidden="true"
                  className={cn(
                    'relative h-6 w-11 shrink-0 rounded-full transition-colors',
                    form.hasFollowUp ? 'bg-[#16836a]' : 'bg-[#cbd5e1]',
                  )}
                >
                  <span
                    className={cn(
                      'absolute top-0.5 left-0.5 size-5 rounded-full bg-white transition-transform',
                      form.hasFollowUp && 'translate-x-5',
                    )}
                  />
                </span>
              </button>
            </div>

            <fieldset className="border-t border-[#82aedb]/35 pt-3">
              <legend className="sr-only">Phân bổ độ khó</legend>
              <div className="flex items-center justify-between gap-2">
                <span className={fieldLabelClass(false)}>Phân bổ độ khó</span>
                <span className="text-xs font-semibold text-slate-600 tabular-nums">
                  {Object.values(difficulty).reduce((sum, count) => sum + count, 0)} câu
                </span>
              </div>
              <div className="mt-2 grid w-full grid-cols-3 gap-x-3 sm:gap-x-8">
                {DIFFICULTIES.map(({ value: level, label }) => (
                  <label
                    key={level}
                    className="grid min-w-0 grid-cols-[minmax(0,max-content)_auto] items-center gap-1.5 first:justify-self-start last:justify-self-end nth-2:justify-self-center"
                  >
                    <span
                      aria-hidden="true"
                      className="invisible col-start-1 row-start-1 text-sm font-bold"
                    >
                      Trung bình:
                    </span>
                    <span className="col-start-1 row-start-1 text-sm font-bold text-[#0F2854]">
                      {label}:
                    </span>
                    <input
                      type="number"
                      min={0}
                      max={100}
                      required
                      disabled={saving}
                      className={cn(
                        ds.field,
                        'min-h-9 w-10 min-w-0 shrink px-1 text-center tabular-nums sm:w-16',
                      )}
                      value={difficulty[level] ?? 0}
                      onChange={(event) =>
                        setDifficulty({ ...difficulty, [level]: Number(event.target.value) })
                      }
                    />
                  </label>
                ))}
              </div>
            </fieldset>
          </>
        )}
      </form>
    </FormDialog>
  )
}
