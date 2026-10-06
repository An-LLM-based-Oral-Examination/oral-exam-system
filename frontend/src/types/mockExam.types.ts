/**
 * MF-02 Mock Exam Session Contract
 */
export interface MockExamSession {
  id: string
  userId: string
  subjectId: string
  startTime: string
  maxDurationSeconds: number
  status: string
  dailyQuotaRemaining?: number
}
