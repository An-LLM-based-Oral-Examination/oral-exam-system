export const CLO_BLOOM_LEVELS = ['Remember', 'Understand', 'Apply', 'Analyze', 'Evaluate', 'Create'] as const
export type CloBloomLevel = typeof CLO_BLOOM_LEVELS[number]

export interface ManagedCloCourse {
  id: string
  code: string
  name: string
  credits?: number
  description?: string | null
  academicProgramName?: string | null
  activeCloCount?: number
  totalCloCount?: number
  isActive?: boolean
}
export interface CourseClo {
  id: string
  courseId: string
  cloCode: string
  description: string
  bloomLevel: CloBloomLevel
  orderIndex: number
  isActive: boolean
  createdAt: string
}
export interface CreateCourseCloInput { cloCode: string; description: string; bloomLevel: CloBloomLevel; orderIndex: number }
export interface UpdateCourseCloInput extends CreateCourseCloInput { isActive: boolean }
