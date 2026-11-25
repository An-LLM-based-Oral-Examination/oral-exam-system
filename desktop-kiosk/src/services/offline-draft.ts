import type { TurnDraft, TurnDraftMetadata, TurnDraftStage } from '../types/mf04.types'

const databaseName = 'oral-exam-desktop-kiosk'
const databaseVersion = 2
const turnDraftStoreName = 'encrypted-turn-drafts'
const legacyDraftStoreName = 'encrypted-drafts'
const keyStoreName = 'encryption-keys'
const keyId = 'kiosk-journal-key'

interface EncryptedTurnRecord {
  id: string
  ticketId: string
  metadataIv: ArrayBuffer
  metadataCiphertext: ArrayBuffer
  audioIv: ArrayBuffer
  audioCiphertext: ArrayBuffer
  updatedAt: number
}

interface TurnDraftPayload {
  metadata: TurnDraftMetadata
}

export function buildTurnDraftKey(ticketId: string, questionId: string, followUpIndex: number): string {
  return `${ticketId}:${questionId}:${followUpIndex}`
}

function openDatabase(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(databaseName, databaseVersion)
    request.onerror = () => reject(request.error ?? new Error('Không mở được kho bản nháp cục bộ.'))
    request.onupgradeneeded = () => {
      const database = request.result
      if (!database.objectStoreNames.contains(turnDraftStoreName)) {
        const store = database.createObjectStore(turnDraftStoreName, { keyPath: 'id' })
        store.createIndex('byTicketId', 'ticketId', { unique: false })
      }
      if (!database.objectStoreNames.contains(legacyDraftStoreName)) {
        database.createObjectStore(legacyDraftStoreName, { keyPath: 'id' })
      }
      if (!database.objectStoreNames.contains(keyStoreName)) {
        database.createObjectStore(keyStoreName, { keyPath: 'id' })
      }
    }
    request.onsuccess = () => resolve(request.result)
  })
}

async function getEncryptionKey(database: IDBDatabase): Promise<CryptoKey> {
  const existing = await idbGet<{ id: string; key: CryptoKey }>(database, keyStoreName, keyId)
  if (existing?.key) return existing.key

  const key = await crypto.subtle.generateKey({ name: 'AES-GCM', length: 256 }, false, ['encrypt', 'decrypt'])
  await idbPut(database, keyStoreName, { id: keyId, key })
  return key
}

async function encryptBytes(key: CryptoKey, bytes: BufferSource): Promise<{ iv: ArrayBuffer; ciphertext: ArrayBuffer }> {
  const iv = crypto.getRandomValues(new Uint8Array(12))
  const ciphertext = await crypto.subtle.encrypt({ name: 'AES-GCM', iv }, key, bytes)
  return { iv: iv.buffer, ciphertext }
}

async function decryptBytes(key: CryptoKey, iv: ArrayBuffer, ciphertext: ArrayBuffer): Promise<ArrayBuffer> {
  return crypto.subtle.decrypt({ name: 'AES-GCM', iv: iv.slice(0) }, key, ciphertext)
}

function isTurnDraftStage(value: unknown): value is TurnDraftStage {
  return (
    value === 'RECORDED' ||
    value === 'UPLOAD_AUTHORIZED' ||
    value === 'AUDIO_UPLOADED' ||
    value === 'ANSWER_SUBMITTED'
  )
}

export async function saveTurnDraft(draft: TurnDraft): Promise<void> {
  if (!window.crypto?.subtle || !window.indexedDB) {
    throw new Error('Môi trường không hỗ trợ nhật ký mã hóa cục bộ.')
  }
  const database = await openDatabase()
  try {
    const key = await getEncryptionKey(database)
    const metadataBytes = new TextEncoder().encode(JSON.stringify({ metadata: draft.metadata } satisfies TurnDraftPayload))
    const audioBytes = await draft.audio.arrayBuffer()
    const encryptedMetadata = await encryptBytes(key, metadataBytes)
    const encryptedAudio = await encryptBytes(key, audioBytes)
    const record: EncryptedTurnRecord = {
      id: draft.metadata.draftKey,
      ticketId: draft.metadata.ticketId,
      metadataIv: encryptedMetadata.iv,
      metadataCiphertext: encryptedMetadata.ciphertext,
      audioIv: encryptedAudio.iv,
      audioCiphertext: encryptedAudio.ciphertext,
      updatedAt: Date.now(),
    }
    await idbPut(database, turnDraftStoreName, record)
  } finally {
    database.close()
  }
}

export async function loadTicketTurnDrafts(ticketId: string): Promise<TurnDraft[]> {
  if (!window.crypto?.subtle || !window.indexedDB) return []
  const database = await openDatabase()
  try {
    const records = await idbGetAllByTicket(database, ticketId)
    if (records.length === 0) return []
    const key = await getEncryptionKey(database)
    const drafts: TurnDraft[] = []

    for (const record of records) {
      try {
        const metadataBytes = await decryptBytes(key, record.metadataIv, record.metadataCiphertext)
        const audioBytes = await decryptBytes(key, record.audioIv, record.audioCiphertext)
        const payload = JSON.parse(new TextDecoder().decode(metadataBytes)) as TurnDraftPayload
        if (!payload.metadata || payload.metadata.ticketId !== ticketId) continue
        const stage = isTurnDraftStage(payload.metadata.stage) ? payload.metadata.stage : 'RECORDED'
        drafts.push({
          metadata: { ...payload.metadata, stage },
          audio: new Blob([audioBytes], { type: 'audio/webm' }),
        })
      } catch {
        // Skip unreadable draft entry rather than crashing recovery.
      }
    }

    return drafts.sort((a, b) => a.metadata.createdAt.localeCompare(b.metadata.createdAt))
  } finally {
    database.close()
  }
}

export async function deleteTurnDraft(draftKey: string): Promise<void> {
  if (!window.indexedDB) return
  const database = await openDatabase()
  try {
    await idbDelete(database, turnDraftStoreName, draftKey)
  } finally {
    database.close()
  }
}

export async function clearTicketDrafts(ticketId: string): Promise<void> {
  if (!window.indexedDB) return
  const database = await openDatabase()
  try {
    const records = await idbGetAllByTicket(database, ticketId)
    for (const record of records) {
      await idbDelete(database, turnDraftStoreName, record.id)
    }
    if (database.objectStoreNames.contains(legacyDraftStoreName)) {
      await idbDelete(database, legacyDraftStoreName, ticketId)
    }
  } finally {
    database.close()
  }
}

function idbGet<T>(database: IDBDatabase, storeName: string, id: IDBValidKey): Promise<T | null> {
  return new Promise((resolve, reject) => {
    const transaction = database.transaction(storeName, 'readonly')
    const request = transaction.objectStore(storeName).get(id)
    request.onerror = () => reject(request.error ?? new Error('Không đọc được nhật ký cục bộ.'))
    request.onsuccess = () => resolve((request.result as T | undefined) ?? null)
  })
}

function idbGetAllByTicket(database: IDBDatabase, ticketId: string): Promise<EncryptedTurnRecord[]> {
  return new Promise((resolve, reject) => {
    const transaction = database.transaction(turnDraftStoreName, 'readonly')
    const store = transaction.objectStore(turnDraftStoreName)
    const index = store.index('byTicketId')
    const request = index.getAll(ticketId)
    request.onerror = () => reject(request.error ?? new Error('Không đọc được danh sách nhật ký cục bộ.'))
    request.onsuccess = () => resolve((request.result as EncryptedTurnRecord[] | undefined) ?? [])
  })
}

function idbPut(database: IDBDatabase, storeName: string, value: unknown): Promise<void> {
  return new Promise((resolve, reject) => {
    const transaction = database.transaction(storeName, 'readwrite')
    const request = transaction.objectStore(storeName).put(value)
    request.onerror = () => reject(request.error ?? new Error('Không lưu được nhật ký cục bộ.'))
    request.onsuccess = () => resolve()
  })
}

function idbDelete(database: IDBDatabase, storeName: string, id: IDBValidKey): Promise<void> {
  return new Promise((resolve, reject) => {
    const transaction = database.transaction(storeName, 'readwrite')
    const request = transaction.objectStore(storeName).delete(id)
    request.onerror = () => reject(request.error ?? new Error('Không xóa được nhật ký cục bộ.'))
    request.onsuccess = () => resolve()
  })
}
