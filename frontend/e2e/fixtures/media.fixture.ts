import type { Page } from '@playwright/test'
import { isLiveApiMode } from './env'

export type MicErrorName = 'NotAllowedError' | 'NotFoundError'

export interface MediaFixture {
  /**
   * Enables active virtual mic stream at the given RMS level (default `0.05`) and clears mic errors.
   * Due to `Uint8Array` quantization in `getByteTimeDomainData` (`Math.round(rms * 128)`),
   * callers testing active RMS gates (`>= 0.012`) should pass `rms >= 0.012` (default `0.05`).
   */
  enableMicStream: (rms?: number) => Promise<void>
  setSpeechTranscript: (text: string) => Promise<void>
  simulateMicError: (errorName: MicErrorName) => Promise<void>
  triggerDeviceChange: () => Promise<void>
  setSpeechRecognitionSupported: (supported: boolean) => Promise<void>
}

interface PersistedMediaState {
  __seq?: number
  micError: MicErrorName | null
  rmsLevel: number
  speechTranscript: string
  deviceLabel: string
  isMuted: boolean
  speechRecognitionEnabled: boolean
}

const MEDIA_STATE_STORAGE_KEY = '__e2e_media_state'

interface E2eMediaControlWindow {
  __e2eMediaControl?: {
    micError: MicErrorName | null
    rmsLevel: number
    speechTranscript: string
    deviceLabel: string
    isMuted: boolean
    speechRecognitionEnabled: boolean
    emitTranscript: (text: string) => void
    dispatchDeviceChange: () => void
    setSpeechRecognitionSupported: (supported: boolean) => void
  }
}

export async function mountMediaMocks(page: Page): Promise<MediaFixture> {
  let patchSeq = 0

  if (!isLiveApiMode()) {
    await page.addInitScript((storageKey) => {
      const defaultState: PersistedMediaState = {
        __seq: 0,
        micError: null,
        rmsLevel: 0.05,
        speechTranscript:
          'Dependency Inversion Principle giúp module cấp cao và module cấp thấp phụ thuộc vào abstraction.',
        deviceLabel: 'E2E Virtual Microphone',
        isMuted: false,
        speechRecognitionEnabled: true,
      }

      const readStoredState = (): PersistedMediaState => {
        try {
          const raw = sessionStorage.getItem(storageKey)
          if (!raw) return { ...defaultState }
          const parsed = JSON.parse(raw) as Partial<PersistedMediaState>
          return {
            __seq: typeof parsed.__seq === 'number' ? parsed.__seq : 0,
            micError:
              parsed.micError === 'NotAllowedError' || parsed.micError === 'NotFoundError'
                ? parsed.micError
                : null,
            rmsLevel: typeof parsed.rmsLevel === 'number' ? parsed.rmsLevel : defaultState.rmsLevel,
            speechTranscript:
              typeof parsed.speechTranscript === 'string'
                ? parsed.speechTranscript
                : defaultState.speechTranscript,
            deviceLabel:
              typeof parsed.deviceLabel === 'string'
                ? parsed.deviceLabel
                : defaultState.deviceLabel,
            isMuted: typeof parsed.isMuted === 'boolean' ? parsed.isMuted : defaultState.isMuted,
            speechRecognitionEnabled:
              typeof parsed.speechRecognitionEnabled === 'boolean'
                ? parsed.speechRecognitionEnabled
                : defaultState.speechRecognitionEnabled,
          }
        } catch {
          return { ...defaultState }
        }
      }

      const state = readStoredState()

      const persistState = () => {
        try {
          const existing = readStoredState()
          sessionStorage.setItem(
            storageKey,
            JSON.stringify({
              ...state,
              __seq: Math.max(existing.__seq ?? 0, state.__seq ?? 0),
            }),
          )
        } catch {
          // Ignore storage restrictions on opaque origins
        }
      }

      const listeners = new Map<string, Set<EventListenerOrEventListenerObject>>()

      interface MockSpeechRecognitionInstance {
        lang: string
        continuous: boolean
        interimResults: boolean
        onstart: (() => void) | null
        onresult: ((event: unknown) => void) | null
        onerror: ((event: unknown) => void) | null
        onend: (() => void) | null
        start: () => void
        stop: () => void
        abort: () => void
      }

      const activeRecognitions = new Set<MockSpeechRecognitionInstance>()

      function emitSpeechResult(instance: MockSpeechRecognitionInstance, transcriptText: string) {
        if (!instance.onresult) return
        const resultItem = Object.assign([{ transcript: transcriptText, confidence: 0.96 }], {
          isFinal: true,
        })
        const results = Object.assign([resultItem], {
          item: (index: number) => (index === 0 ? resultItem : undefined),
        })
        instance.onresult({
          resultIndex: 0,
          results,
        })
      }

      class E2eSpeechRecognition implements MockSpeechRecognitionInstance {
        lang = 'vi-VN'
        continuous = true
        interimResults = true
        onstart: (() => void) | null = null
        onresult: ((event: unknown) => void) | null = null
        onerror: ((event: unknown) => void) | null = null
        onend: (() => void) | null = null
        private running = false

        start(): void {
          if (state.micError) {
            queueMicrotask(() => {
              this.onerror?.({ error: state.micError === 'NotAllowedError' ? 'not-allowed' : 'audio-capture' })
              this.onend?.()
            })
            return
          }
          this.running = true
          activeRecognitions.add(this)
          queueMicrotask(() => {
            if (!this.running) return
            this.onstart?.()
            if (state.speechTranscript) {
              emitSpeechResult(this, state.speechTranscript)
            }
          })
        }

        stop(): void {
          this.running = false
          activeRecognitions.delete(this)
          queueMicrotask(() => {
            this.onend?.()
          })
        }

        abort(): void {
          this.stop()
        }
      }

      function installSpeechRecognition(enabled: boolean) {
        const win = window as unknown as Record<string, unknown>
        if (enabled) {
          win.SpeechRecognition = E2eSpeechRecognition
          win.webkitSpeechRecognition = E2eSpeechRecognition
        } else {
          delete win.SpeechRecognition
          delete win.webkitSpeechRecognition
        }
      }

      installSpeechRecognition(state.speechRecognitionEnabled)

      class E2eAudioAnalyser {
        fftSize = 2048
        frequencyBinCount = 1024

        getByteTimeDomainData(array: Uint8Array): void {
          const delta = Math.max(0, Math.min(127, Math.round(state.rmsLevel * 128)))
          for (let i = 0; i < array.length; i += 1) {
            array[i] = i % 2 === 0 ? 128 + delta : 128 - delta
          }
        }

        getFloatTimeDomainData(array: Float32Array): void {
          const level = state.rmsLevel
          for (let i = 0; i < array.length; i += 1) {
            array[i] = i % 2 === 0 ? level : -level
          }
        }

        connect(): void {}
        disconnect(): void {}
      }

      class E2eAudioContext {
        state = 'running'

        createAnalyser(): E2eAudioAnalyser {
          return new E2eAudioAnalyser()
        }

        createMediaStreamSource(): { connect: () => void; disconnect: () => void } {
          return {
            connect: () => {},
            disconnect: () => {},
          }
        }

        resume(): Promise<void> {
          this.state = 'running'
          return Promise.resolve()
        }

        close(): Promise<void> {
          this.state = 'closed'
          return Promise.resolve()
        }
      }

      const globalWindow = window as unknown as Record<string, unknown>
      globalWindow.AudioContext = E2eAudioContext
      globalWindow.webkitAudioContext = E2eAudioContext

      class E2eMediaRecorder {
        state: 'inactive' | 'recording' | 'paused' = 'inactive'
        mimeType: string
        ondataavailable: ((event: { data: Blob }) => void) | null = null
        onstop: (() => void) | null = null
        onerror: ((event: unknown) => void) | null = null

        static isTypeSupported(): boolean {
          return true
        }

        constructor(_stream: unknown, options?: { mimeType?: string }) {
          this.mimeType = options?.mimeType || 'audio/webm;codecs=opus'
        }

        start(): void {
          this.state = 'recording'
        }

        requestData(): void {
          const payload = new Uint8Array(4096)
          payload.fill(65)
          const blob = new Blob([payload], { type: this.mimeType })
          this.ondataavailable?.({ data: blob })
        }

        stop(): void {
          if (this.state === 'inactive') return
          this.state = 'inactive'
          queueMicrotask(() => {
            this.requestData()
            this.onstop?.()
          })
        }
      }

      globalWindow.MediaRecorder = E2eMediaRecorder

      const mockMediaDevices = {
        getUserMedia(): Promise<MediaStream> {
          if (state.micError) {
            const message =
              state.micError === 'NotAllowedError'
                ? 'Permission denied by user'
                : 'Requested device not found'
            return Promise.reject(new DOMException(message, state.micError))
          }

          let readyState: 'live' | 'ended' = 'live'
          const track = {
            kind: 'audio',
            id: 'e2e-audio-track-1',
            label: state.deviceLabel,
            enabled: true,
            get muted() {
              return state.isMuted
            },
            get readyState() {
              return readyState
            },
            stop() {
              readyState = 'ended'
            },
          }
          const stream = {
            id: 'e2e-media-stream-1',
            active: true,
            getAudioTracks: () => [track],
            getTracks: () => [track],
          }
          return Promise.resolve(stream as unknown as MediaStream)
        },

        enumerateDevices(): Promise<MediaDeviceInfo[]> {
          if (state.micError === 'NotFoundError') {
            return Promise.resolve([])
          }
          return Promise.resolve([
            {
              deviceId: 'default',
              groupId: 'e2e-audio-group',
              kind: 'audioinput',
              label: state.deviceLabel,
              toJSON: () => ({}),
            } as MediaDeviceInfo,
          ])
        },

        addEventListener(type: string, listener: EventListenerOrEventListenerObject | null): void {
          if (!listener) return
          let set = listeners.get(type)
          if (!set) {
            set = new Set()
            listeners.set(type, set)
          }
          set.add(listener)
        },

        removeEventListener(
          type: string,
          listener: EventListenerOrEventListenerObject | null,
        ): void {
          if (!listener) return
          listeners.get(type)?.delete(listener)
        },

        dispatchEvent(event: Event): boolean {
          const set = listeners.get(event.type)
          if (!set) return true
          for (const listener of set) {
            if (typeof listener === 'function') {
              listener.call(mockMediaDevices, event)
            } else {
              listener.handleEvent(event)
            }
          }
          return true
        },
      }

      try {
        Object.defineProperty(navigator, 'mediaDevices', {
          configurable: true,
          get: () => mockMediaDevices,
        })
      } catch {
        // Fallback when navigator.mediaDevices is non-configurable
      }

      ;(window as unknown as E2eMediaControlWindow).__e2eMediaControl = {
        get micError() {
          return state.micError
        },
        set micError(value: 'NotAllowedError' | 'NotFoundError' | null) {
          state.micError = value
          persistState()
        },
        get rmsLevel() {
          return state.rmsLevel
        },
        set rmsLevel(value: number) {
          state.rmsLevel = value
          persistState()
        },
        get speechTranscript() {
          return state.speechTranscript
        },
        set speechTranscript(value: string) {
          state.speechTranscript = value
          persistState()
        },
        get deviceLabel() {
          return state.deviceLabel
        },
        set deviceLabel(value: string) {
          state.deviceLabel = value
          persistState()
        },
        get isMuted() {
          return state.isMuted
        },
        set isMuted(value: boolean) {
          state.isMuted = value
          persistState()
        },
        get speechRecognitionEnabled() {
          return state.speechRecognitionEnabled
        },
        set speechRecognitionEnabled(value: boolean) {
          state.speechRecognitionEnabled = value
          installSpeechRecognition(value)
          persistState()
        },
        emitTranscript(text: string) {
          state.speechTranscript = text
          persistState()
          for (const instance of activeRecognitions) {
            emitSpeechResult(instance, text)
          }
        },
        dispatchDeviceChange() {
          mockMediaDevices.dispatchEvent(new Event('devicechange'))
        },
        setSpeechRecognitionSupported(supported: boolean) {
          state.speechRecognitionEnabled = supported
          installSpeechRecognition(supported)
          persistState()
        },
      }
    }, MEDIA_STATE_STORAGE_KEY)
  }

  const applyPersistedPatch = async (patch: Partial<PersistedMediaState>): Promise<void> => {
    if (isLiveApiMode()) return
    patchSeq += 1
    const seq = patchSeq

    await page.addInitScript(
      ({ storageKey, nextPatch, nextSeq }) => {
        try {
          const raw = sessionStorage.getItem(storageKey)
          const parsed = raw ? (JSON.parse(raw) as Partial<PersistedMediaState>) : {}
          const currentSeq = typeof parsed.__seq === 'number' ? parsed.__seq : 0
          if (currentSeq >= nextSeq) return
          sessionStorage.setItem(
            storageKey,
            JSON.stringify({ ...parsed, ...nextPatch, __seq: nextSeq }),
          )
        } catch {
          // Ignore storage restrictions on opaque origins
        }
        const ctrl = (window as unknown as E2eMediaControlWindow).__e2eMediaControl
        if (!ctrl) return
        if ('micError' in nextPatch && nextPatch.micError !== undefined) {
          ctrl.micError = nextPatch.micError
        }
        if (typeof nextPatch.rmsLevel === 'number') {
          ctrl.rmsLevel = nextPatch.rmsLevel
        }
        if (typeof nextPatch.speechTranscript === 'string') {
          ctrl.speechTranscript = nextPatch.speechTranscript
        }
        if (typeof nextPatch.speechRecognitionEnabled === 'boolean') {
          ctrl.setSpeechRecognitionSupported(nextPatch.speechRecognitionEnabled)
        }
      },
      { storageKey: MEDIA_STATE_STORAGE_KEY, nextPatch: patch, nextSeq: seq },
    )

    const currentUrl = page.url()
    if (currentUrl.startsWith('http://') || currentUrl.startsWith('https://')) {
      await page.evaluate(
        ({ storageKey, nextPatch, nextSeq }) => {
          try {
            const raw = sessionStorage.getItem(storageKey)
            const parsed = raw ? (JSON.parse(raw) as Partial<PersistedMediaState>) : {}
            sessionStorage.setItem(
              storageKey,
              JSON.stringify({ ...parsed, ...nextPatch, __seq: nextSeq }),
            )
          } catch {
            // Ignore storage restrictions
          }
          const ctrl = (window as unknown as E2eMediaControlWindow).__e2eMediaControl
          if (!ctrl) return
          if ('micError' in nextPatch && nextPatch.micError !== undefined) {
            ctrl.micError = nextPatch.micError
          }
          if (typeof nextPatch.rmsLevel === 'number') {
            ctrl.rmsLevel = nextPatch.rmsLevel
          }
          if (typeof nextPatch.speechTranscript === 'string') {
            ctrl.emitTranscript(nextPatch.speechTranscript)
          }
          if (typeof nextPatch.speechRecognitionEnabled === 'boolean') {
            ctrl.setSpeechRecognitionSupported(nextPatch.speechRecognitionEnabled)
          }
        },
        { storageKey: MEDIA_STATE_STORAGE_KEY, nextPatch: patch, nextSeq: seq },
      )
    }
  }

  return {
    enableMicStream: async (rms = 0.05) => {
      await applyPersistedPatch({ micError: null, rmsLevel: rms })
    },
    setSpeechTranscript: async (text: string) => {
      await applyPersistedPatch({ speechTranscript: text })
    },
    simulateMicError: async (errorName: MicErrorName) => {
      await applyPersistedPatch({ micError: errorName })
    },
    triggerDeviceChange: async () => {
      if (isLiveApiMode()) return
      const currentUrl = page.url()
      if (!currentUrl.startsWith('http://') && !currentUrl.startsWith('https://')) return
      await page.evaluate(() => {
        const ctrl = (window as unknown as E2eMediaControlWindow).__e2eMediaControl
        ctrl?.dispatchDeviceChange()
      })
    },
    setSpeechRecognitionSupported: async (supported: boolean) => {
      await applyPersistedPatch({ speechRecognitionEnabled: supported })
    },
  }
}
