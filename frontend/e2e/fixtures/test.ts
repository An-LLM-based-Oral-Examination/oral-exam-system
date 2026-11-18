import { test as base, expect } from '@playwright/test'
import { createAuthSessionFixture, type AuthSessionFixture } from './auth.fixture'
import { isLiveApiMode } from './env'
import { mountMediaMocks, type MediaFixture } from './media.fixture'
import { createSignalRFixture, type SignalRFixture } from './signalr.fixture'
import {
  createApiMocksController,
  createRequestTracker,
  mountCatchAllApiFallback,
  type ApiMocksFixture,
} from '../helpers/api-mocks'

export interface Wave01Fixtures {
  apiMocks: ApiMocksFixture
  authSession: AuthSessionFixture
  signalr: SignalRFixture
  mediaMock: MediaFixture
}

export const test = base.extend<Wave01Fixtures>({
  apiMocks: [
    async ({ page }, provide) => {
      const tracker = createRequestTracker(page)
      const unmockedHits: string[] = []

      await mountCatchAllApiFallback(page, unmockedHits)
      const controller = createApiMocksController(page, tracker, unmockedHits)
      await controller.setupWave01Defaults()

      await provide(controller)

      if (!isLiveApiMode()) {
        expect(
          unmockedHits,
          `Unexpected unmocked API routes hit 501 fallback: ${unmockedHits.join(', ')}`,
        ).toEqual([])
      }
    },
    { auto: true },
  ],

  authSession: async ({ page, apiMocks }, provide) => {
    const auth = createAuthSessionFixture(page, apiMocks)
    await provide(auth)
  },

  signalr: [
    async ({ page }, provide) => {
      const hub = await createSignalRFixture(page)
      await provide(hub)
    },
    { auto: true },
  ],

  mediaMock: [
    async ({ page }, provide) => {
      const media = await mountMediaMocks(page)
      await provide(media)
    },
    { auto: true },
  ],
})

export { expect }
