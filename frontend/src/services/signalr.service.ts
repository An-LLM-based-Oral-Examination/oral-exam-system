import * as signalR from '@microsoft/signalr'

export function createPracticeHubConnection(hubUrl: string, accessToken: string | null) {
  return new signalR.HubConnectionBuilder()
    .withUrl(hubUrl, {
      accessTokenFactory: () => accessToken || '',
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000])
    .configureLogging(signalR.LogLevel.Warning)
    .build()
}
