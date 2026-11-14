import { beforeEach, describe, expect, it } from 'vitest'
import {
  clearPracticeRuntimes,
  loadPracticeRuntime,
  removePracticeRuntime,
  savePracticeRuntime,
  type StoredPracticeRuntime,
} from './practiceSessionStorage'

const runtime: StoredPracticeRuntime = {
  version: 1,
  sessionId: 'session-1',
  studentId: 'student-1',
  transcriptBufferSeconds: 60,
  config: {
    courseId: 'course-1',
    courseName: 'Software Architecture',
    courseCode: 'SWD392',
    mode: 'PER_QUESTION',
    selectedDifficulties: ['medium'],
    questionCount: null,
    microphoneName: 'Default microphone',
  },
  questions: [],
  drafts: [],
  currentQuestionIndex: 0,
  mainQuestionsCompleted: 0,
  questionDifficulties: {},
  activeFollowUp: null,
  pendingAnswerId: null,
  resolvedAnswer: null,
  fullSessionSubmitted: false,
  pendingSubmission: null,
}

describe('practice session recovery journal', () => {
  beforeEach(() => {
    window.sessionStorage.clear()
  })

  it('round-trips a valid runtime and applies safe recovery defaults', () => {
    savePracticeRuntime(runtime)

    expect(loadPracticeRuntime('session-1')).toMatchObject({
      sessionId: 'session-1',
      pendingSubmission: null,
      submittedAnswerIdsByQuestion: {},
      draftAnswerText: '',
      draftAnswerMode: 'voice',
      showBuffer: false,
      bufferDeadlineTimestamp: null,
    })
  })

  it('rejects malformed or cross-session records instead of restoring them', () => {
    window.sessionStorage.setItem('mf01_practice_session:bad', '{"version":1}')
    expect(loadPracticeRuntime('bad')).toBeNull()
    expect(loadPracticeRuntime('missing')).toBeNull()
  })

  it('removes one session or all MF-01 sessions without touching unrelated storage', () => {
    savePracticeRuntime(runtime)
    savePracticeRuntime({ ...runtime, sessionId: 'session-2' })
    window.sessionStorage.setItem('unrelated', 'keep')

    removePracticeRuntime('session-1')
    expect(loadPracticeRuntime('session-1')).toBeNull()
    expect(loadPracticeRuntime('session-2')).not.toBeNull()

    clearPracticeRuntimes()
    expect(loadPracticeRuntime('session-2')).toBeNull()
    expect(window.sessionStorage.getItem('unrelated')).toBe('keep')
  })
})
