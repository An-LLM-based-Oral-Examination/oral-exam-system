/**
 * Bloom's Taxonomy Cognitive Levels
 */
export type BloomLevel = 'Remember' | 'Understand' | 'Apply' | 'Analyze' | 'Evaluate' | 'Create'

/**
 * Question Scope Classification
 */
export type ScopeType = 'PRACTICE_ONLY' | 'EXAM_ONLY' | 'SHARED'

/**
 * Rubric Criterion Contract (Sum must equal 10.0)
 */
export interface RubricCriterion {
  id?: string
  questionId?: string
  name: string
  maxScore: number
  description?: string
}

/**
 * Question Entity Contract
 */
export interface Question {
  id: string
  subjectId: string
  subjectCode?: string
  chapterId: string
  chapterTitle?: string
  title: string
  bloomLevel: BloomLevel
  scope: ScopeType
  sampleAnswer?: string
  criteria: RubricCriterion[]
  createdAt?: string
}

/**
 * Subject / Course Contract
 */
export interface Subject {
  id: string
  code: string
  name: string
  description?: string
}

/**
 * Chapter / Module Contract
 */
export interface Chapter {
  id: string
  subjectId: string
  chapterNumber: number
  title: string
}

/**
 * Create Question Command Payload
 */
export interface CreateQuestionRequest {
  subjectId: string
  chapterId: string
  title: string
  bloomLevel: BloomLevel
  scope: ScopeType
  sampleAnswer: string
  criteria: {
    name: string
    maxScore: number
    description: string
  }[]
}
