import * as signalR from '@microsoft/signalr'
import { ensureFreshAccessToken } from '@/services/api.client'

export function createPracticeHubConnection(
  hubUrl: string,
  accessTokenFactory: () => string | Promise<string>,
) {
  return new signalR.HubConnectionBuilder()
    .withUrl(hubUrl, {
      accessTokenFactory,
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000])
    .configureLogging(signalR.LogLevel.Warning)
    .build()
}

export function createInClassShiftHubConnection(
  hubUrl: string,
  accessTokenFactory: () => string | Promise<string>,
) {
  return new signalR.HubConnectionBuilder()
    .withUrl(hubUrl, {
      accessTokenFactory,
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000])
    .configureLogging(signalR.LogLevel.Warning)
    .build()
}

export function createAntiCheatHubConnection(
  hubUrl: string = import.meta.env.VITE_ANTI_CHEAT_HUB_URL || '/hubs/anti-cheat',
  accessTokenFactory: () => string | Promise<string> = () => ensureFreshAccessToken(90),
) {
  return new signalR.HubConnectionBuilder()
    .withUrl(hubUrl, {
      accessTokenFactory,
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000])
    .configureLogging(signalR.LogLevel.Warning)
    .build()
}
