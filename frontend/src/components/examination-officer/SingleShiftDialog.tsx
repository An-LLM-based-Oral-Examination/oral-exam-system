import { type FormEvent } from 'react'
import {
  BookOpen,
  CalendarDays,
  CheckCircle2,
  GraduationCap,
  LoaderCircle,
  Plus,
} from 'lucide-react'
import { type ShiftStaffCandidate } from '@/services/course.service'
import type { SemesterCourseOffering, SemesterOption } from '@/types/class.types'
import type { ExamShift, OfficialExamSession } from '@/types/officialExam.types'
import { DialogFooter, FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'
import {
  DEFAULT_SHIFT_CAPACITY,
  MAX_SHIFT_CAPACITY,
  MIN_SHIFT_CAPACITY,
  validateShiftCapacityInput,
} from '@/lib/shiftCapacity'
import type {
  CreateShiftFormState,
  CreatedShiftNotice,
  EditShiftFormState,
} from './shiftStaffHelpers'

interface CreateSingleShiftDialogProps {
  open: boolean
  busy: boolean
  isLoading: boolean
  isLoadingOfferings: boolean
  isLoadingLecturers: boolean
  semesters: SemesterOption[]
  offerings: SemesterCourseOffering[]
  selectedSemester: SemesterOption | undefined
  selectedCourse: SemesterCourseOffering | undefined
  selectedCourseSessions: OfficialExamSession[]
  proctorOptions: ShiftStaffCandidate[]
  examinerOptions: ShiftStaffCandidate[]
  shiftForm: CreateShiftFormState
  error: string
  offeringsError: string
  lecturersError: string
  onClose: () => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
  onSemesterChange: (semesterId: string) => void
  onCourseChange: (courseId: string) => void
  onChangeField: <K extends keyof CreateShiftFormState>(
    key: K,
    value: CreateShiftFormState[K],
  ) => void
}

export function CreateSingleShiftDialog({
  open,
  busy,
  isLoading,
  isLoadingOfferings,
  isLoadingLecturers,
  semesters,
  offerings,
  selectedSemester,
  selectedCourse,
  selectedCourseSessions,
  proctorOptions,
  examinerOptions,
  shiftForm,
  error,
  offeringsError,
  lecturersError,
  onClose,
  onSubmit,
  onSemesterChange,
  onCourseChange,
  onChangeField,
}: CreateSingleShiftDialogProps) {
  const createCapacityValidation = validateShiftCapacityInput(shiftForm.maxSeats, {
    defaultWhenEmpty: DEFAULT_SHIFT_CAPACITY,
  })

  return (
    <FormDialog
      open={open}
      align="center"
      size="xl"
      maxHeight="max-h-[min(94dvh,900px)]"
      eyebrow="MỞ CA THI MF-04"
      title="Tạo ca thi"
      icon={CalendarDays}
      busy={busy}
      onClose={onClose}
      footer={
        <DialogFooter className="justify-end bg-white">
          <button type="button" className={ds.outlineButton} onClick={onClose} disabled={busy}>
            Hủy
          </button>
          <button
            type="submit"
            form="create-official-shift-form"
            disabled={busy || !selectedSemester || !selectedCourse}
            className={ds.primaryButton}
          >
            {busy ? <LoaderCircle className="size-4 animate-spin" /> : <Plus className="size-4" />}
            {busy
              ? 'Đang tạo ca…'
              : selectedCourse
                ? `Tạo ca thi cho ${selectedCourse.courseCode}`
                : 'Chọn môn để tạo ca'}
          </button>
        </DialogFooter>
      }
    >
      <form id="create-official-shift-form" onSubmit={onSubmit} className="grid gap-5">
        {error ? (
          <div
            role="alert"
            className="rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-4 py-3 text-sm font-semibold text-[#a43d4b]"
          >
            {error}
          </div>
        ) : null}

        <section className="rounded-2xl border border-[#d9e8f2] bg-[#F8FBFD] p-4 sm:p-5">
          <div className="mb-3 flex items-start gap-3">
            <span className="grid size-9 shrink-0 place-items-center rounded-xl bg-[#dbeefa] text-[#245997]">
              <GraduationCap className="size-5" aria-hidden="true" />
            </span>
            <div>
              <h3 className="text-base font-black text-[#0F2854]">Học kỳ và môn thi</h3>
              <p className="mt-1 text-xs text-[#71829a]">
                Chỉ chọn các môn đã được Admin đưa vào học kỳ.
              </p>
            </div>
          </div>
          <div className="grid items-start gap-3 lg:grid-cols-2">
            <label
              className="flex min-w-0 flex-col gap-1.5 text-xs font-bold text-[#52657d]"
              htmlFor="exam-semester"
            >
              Học kỳ *
              <select
                id="exam-semester"
                required
                className={ds.field}
                value={shiftForm.semesterId}
                disabled={isLoading}
                onChange={(event) => onSemesterChange(event.target.value)}
              >
                <option value="">Chọn học kỳ đang học</option>
                {semesters.map((semester) => (
                  <option key={semester.id} value={semester.id}>
                    {semester.name || semester.code} · {semester.code}
                  </option>
                ))}
              </select>
              {selectedSemester ? (
                <span className="text-[11px] font-medium text-[#71829a]">
                  {selectedSemester.startDate.slice(0, 10)} – {selectedSemester.endDate.slice(0, 10)}
                </span>
              ) : null}
            </label>

            <label
              className="flex min-w-0 flex-col gap-1.5 text-xs font-bold text-[#52657d]"
              htmlFor="exam-course"
            >
              Môn thi trong học kỳ *
              <select
                id="exam-course"
                required
                className={ds.field}
                value={shiftForm.courseId}
                disabled={!shiftForm.semesterId || isLoadingOfferings}
                onChange={(event) => onCourseChange(event.target.value)}
              >
                <option value="">
                  {isLoadingOfferings
                    ? 'Đang tải danh sách môn…'
                    : 'Chọn môn đã được đưa vào học kỳ'}
                </option>
                {offerings.map((offering) => (
                  <option key={offering.courseId} value={offering.courseId}>
                    {offering.courseCode} — {offering.courseName}
                  </option>
                ))}
              </select>
              {offeringsError ? (
                <span className="text-[11px] font-semibold text-[#a43d4b]">{offeringsError}</span>
              ) : null}
              {!offeringsError &&
              shiftForm.semesterId &&
              !isLoadingOfferings &&
              offerings.length === 0 ? (
                <span className="text-[11px] font-medium text-[#71829a]">
                  Học kỳ này chưa có môn được Admin đưa vào.
                </span>
              ) : null}
            </label>
          </div>
          {selectedCourse ? (
            <div className="mt-3 flex flex-wrap items-center gap-3 rounded-xl border border-[#bcd6ef] bg-white px-4 py-3 text-sm text-[#245997]">
              <CheckCircle2 className="size-5 text-[#168b70]" />
              <span>
                Đã chọn <strong>{selectedCourse.courseCode}</strong> · {selectedCourse.courseName}
              </span>
              <span className="rounded-full bg-[#f3f8fd] px-3 py-1 text-xs font-semibold text-[#667892]">
                {selectedCourseSessions.length} đợt kỹ thuật
              </span>
            </div>
          ) : null}
        </section>

        <section className="rounded-2xl border border-[#d9e8f2] bg-white p-4 sm:p-5">
          <div className="mb-3 flex items-start gap-3">
            <span className="grid size-9 shrink-0 place-items-center rounded-xl bg-[#dbeefa] text-[#245997]">
              <BookOpen className="size-5" aria-hidden="true" />
            </span>
            <div>
              <h3 className="text-base font-black text-[#0F2854]">Thông tin ca thi</h3>
              <p className="mt-1 text-xs text-[#71829a]">
                {selectedCourse ? `Tạo ca cho ${selectedCourse.courseCode}` : 'Chọn môn thi trước'}
              </p>
            </div>
          </div>
          <div className="grid gap-3 lg:grid-cols-2">
            <label className="grid gap-1.5 text-xs font-bold text-[#52657d]" htmlFor="shift-name">
              Tên ca *
              <input
                id="shift-name"
                required
                className={ds.field}
                value={shiftForm.shiftName}
                onChange={(event) => onChangeField('shiftName', event.target.value)}
              />
            </label>
            <label className="grid gap-1.5 text-xs font-bold text-[#52657d]" htmlFor="shift-room">
              Phòng Lab *
              <input
                id="shift-room"
                required
                className={ds.field}
                value={shiftForm.roomLab}
                onChange={(event) => onChangeField('roomLab', event.target.value)}
              />
            </label>
            <label
              className="grid gap-1.5 text-xs font-bold text-[#52657d] lg:col-span-2"
              htmlFor="shift-max-seats"
            >
              Sức chứa tối đa (maxSeats) *
              <input
                id="shift-max-seats"
                type="number"
                min={MIN_SHIFT_CAPACITY}
                max={MAX_SHIFT_CAPACITY}
                step={1}
                className={ds.field}
                value={shiftForm.maxSeats}
                onChange={(event) => onChangeField('maxSeats', event.target.value)}
              />
              <span className="text-[11px] font-medium text-[#71829a]">
                Mặc định {DEFAULT_SHIFT_CAPACITY} ghế · Giá trị hợp lệ: số nguyên từ{' '}
                {MIN_SHIFT_CAPACITY} đến {MAX_SHIFT_CAPACITY} (trần cứng {MAX_SHIFT_CAPACITY}).
              </span>
              {shiftForm.maxSeats.trim() !== '' && createCapacityValidation.error ? (
                <span role="alert" className="text-[11px] font-semibold text-[#a43d4b]">
                  {createCapacityValidation.error}
                </span>
              ) : null}
            </label>
            <label className="grid gap-1.5 text-xs font-bold text-[#52657d]" htmlFor="shift-start">
              Bắt đầu *
              <input
                id="shift-start"
                required
                type="datetime-local"
                className={ds.field}
                value={shiftForm.startTime}
                onChange={(event) => onChangeField('startTime', event.target.value)}
              />
            </label>
            <label className="grid gap-1.5 text-xs font-bold text-[#52657d]" htmlFor="shift-end">
              Kết thúc *
              <input
                id="shift-end"
                required
                type="datetime-local"
                className={ds.field}
                value={shiftForm.endTime}
                onChange={(event) => onChangeField('endTime', event.target.value)}
              />
            </label>
            <label className="grid gap-1.5 text-xs font-bold text-[#52657d]" htmlFor="shift-proctor">
              Giám thị *
              <select
                id="shift-proctor"
                required
                className={ds.field}
                value={shiftForm.proctorUserId}
                onChange={(event) => onChangeField('proctorUserId', event.target.value)}
                disabled={!selectedCourse || isLoadingLecturers || proctorOptions.length === 0}
              >
                <option value="">
                  {isLoadingLecturers
                    ? 'Đang tải danh sách giám thị…'
                    : selectedCourse
                      ? 'Chọn giám thị'
                      : 'Chọn môn thi trước'}
                </option>
                {proctorOptions.map((candidate) => (
                  <option
                    key={`proctor-${candidate.userId}`}
                    value={candidate.userId}
                    disabled={candidate.userId === shiftForm.examinerUserId}
                  >
                    {candidate.fullName}
                    {candidate.detail ? ` · ${candidate.detail}` : ''}
                  </option>
                ))}
              </select>
            </label>
            <label
              className="grid gap-1.5 text-xs font-bold text-[#52657d]"
              htmlFor="shift-examiner"
            >
              Giám khảo *
              <select
                id="shift-examiner"
                required
                className={ds.field}
                value={shiftForm.examinerUserId}
                onChange={(event) => onChangeField('examinerUserId', event.target.value)}
                disabled={!selectedCourse || isLoadingLecturers || examinerOptions.length === 0}
              >
                <option value="">
                  {isLoadingLecturers
                    ? 'Đang tải danh sách giám khảo…'
                    : selectedCourse
                      ? 'Chọn giám khảo'
                      : 'Chọn môn thi trước'}
                </option>
                {examinerOptions.map((candidate) => (
                  <option
                    key={`examiner-${candidate.userId}`}
                    value={candidate.userId}
                    disabled={candidate.userId === shiftForm.proctorUserId}
                  >
                    {candidate.fullName}
                    {candidate.detail ? ` · ${candidate.detail}` : ''}
                  </option>
                ))}
              </select>
            </label>
          </div>
          {selectedCourse ? (
            <p
              className="mt-3 text-xs font-medium text-[#71829a]"
              role={lecturersError ? 'alert' : undefined}
            >
              {isLoadingLecturers
                ? 'Đang tải danh sách nhân sự coi thi và chấm thi cho môn đã chọn…'
                : lecturersError ||
                  `Sẵn sàng phân công: ${proctorOptions.length} giám thị · ${examinerOptions.length} giám khảo đủ điều kiện.`}
            </p>
          ) : null}
        </section>
      </form>
    </FormDialog>
  )
}

interface CreatedShiftSuccessDialogProps {
  notice: CreatedShiftNotice | null
  onClose: () => void
}

export function CreatedShiftSuccessDialog({ notice, onClose }: CreatedShiftSuccessDialogProps) {
  if (!notice) return null
  return (
    <FormDialog
      open
      align="center"
      size="md"
      titleId="created-shift-success-title"
      eyebrow="Cập nhật lịch thi"
      title="Tạo ca thi thành công"
      icon={CheckCircle2}
      onClose={onClose}
      footer={
        <DialogFooter className="justify-end bg-white">
          <button type="button" className={ds.primaryButton} onClick={onClose}>
            Đã hiểu
          </button>
        </DialogFooter>
      }
    >
      <div className="space-y-3">
        <div className="rounded-xl border border-[#b9e7d5] bg-[#effbf5] px-4 py-3 text-sm font-semibold text-[#14795f]">
          Ca thi đã được lưu và sẵn sàng để tiếp tục phân bổ vé thi.
        </div>
        <dl className="grid gap-3 rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] p-4 text-sm sm:grid-cols-2">
          <div>
            <dt className="text-xs font-bold text-[#71829a]">Môn thi</dt>
            <dd className="mt-1 font-bold text-[#0F2854]">{notice.courseCode}</dd>
          </div>
          <div>
            <dt className="text-xs font-bold text-[#71829a]">Tên ca</dt>
            <dd className="mt-1 font-bold text-[#0F2854]">{notice.shiftName}</dd>
          </div>
          <div>
            <dt className="text-xs font-bold text-[#71829a]">Phòng Lab</dt>
            <dd className="mt-1 font-bold text-[#0F2854]">{notice.roomLab}</dd>
          </div>
          <div>
            <dt className="text-xs font-bold text-[#71829a]">Thời gian</dt>
            <dd className="mt-1 font-bold text-[#0F2854]">
              {notice.startTime} – {notice.endTime}
            </dd>
          </div>
          <div>
            <dt className="text-xs font-bold text-[#71829a]">Sức chứa tối đa</dt>
            <dd className="mt-1 font-bold text-[#0F2854]">{notice.maxSeats} ghế</dd>
          </div>
        </dl>
      </div>
    </FormDialog>
  )
}

interface EditSingleShiftDialogProps {
  shift: ExamShift | null
  form: EditShiftFormState
  proctorOptions: ShiftStaffCandidate[]
  examinerOptions: ShiftStaffCandidate[]
  busy: boolean
  error: string
  onClose: () => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
  onChangeField: <K extends keyof EditShiftFormState>(
    key: K,
    value: EditShiftFormState[K],
  ) => void
}

export function EditSingleShiftDialog({
  shift,
  form,
  proctorOptions,
  examinerOptions,
  busy,
  error,
  onClose,
  onSubmit,
  onChangeField,
}: EditSingleShiftDialogProps) {
  if (!shift) return null
  const editCapacityValidation = validateShiftCapacityInput(form.maxSeats, {
    defaultWhenEmpty: shift.maxSeats ?? DEFAULT_SHIFT_CAPACITY,
  })

  return (
    <FormDialog
      open
      align="center"
      size="lg"
      eyebrow="CẤU HÌNH CA THI MF-04"
      title="Cập nhật ca thi"
      icon={CalendarDays}
      busy={busy}
      onClose={onClose}
      footer={
        <DialogFooter className="justify-end bg-white">
          <button type="button" className={ds.outlineButton} onClick={onClose} disabled={busy}>
            Hủy
          </button>
          <button
            type="submit"
            form="update-official-shift-form"
            disabled={busy}
            className={ds.primaryButton}
          >
            {busy ? <LoaderCircle className="size-4 animate-spin" /> : null}
            {busy ? 'Đang lưu…' : 'Lưu cập nhật ca thi'}
          </button>
        </DialogFooter>
      }
    >
      <form id="update-official-shift-form" onSubmit={onSubmit} className="grid gap-3">
        {error ? (
          <div
            role="alert"
            className="rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-4 py-3 text-sm font-semibold text-[#a43d4b]"
          >
            {error}
          </div>
        ) : null}

        <div className="grid gap-3 sm:grid-cols-2">
          <label className="grid gap-1.5 text-xs font-bold text-[#52657d]" htmlFor="edit-shift-name">
            Tên ca *
            <input
              id="edit-shift-name"
              required
              className={ds.field}
              value={form.shiftName}
              onChange={(event) => onChangeField('shiftName', event.target.value)}
            />
          </label>
          <label className="grid gap-1.5 text-xs font-bold text-[#52657d]" htmlFor="edit-shift-room">
            Phòng Lab *
            <input
              id="edit-shift-room"
              required
              className={ds.field}
              value={form.roomLab}
              onChange={(event) => onChangeField('roomLab', event.target.value)}
            />
          </label>
          <label
            className="grid gap-1.5 text-xs font-bold text-[#52657d] sm:col-span-2"
            htmlFor="edit-shift-max-seats"
          >
            Sức chứa tối đa (maxSeats) *
            <input
              id="edit-shift-max-seats"
              type="number"
              min={MIN_SHIFT_CAPACITY}
              max={MAX_SHIFT_CAPACITY}
              step={1}
              className={ds.field}
              value={form.maxSeats}
              onChange={(event) => onChangeField('maxSeats', event.target.value)}
            />
            <span className="text-[11px] font-medium text-[#71829a]">
              Sức chứa hiện tại: {shift.maxSeats ?? DEFAULT_SHIFT_CAPACITY} · Đã phát:{' '}
              {shift.assignedTicketsCount ?? 0} vé · Trần hệ thống: {MAX_SHIFT_CAPACITY}
            </span>
            {form.maxSeats.trim() !== '' && editCapacityValidation.error ? (
              <span role="alert" className="text-[11px] font-semibold text-[#a43d4b]">
                {editCapacityValidation.error}
              </span>
            ) : null}
          </label>
          <label
            className="grid gap-1.5 text-xs font-bold text-[#52657d]"
            htmlFor="edit-shift-start"
          >
            Bắt đầu *
            <input
              id="edit-shift-start"
              required
              type="datetime-local"
              className={ds.field}
              value={form.startTime}
              onChange={(event) => onChangeField('startTime', event.target.value)}
            />
          </label>
          <label className="grid gap-1.5 text-xs font-bold text-[#52657d]" htmlFor="edit-shift-end">
            Kết thúc *
            <input
              id="edit-shift-end"
              required
              type="datetime-local"
              className={ds.field}
              value={form.endTime}
              onChange={(event) => onChangeField('endTime', event.target.value)}
            />
          </label>
          <label
            className="grid gap-1.5 text-xs font-bold text-[#52657d]"
            htmlFor="edit-shift-proctor"
          >
            Giám thị *
            <select
              id="edit-shift-proctor"
              required
              className={ds.field}
              value={form.proctorUserId}
              onChange={(event) => onChangeField('proctorUserId', event.target.value)}
            >
              <option value="">Chọn giám thị</option>
              {shift.proctorUserId &&
              !proctorOptions.some((item) => item.userId === shift.proctorUserId) ? (
                <option value={shift.proctorUserId}>
                  {shift.proctorName || shift.proctorUserId}
                </option>
              ) : null}
              {proctorOptions.map((candidate) => (
                <option
                  key={`edit-proctor-${candidate.userId}`}
                  value={candidate.userId}
                  disabled={candidate.userId === form.examinerUserId}
                >
                  {candidate.fullName}
                  {candidate.detail ? ` · ${candidate.detail}` : ''}
                </option>
              ))}
            </select>
          </label>
          <label
            className="grid gap-1.5 text-xs font-bold text-[#52657d]"
            htmlFor="edit-shift-examiner"
          >
            Giám khảo *
            <select
              id="edit-shift-examiner"
              required
              className={ds.field}
              value={form.examinerUserId}
              onChange={(event) => onChangeField('examinerUserId', event.target.value)}
            >
              <option value="">Chọn giám khảo</option>
              {shift.examinerUserId &&
              !examinerOptions.some((item) => item.userId === shift.examinerUserId) ? (
                <option value={shift.examinerUserId}>
                  {shift.examinerName || shift.examinerUserId}
                </option>
              ) : null}
              {examinerOptions.map((candidate) => (
                <option
                  key={`edit-examiner-${candidate.userId}`}
                  value={candidate.userId}
                  disabled={candidate.userId === form.proctorUserId}
                >
                  {candidate.fullName}
                  {candidate.detail ? ` · ${candidate.detail}` : ''}
                </option>
              ))}
            </select>
          </label>
        </div>
      </form>
    </FormDialog>
  )
}
