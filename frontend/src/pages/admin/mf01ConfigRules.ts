import type { SystemConfigItem } from '@/services/admin.service'

export interface MF01ConfigRule {
  min: number
  max: number
  defaultValue: string
  description: string
  validate?: (val: number, allConfigs: SystemConfigItem[]) => string | null
}

export const MF01_CONFIG_RULES: Record<string, MF01ConfigRule> = {
  MaxPracticeFollowUpQuestions: {
    min: 1,
    max: 5,
    defaultValue: '2',
    description: 'Số câu hỏi phụ tối đa khi điểm câu gốc trong [4.0, 8.0] (1 đến 5, mặc định 2).',
  },
  MaxPracticeQuestionsPerSession: {
    min: 1,
    max: 50,
    defaultValue: '10',
    description:
      'Số lượng câu hỏi luyện tập tối đa trong một phiên đơn mức độ (1 đến 50 câu, mặc định 10).',
  },
  MinMixedPracticeQuestions: {
    min: 3,
    max: 10,
    defaultValue: '3',
    description: 'Số câu hỏi tối thiểu trong phiên Progressive/Mixed (3 đến 10 câu).',
    validate: (val, allConfigs) => {
      const maxCfg = allConfigs.find(c => c.key === 'MaxMixedPracticeQuestions')
      if (maxCfg) {
        const maxVal = parseInt(maxCfg.value, 10)
        if (!isNaN(maxVal) && val > maxVal) {
          return `Số câu tối thiểu (${val}) không được lớn hơn số câu tối đa (${maxVal}).`
        }
      }
      return null
    },
  },
  MaxMixedPracticeQuestions: {
    min: 3,
    max: 10,
    defaultValue: '10',
    description: 'Số câu hỏi tối đa trong phiên Progressive/Mixed (3 đến 10 câu, phải >= Min).',
    validate: (val, allConfigs) => {
      const minCfg = allConfigs.find(c => c.key === 'MinMixedPracticeQuestions')
      if (minCfg) {
        const minVal = parseInt(minCfg.value, 10)
        if (!isNaN(minVal) && val < minVal) {
          return `Số câu tối đa (${val}) không được nhỏ hơn số câu tối thiểu (${minVal}).`
        }
      }
      return null
    },
  },
  TranscriptBufferSeconds: {
    min: 10,
    max: 300,
    defaultValue: '60',
    description: 'Thời gian đệm nghe audio/sửa transcript (10 đến 300 giây, mặc định 60s).',
  },
  SessionInactivityTimeoutMinutes: {
    min: 1,
    max: 60,
    defaultValue: '5',
    description: 'Thời gian không tương tác trước khi tự động đóng phiên (1 đến 60 phút, mặc định 5p).',
  },
}
