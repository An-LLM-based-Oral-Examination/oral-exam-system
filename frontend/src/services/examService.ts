import { practiceService } from './practice.service'
import { mockExamService } from './mockExam.service'
import { officialExamService } from './officialExam.service'

/**
 * Compatibility facade for the former combined exam service.
 */
export const examService = {
  ...practiceService,
  ...mockExamService,
  ...officialExamService,
}
