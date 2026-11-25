import assert from 'node:assert/strict'
import test from 'node:test'
import {
  ApiClient,
  ApiProblemError,
  classifyByodConflict,
  describeByodConflict,
} from '../node_modules/.tmp/test-dist/services/api.js'

function jsonResponse(body, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  })
}

test('ApiClient attaches BYOD headers, rotates activeSessionToken on check-in, and calls heartbeat/per-turn/follow-up', async () => {
  const recordedCalls = []
  const originalFetch = globalThis.fetch

  globalThis.fetch = async (url, init = {}) => {
    const urlStr = String(url)
    recordedCalls.push({ url: urlStr, method: init.method ?? 'GET', headers: init.headers ?? {}, body: init.body })

    if (urlStr.endsWith('/auth/password-login')) {
      return jsonResponse({
        accessToken: 'jwt-access-1',
        refreshToken: 'jwt-refresh-1',
        tokenType: 'Bearer',
        expiresIn: 900,
        mustChangePassword: false,
        user: { id: 'u1', email: 'sv@fpt.edu.vn', fullName: 'Nguyen Van A', role: 'student' },
      })
    }

    if (urlStr.endsWith('/tickets/t1/check-in')) {
      return jsonResponse({
        id: 't1',
        shiftId: 's1',
        studentId: 'u1',
        seatNumber: 5,
        ipAddress: '127.0.0.1',
        status: 'IN_PROGRESS',
        checkedInAt: '2026-11-25T08:00:00Z',
        examSetId: 'set1',
        activeSessionToken: 'kiosk-session-secret-token',
      })
    }

    if (urlStr.endsWith('/tickets/t1/heartbeat')) {
      return jsonResponse({
        ticketId: 't1',
        shiftId: 's1',
        ticketStatus: 'IN_PROGRESS',
        shiftStatus: 'in_progress',
        serverUtcNow: '2026-11-25T08:05:00Z',
        remainingShiftSeconds: 1200,
        isLocked: false,
        deviceUnlockRequired: false,
        shouldTerminateExam: false,
        notice: null,
      })
    }

    if (urlStr.endsWith('/tickets/t1/questions/q1/audio-upload-authorizations')) {
      return jsonResponse({
        objectKey: 'official/s1/t1/q1_0.webm',
        fileName: '05_SE001_Q1.webm',
        uploadUrl: 'https://r2.example/upload',
        expiresAt: '2026-11-25T08:15:00Z',
        requiredHeaders: { 'Content-Type': 'audio/webm' },
      }, 201)
    }

    if (urlStr === 'https://r2.example/upload') {
      return new Response(null, { status: 200 })
    }

    if (urlStr.endsWith('/tickets/t1/questions/q1/answers')) {
      return jsonResponse({
        submissionId: 'sub1',
        ticketId: 't1',
        examQuestionId: 'q1',
        followUpIndex: 0,
        audioObjectKey: 'official/s1/t1/q1_0.webm',
        audioHashSha256: 'a'.repeat(64),
        status: 'RECORDED',
        canRequestFollowUp: true,
      })
    }

    if (urlStr.endsWith('/tickets/t1')) {
      return jsonResponse({
        ticketId: 't1',
        shiftId: 's1',
        status: 'IN_PROGRESS',
        isExamPublished: true,
        isKioskLocked: false,
        violationCount: 1,
        maxViolations: 3,
        lockThreshold: 4,
        remainingStrikes: 3,
        serverNowUtc: '2026-11-25T08:04:00Z',
        deviceId: 'oes-win-1234567890abcdef',
        deviceLockedAt: '2026-11-25T08:00:00Z',
        deviceUnlockCount: 0,
        followUps: [
          {
            submissionId: 'sub1',
            parentSubmissionId: null,
            examQuestionId: 'q1',
            followUpIndex: 0,
            followUpPrompt: null,
            generatedFollowUpPrompt: 'Giải thích thêm về DIP?',
            transcriptWhisper: 'raw text',
            audioObjectKey: 'official/s1/t1/q1_0.webm',
            audioHashSha256: 'a'.repeat(64),
          },
        ],
      })
    }

    if (urlStr.endsWith('/tickets/t1/submissions/sub1/next-followup')) {
      return jsonResponse({
        hasMoreFollowUp: true,
        followUpIndex: 1,
        maxFollowUpQuestions: 2,
        parentSubmissionId: 'sub1',
        questionId: 'q1',
        followUpPrompt: 'Hãy giải thích rõ hơn về nguyên lý Dependency Inversion?',
      })
    }

    if (urlStr.endsWith('/tickets/t1/submission')) {
      return jsonResponse({
        ticketId: 't1',
        status: 'AI_GRADED',
        submittedAt: '2026-11-25T08:25:00Z',
        submissionCount: 2,
        isKioskLocked: true,
      })
    }

    throw new Error(`Unexpected fetch URL: ${urlStr}`)
  }

  try {
    const client = new ApiClient({
      baseUrl: 'http://127.0.0.1:5000/api/v1',
      deviceId: 'oes-win-1234567890abcdef',
      kioskVersion: 'desktop-kiosk-1.0.0',
    })

    await client.login('sv001', 'Password123!')
    const checkedIn = await client.checkIn('t1')
    assert.equal(checkedIn.activeSessionToken, 'kiosk-session-secret-token')
    assert.equal(client.getKioskSessionToken(), 'kiosk-session-secret-token')

    // Heartbeat must clamp audioLevelDb > 150 -> 150 and send all 3 BYOD headers
    const hb = await client.sendHeartbeat('t1', { audioLevelDb: 999, isFullscreen: true, displayCount: 1 })
    assert.equal(hb.remainingShiftSeconds, 1200)
    const hbCall = recordedCalls.find((c) => c.url.endsWith('/heartbeat'))
    assert.equal(hbCall.headers['X-Device-Id'], 'oes-win-1234567890abcdef')
    assert.equal(hbCall.headers['X-Kiosk-Version'], 'desktop-kiosk-1.0.0')
    assert.equal(hbCall.headers['X-Kiosk-Session-Token'], 'kiosk-session-secret-token')
    assert.equal(JSON.parse(hbCall.body).audioLevelDb, 150)

    // Per-turn upload & answer + follow-up
    const authz = await client.createQuestionAudioUploadAuthorization('t1', 'q1', 0)
    await client.uploadAudio(authz, new Blob(['webm'], { type: 'audio/webm' }))
    const answerRes = await client.submitQuestionAnswer('t1', 'q1', {
      followUpIndex: 0,
      parentSubmissionId: null,
      audioObjectKey: authz.objectKey,
      audioHashSha256: 'a'.repeat(64),
      initialVoiceTranscript: 'raw text',
      transcriptWhisper: 'raw text',
      timestampsWhisper: null,
      timeSpentSeconds: 30,
      isManuallyEdited: false,
    })
    assert.equal(answerRes.canRequestFollowUp, true)

    const followUp = await client.requestNextFollowUp('t1', answerRes.submissionId)
    assert.equal(followUp.hasMoreFollowUp, true)
    assert.equal(followUp.followUpIndex, 1)

    // Ticket state recovery matches Backend OfficialExamTicketStateResponse exact JSON keys
    const ticketState = await client.getTicketState('t1')
    assert.equal(ticketState.violationCount, 1)
    assert.equal(ticketState.serverUtcNow, '2026-11-25T08:04:00Z')
    assert.equal(ticketState.followUps.length, 1)
    assert.equal(ticketState.followUps[0].questionId, 'q1')
    assert.equal(ticketState.followUps[0].generatedFollowUpPrompt, 'Giải thích thêm về DIP?')

    // Finalize accepts AI_GRADED
    const finalize = await client.submitExam('t1', [])
    assert.equal(finalize.status, 'AI_GRADED')
  } finally {
    globalThis.fetch = originalFetch
  }
})

test('classifyByodConflict detects all 3 Backend 409 BYOD conflict types', () => {
  const mismatch = new ApiProblemError(
    { status: 409, type: 'https://oralexam.fpt.edu.vn/errors/device-mismatch', detail: 'DEVICE_MISMATCH' },
    409,
  )
  assert.equal(classifyByodConflict(mismatch), 'DEVICE_MISMATCH')
  assert.match(describeByodConflict('DEVICE_MISMATCH'), /mở khóa thiết bị BYOD/)

  const sessionConflict = new ApiProblemError(
    { status: 409, type: 'https://oralexam.fpt.edu.vn/errors/active-session-conflict' },
    409,
  )
  assert.equal(classifyByodConflict(sessionConflict), 'ACTIVE_SESSION_CONFLICT')

  const otherTicket = new ApiProblemError(
    { status: 409, detail: 'DEVICE_IN_USE_OTHER_TICKET' },
    409,
  )
  assert.equal(classifyByodConflict(otherTicket), 'DEVICE_IN_USE_OTHER_TICKET')
})
