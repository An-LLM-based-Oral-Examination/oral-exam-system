import type {
  InClassCloPoolStat,
  InClassCreatePoolMode,
  InClassDifficulty,
  InClassDifficultyCounts,
  InClassQuestionPoolMode,
  InClassShiftPoolStats,
} from '@/types/inClassShift.types'

export const IN_CLASS_DIFFICULTIES: ReadonlyArray<{
  value: InClassDifficulty
  label: string
}> = [
  { value: 'easy', label: 'Dễ' },
  { value: 'medium', label: 'Trung bình' },
  { value: 'hard', label: 'Khó' },
]

export function getInClassDifficultyLabel(difficulty: InClassDifficulty): string {
  if (difficulty === 'easy') return 'Dễ'
  if (difficulty === 'medium') return 'Trung bình'
  return 'Khó'
}

export function formatDifficultyListLabel(difficulties: readonly InClassDifficulty[]): string {
  const ordered = IN_CLASS_DIFFICULTIES.map((item) => item.value).filter((value) =>
    difficulties.includes(value),
  )
  if (ordered.length === 0) return 'Chưa chọn mức'
  if (ordered.length === IN_CLASS_DIFFICULTIES.length) return 'Dễ · Trung bình · Khó'
  return ordered.map(getInClassDifficultyLabel).join(' · ')
}

export function formatPoolModeLabel(
  mode: InClassQuestionPoolMode,
  cloCodes?: readonly string[],
  cloCount = 0,
): string {
  if (mode === 'SELECTED_CLOS') {
    const cleanCodes = (cloCodes ?? []).map((item) => item.trim()).filter(Boolean)
    if (cleanCodes.length > 0) return `Theo CLO (${cleanCodes.join(', ')})`
    if (cloCount > 0) return `Theo CLO (${cloCount} CLO)`
    return 'Theo CLO'
  }
  if (mode === 'SELECTED_POOL') {
    return 'Bộ câu chọn sẵn (ca cũ)'
  }
  return 'Ngân hàng môn'
}

export function sumCountsForDifficulties(
  counts: InClassDifficultyCounts,
  selectedDifficulties: readonly InClassDifficulty[],
): number {
  let sum = 0
  for (const diff of selectedDifficulties) {
    sum += Math.max(0, counts[diff] || 0)
  }
  return sum
}

export interface CloAllocationPreviewItem {
  cloId: string
  cloCode: string
  description: string
  availableInSelectedDifficulties: number
  allocatedCount: number
}

export interface DifficultyAllocationPreviewItem {
  difficulty: InClassDifficulty
  label: string
  availableCount: number
  allocatedCount: number
}

export interface InClassPoolEvaluation {
  isValid: boolean
  totalAvailable: number
  errors: string[]
  cloPreview: CloAllocationPreviewItem[]
  difficultyPreview: DifficultyAllocationPreviewItem[]
}

function distributeBalanced(
  capacities: ReadonlyArray<{ id: string; available: number }>,
  targetTotal: number,
  requireAtLeastOneEach: boolean,
): Map<string, number> {
  const allocation = new Map<string, number>()
  for (const item of capacities) {
    allocation.set(item.id, 0)
  }
  if (targetTotal <= 0 || capacities.length === 0) return allocation

  let remaining = targetTotal

  if (requireAtLeastOneEach) {
    for (const item of capacities) {
      if (remaining <= 0) break
      if (item.available > 0) {
        allocation.set(item.id, 1)
        remaining -= 1
      }
    }
  }

  while (remaining > 0) {
    let progressed = false
    for (const item of capacities) {
      if (remaining <= 0) break
      const current = allocation.get(item.id) ?? 0
      if (current < item.available) {
        allocation.set(item.id, current + 1)
        remaining -= 1
        progressed = true
      }
    }
    if (!progressed) break
  }

  return allocation
}

export function evaluateInClassPoolSelection(params: {
  poolStats: InClassShiftPoolStats | null
  poolMode: InClassCreatePoolMode
  totalQuestionsPerStudent: number
  selectedDifficulties: readonly InClassDifficulty[]
  selectedCloIds: readonly string[]
}): InClassPoolEvaluation {
  const {
    poolStats,
    poolMode,
    totalQuestionsPerStudent,
    selectedDifficulties,
    selectedCloIds,
  } = params

  const errors: string[] = []
  const orderedDiffs = IN_CLASS_DIFFICULTIES.map((item) => item.value).filter((diff) =>
    selectedDifficulties.includes(diff),
  )

  if (
    !Number.isInteger(totalQuestionsPerStudent) ||
    totalQuestionsPerStudent < 1 ||
    totalQuestionsPerStudent > 10
  ) {
    errors.push('Số câu mỗi sinh viên phải là số nguyên từ 1 đến 10.')
  }

  if (orderedDiffs.length === 0) {
    errors.push('Vui lòng chọn ít nhất một mức độ câu hỏi (Dễ, Trung bình hoặc Khó).')
  }

  if (!poolStats) {
    return {
      isValid: false,
      totalAvailable: 0,
      errors,
      cloPreview: [],
      difficultyPreview: [],
    }
  }

  const targetCount = Number.isInteger(totalQuestionsPerStudent)
    ? Math.max(0, Math.min(10, totalQuestionsPerStudent))
    : 0

  if (poolMode === 'ALL_COURSE_BANK') {
    const totalAvailable = sumCountsForDifficulties(poolStats.courseBankCounts, orderedDiffs)
    const zeroDiffs = orderedDiffs.filter(
      (diff) => (poolStats.courseBankCounts[diff] || 0) <= 0,
    )

    if (orderedDiffs.length > 0 && zeroDiffs.length > 0) {
      const missingLabels = zeroDiffs.map(getInClassDifficultyLabel).join(', ')
      errors.push(
        `Thiếu câu hỏi hợp lệ ở mức ${missingLabels} (0 câu đã duyệt trong ngân hàng môn). Không tự bù bằng mức ngoài lựa chọn.`,
      )
    }

    if (orderedDiffs.length > 0 && targetCount >= 1 && totalAvailable < targetCount) {
      const breakdown = orderedDiffs
        .map((diff) => `${getInClassDifficultyLabel(diff)}: ${poolStats.courseBankCounts[diff] || 0}`)
        .join(', ')
      errors.push(
        `Ngân hàng môn không đủ câu hỏi hợp lệ: cần ${targetCount} câu mỗi sinh viên nhưng chỉ có ${totalAvailable} câu (${breakdown}).`,
      )
    }

    const diffAlloc = distributeBalanced(
      orderedDiffs.map((diff) => ({
        id: diff,
        available: Math.max(0, poolStats.courseBankCounts[diff] || 0),
      })),
      Math.min(targetCount, totalAvailable),
      false,
    )

    const difficultyPreview: DifficultyAllocationPreviewItem[] = orderedDiffs.map((diff) => ({
      difficulty: diff,
      label: getInClassDifficultyLabel(diff),
      availableCount: Math.max(0, poolStats.courseBankCounts[diff] || 0),
      allocatedCount: diffAlloc.get(diff) ?? 0,
    }))

    return {
      isValid: errors.length === 0,
      totalAvailable,
      errors,
      cloPreview: [],
      difficultyPreview,
    }
  }

  // Chế độ SELECTED_CLOS
  const activeCloMap = new Map<string, InClassCloPoolStat>()
  for (const clo of poolStats.clos) {
    activeCloMap.set(clo.cloId, clo)
  }

  const chosenClos = selectedCloIds
    .map((id) => activeCloMap.get(id))
    .filter((item): item is InClassCloPoolStat => Boolean(item))

  if (chosenClos.length === 0) {
    errors.push('Vui lòng chọn ít nhất một chuẩn đầu ra (CLO) đang hoạt động.')
    return {
      isValid: false,
      totalAvailable: 0,
      errors,
      cloPreview: [],
      difficultyPreview: [],
    }
  }

  if (targetCount >= 1 && chosenClos.length > targetCount) {
    errors.push(
      `Số CLO đã chọn (${chosenClos.length}) lớn hơn số câu mỗi sinh viên (${targetCount}). Hãy giảm số CLO hoặc tăng số câu mỗi sinh viên để mỗi sinh viên nhận ít nhất 1 câu từ mỗi CLO.`,
    )
  }

  const aggregatedByDiff: InClassDifficultyCounts = {
    easy: 0,
    medium: 0,
    hard: 0,
    total: 0,
  }
  const emptyClos: string[] = []

  for (const clo of chosenClos) {
    aggregatedByDiff.easy += Math.max(0, clo.counts.easy || 0)
    aggregatedByDiff.medium += Math.max(0, clo.counts.medium || 0)
    aggregatedByDiff.hard += Math.max(0, clo.counts.hard || 0)
    const cloValid = sumCountsForDifficulties(clo.counts, orderedDiffs)
    if (orderedDiffs.length > 0 && cloValid <= 0) {
      emptyClos.push(clo.cloCode)
    }
  }
  aggregatedByDiff.total = sumCountsForDifficulties(aggregatedByDiff, orderedDiffs)

  if (emptyClos.length > 0) {
    const diffText = orderedDiffs.map(getInClassDifficultyLabel).join(', ')
    errors.push(
      `Thiếu câu hỏi hợp lệ ở ${emptyClos.join(', ')} cho mức ${diffText} (0 câu). Hệ thống không tự bù bằng câu ngoài CLO/mức đã chọn.`,
    )
  }

  const zeroDiffsInClos = orderedDiffs.filter((diff) => aggregatedByDiff[diff] <= 0)
  if (orderedDiffs.length > 0 && zeroDiffsInClos.length > 0) {
    const missingDiffLabels = zeroDiffsInClos.map(getInClassDifficultyLabel).join(', ')
    errors.push(
      `Các CLO đã chọn chưa có câu hỏi hợp lệ ở mức ${missingDiffLabels} (0 câu).`,
    )
  }

  if (orderedDiffs.length > 0 && targetCount >= 1 && aggregatedByDiff.total < targetCount) {
    const perCloSummary = chosenClos
      .map((clo) => `${clo.cloCode}: ${sumCountsForDifficulties(clo.counts, orderedDiffs)} câu`)
      .join(', ')
    errors.push(
      `Tổng câu hợp lệ của các CLO đã chọn không đủ: cần ${targetCount} câu mỗi sinh viên nhưng chỉ có ${aggregatedByDiff.total} câu (${perCloSummary}).`,
    )
  }

  const cloAlloc = distributeBalanced(
    chosenClos.map((clo) => ({
      id: clo.cloId,
      available: sumCountsForDifficulties(clo.counts, orderedDiffs),
    })),
    Math.min(targetCount, aggregatedByDiff.total),
    chosenClos.length <= targetCount,
  )

  const diffAlloc = distributeBalanced(
    orderedDiffs.map((diff) => ({
      id: diff,
      available: aggregatedByDiff[diff],
    })),
    Math.min(targetCount, aggregatedByDiff.total),
    false,
  )

  const cloPreview: CloAllocationPreviewItem[] = chosenClos.map((clo) => ({
    cloId: clo.cloId,
    cloCode: clo.cloCode,
    description: clo.description,
    availableInSelectedDifficulties: sumCountsForDifficulties(clo.counts, orderedDiffs),
    allocatedCount: cloAlloc.get(clo.cloId) ?? 0,
  }))

  const difficultyPreview: DifficultyAllocationPreviewItem[] = orderedDiffs.map((diff) => ({
    difficulty: diff,
    label: getInClassDifficultyLabel(diff),
    availableCount: aggregatedByDiff[diff],
    allocatedCount: diffAlloc.get(diff) ?? 0,
  }))

  return {
    isValid: errors.length === 0,
    totalAvailable: aggregatedByDiff.total,
    errors,
    cloPreview,
    difficultyPreview,
  }
}
