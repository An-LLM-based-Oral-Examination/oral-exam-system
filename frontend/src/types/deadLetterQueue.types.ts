export interface DeadLetterQueueTask {
  id: string
  taskType: string
  flowType?: string | null
  managedByRole?: string | null
  payloadJson: string
  errorMessage: string
  retryCount: number
  status: string
  createdAt: string
  resolvedAt: string | null
}

export interface ReplayDeadLetterQueueResponse {
  id: string
  dlqId: string
  status: string
  message: string
}

export interface ResolveDeadLetterQueueResponse {
  id: string
  dlqId: string
  status: string
  message: string
}
