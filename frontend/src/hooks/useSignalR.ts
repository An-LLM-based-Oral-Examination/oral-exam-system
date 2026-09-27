import { useEffect, useState, useRef } from 'react'
import * as signalR from '@microsoft/signalr'
import type { ScorecardPayload } from '@/types/exam.types'

/**
 * SignalR Real-time Connection Hook for PracticeHub (/hubs/practice)
 */
export function useSignalR(hubUrl: string = import.meta.env.VITE_SIGNALR_HUB_URL || '/hubs/practice') {
  const [isConnected, setIsConnected] = useState(false)
  const [scorecard, setScorecard] = useState<ScorecardPayload | null>(null)
  const connectionRef = useRef<signalR.HubConnection | null>(null)

  useEffect(() => {
    const token = sessionStorage.getItem('access_token')

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => token || '',
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .configureLogging(signalR.LogLevel.Warning)
      .build()

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

    connectionRef.current = connection

    return () => {
      connection.stop()
    }
  }, [hubUrl])

  return {
    isConnected,
    scorecard,
    clearScorecard: () => setScorecard(null),
    connection: connectionRef.current,
  }
}
