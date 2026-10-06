import { useEffect, useState } from 'react'
import type { ScorecardPayload } from '@/types/practice.types'
import { createPracticeHubConnection } from '@/services/signalr.service'

/**
 * SignalR Real-time Connection Hook for PracticeHub (/hubs/practice)
 */
export function useSignalR(
  hubUrl: string = import.meta.env.VITE_SIGNALR_HUB_URL || '/hubs/practice',
) {
  const [isConnected, setIsConnected] = useState(false)
  const [scorecard, setScorecard] = useState<ScorecardPayload | null>(null)

  useEffect(() => {
    const token = sessionStorage.getItem('access_token')
    const connection = createPracticeHubConnection(hubUrl, token)

    connection.on('ReceiveScorecard', (data: ScorecardPayload) => {
      setScorecard(data)
    })

    connection
      .start()
      .then(() => {
        setIsConnected(true)
      })
      .catch((err) => {
        console.warn('SignalR connection failed (Backend may be offline):', err.message)
      })

    return () => {
      void connection.stop()
    }
  }, [hubUrl])

  return {
    isConnected,
    scorecard,
    clearScorecard: () => setScorecard(null),
  }
}
