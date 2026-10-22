export interface DeadLetterQueueTask {
  id: string
  taskType: string
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
